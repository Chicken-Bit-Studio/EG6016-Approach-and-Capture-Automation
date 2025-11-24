"""
Primary reinforcement-learning entry point for the PPO algorithm.

Responsibilities:
  - Instantiate the Unity environment via UnityEnvWrapper.
  - Configure and train the PPO model (Stable-Baselines3).
  - Set up logging (TensorBoard + CSV + console) using utilities from utils.
  - Save trained models and periodic evaluation summaries.

Prerequisites:
  1. Unity scene must be running the EnvironmentSocketServer
     and listening on the configured port.
  2. All Python dependencies installed:
         pip install stable-baselines3 torch gymnasium
"""

import sys, os
import numpy as np
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import BaseCallback, EvalCallback
from stable_baselines3.common.monitor import Monitor
from datetime import datetime

# --- project imports ---
# ensure Python can find sibling packages when running a script directly
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if ROOT not in sys.path:
    sys.path.append(ROOT)
from utils import config
from utils.logger_setup import initialise_logger, create_csv_logger, append_csv_row, launch_tensorboard
from utils.smart_path_junction_handler import handle_long_path
from unity_remote.unity_env_wrapper import UnityEnvWrapper


# ============================================================================
# Custom callback for console updates and CSV logging
# ============================================================================

class ProgressLoggerCallback(BaseCallback):
    """
    Custom callback printing periodic progress to console and writing
    aggregated statistics to a CSV file.

    Works with any Stable-Baselines3 algorithm.
    """

    def __init__(self, csv_path, console_interval, verbose=1):
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

            # Extract episode rewards from the info buffer
            if len(self.model.ep_info_buffer) > 0:
                mean_reward = np.mean([ep_info['r'] for ep_info in self.model.ep_info_buffer])
            else:
                mean_reward = 0.0
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
    # Note: Using a temporary folder for TensorBoard logs to debug
    base_path = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    run_dir, logger = initialise_logger(algo_name, base_path)
    tb_root_original = os.path.join(run_dir, "tensorboard")
    #tb_root_original = r"C:\Junctions"
    os.makedirs(tb_root_original, exist_ok=True)
    tb_root = handle_long_path(path=tb_root_original, junctionName="tb_write") 
    # Verify the path is writable
    if not os.access(tb_root, os.W_OK):
        logger.error("TensorBoard log folder is not writable: %s", tb_root)
        tb_root = None
    if tb_root:
        # Point TensorBoard at LogsAndVisualisations to see all runs. Launch it automatically.
        logs_visualizations = handle_long_path(path=os.path.dirname(run_dir), junctionName="tb_read")
        tensorboard_process = launch_tensorboard(logs_visualizations)
        #tensorboard_process = launch_tensorboard(r"C:\Junctions\PPO_1")
    print(f"\033[92m Initialised logging stage complete \033[0m")

    # --- Create Unity environment ---
    env = UnityEnvWrapper(host=config.UNITY_HOST, port=config.UNITY_PORT)
    env = Monitor(env)  # adds reward tracking for SB3 callbacks
    print(f"\033[92m Create Unity Wrapper stage complete \033[0m")

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
        tensorboard_log=tb_root,
        verbose=1,
    )
    print(f"\033[92m Building PPO Model stage complete \033[0m")

    # --- Evaluation callback setup ---
    eval_env = UnityEnvWrapper(host=config.UNITY_HOST, port=config.UNITY_PORT)
    eval_cb = EvalCallback(
        eval_env,
        best_model_save_path=os.path.join(run_dir, "best_model"),
        log_path=run_dir,
        eval_freq=config.EVAL_FREQ,
        n_eval_episodes=config.EVAL_EPISODES,
        deterministic=True,
        render=False,
    )
    print(f"\033[92m Evaluation callback setup stage complete \033[0m")

    # --- CSV progress callback ---
    csv_path = os.path.join(run_dir, "progress_summary.csv")
    csv_cb = ProgressLoggerCallback(
        csv_path, console_interval=config.CONSOLE_UPDATE_INTERVAL)
    print(f"\033[92m CSV Progress Callback stage complete \033[0m")

    # --- Train model ---
    logger.info(f"Starting PPO training for {config.TOTAL_TIMESTEPS:,} timesteps")
    model.learn(total_timesteps=config.TOTAL_TIMESTEPS,
                callback=[eval_cb, csv_cb],
                log_interval=10)
    print(f"\033[92m Train Model stage complete \033[0m")

    # --- Save model checkpoint ---
    model_path = os.path.join(run_dir, "final_model.zip")
    model.save(model_path)
    logger.info(f"Training complete. Model saved to: {model_path}")
    print(f"\033[92m Save Model Checkpoint stage complete \033[0m")

    # --- Graceful cleanup ---
    env.close()
    eval_env.close()
    print(f"\033[92m Graceful Cleanup stage complete \033[0m")

if __name__ == "__main__":
    main()