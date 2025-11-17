"""
Low-level TCP client used to communicate with the Unity simulation through the
EnvironmentSocketServer.cs script.

Responsibilities:
  • Establish and maintain connection with Unity's TCP listener.
  • Perform initial handshake to obtain observation and action vector sizes.
  • Serialize and send actions; receive and decode environment responses.
  • Support environment reset (with random seed) and handle dynamic dimension changes.

This class performs only protocol-level work; Gymnasium-style wrapper logic
(e.g. `step()` and `reset()` conforming to gym.Env API) is implemented higher up.
"""

import socket
import struct
import numpy as np


class UnitySocketClient:
    """Simple TCP client for binary communication with Unity socket server."""

    # --- opcode definitions (match Unity) ---
    OPCODE_STEP = 0
    OPCODE_RESET = 1
    OPCODE_HANDSHAKE = 100

    def __init__(self, host="127.0.0.1", port=5005, timeout=2.0):
        """
        Parameters
        ----------
        host : str
            IP address of the Unity machine (default: localhost).
        port : int
            TCP port number matching Unity server port.
        timeout : float
            Timeout in seconds for socket operations.
        """
        self.host = host
        self.port = port
        self.timeout = timeout
        self.sock = None

        # dimensions discovered during handshake
        self.obs_len = None
        self.act_len = None

    # ======================================================================
    # Connection management
    # ======================================================================

    def connect(self):
        """Establishes a TCP connection and runs initial handshake."""
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(self.timeout)
        self.sock.connect((self.host, self.port))
        self._receive_handshake()

    def close(self):
        """Closes connection gracefully."""
        if self.sock:
            try:
                self.sock.close()
            except Exception:
                pass
        self.sock = None

    # ======================================================================
    # Protocol methods
    # ======================================================================

    def _receive_handshake(self):
        """
        Reads a handshake packet from Unity:
        [opcode(int32)=100][obs_len(int32)][act_len(int32)]
        """
        data = self._recvall(12)
        opcode, obs_len, act_len = struct.unpack("<3i", data)
        if opcode != self.OPCODE_HANDSHAKE:
            raise RuntimeError(f"Unexpected opcode {opcode} during handshake.")
        self.obs_len = obs_len
        self.act_len = act_len

    def send_reset(self, seed: int):
        """
        Requests a reset of the Unity environment.
        Parameters
        ----------
        seed : int
            Random seed to initialize Unity's environment.
        """
        packet = struct.pack("<2i", self.OPCODE_RESET, seed)
        self.sock.sendall(packet)

        # Unity responds with a legacy 4-byte zero (int32)
        _ = self._recvall(4)

        # After reset, Unity automatically sends a new handshake
        self._receive_handshake()

    def send_action(self, action: np.ndarray):
        """
        Sends an action vector to Unity and receives the resulting
        observation, reward, and done flag.
        """
        if self.obs_len is None or self.act_len is None:
            raise RuntimeError("Handshake must occur before .send_action()")

        # ensure dtype and shape correctness
        action = np.asarray(action, dtype=np.float32).flatten()
        if len(action) != self.act_len:
            raise ValueError(f"Action length {len(action)} "
                             f"does not match expected {self.act_len}")

        # Construct STEP packet:
        # [opcode(int32)=0][action_len(int32)][float32 * action_len]
        header = struct.pack("<2i", self.OPCODE_STEP, len(action))
        self.sock.sendall(header + action.tobytes())

        # Response layout:
        # [obs_len(int32)][float32 * obs_len][reward(float32)][done(byte)]
        obs_len_data = self._recvall(4)
        obs_len = struct.unpack("<i", obs_len_data)[0]

        obs_bytes = self._recvall(obs_len * 4)
        obs = np.frombuffer(obs_bytes, dtype=np.float32, count=obs_len)

        reward_bytes = self._recvall(4)
        reward = struct.unpack("<f", reward_bytes)[0]

        done_bytes = self._recvall(1)
        done = bool(struct.unpack("<B", done_bytes)[0])

        return obs, reward, done

    # ======================================================================
    # Utility
    # ======================================================================

    def _recvall(self, num_bytes: int) -> bytes:
        """
        Helper: ensure we read exactly `num_bytes` bytes before returning.
        """
        data = bytearray()
        while len(data) < num_bytes:
            packet = self.sock.recv(num_bytes - len(data))
            if not packet:
                raise ConnectionError("Socket connection closed unexpectedly.")
            data.extend(packet)
        return bytes(data)