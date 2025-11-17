from stable_baselines3 import PPO
from stable_baselines3.common.env_util import make_vec_env
from PythonToUnity_BridgeEnvironment import UnityBridgeEnv

env = make_vec_env(lambda: UnityBridgeEnv(), n_envs=1)
model = PPO("MlpPolicy", env, verbose=1)
model.learn(total_timesteps=100000)