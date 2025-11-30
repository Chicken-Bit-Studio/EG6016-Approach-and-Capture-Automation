================================================================================
UNITY-PYTHON REINFORCEMENT LEARNING PIPELINE
================================================================================

A modular, maintainable framework for training reinforcement learning agents
in Unity environments using Stable-Baselines3.

Author: [Your Name]
Version: 2.0
Last Updated: November 2025


================================================================================
1. PROJECT OVERVIEW
================================================================================

This project provides a complete pipeline for training machine learning agents
to control Unity simulations. It uses:

- Unity 2022.3.62f2 for simulation
- Python 3.12.4 for training logic
- Stable-Baselines3 for RL algorithms
- TensorBoard for monitoring
- TCP sockets for Unity-Python communication

The system is designed for:
- Ease of extension to new algorithms
- Robust error handling and recovery
- Comprehensive logging and monitoring
- Maintainable, well-documented code


================================================================================
2. PREREQUISITES
================================================================================

Required Software:
------------------
- Unity 2022.3.62f2 or compatible version
- Python 3.12.4 or higher
- Git (for cloning/version control)
- Windows OS (current implementation is Windows-specific)

Python Knowledge:
-----------------
- Basic understanding of Python syntax
- Familiarity with virtual environments
- (Optional) Understanding of reinforcement learning concepts


================================================================================
3. INSTALLATION
================================================================================

Step 1: Clone Repository
-------------------------
If you haven't already, clone this repository:
    #git clone [repository_url]
    #cd [project_directory]
    TODO

Step 2: Set Up Python Environment
----------------------------------
1. Open Command Prompt or PowerShell
2. Navigate to the Python/ directory:
    cd Python

3. Create a virtual environment:
    python -m venv venv

4. Activate the virtual environment:
    Windows: venv\Scripts\activate
    (You should see (venv) in your command prompt)

5. Install dependencies:
    pip install -r requirements.txt

6. Verify installation:
    python -c "import stable_baselines3; import torch; print('Success!')"

If you see "Success!", the installation is complete.

Step 3: Verify Unity Setup
---------------------------
1. Open the Unity project (Unity/ directory)
2. Locate the UnityMLServer script in Assets/Scripts/
3. Find the GameObject using UnityMLServer in your scene
4. Verify EnvironmentController reference is assigned
5. Check that port is set to 5005 (default)


================================================================================
4. FILE STRUCTURE
================================================================================

Project Root/
├── README.txt                          # This file
├── Unity/                              # Unity project directory
│   └── Assets/Scripts/MachineLearning/
│       ├── UnityMLServer.cs           # TCP server for Python communication
│       └── EnvironmentController.cs    # Environment management
└── Python/                             # Self-contained Python codebase
    ├── algorithms/                     # Training scripts by algorithm
    │   ├── ppo/
    │   │   ├── train_ppo.py           # PPO training entry point
    │   │   ├── LogsAndVisualisations/ # Training outputs
    │   │   │   ├── tensorboard/       # TensorBoard logs (all runs)
    │   │   │   ├── run_YYYYMMDD_HHMMSS/  # Individual run data
    │   │   │   │   ├── session_log.txt
    │   │   │   │   ├── progress_summary.csv
    │   │   │   │   ├── final_model.zip
    │   │   │   │   └── checkpoint_step_*.zip
    │   │   │   └── [additional runs...]
    │   │   └── best_model/            # Best model across all runs
    │   │       ├── best_model.zip
    │   │       └── best_model_info.txt
    │   └── [other algorithms...]
    ├── environments/                   # Environment wrappers
    │   ├── unity_env_wrapper.py       # Gymnasium interface
    │   └── unity_socket_client.py     # TCP communication
    ├── utilities/                      # Helper modules
    │   ├── logger.py                  # Logging and run management
    │   ├── tensorboard_manager.py     # TensorBoard automation
    │   └── path_utils.py              # Windows path handling
    ├── configs/                        # Configuration
    │   └── config.py                  # All configuration constants
    ├── tests/                          # Testing suite
    │   ├── unit/                      # Unit tests
    │   ├── integration/               # Integration tests
    │   └── run_all_tests.py           # Master test runner
    └── requirements.txt                # Python dependencies


