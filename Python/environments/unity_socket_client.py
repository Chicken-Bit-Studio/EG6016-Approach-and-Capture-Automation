"""
Low-level TCP client for Unity ML environment communication.

Handles binary protocol communication with Unity's UnityMLServer, including
connection management, handshake negotiation, and automatic reconnection
with exponential backoff.
"""

import socket
import struct
import numpy as np
import time
import logging
from typing import Tuple, Optional
from configs.config import Connection, Colors


logger = logging.getLogger(__name__)


class UnitySocketClient:
    """
    TCP client implementing binary protocol for Unity ML communication.
    
    Protocol uses opcode-based messages:
    - OPCODE_STEP (0): Send action, receive observation/reward/done
    - OPCODE_RESET (1): Reset environment with seed
    - OPCODE_HANDSHAKE (100): Receive observation/action space sizes
    """
    
    # Protocol opcodes (must match Unity UnityMLServer)
    OPCODE_STEP = 0
    OPCODE_RESET = 1
    OPCODE_HANDSHAKE = 100
    
    # Class-level space dimensions (shared across instances after first handshake)
    obs_len: Optional[int] = None
    act_len: Optional[int] = None
    
    def __init__(self, host: str = None, port: int = None, timeout: float = None):
        """
        Initializes socket client with connection parameters.
        
        :param host: Unity server IP address (uses config default if None)
        :param port: Unity server port (uses config default if None)
        :param timeout: Socket timeout in seconds (uses config default if None)
        """
        self.host = host or Connection.UNITY_HOST
        self.port = port or Connection.UNITY_PORT
        self.timeout = timeout or Connection.TIMEOUT
        self.sock: Optional[socket.socket] = None
        self._is_connected = False
    
    # ============================================================================
    # Connection Management
    # ============================================================================
    
    def connect(self) -> None:
        """
        Establishes connection to Unity server with automatic retry.
        
        Attempts connection with exponential backoff. Performs handshake to
        discover observation and action space dimensions.
        
        :raises ConnectionError: If all connection attempts fail
        :raises TimeoutError: If handshake times out
        """
        if not self._connect_with_retry():
            raise ConnectionError(
                f"Failed to connect to Unity at {self.host}:{self.port} "
                f"after {Connection.RECONNECT_ATTEMPTS} attempts"
            )
        
        # Perform handshake if dimensions not yet known
        if UnitySocketClient.obs_len is None or UnitySocketClient.act_len is None:
            self._receive_handshake()
        
        self._is_connected = True
        logger.info(f"{Colors.SUCCESS}Connected to Unity (obs={self.obs_len}, act={self.act_len}){Colors.RESET}")
    
    def _connect_with_retry(self) -> bool:
        """
        Attempts socket connection with exponential backoff.
        
        :return: True if connection successful, False if all attempts exhausted
        """
        for attempt in range(Connection.RECONNECT_ATTEMPTS):
            try:
                # Create fresh socket
                self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.sock.settimeout(self.timeout)
                self.sock.connect((self.host, self.port))
                return True
                
            except (socket.timeout, ConnectionRefusedError, OSError) as e:
                if attempt < Connection.RECONNECT_ATTEMPTS - 1:
                    # Exponential backoff
                    wait_time = Connection.RECONNECT_INTERVAL * (2 ** attempt)
                    logger.warning(
                        f"{Colors.WARNING}Connection attempt {attempt + 1} failed: {e}. "
                        f"Retrying in {wait_time:.1f}s...{Colors.RESET}"
                    )
                    time.sleep(wait_time)
                else:
                    logger.error(f"{Colors.ERROR}All connection attempts failed: {e}{Colors.RESET}")
                    return False
        
        return False
    
    def reconnect(self) -> bool:
        """
        Attempts to reconnect after connection loss.
        
        :return: True if reconnection successful
        """
        logger.warning(f"{Colors.WARNING}Connection lost. Attempting reconnection...{Colors.RESET}")
        self.close()
        
        try:
            self.connect()
            return True
        except (ConnectionError, TimeoutError) as e:
            logger.error(f"{Colors.ERROR}Reconnection failed: {e}{Colors.RESET}")
            return False
    
    def close(self) -> None:
        """Closes socket connection gracefully."""
        self._is_connected = False
        if self.sock:
            try:
                self.sock.close()
            except Exception:
                pass
            self.sock = None
    
    @property
    def is_connected(self) -> bool:
        """Returns True if socket is currently connected."""
        return self._is_connected and self.sock is not None
    
    # ============================================================================
    # Protocol Methods
    # ============================================================================
    
    def _receive_handshake(self) -> None:
        """
        Receives handshake packet from Unity containing space dimensions.
        
        Handshake packet format:
        [int32: opcode=100][int32: obs_len][int32: act_len]
        
        :raises RuntimeError: If handshake has unexpected format
        :raises TimeoutError: If handshake times out
        """
        try:
            data = self._recvall(12)  # 3 int32 values
            opcode, obs_len, act_len = struct.unpack("<3i", data)
            
            if opcode != self.OPCODE_HANDSHAKE:
                raise RuntimeError(f"Expected handshake opcode {self.OPCODE_HANDSHAKE}, got {opcode}")
            
            # Store dimensions at class level (shared across instances)
            UnitySocketClient.obs_len = obs_len
            UnitySocketClient.act_len = act_len
            
            logger.debug(f"Handshake complete: obs_len={obs_len}, act_len={act_len}")
            
        except socket.timeout:
            raise TimeoutError("Handshake timed out")
    
    def send_reset(self, seed: int) -> None:
        """
        Sends reset command to Unity environment.
        
        Reset packet format:
        [int32: opcode=1][int32: seed]
        
        Unity responds with 4-byte acknowledgment.
        
        :param seed: Random seed for environment initialization
        :raises ConnectionError: If connection lost during reset
        """
        if not self.is_connected:
            raise ConnectionError("Not connected to Unity")
        
        try:
            # Send reset packet
            packet = struct.pack("<2i", self.OPCODE_RESET, seed)
            self.sock.sendall(packet)
            
            # Receive acknowledgment
            _ = self._recvall(4)
            
            logger.debug(f"Environment reset with seed {seed}")
            
        except (socket.timeout, ConnectionError, OSError) as e:
            self._is_connected = False
            raise ConnectionError(f"Connection lost during reset: {e}")
    
    def send_action(self, action: np.ndarray) -> Tuple[np.ndarray, float, bool]:
        """
        Sends action to Unity and receives step response.
        
        Action packet format:
        [int32: opcode=0][int32: action_len][float32 * action_len]
        
        Response packet format:
        [int32: resp_len][int32: obs_len][float32 * obs_len][float32: reward][byte: done]
        
        :param action: Action vector
        :return: Tuple of (observation, reward, done)
        :raises ValueError: If action dimensions don't match expected
        :raises ConnectionError: If connection lost during step
        """
        if not self.is_connected:
            raise ConnectionError("Not connected to Unity")
        
        if self.obs_len is None or self.act_len is None:
            raise RuntimeError("Handshake must complete before sending actions")
        
        # Validate and prepare action
        action = np.asarray(action, dtype=np.float32).flatten()
        if len(action) != self.act_len:
            raise ValueError(
                f"Action length mismatch: expected {self.act_len}, got {len(action)}"
            )
        
        try:
            # Send action packet
            header = struct.pack("<2i", self.OPCODE_STEP, len(action))
            self.sock.sendall(header + action.tobytes())
            
            # Receive response
            resp_len = struct.unpack("<i", self._recvall(4))[0]
            obs_len = struct.unpack("<i", self._recvall(4))[0]
            
            # Read observation
            obs_bytes = self._recvall(obs_len * 4)  # float32 = 4 bytes
            obs = np.frombuffer(obs_bytes, dtype=np.float32, count=obs_len)
            
            # Read reward and done flag
            reward = struct.unpack("<f", self._recvall(4))[0]
            done = bool(struct.unpack("<B", self._recvall(1))[0])
            
            return obs, reward, done
            
        except (socket.timeout, ConnectionError, OSError) as e:
            self._is_connected = False
            raise ConnectionError(f"Connection lost during step: {e}")
    
    # ============================================================================
    # Utility Methods
    # ============================================================================
    
    def _recvall(self, num_bytes: int) -> bytes:
        """
        Receives exact number of bytes from socket.
        
        Blocks until all requested bytes received or connection closes.
        
        :param num_bytes: Number of bytes to receive
        :return: Received bytes
        :raises ConnectionError: If socket closes before all bytes received
        """
        data = bytearray()
        while len(data) < num_bytes:
            packet = self.sock.recv(num_bytes - len(data))
            if not packet:
                raise ConnectionError("Socket connection closed unexpectedly")
            data.extend(packet)
        return bytes(data)
