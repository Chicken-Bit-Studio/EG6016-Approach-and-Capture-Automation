"""
Windows path handling utilities for managing long file paths.

Windows has a MAX_PATH limitation of 260 characters. This module provides
junction-based workarounds to handle paths exceeding safe limits.
"""

import os
import subprocess
import sys
from pathlib import Path
from configs.config import Paths, Colors


def handle_long_path(path: str, junction_name: str) -> str:
    """
    Creates or reuses a junction to shorten excessively long Windows paths.
    
    If the provided path exceeds the safe length threshold, creates a junction
    in the configured junction root directory. If the path is already short
    enough, returns it unchanged.
    
    :param path: Absolute path to directory
    :param junction_name: Name for the junction to create
    :return: Original path if short enough, otherwise junction path
    :raises OSError: If junction creation fails
    """
    # Normalize to absolute path
    path = os.path.abspath(path)
    
    # Return immediately if path is within safe limits
    if len(path) <= Paths.SAFE_PATH_LENGTH:
        return path
    
    # Define junction paths
    junction_dir = Path(Paths.JUNCTION_ROOT)
    junction_path = junction_dir / junction_name
    
    # Ensure junction root directory exists
    try:
        junction_dir.mkdir(parents=True, exist_ok=True)
    except Exception as e:
        print(f"{Colors.ERROR}Failed to create junction directory: {e}{Colors.RESET}", 
              file=sys.stderr)
        return path
    
    # Check if junction already exists and points to correct target
    if junction_path.exists():
        existing_target = _get_junction_target(junction_path)
        
        # Reuse if pointing to same location
        if existing_target and os.path.normcase(existing_target) == os.path.normcase(path):
            return str(junction_path)
        
        # Remove if pointing elsewhere
        _remove_junction(junction_path)
    
    # Create new junction
    return _create_junction(junction_path, path)


def _get_junction_target(junction_path: Path) -> str | None:
    """
    Retrieves the target path of an existing junction.
    
    :param junction_path: Path to junction
    :return: Target path if successful, None otherwise
    """
    try:
        result = subprocess.run(
            ['fsutil', 'reparsepoint', 'query', str(junction_path)],
            capture_output=True,
            text=True,
            check=True
        )
        
        # Parse target from fsutil output
        for line in result.stdout.split('\n'):
            if 'Print Name:' in line:
                target = line.split('Print Name:')[1].strip()
                return os.path.abspath(target)
        
        return None
        
    except subprocess.CalledProcessError:
        return None


def _remove_junction(junction_path: Path) -> bool:
    """
    Removes an existing junction.
    
    :param junction_path: Path to junction to remove
    :return: True if successful, False otherwise
    """
    try:
        subprocess.run(
            ['rmdir', str(junction_path)],
            check=True,
            shell=True,
            capture_output=True
        )
        return True
    except subprocess.CalledProcessError as e:
        print(f"{Colors.WARNING}Failed to remove junction {junction_path}: {e}{Colors.RESET}", 
              file=sys.stderr)
        return False


def _create_junction(junction_path: Path, target_path: str) -> str:
    """
    Creates a new junction pointing to target path.
    
    :param junction_path: Path where junction will be created
    :param target_path: Path that junction should point to
    :return: Junction path if successful, original path if failed
    """
    try:
        subprocess.run(
            ['mklink', '/J', str(junction_path), target_path],
            check=True,
            shell=True,
            capture_output=True
        )
        return str(junction_path)
    except subprocess.CalledProcessError as e:
        print(f"{Colors.ERROR}Failed to create junction: {e}{Colors.RESET}", 
              file=sys.stderr)
        return target_path


def ensure_directory_exists(path: str) -> bool:
    """
    Creates directory if it doesn't exist, handling long paths automatically.
    
    :param path: Directory path to create
    :return: True if directory exists or was created successfully
    """
    try:
        os.makedirs(path, exist_ok=True)
        return True
    except OSError as e:
        print(f"{Colors.ERROR}Failed to create directory {path}: {e}{Colors.RESET}", 
              file=sys.stderr)
        return False
