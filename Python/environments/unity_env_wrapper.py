"""
Gymnasium-compatible wrapper for Unity ML environments.

Provides standard Gymnasium interface (step/reset/close) over UnitySocketClient,
enabling seamless integration with Stable-Baselines3 and other RL frameworks.
"""

import numpy as np
import gymnasium as gym
from gymnasium import spaces
import logging
from typing import Tuple, Dict, Any, Optional
from environments.unity_socket_client import UnitySocketClient
from configs.config import Connection, Colors


logger = logging.getLogger(__name__)


class UnityEnvWrapper(gym.Env):
    """
    Gymnasium environment wrapper for Unity simulations.
    
    Communicates with Unity via UnitySocketClient, translating between
    Gymnasium's standard interface and Unity's binary protocol.
    """
    
    metadata = {"render_modes": []}
    
    def __init__(self, host: str = None, port: int = None, timeout: float = None, 
                 auto_reconnect: bool = True):
        """
        Initializes Unity environment wrapper.
        
        Establishes connection to Unity and discovers observation/action spaces
        through handshake protocol.
        
        :param host: Unity server IP address (uses config default if None)
        :param port: Unity server port (uses config default if None)
        :param timeout: Socket timeout in seconds (uses config default if None)
        :param auto_reconnect: Automatically attempt reconnection on connection loss
        """
        super().__init__()
        
        self.auto_reconnect = auto_reconnect
        
        # Initialize socket client
        self.client = UnitySocketClient(host, port, timeout)
        
        # Establish connection and perform handshake
        try:
            self.client.connect()
        except (ConnectionError, TimeoutError) as e:
            logger.error(f"{Colors.ERROR}Failed to connect to Unity: {e}{Colors.RESET}")
            raise
        
        # Construct Gymnasium spaces from handshake data
        self._build_spaces()
        
        # Episode tracking
        self.current_obs = np.zeros(self.client.obs_len, dtype=np.float32)
        self._episode_step = 0
        self._episode_reward = 0.0
    
    def _build_spaces(self) -> None:
        """
        Constructs Gymnasium observation and action spaces.
        
        Uses dimensions discovered during handshake. Spaces are continuous
        (Box type) with infinite observation bounds and [-1, 1] action bounds.
        """
        obs_len = self.client.obs_len
        act_len = self.client.act_len
        
        self.observation_space = spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(obs_len,),
            dtype=np.float32
        )
        
        self.action_space = spaces.Box(
            low=-1.0,
            high=1.0,
            shape=(act_len,),
            dtype=np.float32
        )
        
        logger.debug(f"Spaces built: obs={obs_len}, act={act_len}")
    
    # ============================================================================
    # Gymnasium Interface
    # ============================================================================
    
    def reset(self, seed: Optional[int] = None, options: Optional[Dict[str, Any]] = None) -> Tuple[np.ndarray, Dict[str, Any]]:
        """
        Resets environment to initial state.
        
        Sends reset command to Unity with random seed. Unity reinitializes
        simulation and returns to starting conditions.
        
        :param seed: Random seed for environment (generated if None)
        :param options: Additional reset options (unused)
        :return: Tuple of (initial_observation, info_dict)
        :raises ConnectionError: If reset fails and reconnection unsuccessful
        """
        super().reset(seed=seed)
        
        # Generate seed if not provided
        if seed is None:
            seed = np.random.randint(0, 2**31 - 1)
        
        # Attempt reset with automatic reconnection
        for attempt in range(2):  # Try once, reconnect if needed, try again
            try:
                self.client.send_reset(seed)
                break
            except ConnectionError as e:
                if attempt == 0 and self.auto_reconnect:
                    logger.warning(f"{Colors.WARNING}Reset failed, attempting reconnection...{Colors.RESET}")
                    if not self.client.reconnect():
                        raise ConnectionError("Reset failed and reconnection unsuccessful") from e
                else:
                    raise
        
        # Reset episode tracking
        self._episode_step = 0
        self._episode_reward = 0.0
        
        # Unity doesn't provide initial observation on reset; use zero vector
        self.current_obs = np.zeros(self.client.obs_len, dtype=np.float32)
        
        info = {"seed": seed}
        return self.current_obs.copy(), info
    
    def step(self, action: np.ndarray) -> Tuple[np.ndarray, float, bool, bool, Dict[str, Any]]:
        """
        Executes one environment step with given action.
        
        Sends action to Unity, receives observation/reward/done response.
        Implements automatic reconnection on connection loss.
        
        :param action: Action vector in [-1, 1] range
        :return: Tuple of (observation, reward, terminated, truncated, info)
        :raises ConnectionError: If step fails and reconnection unsuccessful
        """
        # Attempt step with automatic reconnection
        for attempt in range(2):
            try:
                obs, reward, done = self.client.send_action(action)
                break
            except ConnectionError as e:
                if attempt == 0 and self.auto_reconnect:
                    logger.warning(f"{Colors.WARNING}Step failed, attempting reconnection...{Colors.RESET}")
                    if not self.client.reconnect():
                        raise ConnectionError("Step failed and reconnection unsuccessful") from e
                    # After reconnect, need to reset environment state
                    self.reset()
                else:
                    raise
        
        # Update internal state
        self.current_obs = obs
        self._episode_step += 1
        self._episode_reward += reward
        
        # Gymnasium distinguishes terminated (done) vs truncated (timeout)
        # Unity only provides single 'done' flag; treat as termination
        terminated = done
        truncated = False
        
        info = {
            "episode_step": self._episode_step,
            "episode_reward": self._episode_reward
        }
        
        return obs, reward, terminated, truncated, info
    
    def render(self) -> None:
        """
        Render method (no-op for Unity environments).
        
        Unity handles its own rendering internally. This method exists for
        Gymnasium API compatibility.
        """
        pass
    
    def close(self) -> None:
        """Closes connection to Unity server."""
        self.client.close()
        logger.debug("Unity environment closed")
    
    # ============================================================================
    # Additional Methods
    # ============================================================================
    
    def force_reconnect(self) -> bool:
        """
        Manually triggers reconnection attempt.
        
        Useful for recovering from known connection issues.
        
        :return: True if reconnection successful
        """
        return self.client.reconnect()
    
    @property
    def is_connected(self) -> bool:
        """Returns True if currently connected to Unity."""
        return self.client.is_connected
