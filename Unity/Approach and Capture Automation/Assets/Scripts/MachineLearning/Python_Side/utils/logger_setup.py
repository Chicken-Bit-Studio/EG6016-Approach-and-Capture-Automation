"""
Creates and maintains structured logging directories for each training run.
Handles:
  • Timestamped 'run_YYYYMMDD_HHMMSS' folder creation.
  • Enforcement of MAX_RUN_HISTORY retention policy.
  • Construction of TensorBoard-compatible log directories.
  • Optional CSV summary creation for quick statistics export.
"""

import os
import time
import shutil
import logging
import csv
from .config import MAX_RUN_HISTORY


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
    """
    existing = [os.path.join(log_root, d) for d in os.listdir(log_root)
                if os.path.isdir(os.path.join(log_root, d))]
    existing.sort(reverse=True)  # newest first

    for old_dir in existing[max_runs:]:
        try:
            shutil.rmtree(old_dir)
            print(f"[logger_setup] Removed old run directory: {old_dir}")
        except Exception as e:
            print(f"[logger_setup] Warning: Could not remove {old_dir}: {e}")


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