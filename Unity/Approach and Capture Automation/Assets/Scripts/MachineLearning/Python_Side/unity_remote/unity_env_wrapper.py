"""
Gymnasium-compatible wrapper around the UnitySocketClient.  This class allows
Unity simulations to act as reinforcement-learning environments directly
usable within Stable Baselines3.

Responsibilities:
  - Manage UnitySocketClient lifecycle (connect, reset, close).
  - Translate Unity messages into Gymnasium's canonical step/reset interface.
  - Provide dynamically constructed action and observation spaces from
    handshake metadata.
  - Handle graceful shutdowns and minor communication errors gracefully.
"""

import numpy as np
import gymnasium as gym
from gymnasium import spaces
from .unity_socket_client import UnitySocketClient


class UnityEnvWrapper(gym.Env):
    """
    OpenAI Gymnasium-style wrapper around Unity environment via TCP socket.
    """

    metadata = {"render.modes": []}

    def __init__(self, host="127.0.0.1", port=5005, timeout=60.0):
        """
        Parameters
        ----------
        host : str
            IP address or hostname where Unity environment runs.
        port : int
            TCP port defined in EnvironmentSocketServer.cs.
        timeout : float
            Timeout in seconds for socket operations.
        """
        super().__init__()

        # Underlying client managing socket protocol
        self.client = UnitySocketClient(host, port, timeout)
        self.client.connect()

        # Discover observation/action dimensions via handshake
        obs_len, act_len = self.client.obs_len, self.client.act_len

        # Construct continuous (Box) spaces
        # Note: bounds are set broad; Unity itself clips if necessary.
        self.observation_space = spaces.Box(
            low=-np.inf, high=np.inf, shape=(obs_len,), dtype=np.float32
        )
        self.action_space = spaces.Box(
            low=-1.0, high=1.0, shape=(act_len,), dtype=np.float32
        )

        # Internal episode bookkeeping
        self.current_obs = np.zeros(obs_len, dtype=np.float32)
        self._episode_step = 0
        self._episode_reward = 0.0

    # ======================================================================
    # Gymnasium core methods
    # ======================================================================

    def reset(self, seed=None, options=None):
        """
        Requests an environment reset. Unity reinitializes scene and sends
        a new handshake (obs/action sizes).
        """
        super().reset(seed=seed)
        if seed is None:
            seed = np.random.randint(0, 2**31 - 1)
        self.client.send_reset(seed)

        # Re-update action/obs spaces if their size changed
        if (self.client.obs_len != self.observation_space.shape[0]
            or self.client.act_len != self.action_space.shape[0]):
            self._update_spaces()

        # Reset counters
        self._episode_step = 0
        self._episode_reward = 0.0

        # On reset, Unity doesn't provide initial obs; use zero-vector default
        self.current_obs = np.zeros(self.client.obs_len, dtype=np.float32)

        info = {"seed": seed}
        return self.current_obs.copy(), info

    def step(self, action):
        """
        Executes one environment step by sending an action to Unity.
        Returns observation, reward, terminated, truncated, info.
        """
        obs, reward, done = self.client.send_action(action)

        self.current_obs = obs
        self._episode_step += 1
        self._episode_reward += reward

        info = {"episode_step": self._episode_step,
                "episode_reward": self._episode_reward}

        # Gymnasium split: assume all 'done' flags are terminal (no truncation)
        terminated = done
        truncated = False

        return obs, reward, terminated, truncated, info

    def render(self):
        """Unity handles rendering internally; nothing required here."""
        pass

    def close(self):
        """Closes network connection."""
        self.client.close()

    # ======================================================================
    # Internal helpers
    # ======================================================================

    def _update_spaces(self):
        """
        Updates Gymnasium spaces dynamically if Unity sends new dimensions.
        """
        obs_len, act_len = self.client.obs_len, self.client.act_len
        self.observation_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(obs_len,), dtype=np.float32
        )
        self.action_space = gym.spaces.Box(
            low=-1.0, high=1.0, shape=(act_len,), dtype=np.float32
        )