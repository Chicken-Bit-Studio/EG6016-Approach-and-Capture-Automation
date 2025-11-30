"""
TensorBoard process management and launch utilities.

Handles automatic TensorBoard startup, port conflict resolution, and browser
integration for monitoring training progress.
"""

import subprocess
import time
import webbrowser
import socket
from typing import Optional
from configs.config import TensorBoard as TBConfig, Colors


def is_port_in_use(port: int) -> bool:
    """
    Checks if a TCP port is currently in use.
    
    :param port: Port number to check
    :return: True if port is in use, False otherwise
    """
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        return s.connect_ex(('localhost', port)) == 0


def kill_existing_tensorboard() -> bool:
    """
    Terminates any existing TensorBoard processes.
    
    Uses taskkill on Windows to forcefully terminate tensorboard.exe processes.
    Waits briefly to ensure process termination completes.
    
    :return: True if kill command executed successfully
    """
    try:
        subprocess.run(
            ["taskkill", "/F", "/IM", "tensorboard.exe"],
            capture_output=True,
            check=False  # Don't raise if no process found
        )
        time.sleep(2)  # Allow time for process cleanup
        return True
    except Exception as e:
        print(f"{Colors.WARNING}Failed to kill existing TensorBoard: {e}{Colors.RESET}")
        return False


def launch_tensorboard(log_dir: str, port: int = None) -> Optional[subprocess.Popen]:
    """
    Launches TensorBoard in a separate process and optionally opens browser.
    
    Handles port conflicts by killing existing instances, launches TensorBoard
    with specified log directory, and opens the web interface in default browser
    if configured to do so.
    
    :param log_dir: Path to directory containing TensorBoard log files
    :param port: Port for TensorBoard web interface (uses config default if None)
    :return: Process handle if successful, None if launch failed
    """
    if port is None:
        port = TBConfig.PORT
    
    # Handle port conflicts
    if is_port_in_use(port):
        print(f"{Colors.WARNING}Port {port} in use, terminating existing TensorBoard...{Colors.RESET}")
        kill_existing_tensorboard()
        
        # Verify port is now free
        if is_port_in_use(port):
            print(f"{Colors.ERROR}Failed to free port {port}. TensorBoard may not launch.{Colors.RESET}")
    
    # Launch TensorBoard process
    try:
        print(f"{Colors.INFO}Launching TensorBoard on port {port}...{Colors.RESET}")
        
        process = subprocess.Popen(
            ["tensorboard", "--logdir", log_dir, "--port", str(port)],
            creationflags=subprocess.CREATE_NO_WINDOW,  # Windows: don't open a console window
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL
        )
        
        # Wait for TensorBoard to start up
        time.sleep(TBConfig.STARTUP_WAIT)
        
        # Verify process is still running
        if process.poll() is not None:
            print(f"{Colors.ERROR}TensorBoard process terminated unexpectedly{Colors.RESET}")
            return None
        
        # Open browser if configured
        if TBConfig.AUTO_OPEN_BROWSER:
            url = f"http://localhost:{port}"
            print(f"{Colors.SUCCESS}Opening TensorBoard at {url}{Colors.RESET}")
            webbrowser.open(url)
        
        return process
        
    except FileNotFoundError:
        print(f"{Colors.ERROR}TensorBoard not found. Install with: pip install tensorboard{Colors.RESET}")
        return None
    except Exception as e:
        print(f"{Colors.ERROR}Failed to launch TensorBoard: {e}{Colors.RESET}")
        return None


def verify_tensorboard_running(port: int = None) -> bool:
    """
    Verifies that TensorBoard is accessible on the specified port.
    
    :param port: Port to check (uses config default if None)
    :return: True if TensorBoard is responding
    """
    if port is None:
        port = TBConfig.PORT
    
    return is_port_in_use(port)


def stop_tensorboard(process: subprocess.Popen) -> bool:
    """
    Gracefully stops a TensorBoard process.
    
    :param process: TensorBoard process handle
    :return: True if stopped successfully
    """
    if process is None:
        return False
    
    try:
        process.terminate()
        process.wait(timeout=5)
        return True
    except subprocess.TimeoutExpired:
        process.kill()
        return True
    except Exception as e:
        print(f"{Colors.WARNING}Failed to stop TensorBoard: {e}{Colors.RESET}")
        return False
