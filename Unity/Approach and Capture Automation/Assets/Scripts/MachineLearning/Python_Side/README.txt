===================================================================================
Machine Learning Interface for Unity Simulation - Approach and Capture Automation
===================================================================================

This document describes how to set up, run, and interpret Python-side training
scripts that communicate with this Unity environment via TCP.

-------------------------------------------------------------------------------
1. Folder Structure Overview
-------------------------------------------------------------------------------

Assets/
└── Scripts/
    └── MachineLearning/
        └── Python_Side/
            ├── unity_remote/
            │   ├── unity_socket_client.py
            │   └── unity_env_wrapper.py
            │
            ├── utils/
            │   ├── config.py
            │   └── logger_setup.py
            │
            ├── algorithms/
            │   ├── ppo/
            │   │   ├── train_ppo.py
            │   │   └── LogsAndVisualisations/
            │   └── (other algorithms may follow similar structure)
            │
            └── README.txt  ← This file

-------------------------------------------------------------------------------
2. Prerequisites
-------------------------------------------------------------------------------

1. Unity scene requirements
   - The Unity scene must be ran in the Editor - not a standalone build.
   - The Unity scene must contain an active GameObject hosting the
     "EnvironmentController" and "EnvironmentSocketServer.cs" components, with 
     their public properties correctly assigned as described.
   - The "Port" value within that script must match the Python constant
     UNITY_PORT in utils/config.py (default: 5005).

2. Python environment
   - Python version 3.9+ recommended.
   - Required packages (installed once via PowerShell/terminal):
         pip install stable-baselines3 torch gymnasium
   - TensorBoard for visualization:
         pip install tensorboard

3. Connectivity
   - Unity must be running **before** the Python script starts.
   - Ensure the system’s firewall allows TCP communication on the chosen port.

-------------------------------------------------------------------------------
3. Running a Training Session
-------------------------------------------------------------------------------

Example (PowerShell or terminal inside Python_Side folder):

    python algorithms/ppo/train_ppo.py

Steps performed:
  1. Python connects to Unity's socket server.
  2. A handshake occurs exchanging observation and action vector lengths.
  3. PPO training begins using hyperparameters defined in utils/config.py.
  4. Logs, CSV summaries, TensorBoard data, and model checkpoints are created
     under:
        algorithms/ppo/LogsAndVisualisations/run_YYYYMMDD_HHMMSS/

-------------------------------------------------------------------------------
4. Understanding Outputs
-------------------------------------------------------------------------------

Each algorithm’s LogsAndVisualisations folder contains multiple runs:

    LogsAndVisualisations/
        run_20240316_182200/
           |- session.log              Raw textual log of this training run.
           |- progress_summary.csv     Simple numeric record of reward metrics.
           |- best_model/              Saved best-performing model (optional).
           |- final_model.zip          Final trained model checkpoint.
           |- events.out.tfevents...   TensorBoard data files.

Retention policy (see utils/config.py):
  - The newest MAX_RUN_HISTORY runs (default = 5) are retained.
  - Older folders are automatically deleted on startup of new sessions.

-------------------------------------------------------------------------------
5. Viewing Results with TensorBoard
-------------------------------------------------------------------------------

TensorBoard provides live graphs for rewards, losses, and evaluation metrics.

Start TensorBoard in PowerShell / terminal:

    tensorboard --logdir algorithms/ppo/LogsAndVisualisations

Then open the provided localhost address (usually http://localhost:6006)
in a web browser.

Graphs update in real time while training is active.

-------------------------------------------------------------------------------
6. Console Output During Training
-------------------------------------------------------------------------------

During training, progress messages will appear in the console such as:

    [ PPO Training | Step 60000 ]
      - Mean Reward (100 ep): 114.2
      - Episodes: 48

These updates are governed by CONSOLE_UPDATE_INTERVAL defined in config.py.

-------------------------------------------------------------------------------
7. Adapting for New Algorithms
-------------------------------------------------------------------------------

To add another algorithm:

  1. Create a new subfolder under algorithms/ (e.g., algorithms/sac/).
  2. Copy an existing train_*.py file as a template.
  3. Import and adapt any algorithm-specific hyperparameters.
  4. Logs and visualisations will still appear under a separate directory:
        algorithms/<alg_name>/LogsAndVisualisations

-------------------------------------------------------------------------------
8. Configuration Notes
-------------------------------------------------------------------------------

Common constants preset in utils/config.py include:

    UNITY_HOST / UNITY_PORT          Network connection to Unity.
    MAX_RUN_HISTORY                  Run retention count.
    LEARNING_RATE, GAMMA, etc.       Universal RL hyperparameters.
    TOTAL_TIMESTEPS, EVAL_FREQ, ...  Training scales and schedules.

Changes to these will affect **every** training algorithm.

-------------------------------------------------------------------------------
9. Troubleshooting
-------------------------------------------------------------------------------

 - "ConnectionRefusedError":
      Ensure Unity is running with EnvironmentSocketServer active.

 - "Unexpected opcode" or timeouts:
      Port mismatch or environment not ready; restart Unity and re-run script.

 - Training extremely slow or unstable:
      Tune LEARNING_RATE or N_STEPS in utils/config.py.

 - TensorBoard shows older runs only:
      Ensure you launch TensorBoard pointing at the base LogsAndVisualisations
      folder containing multiple run_YYYYMMDD_HHMMSS subfolders.

-------------------------------------------------------------------------------
10. Recommended Workflow Overview
-------------------------------------------------------------------------------

1. Launch Unity scene containing the server component.
2. Open a terminal in:
       Assets/Scripts/MachineLearning/Python_Side/
3. Start the desired algorithm’s training script.
4. Monitor live output in terminal and TensorBoard.
5. Review saved models and CSV summaries in algorithm folder.
6. Optional: adjust configuration in utils/config.py for next session.

-------------------------------------------------------------------------------
End of README
-------------------------------------------------------------------------------