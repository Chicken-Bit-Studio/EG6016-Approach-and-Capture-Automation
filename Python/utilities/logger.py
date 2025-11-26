"""
Logging infrastructure for training sessions.

Manages timestamped run directories, enforces retention policies, and configures
Python logging with both file and console outputs.
"""

import os
import time
import shutil
import logging
from datetime import datetime
from pathlib import Path
from typing import Tuple
from configs.config import Logging as LogConfig, Colors


def create_run_directory(algorithm_name: str, base_path: str) -> Tuple[str, str, str]:
    """
    Creates a timestamped run directory with proper structure.
    
    Creates directory structure:
        algorithm/LogsAndVisualisations/run_YYYYMMDD_HHMMSS/
        algorithm/LogsAndVisualisations/tensorboard/run_YYYYMMDD_HHMMSS/
    
    Enforces retention policy before creating new run.
    
    :param algorithm_name: Algorithm identifier (e.g., 'ppo')
    :param base_path: Base path for algorithm directories
    :return: Tuple of (run_dir, tensorboard_dir, run_id)
    """
    # Construct paths
    algo_dir = os.path.join(base_path, algorithm_name)
    logs_root = os.path.join(algo_dir, "LogsAndVisualisations")
    
    os.makedirs(logs_root, exist_ok=True)
    
    # Enforce retention policy
    _enforce_retention_policy(logs_root)
    
    # Create timestamped run ID
    run_id = datetime.now().strftime("run_%Y%m%d_%H%M%S")
    
    # Create run directory
    run_dir = os.path.join(logs_root, run_id)
    os.makedirs(run_dir, exist_ok=True)
    
    # Create TensorBoard directory (flattened structure for easy comparison)
    tensorboard_root = os.path.join(logs_root, "tensorboard")
    os.makedirs(tensorboard_root, exist_ok=True)
    tensorboard_dir = os.path.join(tensorboard_root, run_id)
    os.makedirs(tensorboard_dir, exist_ok=True)
    
    return run_dir, tensorboard_dir, run_id


def _enforce_retention_policy(logs_root: str) -> None:
    """
    Deletes oldest run directories beyond retention limit.
    
    Keeps only the most recent MAX_RUN_HISTORY runs, deleting both the run
    directory and any associated Unity .meta files.
    
    :param logs_root: Root directory containing run folders
    """
    # Find all run directories
    run_dirs = [
        os.path.join(logs_root, d)
        for d in os.listdir(logs_root)
        if os.path.isdir(os.path.join(logs_root, d)) and d.startswith("run_")
    ]
    
    # Sort by modification time (newest first)
    run_dirs.sort(key=lambda x: os.path.getmtime(x), reverse=True)
    
    # Delete runs beyond retention limit
    for old_dir in run_dirs[LogConfig.MAX_RUN_HISTORY:]:
        try:
            shutil.rmtree(old_dir)
            
            # Remove Unity .meta file if exists
            meta_path = old_dir + ".meta"
            if os.path.exists(meta_path):
                os.remove(meta_path)
                
        except Exception as e:
            # Silent failure - retention policy shouldn't block training
            pass


def setup_logging(run_dir: str, run_id: str, algorithm_name: str) -> logging.Logger:
    """
    Configures Python logging for training session.
    
    Creates logger with both file and console handlers. File handler captures
    all logs, console handler uses color-coded output.
    
    :param run_dir: Directory for log file
    :param run_id: Run identifier
    :param algorithm_name: Algorithm name for logger
    :return: Configured logger instance
    """
    # Create logger
    logger = logging.getLogger(algorithm_name)
    logger.setLevel(logging.DEBUG)
    logger.handlers.clear()  # Remove any existing handlers
    
    # File handler - captures everything
    log_file = os.path.join(run_dir, "session_log.txt")
    file_handler = logging.FileHandler(log_file, mode="w", encoding="utf-8")
    file_handler.setLevel(logging.DEBUG)
    
    # Console handler - only important messages
    console_handler = logging.StreamHandler()
    console_handler.setLevel(logging.INFO)
    
    # Formatter
    formatter = logging.Formatter(
        "%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S"
    )
    file_handler.setFormatter(formatter)
    console_handler.setFormatter(formatter)
    
    # Add handlers
    logger.addHandler(file_handler)
    logger.addHandler(console_handler)
    
    # Log session start
    logger.info(f"Logging initialized for {algorithm_name} - Run ID: {run_id}")
    logger.info(f"Session log: {log_file}")
    
    return logger


