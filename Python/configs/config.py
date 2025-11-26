"""
Central configuration module for Unity-Python reinforcement learning pipeline.

Organizes all configuration constants into domain-specific classes for easy
management and extension. Import specific classes as needed throughout the project.

Usage:
    from configs.config import Connection, PPO, Logging, Colors
"""


# ============================================================================
# Connection Configuration
# ============================================================================

class Connection:
    """TCP socket connection settings for Unity communication."""
    
    UNITY_HOST = "127.0.0.1"
    """IP address of Unity simulation (localhost for same-machine setup)."""
    
    UNITY_PORT = 5005
    """TCP port matching Unity's UnityMLServer port setting."""
    
    TIMEOUT = 60.0
    """Socket operation timeout in seconds."""
    
    RECONNECT_ATTEMPTS = 5
    """Number of reconnection attempts before declaring connection lost."""
    
    RECONNECT_INTERVAL = 2.0
    """Base interval in seconds between reconnection attempts (exponential backoff applied)."""


# ============================================================================
# Logging & Monitoring Configuration
# ============================================================================

class Logging:
    """Configuration for logging, monitoring, and data retention."""
    
    MAX_RUN_HISTORY = 5
    """Maximum number of training runs to retain per algorithm."""
    
    CONSOLE_UPDATE_INTERVAL = 10_000
    """Timestep interval for progress updates to console."""
    
    CHECKPOINT_KEEP_LAST_N = 3
    """Number of most recent checkpoints to retain (older deleted automatically)."""
    
    CHECKPOINT_FREQUENCY = 100_000
    """Timestep interval for saving training checkpoints."""


class Colors:
    """ANSI color codes for console output formatting."""
    
    SUCCESS = '\033[92m'    # Green
    ERROR = '\033[91m'      # Red
    WARNING = '\033[93m'    # Yellow
    INFO = '\033[96m'       # Cyan
    DEBUG = '\033[90m'      # Gray
    RESET = '\033[0m'       # Reset to default


# ============================================================================
# Algorithm Hyperparameters
# ============================================================================

class PPO:
    """Proximal Policy Optimization (PPO) hyperparameters."""
    
    # Training scale
    TOTAL_TIMESTEPS = 1_000_000
    """Total environment steps for training session."""
    
    # Model architecture
    POLICY = "MlpPolicy"
    """Policy network architecture (MlpPolicy for continuous control)."""
    
    # Learning parameters
    LEARNING_RATE = 3e-4
    """Learning rate for policy and value function optimization."""
    
    N_STEPS = 2048
    """Number of steps to collect before each policy update."""
    
    BATCH_SIZE = 64
    """Minibatch size for gradient updates."""
    
    N_EPOCHS = 10
    """Number of epochs to train on collected rollout data."""
    
    # Advantage estimation
    GAMMA = 0.99
    """Discount factor for future rewards."""
    
    GAE_LAMBDA = 0.95
    """Lambda parameter for Generalized Advantage Estimation (GAE)."""
    
    # PPO-specific
    CLIP_RANGE = 0.2
    """Clipping parameter for policy ratio (epsilon in PPO paper)."""
    
    # Evaluation
    EVAL_FREQ = 50_000
    """Timestep interval for running evaluation episodes."""
    
    EVAL_EPISODES = 5
    """Number of episodes per evaluation phase."""


# ============================================================================
# Path Configuration
# ============================================================================

class Paths:
    """File path configuration and Windows long path handling."""
    
    SAFE_PATH_LENGTH = 200
    """Maximum path length before junction workaround is applied (Windows limitation)."""
    
    JUNCTION_ROOT = "C:/Junctions"
    """Root directory for Windows path junctions (if needed)."""


# ============================================================================
# TensorBoard Configuration
# ============================================================================

class TensorBoard:
    """TensorBoard launch and management settings."""
    
    PORT = 6006
    """Web interface port for TensorBoard."""
    
    AUTO_LAUNCH = True
    """Automatically launch TensorBoard on training start."""
    
    AUTO_OPEN_BROWSER = True
    """Automatically open browser to TensorBoard interface."""
    
    STARTUP_WAIT = 3.0
    """Seconds to wait for TensorBoard startup before opening browser."""
