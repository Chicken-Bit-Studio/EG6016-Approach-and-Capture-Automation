"""
unity_gym_env.py
A Gymnasium-compatible wrapper around the UnityEnvClient socket interface.
"""

import numpy as np
import gymnasium as gym
from gymnasium import spaces
from PythonToUnity_EnvironmentClient import UnityEnvClient


class UnityBridgeEnv(gym.Env):
    """Bridge between Stable-Baselines3 and a Unity simulation."""

    metadata = {"render_modes": []}

    def __init__(self,
                 action_size: int = 6,
                 obs_size: int = 271,       # Hard-set, but change this if observation array size changes
                 host: str = "127.0.0.1",
                 port: int = 5005):
        super().__init__()
        self.client = UnityEnvClient(host=host, port=port)
        self.action_size = action_size
        self.obs_size = obs_size

        # --- Define Gym spaces ---
        self.action_space = spaces.Box(low=0.0, high=1.0,
                                       shape=(self.action_size,),
                                       dtype=np.float32)
        self.observation_space = spaces.Box(low=-np.inf, high=np.inf,
                                            shape=(self.obs_size,),
                                            dtype=np.float32)

    def reset(self, *, seed=None, options=None):
        """Resets the Unity environment and returns the initial observation."""
        self.client.reset(seed or 0)
        obs, reward, done = self.client.step([0.0] * self.action_size)
        return np.array(obs, dtype=np.float32), {}

    def step(self, action):
        """Sends the action to Unity and receives observations, reward, and done."""
        obs, reward, done = self.client.step(action.tolist())
        info = {}
        # Gymnasium’s step returns: obs, reward, terminated, truncated, info
        return np.array(obs, dtype=np.float32), float(reward), done, False, info

    def close(self):
        self.client.close()