"""
PPO training script for Unity ML environments.

Primary entry point for training Proximal Policy Optimization agents.
Handles environment setup, model configuration, training loop, evaluation,
and comprehensive logging with TensorBoard integration.
"""

import os
import sys
import time
import numpy as np
from datetime import datetime
from pathlib import Path

# Ensure Python can find project modules
PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

# Core imports
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import BaseCallback, EvalCallback
from stable_baselines3.common.monitor import Monitor
from rich.progress import Progress, SpinnerColumn, BarColumn, TextColumn, TimeElapsedColumn
from rich.console import Console

# Project imports
from configs.config import PPO as PPOConfig, Connection, Logging, Colors, TensorBoard as TBConfig
from environments.unity_env_wrapper import UnityEnvWrapper
from utilities.logger import (
    create_run_directory, setup_logging, save_best_model_info, manage_checkpoints
)
from utilities.tensorboard_manager import launch_tensorboard
from utilities.path_utils import handle_long_path


console = Console()


# ============================================================================
# Custom Callbacks
# ============================================================================

class RichProgressCallback(BaseCallback):
    """
    Callback for Rich progress bar updates during training.
    
    Updates comprehensive progress display with steps, episodes, rewards,
    and FPS at configured intervals.
    """
    
    def __init__(self, progress: Progress, task_id, total_timesteps: int, verbose: int = 0):
        super().__init__(verbose)
        self.progress = progress
        self.task_id = task_id
        self.total_timesteps = total_timesteps
        self.last_update_step = 0
        self.start_time = time.time()
    
    def _on_step(self) -> bool:
        """Updates progress bar at regular intervals."""
        if self.num_timesteps - self.last_update_step >= Logging.CONSOLE_UPDATE_INTERVAL:
            self.last_update_step = self.num_timesteps
            
            # Calculate metrics
            if len(self.model.ep_info_buffer) > 0:
                mean_reward = np.mean([ep['r'] for ep in self.model.ep_info_buffer])
                ep_count = len(self.model.ep_info_buffer)
            else:
                mean_reward = 0.0
                ep_count = 0
            
            elapsed = time.time() - self.start_time
            fps = self.num_timesteps / elapsed if elapsed > 0 else 0
            
            # Update Rich progress bar
            self.progress.update(
                self.task_id,
                completed=self.num_timesteps,
                description=(
                    f"[cyan]Training PPO[/cyan] | "
                    f"Steps: {self.num_timesteps:,}/{self.total_timesteps:,} | "
                    f"Episodes: {ep_count} | "
                    f"Mean Reward: {mean_reward:.1f} | "
                    f"FPS: {fps:.0f}"
                )
            )
        
        return True


class CheckpointCallback(BaseCallback):
    """
    Callback for periodic model checkpointing.
    
    Saves model checkpoints at regular intervals, maintaining only the
    most recent N checkpoints to conserve disk space.
    """
    
    def __init__(self, run_dir: str, checkpoint_freq: int, verbose: int = 0):
        super().__init__(verbose)
        self.run_dir = run_dir
        self.checkpoint_freq = checkpoint_freq
        self.last_checkpoint_step = 0
    
    def _on_step(self) -> bool:
        """Saves checkpoint if interval reached."""
        if self.num_timesteps - self.last_checkpoint_step >= self.checkpoint_freq:
            self.last_checkpoint_step = self.num_timesteps
            manage_checkpoints(
                self.run_dir,
                self.num_timesteps,
                self.model.save
            )
            self.logger.info(f"Checkpoint saved at step {self.num_timesteps:,}")
        return True


class BestModelCallback(BaseCallback):
    """
    Callback for tracking and saving best performing model.
    
    Monitors evaluation performance and updates best model when improvements
    are detected.
    """
    
    def __init__(self, best_model_dir: str, run_id: str, verbose: int = 0):
        super().__init__(verbose)
        self.best_model_dir = best_model_dir
        self.run_id = run_id
        self.best_mean_reward = -np.inf
    
    def _on_step(self) -> bool:
        """Checks for new best model after evaluation."""
        # Check if evaluation results available
        if len(self.model.ep_info_buffer) > 0:
            mean_reward = np.mean([ep['r'] for ep in self.model.ep_info_buffer])
            
            if mean_reward > self.best_mean_reward:
                self.best_mean_reward = mean_reward
                
                # Save model
                model_path = os.path.join(self.best_model_dir, "best_model.zip")
                self.model.save(model_path)
                
                # Save metadata
                save_best_model_info(
                    self.best_model_dir,
                    self.run_id,
                    self.num_timesteps,
                    mean_reward,
                    episodes=len(self.model.ep_info_buffer),
                    hyperparameters={
                        'learning_rate': PPOConfig.LEARNING_RATE,
                        'n_steps': PPOConfig.N_STEPS,
                        'batch_size': PPOConfig.BATCH_SIZE,
                        'n_epochs': PPOConfig.N_EPOCHS,
                        'gamma': PPOConfig.GAMMA,
                    }
                )
                
                self.logger.info(
                    f"{Colors.SUCCESS}New best model! Mean reward: {mean_reward:.2f}{Colors.RESET}"
                )
        
        return True


