"""
Unit tests for UnitySocketClient.

Tests socket client functionality with mocked network operations.
Does not require running Unity instance.
"""

import pytest
import socket
import struct
import numpy as np
from unittest.mock import Mock, patch, MagicMock
from environments.unity_socket_client import UnitySocketClient
from configs.config import Connection


class TestUnitySocketClient:
    """Test suite for UnitySocketClient class."""
    
    def setup_method(self):
        """Set up test fixtures before each test."""
        # Reset class-level dimensions
        UnitySocketClient.obs_len = None
        UnitySocketClient.act_len = None
    
    def test_initialization(self):
        """Test client initialization with default parameters."""
        client = UnitySocketClient()
        
        assert client.host == Connection.UNITY_HOST
        assert client.port == Connection.UNITY_PORT
        assert client.timeout == Connection.TIMEOUT
        assert client.sock is None
        assert not client.is_connected
    
    def test_initialization_with_custom_params(self):
        """Test client initialization with custom parameters."""
        client = UnitySocketClient(host="192.168.1.100", port=6000, timeout=30.0)
        
        assert client.host == "192.168.1.100"
        assert client.port == 6000
        assert client.timeout == 30.0
    
    @patch('socket.socket')
    def test_successful_connection(self, mock_socket_class):
        """Test successful connection and handshake."""
        # Mock socket
        mock_sock = MagicMock()
        mock_socket_class.return_value = mock_sock
        
        # Mock handshake response
        handshake_data = struct.pack("<3i", 100, 42, 10)  # opcode=100, obs=42, act=10
        mock_sock.recv.return_value = handshake_data
        
        # Create client and connect
        client = UnitySocketClient()
        client.connect()
        
        # Verify connection attempt
        mock_sock.connect.assert_called_once_with((Connection.UNITY_HOST, Connection.UNITY_PORT))
        
        # Verify handshake processed
        assert UnitySocketClient.obs_len == 42
        assert UnitySocketClient.act_len == 10
        assert client.is_connected
    
    @patch('socket.socket')
    def test_connection_retry_on_failure(self, mock_socket_class):
        """Test connection retry with exponential backoff."""
        # Mock socket that fails then succeeds
        mock_sock = MagicMock()
        mock_socket_class.return_value = mock_sock
        
        # First attempt fails, second succeeds
        mock_sock.connect.side_effect = [
            ConnectionRefusedError("Connection refused"),
            None  # Success
        ]
        
        # Mock handshake
        handshake_data = struct.pack("<3i", 100, 42, 10)
        mock_sock.recv.return_value = handshake_data
        
        # Create client and connect
        client = UnitySocketClient()
        
        with patch('time.sleep'):  # Mock sleep to speed up test
            client.connect()
        
        # Verify multiple connection attempts
        assert mock_sock.connect.call_count == 2
        assert client.is_connected
    
    @patch('socket.socket')
    def test_connection_failure_all_attempts(self, mock_socket_class):
        """Test connection failure after all retry attempts exhausted."""
        mock_sock = MagicMock()
        mock_socket_class.return_value = mock_sock
        
        # All attempts fail
        mock_sock.connect.side_effect = ConnectionRefusedError("Connection refused")
        
        client = UnitySocketClient()
        
        with patch('time.sleep'):  # Mock sleep
            with pytest.raises(ConnectionError):
                client.connect()
    
    def test_send_action_not_connected(self):
        """Test send_action raises error when not connected."""
        client = UnitySocketClient()
        action = np.array([0.5, 0.5, 0.5])
        
        with pytest.raises(ConnectionError, match="Not connected"):
            client.send_action(action)
    
    @patch('socket.socket')
    def test_send_action_dimension_mismatch(self, mock_socket_class):
        """Test send_action validates action dimensions."""
        # Setup connected client
        mock_sock = MagicMock()
        mock_socket_class.return_value = mock_sock
        
        handshake_data = struct.pack("<3i", 100, 42, 10)
        mock_sock.recv.return_value = handshake_data
        
        client = UnitySocketClient()
        client.connect()
        
        # Send action with wrong dimensions
        wrong_action = np.array([0.5] * 5)  # Expected 10, got 5
        
        with pytest.raises(ValueError, match="Action length mismatch"):
            client.send_action(wrong_action)
    
    @patch('socket.socket')
    def test_send_action_success(self, mock_socket_class):
        """Test successful action transmission and response parsing."""
        # Setup connected client
        mock_sock = MagicMock()
        mock_socket_class.return_value = mock_sock
        
        # Handshake
        handshake_data = struct.pack("<3i", 100, 3, 2)  # obs=3, act=2
        
        # Step response
        obs_data = np.array([1.0, 2.0, 3.0], dtype=np.float32)
        reward = 5.5
        done = True
        
        step_response = (
            struct.pack("<i", 4 + 4 + 12 + 4 + 1) +  # resp_len
            struct.pack("<i", 3) +  # obs_len
            obs_data.tobytes() +  # observations
            struct.pack("<f", reward) +  # reward
            struct.pack("<B", 1)  # done
        )
        
        # Mock recv to return data in sequence
        recv_sequence = [
            handshake_data,  # Initial handshake
            step_response[:4],  # resp_len
            step_response[4:8],  # obs_len
            step_response[8:20],  # obs floats
            step_response[20:24],  # reward
            step_response[24:25],  # done
        ]
        mock_sock.recv.side_effect = recv_sequence
        
        client = UnitySocketClient()
        client.connect()
        
        # Send action
        action = np.array([0.5, 0.8], dtype=np.float32)
        obs, rew, done_flag = client.send_action(action)
        
        # Verify response
        np.testing.assert_array_almost_equal(obs, obs_data)
        assert abs(rew - reward) < 0.001
        assert done_flag == done
    
    def test_close(self):
        """Test socket closure."""
        client = UnitySocketClient()
        client.sock = MagicMock()
        client._is_connected = True
        
        client.close()
        
        client.sock.close.assert_called_once()
        assert not client.is_connected
        assert client.sock is None
    
    def test_close_when_not_connected(self):
        """Test close is safe when already closed."""
        client = UnitySocketClient()
        client.close()  # Should not raise error
        
        assert not client.is_connected


# Run tests if executed directly
if __name__ == "__main__":
    pytest.main([__file__, "-v"])
