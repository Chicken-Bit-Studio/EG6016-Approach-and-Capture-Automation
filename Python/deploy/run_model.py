"""
Model deployment script for trained RL agents.

Loads a trained model and runs it in Unity environment for demonstration
and evaluation. Supports both GUI file selection and command-line arguments.

Usage:
    # GUI file picker (default)
    python deploy/run_model.py
    
    # Direct model path
    python deploy/run_model.py --model path/to/model.zip
    
    # Fixed number of episodes
    python deploy/run_model.py --episodes 10
    
    # Stochastic actions
    python deploy/run_model.py --stochastic
"""

import os
import sys
import argparse
import time
from pathlib import Path
from typing import Optional, Tuple
import tkinter as tk
from tkinter import filedialog

# Add project root to path
PROJECT_ROOT = Path(__file__).resolve().parents[1]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

# Core imports
import numpy as np
from stable_baselines3 import PPO, A2C, SAC, TD3, DQN
from rich.console import Console

# Project imports
from configs.config import Connection, Colors
from environments.unity_env_wrapper import UnityEnvWrapper


console = Console()


# ============================================================================
# Model Loading and Validation
# ============================================================================

def select_model_file(initial_dir: str = None) -> Optional[str]:
    """
    Opens GUI file picker for model selection.
    
    :param initial_dir: Starting directory for file picker
    :return: Selected model path, or None if cancelled
    """
    # Create hidden root window
    root = tk.Tk()
    root.withdraw()
    root.attributes('-topmost', True)
    
    # Default to Python root if no initial directory provided
    if initial_dir is None:
        initial_dir = str(PROJECT_ROOT)
    
    # Open file dialog
    model_path = filedialog.askopenfilename(
        title="Select Trained Model",
        initialdir=initial_dir,
        filetypes=[
            ("Model files", "*.zip"),
            ("All files", "*.*")
        ]
    )
    
    root.destroy()
    
    return model_path if model_path else None


def load_model(model_path: str) -> Tuple[object, str]:
    """
    Loads model from file, automatically detecting algorithm type.
    
    :param model_path: Path to model .zip file
    :return: Tuple of (loaded_model, algorithm_name)
    :raises ValueError: If model cannot be loaded
    :raises FileNotFoundError: If model file doesn't exist
    """
    if not os.path.exists(model_path):
        raise FileNotFoundError(f"Model file not found: {model_path}")
    
    # Try loading with different algorithms
    algorithms = [
        ('PPO', PPO),
        ('A2C', A2C),
        ('SAC', SAC),
        ('TD3', TD3),
        ('DQN', DQN)
    ]
    
    for algo_name, algo_class in algorithms:
        try:
            model = algo_class.load(model_path)
            return model, algo_name
        except Exception:
            continue
    
    raise ValueError(
        f"Could not load model. File may be corrupted or from unsupported algorithm."
    )