================================================================================
5. QUICK START GUIDE
================================================================================

Running Your First Training Session:
-------------------------------------

1. Start Unity:
   - Open Unity project
   - Press Play to start simulation
   - Verify "Server started on port 5005" appears in Console

2. Start Training:
   - Open Command Prompt in Python/ directory
   - Activate virtual environment: venv\Scripts\activate
   - Run training: python algorithms/ppo/train_ppo.py

3. Monitor Progress:
   - TensorBoard will automatically open in your browser
   - Watch console for training updates
   - Training runs until completion or Ctrl+C interruption

4. Results:
   - Final model: algorithms/ppo/LogsAndVisualisations/run_*/final_model.zip
   - Best model: algorithms/ppo/best_model/best_model.zip
   - Logs: algorithms/ppo/LogsAndVisualisations/run_*/session_log.txt


================================================================================
6. CONFIGURATION GUIDE
================================================================================

All configuration is centralized in: Python/configs/config.py

Key Configuration Classes (with defaults):
--------------------------

Connection:
    UNITY_HOST = "127.0.0.1"        # Unity server address
    UNITY_PORT = 5005                # Must match Unity server port
    TIMEOUT = 60.0                   # Socket timeout (seconds)
    RECONNECT_ATTEMPTS = 5           # Reconnection retries
    RECONNECT_INTERVAL = 2.0         # Base retry interval (seconds)

Logging:
    MAX_RUN_HISTORY = 5              # Runs to keep per algorithm
    CONSOLE_UPDATE_INTERVAL = 10000  # Steps between console updates
    CHECKPOINT_KEEP_LAST_N = 3       # Checkpoints to retain
    CHECKPOINT_FREQUENCY = 100000    # Steps between checkpoints

PPO:
    TOTAL_TIMESTEPS = 1000000        # Training duration
    LEARNING_RATE = 3e-4             # Optimizer learning rate
    N_STEPS = 2048                   # Rollout length
    BATCH_SIZE = 64                  # Minibatch size
    N_EPOCHS = 10                    # Update epochs per rollout
    GAMMA = 0.99                     # Reward discount factor
    GAE_LAMBDA = 0.95                # GAE smoothing
    CLIP_RANGE = 0.2                 # PPO clipping parameter
    EVAL_FREQ = 50000                # Steps between evaluations
    EVAL_EPISODES = 5                # Episodes per evaluation

Colors:
    SUCCESS = '\033[92m'             # Green
    ERROR = '\033[91m'               # Red
    WARNING = '\033[93m'             # Yellow
    INFO = '\033[96m'                # Cyan
    (Edit these to customize console output colors)

TensorBoard:
    PORT = 6006                      # Web interface port
    AUTO_LAUNCH = True               # Launch on training start
    AUTO_OPEN_BROWSER = True         # Open browser automatically
    STARTUP_WAIT = 3.0               # Seconds to wait before opening

Modifying Configuration:
------------------------
1. Open Python/configs/config.py
2. Edit desired values
3. Save file
4. Restart training (changes take effect immediately)


================================================================================
7. DEPLOYING TRAINED MODELS
================================================================================

Running Trained Models in Unity:
---------------------------------

After training, you can deploy your models to watch them perform in real-time.
The deployment script provides a simple interface for loading and running any
trained model.

Quick Start:
------------
1. Ensure Unity is running (press Play)
2. Navigate to Python/ directory
3. Activate virtual environment: venv\Scripts\activate
4. Run deployment script: python deploy/run_model.py
5. Select model file in GUI popup
6. Watch agent perform in Unity
7. Press Ctrl+C to stop

Command-Line Usage:
-------------------

# GUI file picker (default)
python deploy/run_model.py

# Specify model directly
python deploy/run_model.py --model algorithms/ppo/best_model/best_model.zip

# Run fixed number of episodes
python deploy/run_model.py --episodes 10

# Use stochastic actions (more exploratory)
python deploy/run_model.py --stochastic

# Combined options
python deploy/run_model.py --model path/to/model.zip --episodes 5 --stochastic

Deployment Output:
------------------
The script displays:
- Model information and metadata
- Episode-by-episode statistics (steps, reward, outcome)
- Summary statistics on completion:
  - Episodes completed
  - Average reward ± standard deviation
  - Average episode length
  - Success rate
  - Total duration

