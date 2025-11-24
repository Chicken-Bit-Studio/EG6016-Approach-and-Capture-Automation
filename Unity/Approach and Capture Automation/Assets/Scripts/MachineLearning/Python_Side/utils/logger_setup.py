"""
Creates and maintains structured logging directories for each training run.
Handles:
  - Timestamped 'run_YYYYMMDD_HHMMSS' folder creation.
  - Enforcement of MAX_RUN_HISTORY retention policy.
  - Construction of TensorBoard-compatible log directories.
  - Optional CSV summary creation for quick statistics export.
"""

import os
import time
import shutil
import logging
import csv
import platform
import subprocess
import webbrowser
import socket
from utils.config import MAX_RUN_HISTORY

def initialise_logger(algorithm_name: str, base_path: str) -> tuple[str, logging.Logger]:
    """
    Creates a new session folder and returns its path and logger handle.
    """

    # algorithm directory layout
    algo_dir = os.path.join(base_path, algorithm_name)
    log_root = os.path.join(algo_dir, "LogsAndVisualisations")

    os.makedirs(log_root, exist_ok=True)

    # --- Retention policy: keep newest N runs ---
    _enforce_retention(log_root, MAX_RUN_HISTORY)

    # Create new timestamped run folder
    run_id = time.strftime("run_%Y%m%d_%H%M%S")
    run_dir = os.path.join(log_root, run_id)
    os.makedirs(run_dir, exist_ok=True)

    # Configure Python logging
    log_file = os.path.join(run_dir, "session.log")
    logger = logging.getLogger(algorithm_name)
    logger.setLevel(logging.INFO)
    logger.handlers.clear()

    file_handler = logging.FileHandler(log_file, mode="w")
    stream_handler = logging.StreamHandler()

    formatter = logging.Formatter("%(asctime)s [%(levelname)s] %(message)s",
                                  datefmt="%H:%M:%S")
    file_handler.setFormatter(formatter)
    stream_handler.setFormatter(formatter)

    logger.addHandler(file_handler)
    logger.addHandler(stream_handler)

    logger.info(f"Logging initialised for {algorithm_name}. Output -> {run_dir}")
    return run_dir, logger


def _enforce_retention(log_root: str, max_runs: int):
    """
    Deletes oldest run directories in 'log_root' beyond the specified limit.
    Also deletes associated Unity .meta files.
    """
    # Collect full paths to all directories inside log_root
    existing = [
        os.path.join(log_root, d)
        for d in os.listdir(log_root)
        if os.path.isdir(os.path.join(log_root, d))
    ]

    # Sort newest first (relies on timestamped or sortable directory names)
    existing.sort(reverse=True)

    # Delete everything older than max_runs
    for old_dir in existing[max_runs+1:]:
        try:
            # Delete the directory itself
            shutil.rmtree(old_dir)

            # Try deleting the Unity .meta file
            meta_path = old_dir + ".meta"
            if os.path.exists(meta_path):
                os.remove(meta_path)

        except Exception as e:
            print(f"[logger_setup] Warning: Could not remove {old_dir}: {e}")


# ---------------------------------------------------------------------------
# TensorBoard utilities
# ---------------------------------------------------------------------------

def is_port_in_use(port):
    """Check if a port is already in use."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        return s.connect_ex(('localhost', port)) == 0

def launch_tensorboard(log_dir, port=6006):
    """
    Launches TensorBoard in a separate process and opens browser.
    Kills any existing TensorBoard processes first.
    
    Parameters
    ----------
    log_dir : str
        Path to the tensorboard logs directory
    port : int
        Port for TensorBoard web interface (default: 6006)
    
    Returns
    -------
    subprocess.Popen
        The TensorBoard process (so you can kill it later if needed)
    """
    try:
        # Check if TensorBoard is already running and kill it
        if is_port_in_use(port):
            print(f"\033[93m[TensorBoard] Port {port} in use, killing existing TensorBoard...\033[0m")
            if platform.system() == "Windows":
                subprocess.run(["taskkill", "/F", "/IM", "tensorboard.exe"], 
                              capture_output=True, check=False)
                time.sleep(2)  # Give it time to die
            else:
                # For Linux/Mac, find and kill the process using the port
                subprocess.run(["pkill", "-f", "tensorboard"], check=False)
                time.sleep(2)
        
        # Launch TensorBoard as a background process
        print(f"\033[96m[TensorBoard] Launching on port {port}...\033[0m")
        
        # Use CREATE_NEW_CONSOLE on Windows to open in separate window
        if platform.system() == "Windows":
            tensorboard_process = subprocess.Popen(
                ["tensorboard", "--logdir", log_dir, "--port", str(port)],
                creationflags=subprocess.CREATE_NEW_CONSOLE,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )
        else:
            tensorboard_process = subprocess.Popen(
                ["tensorboard", "--logdir", log_dir, "--port", str(port)],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )
        
        # Give TensorBoard a moment to start up
        time.sleep(3)
        
        # Open browser automatically
        url = f"http://localhost:{port}"
        print(f"\033[92m[TensorBoard] Opening browser at {url}\033[0m")
        webbrowser.open(url)
        
        return tensorboard_process
        
    except FileNotFoundError:
        print(f"\033[91m[TensorBoard] Error: 'tensorboard' command not found.\033[0m")
        print(f"\033[93m[TensorBoard] Install with: pip install tensorboard\033[0m")
        return None
    except Exception as e:
        print(f"\033[91m[TensorBoard] Failed to launch: {e}\033[0m")
        return None

# ---------------------------------------------------------------------------
# CSV summary utilities
# ---------------------------------------------------------------------------

def create_csv_logger(csv_path: str, fieldnames: list[str]):
    """
    Prepares a CSV file for writing episode-level statistics.
    """
    with open(csv_path, mode="w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()


def append_csv_row(csv_path: str, row: dict):
    """Appends a single record to the CSV log."""
    with open(csv_path, mode="a", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=row.keys())
        writer.writerow(row)