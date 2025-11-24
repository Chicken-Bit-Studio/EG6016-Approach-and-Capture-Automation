import os
import subprocess
import sys
from pathlib import Path

# Windows MAX_PATH is 260, but setting a safe threshold well below this
SAFE_PATH_LENGTH = 200

# ANSI color code for red text
RED = '\033[91m'
RESET = '\033[0m'

def handle_long_path(path, junctionName):
    """
    Creates or reuses a junction to shorten long Windows paths.
    
    Args:
        path: Absolute path to a system file or directory
        junctionName: Name for the junction to create in C:/Junctions
        
    Returns:
        The original path if it's short enough, otherwise returns the junction path
    """
    # Convert to absolute path and normalize
    path = os.path.abspath(path)
    
    # Check if path exceeds safe length threshold
    if len(path) <= SAFE_PATH_LENGTH:
        return path
    
    # Define junction directory and full junction path
    junction_dir = Path("C:/Junctions")
    junction_path = junction_dir / junctionName
    
    # Ensure C:/Junctions directory exists
    try:
        junction_dir.mkdir(parents=True, exist_ok=True)
    except Exception as e:
        print(f"{RED}Error creating junction directory: {e}{RESET}", file=sys.stderr)
        return path
    
    # Check if junction already exists
    if junction_path.exists():
        try:
            # Get the target of the existing junction
            result = subprocess.run(
                ['fsutil', 'reparsepoint', 'query', str(junction_path)],
                capture_output=True,
                text=True,
                check=True
            )
            
            # Parse the target from fsutil output
            existing_target = None
            for line in result.stdout.split('\n'):
                if 'Print Name:' in line:
                    existing_target = line.split('Print Name:')[1].strip()
                    break
            
            # Normalize both paths for comparison
            if existing_target:
                existing_target = os.path.abspath(existing_target)
                
                # If junction points to the same destination, reuse it
                if os.path.normcase(existing_target) == os.path.normcase(path):
                    return str(junction_path)
                
                # Junction exists but points to different location, delete it
                try:
                    subprocess.run(
                        ['rmdir', str(junction_path)],
                        check=True,
                        shell=True,
                        capture_output=True
                    )
                except subprocess.CalledProcessError as e:
                    print(f"{RED}Error removing existing junction: {e}{RESET}", file=sys.stderr)
                    return path
                    
        except subprocess.CalledProcessError as e:
            print(f"{RED}Error querying existing junction: {e}{RESET}", file=sys.stderr)
            return path
    
    # Create new junction
    try:
        subprocess.run(
            ['mklink', '/J', str(junction_path), path],
            check=True,
            shell=True,
            capture_output=True
        )
        return str(junction_path)
    except subprocess.CalledProcessError as e:
        print(f"{RED}Error creating junction: {e}{RESET}", file=sys.stderr)
        return path