def save_best_model_info(best_model_dir: str, run_id: str, timesteps: int, 
                         mean_reward: float, episodes: int = None, 
                         std_reward: float = None, success_rate: float = None,
                         duration_seconds: float = None, hyperparameters: dict = None) -> None:
    """
    Saves best model metadata to human-readable text file.
    
    :param best_model_dir: Directory containing best model
    :param run_id: Run identifier that produced best model
    :param timesteps: Total timesteps at model save
    :param mean_reward: Mean episode reward
    :param episodes: Number of episodes completed (optional)
    :param std_reward: Standard deviation of rewards (optional)
    :param success_rate: Success rate as decimal (optional)
    :param duration_seconds: Training duration in seconds (optional)
    :param hyperparameters: Dictionary of hyperparameter values (optional)
    """
    info_path = os.path.join(best_model_dir, "best_model_info.txt")
    
    with open(info_path, 'w', encoding='utf-8') as f:
        f.write("Best Model Information\n")
        f.write("=" * 50 + "\n")
        f.write(f"Run ID: {run_id}\n")
        f.write(f"Timesteps: {timesteps:,}\n")
        
        if episodes is not None:
            f.write(f"Episodes Completed: {episodes:,}\n")
        
        f.write(f"Mean Reward: {mean_reward:.2f}\n")
        
        if std_reward is not None:
            f.write(f"Std Reward: {std_reward:.2f}\n")
        
        if success_rate is not None:
            f.write(f"Success Rate: {success_rate*100:.1f}%\n")
        
        if duration_seconds is not None:
            hours = int(duration_seconds // 3600)
            minutes = int((duration_seconds % 3600) // 60)
            seconds = int(duration_seconds % 60)
            f.write(f"Training Duration: {hours:02d}h {minutes:02d}m {seconds:02d}s\n")
        
        f.write(f"Saved At: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        
        if hyperparameters:
            f.write("\nHyperparameters:\n")
            for key, value in hyperparameters.items():
                f.write(f"  {key}: {value}\n")


def manage_checkpoints(run_dir: str, timesteps: int, model_save_fn) -> None:
    """
    Manages checkpoint rotation, keeping only most recent N checkpoints.
    
    :param run_dir: Directory to save checkpoint
    :param timesteps: Current timestep count
    :param model_save_fn: Function to call for saving model (receives filepath)
    """
    checkpoint_name = f"checkpoint_step_{timesteps}.zip"
    checkpoint_path = os.path.join(run_dir, checkpoint_name)
    
    # Save new checkpoint
    model_save_fn(checkpoint_path)
    
    # Find all checkpoints
    checkpoints = [
        f for f in os.listdir(run_dir)
        if f.startswith("checkpoint_step_") and f.endswith(".zip")
    ]
    
    # Sort by timestep (extract number from filename)
    def extract_step(filename: str) -> int:
        try:
            return int(filename.replace("checkpoint_step_", "").replace(".zip", ""))
        except ValueError:
            return 0
    
    checkpoints.sort(key=extract_step, reverse=True)
    
    # Delete old checkpoints beyond retention limit
    for old_checkpoint in checkpoints[LogConfig.CHECKPOINT_KEEP_LAST_N:]:
        try:
            os.remove(os.path.join(run_dir, old_checkpoint))
        except Exception:
            pass  # Silent failure for checkpoint cleanup