Example output:
    Episode 3:
      Steps: 1180
      Reward: 350.1
      Outcome: Terminated
      Duration: 23.6s

    ========================================================================
    Deployment Summary
    ========================================================================
    Episodes completed: 10
    Average reward: 345.3 ± 12.5
    Average steps: 1215.4
    Success rate: 70.0%
    Total duration: 00h 03m 54s

Action Modes:
-------------
Deterministic (default):
  - Agent always selects most likely action
  - Consistent, repeatable behavior
  - Best for demonstration and evaluation
  - Use: python deploy/run_model.py

Stochastic:
  - Agent samples from action probability distribution
  - More varied, exploratory behavior
  - Useful for testing robustness
  - Use: python deploy/run_model.py --stochastic

Model Compatibility:
--------------------
The deployment script:
- Automatically detects algorithm type (PPO, A2C, SAC, etc.)
- Validates observation/action space compatibility
- Displays model metadata if available (best_model_info.txt)
- Shows training hyperparameters for reference
- Warns about space mismatches before deployment

Supported Models:
-----------------
- Best model: algorithms/ppo/best_model/best_model.zip
- Final model from any run: LogsAndVisualisations/run_*/final_model.zip
- Checkpoints: LogsAndVisualisations/run_*/checkpoint_step_*.zip
- Any .zip model file trained with Stable-Baselines3

Stopping Deployment:
--------------------
Press Ctrl+C at any time to gracefully stop deployment.
The script will:
- Complete the current episode
- Display summary statistics
- Close Unity connection cleanly

Troubleshooting Deployment:
----------------------------
Problem: "Cannot connect to Unity"
- Ensure Unity is running and in Play mode
- Verify UnityMLServer is active on port 5005

Problem: "Space mismatch detected"
- Model was trained with different observation/action dimensions
- Environment may have changed since training
- Deployment will likely fail - retrain model

Problem: "Could not load model"
- File may be corrupted
- Unsupported algorithm (currently supports PPO, A2C, SAC, TD3, DQN)
- Try loading different model file


================================================================================
8. TENSORBOARD USAGE
================================================================================

Automatic Launch:
-----------------
TensorBoard automatically launches when training starts and opens in your
browser at http://localhost:6006

Manual Launch:
--------------
If automatic launch fails:
1. Open Command Prompt
2. Navigate to Python/algorithms/ppo/
3. Run: tensorboard --logdir=LogsAndVisualisations/tensorboard --port=6006
4. Open browser to: http://localhost:6006

Viewing Multiple Runs:
-----------------------
TensorBoard automatically compares all runs in the tensorboard/ directory.
- Toggle runs on/off using left sidebar
- Use smoothing slider to reduce noise
- Compare hyperparameters, rewards, losses, etc.

Key Metrics to Monitor:
-----------------------
- rollout/ep_rew_mean: Average episode reward (higher is better)
- rollout/ep_len_mean: Average episode length
- train/value_loss: Value function error (should decrease)
- train/policy_gradient_loss: Policy loss
- train/approx_kl: KL divergence (monitor for training stability)


================================================================================
9. TESTING INSTRUCTIONS
================================================================================

Running Tests:
--------------
1. Ensure virtual environment is activated
2. Navigate to Python/ directory
3. Run all tests: python tests/run_all_tests.py
4. Or use pytest: pytest tests/

Test Categories:
----------------
- Unit Tests: Test individual components in isolation
  - Socket client, environment wrapper, utilities
  - Fast execution, no Unity connection required

- Integration Tests: Test full training pipeline
  - Requires running Unity instance
  - Tests connection, training, evaluation
  - Slower execution

Test Output:
------------
Custom formatted output shows:
- Test name and status (✓ passed, ✗ failed)
- Execution time
- Failure details (if any)
- Summary statistics

Example:
    ========================================
    UNIT TESTS
    ========================================
    [✓] Socket Client - Connection        (0.12s)
    [✓] Socket Client - Handshake         (0.08s)
    [✗] Socket Client - Timeout           (2.01s)
        Expected timeout after 2s, got success


================================================================================
10. EXTENDING TO NEW ALGORITHMS
================================================================================

