"""
Central configuration file for Unity ↔ Python reinforcement-learning pipeline.

Holds:
  - Environment connection parameters.
  - Global logging and retention settings.
  - Universal hyperparameters used across all training algorithms.
  - Baseline training-cycle controls such as total timesteps and
    evaluation frequency.

Any algorithm-specific constants should be defined in their respective
train_<algorithm>.py scripts instead of here.
"""

import os

# =============================================================================
# Unity connection
# =============================================================================

UNITY_HOST = "127.0.0.1"   # Localhost by default
UNITY_PORT = 5005          # Must match EnvironmentSocketServer.cs port value

# =============================================================================
# Logging / retention policy
# =============================================================================

# Directory (relative to this file) where algorithm folders and logs live.
LOG_ROOT = os.path.join("..", "algorithms")

# Maximum number of retained runs per algorithm (oldest are deleted automatically)
MAX_RUN_HISTORY = 5

# Interval (timesteps) between console status updates during training
CONSOLE_UPDATE_INTERVAL = 10_000

# =============================================================================
# Universal RL hyperparameters
# =============================================================================

# Learning-rate controls
LEARNING_RATE = 3e-4           # Default step size for gradient updates
LR_SCHEDULE = None             # or "linear" for decay schedule

# Discount factor for future rewards (γ)
GAMMA = 0.99

# PPO, A2C, and similar on-policy algorithms use rollout length 'n_steps'
N_STEPS = 2048

# Batch size for policy/value updates
BATCH_SIZE = 64

# Epochs per update cycle (PPO-style algorithms)
N_EPOCHS = 10

# Generalised Advantage Estimator smoothing (λ)
GAE_LAMBDA = 0.95

# Ratio-clipping parameter (specific to PPO but harmless as a default constant)
CLIP_RANGE = 0.2

# =============================================================================
# Training-cycle scales
# =============================================================================

# Total number of timesteps each training session runs for
TOTAL_TIMESTEPS = 1_000_000

# How often evaluation episodes occur (in timesteps)
EVAL_FREQ = 50_000

# Evaluation episodes per evaluation phase
EVAL_EPISODES = 5