def load_model_metadata(model_path: str) -> Optional[dict]:
    """
    Attempts to load metadata from best_model_info.txt if available.
    
    :param model_path: Path to model .zip file
    :return: Dictionary of metadata, or None if not available
    """
    # Check if this is a best_model
    model_dir = os.path.dirname(model_path)
    info_file = os.path.join(model_dir, "best_model_info.txt")
    
    if not os.path.exists(info_file):
        return None
    
    metadata = {}
    try:
        with open(info_file, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if ':' in line and not line.startswith('='):
                    key, value = line.split(':', 1)
                    metadata[key.strip()] = value.strip()
    except Exception:
        return None
    
    return metadata if metadata else None


def validate_model_spaces(model, env) -> bool:
    """
    Validates that model's observation/action spaces match environment.
    
    :param model: Loaded model
    :param env: Unity environment
    :return: True if spaces match
    """
    model_obs_shape = model.observation_space.shape
    model_act_shape = model.action_space.shape
    env_obs_shape = env.observation_space.shape
    env_act_shape = env.action_space.shape
    
    if model_obs_shape != env_obs_shape:
        print(f"{Colors.WARNING}Warning: Observation space mismatch{Colors.RESET}")
        print(f"  Model expects: {model_obs_shape}")
        print(f"  Environment provides: {env_obs_shape}")
        return False
    
    if model_act_shape != env_act_shape:
        print(f"{Colors.WARNING}Warning: Action space mismatch{Colors.RESET}")
        print(f"  Model expects: {model_act_shape}")
        print(f"  Environment provides: {env_act_shape}")
        return False
    
    return True


# ============================================================================
# Deployment Loop
# ============================================================================

def run_deployment(model_path: str, max_episodes: Optional[int] = None, 
                   deterministic: bool = True) -> None:
    """
    Main deployment loop - runs model in Unity environment.
    
    :param model_path: Path to trained model
    :param max_episodes: Maximum episodes to run (None = infinite)
    :param deterministic: Use deterministic actions if True
    """
    
    # ========================================================================
    # Header
    # ========================================================================
    
    print(f"\n{Colors.INFO}{'='*80}{Colors.RESET}")
    print(f"{Colors.INFO}Model Deployment - RL Agent Demonstration{Colors.RESET}")
    print(f"{Colors.INFO}{'='*80}{Colors.RESET}\n")
    
    # ========================================================================
    # Load Model
    # ========================================================================
    
    print(f"{Colors.INFO}Loading model...{Colors.RESET}")
    
    try:
        model, algorithm = load_model(model_path)
        print(f"{Colors.SUCCESS}✓ Model loaded successfully{Colors.RESET}")
        print(f"{Colors.SUCCESS}✓ Algorithm: {algorithm}{Colors.RESET}")
        print(f"{Colors.SUCCESS}✓ Model file: {os.path.basename(model_path)}{Colors.RESET}")
    except Exception as e:
        print(f"{Colors.ERROR}✗ Failed to load model: {e}{Colors.RESET}\n")
        return
    
    # Load metadata if available
    metadata = load_model_metadata(model_path)
    if metadata:
        print(f"\n{Colors.INFO}Model Information:{Colors.RESET}")
        for key, value in metadata.items():
            if key == "Hyperparameters":
                print(f"{Colors.INFO}Hyperparameters:{Colors.RESET}")
            elif not value.startswith(" "):  # Skip hyperparameter sub-items initially
                print(f"  {key}: {value}")
        
        # Print hyperparameters if they exist
        if metadata:
            print(f"\n{Colors.INFO}Training Hyperparameters:{Colors.RESET}")
            in_hyperparams = False
            with open(os.path.join(os.path.dirname(model_path), "best_model_info.txt"), 'r') as f:
                for line in f:
                    if "Hyperparameters:" in line:
                        in_hyperparams = True
                        continue
                    if in_hyperparams and line.strip() and line.startswith("  "):
                        print(f"  {line.strip()}")
    
    # ========================================================================
    # Connect to Unity
    # ========================================================================
    
    print(f"\n{Colors.INFO}Connecting to Unity environment...{Colors.RESET}")
    
    try:
        env = UnityEnvWrapper(
            host=Connection.UNITY_HOST,
            port=Connection.UNITY_PORT,
            timeout=Connection.TIMEOUT,
            auto_reconnect=True
        )
        print(f"{Colors.SUCCESS}✓ Connected to Unity on port {Connection.UNITY_PORT}{Colors.RESET}")
        print(f"{Colors.SUCCESS}✓ Observation space: {env.observation_space.shape}{Colors.RESET}")
        print(f"{Colors.SUCCESS}✓ Action space: {env.action_space.shape}{Colors.RESET}")
        
    except (ConnectionError, TimeoutError) as e:
        print(f"\n{Colors.ERROR}{'='*80}{Colors.RESET}")
        print(f"{Colors.ERROR}FATAL ERROR: Cannot connect to Unity{Colors.RESET}")
        print(f"{Colors.ERROR}{'='*80}{Colors.RESET}")
        print(f"{Colors.ERROR}Ensure Unity is running with UnityMLServer on port {Connection.UNITY_PORT}{Colors.RESET}\n")
        return
    
    # Validate spaces
    print(f"\n{Colors.INFO}Validating model compatibility...{Colors.RESET}")
    if validate_model_spaces(model, env):
        print(f"{Colors.SUCCESS}✓ Model compatible with environment{Colors.RESET}")
    else:
        print(f"{Colors.WARNING}⚠ Space mismatch detected - deployment may fail{Colors.RESET}")
        user_input = input(f"{Colors.WARNING}Continue anyway? (y/n): {Colors.RESET}")
        if user_input.lower() != 'y':
            env.close()
            return
    
    # ========================================================================
    # Deployment Configuration
    # ========================================================================
    
    action_mode = "Deterministic" if deterministic else "Stochastic"
    episode_mode = f"{max_episodes} episodes" if max_episodes else "Infinite loop"
    
    print(f"\n{Colors.INFO}Deployment Configuration:{Colors.RESET}")
    print(f"  Action mode: {action_mode}")
    print(f"  Episodes: {episode_mode}")
    print(f"  Stop command: {Colors.WARNING}Ctrl+C{Colors.RESET}")
    
    print(f"\n{Colors.INFO}{'='*80}{Colors.RESET}")
    print(f"{Colors.SUCCESS}Starting Deployment{Colors.RESET}")
    print(f"{Colors.INFO}{'='*80}{Colors.RESET}\n")
    
    # ========================================================================
    # Episode Loop
    # ========================================================================
    
    episode_count = 0
    episode_rewards = []
    episode_steps = []
    total_start_time = time.time()
    
    try:
        while True:
            # Check episode limit
            if max_episodes is not None and episode_count >= max_episodes:
                break
            
            episode_count += 1
            episode_start_time = time.time()
            
            # Reset environment
            obs, info = env.reset()
            done = False
            truncated = False
            episode_reward = 0.0
            step_count = 0
            
            print(f"{Colors.INFO}Episode {episode_count}:{Colors.RESET}")
            
            # Episode loop
            while not (done or truncated):
                # Get action from model
                action, _ = model.predict(obs, deterministic=deterministic)
                
                # Step environment
                obs, reward, done, truncated, info = env.step(action)
                
                episode_reward += reward
                step_count += 1
            
            # Episode complete
            episode_duration = time.time() - episode_start_time
            episode_rewards.append(episode_reward)
            episode_steps.append(step_count)
            
            # Determine outcome
            if done:
                outcome = "Terminated"
            elif truncated:
                outcome = "Truncated"
            else:
                outcome = "Unknown"
            
            # Print episode summary
            print(f"  {Colors.SUCCESS}Steps: {step_count}{Colors.RESET}")
            print(f"  {Colors.SUCCESS}Reward: {episode_reward:.2f}{Colors.RESET}")
            print(f"  {Colors.SUCCESS}Outcome: {outcome}{Colors.RESET}")
            print(f"  {Colors.SUCCESS}Duration: {episode_duration:.1f}s{Colors.RESET}\n")
    
    except KeyboardInterrupt:
        print(f"\n{Colors.WARNING}Deployment interrupted by user (Ctrl+C){Colors.RESET}\n")
    
    except ConnectionError as e:
        print(f"\n{Colors.ERROR}Connection lost: {e}{Colors.RESET}")
        print(f"{Colors.WARNING}Deployment terminated{Colors.RESET}\n")
    
    except Exception as e:
        print(f"\n{Colors.ERROR}Unexpected error: {e}{Colors.RESET}\n")
    
    finally:
        # ====================================================================
        # Cleanup and Summary
        # ====================================================================
        
        env.close()
        total_duration = time.time() - total_start_time
        
        if episode_count > 0:
            print(f"{Colors.INFO}{'='*80}{Colors.RESET}")
            print(f"{Colors.INFO}Deployment Summary{Colors.RESET}")
            print(f"{Colors.INFO}{'='*80}{Colors.RESET}")
            
            # Calculate statistics
            mean_reward = np.mean(episode_rewards)
            std_reward = np.std(episode_rewards)
            mean_steps = np.mean(episode_steps)
            
            # Success rate (define success as positive reward)
            success_count = sum(1 for r in episode_rewards if r > 0)
            success_rate = (success_count / episode_count) * 100
            
            # Format duration
            hours = int(total_duration // 3600)
            minutes = int((total_duration % 3600) // 60)
            seconds = int(total_duration % 60)
            
            print(f"{Colors.SUCCESS}Episodes completed: {episode_count}{Colors.RESET}")
            print(f"{Colors.SUCCESS}Average reward: {mean_reward:.2f} ± {std_reward:.2f}{Colors.RESET}")
            print(f"{Colors.SUCCESS}Average steps: {mean_steps:.1f}{Colors.RESET}")
            print(f"{Colors.SUCCESS}Success rate: {success_rate:.1f}%{Colors.RESET}")
            print(f"{Colors.SUCCESS}Total duration: {hours:02d}h {minutes:02d}m {seconds:02d}s{Colors.RESET}")
            print(f"{Colors.INFO}{'='*80}{Colors.RESET}\n")


# ============================================================================
# Main Entry Point
# ============================================================================

def main():
    """Main entry point with argument parsing."""
    
    parser = argparse.ArgumentParser(
        description="Deploy trained RL model in Unity environment",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python deploy/run_model.py
  python deploy/run_model.py --model algorithms/ppo/best_model/best_model.zip
  python deploy/run_model.py --episodes 10
  python deploy/run_model.py --model path/to/model.zip --stochastic
        """
    )
    
    parser.add_argument(
        '--model', '-m',
        type=str,
        default=None,
        help='Path to trained model .zip file (opens GUI if not provided)'
    )
    
    parser.add_argument(
        '--episodes', '-e',
        type=int,
        default=None,
        help='Number of episodes to run (infinite if not specified)'
    )
    
    parser.add_argument(
        '--stochastic', '-s',
        action='store_true',
        help='Use stochastic actions instead of deterministic'
    )
    
    args = parser.parse_args()
    
    # Get model path
    model_path = args.model
    
    if model_path is None:
        print(f"{Colors.INFO}Opening file picker...{Colors.RESET}\n")
        model_path = select_model_file()
        
        if model_path is None:
            print(f"{Colors.WARNING}No model selected. Exiting.{Colors.RESET}\n")
            return 1
    
    # Validate model path
    if not os.path.exists(model_path):
        print(f"{Colors.ERROR}Model file not found: {model_path}{Colors.RESET}\n")
        return 1
    
    # Run deployment
    deterministic = not args.stochastic
    run_deployment(model_path, args.episodes, deterministic)
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