Adding a new algorithm (e.g., A2C):
------------------------------------

1. Create algorithm directory:
   Python/algorithms/a2c/

2. Create training script:
   Python/algorithms/a2c/train_a2c.py
   (Copy train_ppo.py as template)

3. Add algorithm config to config.py:
   class A2C:
       LEARNING_RATE = 7e-4
       N_STEPS = 5
       # ... other hyperparameters

4. Modify training script:
   - Import A2C from stable_baselines3
   - Import config: from configs.config import A2C, TensorBoard as TBConfig, ...
   - Use configs.config.A2C for hyperparameters
   - Update algorithm_name to "a2c"

5. Directory structure will be created automatically:
   Python/algorithms/a2c/
   ├── train_a2c.py
   ├── LogsAndVisualisations/
   │   └── tensorboard/
   └── best_model/

The framework handles:
- Run directory creation
- TensorBoard logging
- Best model tracking
- Checkpoint management
- Retention policy


================================================================================
11. TROUBLESHOOTING
================================================================================

Problem: "Failed to connect to Unity"
Solution:
- Ensure Unity simulation is running (press Play)
- Verify port 5005 is not blocked by firewall
- Check UnityMLServer is attached to GameObject in scene
- Confirm EnvironmentController reference is assigned

Problem: "TensorBoard launch failed"
Solution:
- Install TensorBoard: pip install tensorboard
- Manually launch: tensorboard --logdir=Python/algorithms/ppo/LogsAndVisualisations/tensorboard
- Check no other TensorBoard instances running

Problem: "Connection lost during training"
Solution:
- The system automatically attempts reconnection
- If training pauses, check Unity hasn't crashed
- Model is saved automatically before exit
- Resume training from checkpoint if needed

Problem: "Path too long" errors
Solution:
- System automatically creates junctions for long paths
- Junctions stored in C:/Junctions/
- No manual intervention required

Problem: Training is slow
Solution:
- Check Unity frame rate (should be uncapped in build)
- Monitor CPU/GPU usage
- Reduce N_STEPS if memory constrained
- Consider reducing observation/action space complexity

Problem: Reward not improving
Solution:
- Check reward function in EnvironmentController.cs
- Monitor TensorBoard for value_loss (should decrease)
- Try adjusting learning rate
- Verify observations contain useful information
- Ensure action space is appropriate

Problem: TensorBoard shows "No dashboards are active for the current data set"
Solution:
- Ensure training ran for at least one policy update (~2048 steps with default config)
- Check event files are not empty (should be >1KB after first update)
- Verify tensorboard directory contains subdirectories with .tfevents files
- Confirm log_interval parameter in model.learn() is not None (should be 1 or higher)
- Check TensorBoard is pointing to correct directory (parent of run directories)


================================================================================
12. BEST PRACTICES
================================================================================

Development Workflow:
---------------------
1. Make small changes to Unity environment or config
2. Run short training session (reduce TOTAL_TIMESTEPS temporarily)
3. Verify training runs without errors
4. Monitor TensorBoard to check learning progress
5. Iterate based on results

Model Development:
------------------
- Start with default hyperparameters
- Train multiple runs to assess stability
- Use TensorBoard to compare hyperparameter variations
- Save best performing models
- Document successful configurations

Code Maintenance:
-----------------
- Keep config.py as single source of truth
- Add comments when modifying training scripts
- Run tests after significant changes
- Use version control (Git) for tracking changes


================================================================================
13. ADDITIONAL RESOURCES
================================================================================

Stable-Baselines3 Documentation:
https://stable-baselines3.readthedocs.io/

Gymnasium Documentation:
https://gymnasium.farama.org/

TensorBoard Guide:
https://www.tensorflow.org/tensorboard

PPO Algorithm Paper:
https://arxiv.org/abs/1707.06347


================================================================================
14. SUPPORT & CONTRIBUTION
================================================================================

For questions or issues:
- Review this README thoroughly
- Check Troubleshooting section
- Examine session logs in run directories
- Test with provided test suite

The codebase is designed to be self-explanatory with comprehensive comments.
Refer to inline documentation in Python and C# files for implementation details.


================================================================================
END OF README
================================================================================
