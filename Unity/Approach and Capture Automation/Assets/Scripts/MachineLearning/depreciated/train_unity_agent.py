"""
train_unity_agent.py
Launches training for the Unity satellite‑control environment.
"""

from stable_baselines3 import PPO          # or SAC, TD3, etc.
from stable_baselines3.common.env_util import make_vec_env
from unity_gym_env import UnityBridgeEnv

# ---- 1. Create environment ----
# Adjust action_size and obs_size if the Unity vector lengths change.
env = make_vec_env(lambda: UnityBridgeEnv(action_size=40, obs_size=271), n_envs=1)

# ---- 2. Initialize model ----
model = PPO(
    policy="MlpPolicy",
    env=env,
    verbose=1,
    learning_rate=3e-4,
    batch_size=256,
    n_steps=2048,
    tensorboard_log="./ppo_unity_tensorboard/"
)

# ---- 3. Train ----
model.learn(total_timesteps=1_000_000)

# ---- 4. Save trained policy ----
model.save("unity_satellite_ppo")

# ---- 5. Optionally evaluate ----
obs, _ = env.reset()
done = False
while not done:
    action, _ = model.predict(obs, deterministic=True)
    obs, reward, done, _, _ = env.step(action)
    print(f"Reward: {reward:.4f}")