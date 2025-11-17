"""
Primary reinforcement-learning entry point for the PPO algorithm.

Responsibilities:
  • Instantiate the Unity environment via UnityEnvWrapper.
  • Configure and train the PPO model (Stable Baselines3).
  • Set up logging (TensorBoard + CSV + console) using utilities from utils.
  • Save trained models and periodic evaluation summaries.

Prerequisites:
  1. Unity scene must be running the EnvironmentSocketServer
     and listening on the configured port.
  2. All Python dependencies installed:
         pip install stable-baselines3 torch gymnasium
"""

import os
import numpy as np
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import BaseCallback, EvalCallback
from stable_baselines3.common.monitor import Monitor
from datetime import datetime

# --- project imports ---
from utils import config
from utils.logger_setup import initialise_logger, create_csv_logger, append_csv_row
from unity_remote.unity_env_wrapper import UnityEnvWrapper


# ============================================================================
# Custom callback for console updates and CSV logging
# ============================================================================

class ProgressLoggerCallback(BaseCallback):
    """
    Custom callback printing periodic progress to console and writing
    aggregated statistics to a CSV file.

    Works with any Stable Baselines3 algorithm.
    """

    def __init__(self, csv_path, console_interval,
                 verbose=1):
        super().__init__(verbose)
        self.csv_path = csv_path
        self.console_interval = console_interval
        self.last_log_step = 0

    def _on_training_start(self) -> None:
        fieldnames = ["timesteps", "mean_reward", "episode_count"]
        create_csv_logger(self.csv_path, fieldnames)

    def _on_step(self) -> bool:
        # Check if enough timesteps since last print
        if (self.num_timesteps - self.last_log_step) >= self.console_interval:
            self.last_log_step = self.num_timesteps

            # Safe mean (sometimes no episodes completed yet)
            mean_reward = np.mean(self.model.ep_info_buffer) \
                if len(self.model.ep_info_buffer) > 0 else 0.0
            ep_count = len(self.model.ep_info_buffer)

            self.logger.info(f"[Progress] Steps={self.num_timesteps:,} | "
                             f"MeanEpRew={mean_reward:.2f} | "
                             f"Episodes={ep_count}")
            append_csv_row(self.csv_path,
                           {"timesteps": self.num_timesteps,
                            "mean_reward": mean_reward,
                            "episode_count": ep_count})

        return True  # returning False would stop training


# ============================================================================
# Training entry point
# ============================================================================

def main():
    algo_name = "ppo"

    # --- Initialise logger ---
    base_path = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    run_dir, logger = initialise_logger(algo_name, base_path)

    # --- Create Unity environment ---
    env = UnityEnvWrapper(host=config.UNITY_HOST, port=config.UNITY_PORT)
    env = Monitor(env)  # adds reward tracking for SB3 callbacks

    # --- Configure PPO model ---
    logger.info("Building PPO model...")
    model = PPO(
        policy="MlpPolicy",
        env=env,
        learning_rate=config.LEARNING_RATE,
        n_steps=config.N_STEPS,
        batch_size=config.BATCH_SIZE,
        n_epochs=config.N_EPOCHS,
        gamma=config.GAMMA,
        gae_lambda=config.GAE_LAMBDA,
        clip_range=config.CLIP_RANGE,
        tensorboard_log=run_dir,
        verbose=1,
    )

    # --- Evaluation callback setup ---
    eval_env = UnityEnvWrapper(
        host=config.UNITY_HOST, port=config.UNITY_PORT)
    eval_cb = EvalCallback(
        eval_env,
        best_model_save_path=os.path.join(run_dir, "best_model"),
        log_path=run_dir,
        eval_freq=config.EVAL_FREQ,
        n_eval_episodes=config.EVAL_EPISODES,
        deterministic=True,
        render=False,
    )

    # --- CSV progress callback ---
    csv_path = os.path.join(run_dir, "progress_summary.csv")
    csv_cb = ProgressLoggerCallback(
        csv_path, console_interval=config.CONSOLE_UPDATE_INTERVAL)

    # --- Train model ---
    logger.info(f"Starting PPO training for {config.TOTAL_TIMESTEPS:,} timesteps")
    model.learn(total_timesteps=config.TOTAL_TIMESTEPS,
                callback=[eval_cb, csv_cb],
                log_interval=10)

    # --- Save model checkpoint ---
    model_path = os.path.join(run_dir, "final_model.zip")
    model.save(model_path)
    logger.info(f"Training complete. Model saved to: {model_path}")

    # --- Graceful cleanup ---
    env.close()
    eval_env.close()


if __name__ == "__main__":
    main()