# ============================================================================
# Main Training Function
# ============================================================================

def main():
    """
    Main training loop for PPO algorithm.
    
    Sets up environment, configures model, manages logging and TensorBoard,
    runs training with evaluation, and handles graceful shutdown on errors.
    """
    
    print(f"\n{Colors.INFO}{'='*60}{Colors.RESET}")
    print(f"{Colors.INFO}PPO Training Initialization{Colors.RESET}")
    print(f"{Colors.INFO}{'='*60}{Colors.RESET}\n")
    
    # Track training start time
    training_start = time.time()
    
    # ========================================================================
    # Directory Setup
    # ========================================================================
    
    print(f"{Colors.INFO}Setting up directories...{Colors.RESET}")
    
    algorithms_dir = PROJECT_ROOT / "algorithms"
    run_dir, tensorboard_dir, run_id = create_run_directory("ppo", str(algorithms_dir))
    
    # Setup best model directory
    best_model_dir = os.path.join(algorithms_dir, "ppo", "best_model")
    os.makedirs(best_model_dir, exist_ok=True)
    
    print(f"{Colors.SUCCESS}✓ Run directory: {run_dir}{Colors.RESET}")
    print(f"{Colors.SUCCESS}✓ TensorBoard logs: {tensorboard_dir}{Colors.RESET}\n")
    
    # ========================================================================
    # Logging Setup
    # ========================================================================
    
    print(f"{Colors.INFO}Initializing logging...{Colors.RESET}")
    logger = setup_logging(run_dir, run_id, "ppo")
    print(f"{Colors.SUCCESS}✓ Logging configured{Colors.RESET}\n")
    
    # ========================================================================
    # TensorBoard Launch
    # ========================================================================
    
    if TBConfig.AUTO_LAUNCH:
        print(f"{Colors.INFO}Launching TensorBoard...{Colors.RESET}")
        tensorboard_root = os.path.dirname(tensorboard_dir)
        tb_process = launch_tensorboard(tensorboard_root, TBConfig.PORT)
        
        if tb_process:
            print(f"{Colors.SUCCESS}✓ TensorBoard running at http://localhost:{TBConfig.PORT}{Colors.RESET}\n")
        else:
            print(f"{Colors.WARNING}⚠ TensorBoard launch failed (training will continue){Colors.RESET}\n")
    
    # ========================================================================
    # Environment Setup
    # ========================================================================
    
    print(f"{Colors.INFO}Connecting to Unity environment...{Colors.RESET}")
    
    try:
        env = UnityEnvWrapper(
            host=Connection.UNITY_HOST,
            port=Connection.UNITY_PORT,
            timeout=Connection.TIMEOUT,
            auto_reconnect=True
        )
        env = Monitor(env)  # Wrap with Monitor for episode statistics
        
        print(f"{Colors.SUCCESS}✓ Unity connection established{Colors.RESET}")
        print(f"{Colors.SUCCESS}✓ Observation space: {env.observation_space.shape}{Colors.RESET}")
        print(f"{Colors.SUCCESS}✓ Action space: {env.action_space.shape}{Colors.RESET}\n")
        
    except (ConnectionError, TimeoutError) as e:
        logger.error(f"Failed to connect to Unity: {e}")
        print(f"\n{Colors.ERROR}{'='*60}{Colors.RESET}")
        print(f"{Colors.ERROR}FATAL ERROR: Cannot connect to Unity{Colors.RESET}")
        print(f"{Colors.ERROR}{'='*60}{Colors.RESET}")
        print(f"{Colors.ERROR}Ensure Unity simulation is running and listening on port {Connection.UNITY_PORT}{Colors.RESET}\n")
        return 1
    
    # ========================================================================
    # Model Configuration
    # ========================================================================
    
    print(f"{Colors.INFO}Building PPO model...{Colors.RESET}")
    logger.info("Configuring PPO model")
    
    try:
        model = PPO(
            policy=PPOConfig.POLICY,
            env=env,
            learning_rate=PPOConfig.LEARNING_RATE,
            n_steps=PPOConfig.N_STEPS,
            batch_size=PPOConfig.BATCH_SIZE,
            n_epochs=PPOConfig.N_EPOCHS,
            gamma=PPOConfig.GAMMA,
            gae_lambda=PPOConfig.GAE_LAMBDA,
            clip_range=PPOConfig.CLIP_RANGE,
            tensorboard_log=tensorboard_dir,
            verbose=0,  # Suppress SB3 default output (we use Rich instead)
        )
        
        print(f"{Colors.SUCCESS}✓ PPO model configured{Colors.RESET}\n")
        
    except Exception as e:
        logger.error(f"Model creation failed: {e}", exc_info=True)
        print(f"{Colors.ERROR}Failed to create model: {e}{Colors.RESET}\n")
        env.close()
        return 1
    
    # ========================================================================
    # Callback Setup
    # ========================================================================
    
    print(f"{Colors.INFO}Configuring callbacks...{Colors.RESET}")
    
    # Evaluation callback (uses same environment - sequential evaluation)
    eval_callback = EvalCallback(
        env,
        best_model_save_path=None,  # We handle best model saving ourselves
        log_path=run_dir,
        eval_freq=PPOConfig.EVAL_FREQ,
        n_eval_episodes=PPOConfig.EVAL_EPISODES,
        deterministic=True,
        render=False,
        verbose=0
    )
    
    # Checkpoint callback
    checkpoint_callback = CheckpointCallback(
        run_dir=run_dir,
        checkpoint_freq=Logging.CHECKPOINT_FREQUENCY
    )
    
    # Best model tracker
    best_model_callback = BestModelCallback(
        best_model_dir=best_model_dir,
        run_id=run_id
    )
    
    print(f"{Colors.SUCCESS}✓ Callbacks configured{Colors.RESET}\n")
    
    # ========================================================================
    # Training Loop
    # ========================================================================
    
    print(f"{Colors.INFO}{'='*60}{Colors.RESET}")
    print(f"{Colors.INFO}Starting Training{Colors.RESET}")
    print(f"{Colors.INFO}{'='*60}{Colors.RESET}\n")
    
    logger.info(f"Beginning training for {PPOConfig.TOTAL_TIMESTEPS:,} timesteps")
    
    try:
        # Rich progress bar
        with Progress(
            SpinnerColumn(),
            BarColumn(bar_width=40),
            TextColumn("[progress.description]{task.description}"),
            TimeElapsedColumn(),
            console=console
        ) as progress:
            
            task_id = progress.add_task(
                "[cyan]Training PPO...",
                total=PPOConfig.TOTAL_TIMESTEPS
            )
            
            # Rich progress callback
            rich_callback = RichProgressCallback(progress, task_id, PPOConfig.TOTAL_TIMESTEPS)
            
            # Combine all callbacks
            callbacks = [
                rich_callback,
                eval_callback,
                checkpoint_callback,
                best_model_callback
            ]
            
            # Train model
            model.learn(
                total_timesteps=PPOConfig.TOTAL_TIMESTEPS,
                callback=callbacks,
                log_interval=1,     # Log to TensorBoard every update (required for TB logging)
                progress_bar=False  # We use Rich instead
            )
    
    except KeyboardInterrupt:
        logger.warning("Training interrupted by user (Ctrl+C)")
        print(f"\n{Colors.WARNING}Training interrupted by user{Colors.RESET}")
    
    except ConnectionError as e:
        logger.error(f"Connection lost during training: {e}", exc_info=True)
        print(f"\n{Colors.ERROR}Connection to Unity lost: {e}{Colors.RESET}")
        print(f"{Colors.INFO}Saving model state before exit...{Colors.RESET}")
    
    except Exception as e:
        logger.error(f"Unexpected error during training: {e}", exc_info=True)
        print(f"\n{Colors.ERROR}Training failed with error: {e}{Colors.RESET}")
    
    finally:
        # ====================================================================
        # Cleanup and Final Save
        # ====================================================================
        
        print(f"\n{Colors.INFO}Finalizing training session...{Colors.RESET}")
        
        # Save final model
        final_model_path = os.path.join(run_dir, "final_model.zip")
        try:
            model.save(final_model_path)
            logger.info(f"Final model saved: {final_model_path}")
            print(f"{Colors.SUCCESS}✓ Final model saved{Colors.RESET}")
        except Exception as e:
            logger.error(f"Failed to save final model: {e}")
            print(f"{Colors.ERROR}Failed to save final model: {e}{Colors.RESET}")
        
        # Close environment
        try:
            env.close()
            print(f"{Colors.SUCCESS}✓ Environment closed{Colors.RESET}")
        except Exception as e:
            logger.error(f"Error closing environment: {e}")
        
        # Training summary
        training_duration = time.time() - training_start
        hours = int(training_duration // 3600)
        minutes = int((training_duration % 3600) // 60)
        seconds = int(training_duration % 60)
        
        print(f"\n{Colors.INFO}{'='*60}{Colors.RESET}")
        print(f"{Colors.INFO}Training Complete{Colors.RESET}")
        print(f"{Colors.INFO}{'='*60}{Colors.RESET}")
        print(f"{Colors.SUCCESS}Run ID: {run_id}{Colors.RESET}")
        print(f"{Colors.SUCCESS}Duration: {hours:02d}h {minutes:02d}m {seconds:02d}s{Colors.RESET}")
        print(f"{Colors.SUCCESS}Logs: {run_dir}{Colors.RESET}")
        print(f"{Colors.INFO}{'='*60}{Colors.RESET}\n")
        
        logger.info(f"Training session complete. Duration: {hours:02d}h {minutes:02d}m {seconds:02d}s")
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
