import gymnasium
from gymnasium import spaces
import numpy as np
from PythonToUnity_EnvironmentClient import UnityEnvClient

class UnityBridgeEnv(gymnasium.Env):
    def __init__(self):
        super().__init__()
        self.client = UnityEnvClient()
        self.action_space = spaces.Box(low=0.0, high=1.0, shape=(6,), dtype=np.float32)
        self.observation_space = spaces.Box(low=-np.inf, high=np.inf, shape=(40,), dtype=np.float32)

    def reset(self, *, seed=None, options=None):
        self.client.reset(seed or 0)
        obs,_,_ = self.client.step([0]*6)  # optional first step
        return np.array(obs, dtype=np.float32), {}

    def step(self, action):
        obs, reward, done = self.client.step(action.tolist())
        return np.array(obs, dtype=np.float32), reward, done, False, {}

    def close(self):
        self.client.close()