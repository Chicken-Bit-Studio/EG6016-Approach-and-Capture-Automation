/*
 * UnityMLServer.cs
 * 
 * TCP server for Unity-Python machine learning communication.
 * 
 * Responsibilities:
 *   - Accept TCP client connections (sequential, one at a time)
 *   - Transmit environment metadata via handshake (obs/action sizes)
 *   - Process opcode-based commands (STEP, RESET)
 *   - Thread-safe communication between network and Unity main thread
 *   - Graceful handling of disconnects and reconnections
 * 
 * Thread Safety:
 *   - Network I/O on background thread
 *   - Unity API calls only on main thread
 *   - AutoResetEvent synchronization for data exchange
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using UnityEngine;

/// <summary>
/// Main server component for Unity-Python ML communication.
/// Attach to GameObject and assign EnvironmentController reference.
/// </summary>
public class UnityMLServer : MonoBehaviour
{
    #region Configuration

    [Header("Unity Environment Link")]
    [Tooltip("Environment controller managing simulation stepping and reset")]
    public EnvironmentController envController;
    private int obsLen, actLen;

    [Header("Network Settings")]
    [Tooltip("TCP port for Python client connections")]
    public int port = 5005;

    #endregion

    #region Internal State

    private ServerState state;
    private Thread listenThread;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        // Delay initialization to ensure EnvironmentController is ready
        StartCoroutine(DelayedStart());
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        yield return null;  // Wait one frame

        // Validate configuration
        if (envController == null)
        {
            Debug.LogError("[UnityMLServer] EnvironmentController reference missing. Disabling server.");
            enabled = false;
            yield break;
        }

        // Get observation and action dimensions
        (obsLen, actLen) = envController.GetObservationAndActionArraySizes();

        // Initialize server state
        state = new ServerState(envController, port);

        // Start network listener thread
        listenThread = new Thread(NetworkThread);
        listenThread.IsBackground = true;
        listenThread.Start();

        state.running = true;
        Debug.Log($"[UnityMLServer] Server started on port {port}");
    }

    void Update()
    {
        // Process any pending commands on Unity main thread
        if (state != null) { state.ProcessPendingCommands(); }
    }

    void OnApplicationQuit()
    {
        // Signal shutdown
        state.running = false;

        // Wake any waiting threads
        state.commandAvailable.Set();
        state.responseReady.Set();

        // Close listener
        try { state.listener?.Stop(); }
        catch { }

        // Wait for thread to finish
        listenThread?.Join(500);

        Debug.Log("[UnityMLServer] Server stopped");
    }

    #endregion

    #region Network Thread

    /// <summary>
    /// Network thread: Listens for client connections and processes messages.
    /// Runs independently of Unity's main thread for non-blocking network I/O.
    /// </summary>
    private void NetworkThread()
    {
        try
        {
            state.listener = new TcpListener(IPAddress.Any, port);
            state.listener.Start();

            Debug.Log($"[UnityMLServer] Listening for connections on port {port}");

            while (state.running)
            {
                // Accept client connection (blocking)
                TcpClient client = null;
                try
                {
                    // Check if connection pending (non-blocking check)
                    if (!state.listener.Pending())
                    {
                        Thread.Sleep(100);  // Avoid busy-wait
                        continue;
                    }

                    client = state.listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    if (!state.running) break;
                    continue;
                }

                // Handle client session
                HandleClientSession(client);
            }
        }
        catch (SocketException ex)
        {
            // Benign case: Application quit interrupted socket
            if (!state.running && ex.SocketErrorCode == SocketError.Interrupted)
                return;

            Debug.LogError($"[UnityMLServer] Socket error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMLServer] Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles communication with a single connected client.
    /// Runs on network thread.
    /// </summary>
    private void HandleClientSession(TcpClient client)
    {
        Debug.Log("[UnityMLServer] Client connected");

        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (BinaryReader reader = new BinaryReader(stream))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                stream.ReadTimeout = 60000;  // 60s
                stream.WriteTimeout = 2000;  // 2s

                // Send initial handshake
                SendHandshake(writer);

                // Message processing loop
                while (state.running && client.Connected)
                {
                    // Read opcode
                    int opcode;
                    try
                    {
                        opcode = reader.ReadInt32();
                    }
                    catch (IOException)
                    {
                        // Connection lost or read error
                        break;
                    }

                    // Process command based on opcode
                    bool continueSession = ProcessCommand(opcode, reader, writer);
                    if (!continueSession)
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityMLServer] Client session error: {ex.Message}");
        }
        finally
        {
            Debug.Log("[UnityMLServer] Client disconnected");
        }
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Processes a command based on received opcode.
    /// Runs on network thread.
    /// </summary>
    /// <returns>True to continue session, false to disconnect</returns>
    private bool ProcessCommand(int opcode, BinaryReader reader, BinaryWriter writer)
    {
        switch (opcode)
        {
            case MessageProtocol.OPCODE_RESET:
                return HandleReset(reader, writer);

            case MessageProtocol.OPCODE_STEP:
                return HandleStep(reader, writer);

            default:
                Debug.LogWarning($"[UnityMLServer] Unknown opcode {opcode}, disconnecting client");
                return false;
        }
    }

    /// <summary>
    /// Handles RESET command: Reinitialize environment with seed.
    /// </summary>
    private bool HandleReset(BinaryReader reader, BinaryWriter writer)
    {
        try
        {
            // Read seed
            int seed = reader.ReadInt32();

            // Queue reset command for Unity main thread
            state.QueueReset(seed);

            // Wait for Unity to complete reset
            state.responseReady.WaitOne();

            // Send acknowledgment
            writer.Write(0);  // Legacy empty int for compatibility
            writer.Flush();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMLServer] Reset error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Handles STEP command: Execute action and return observation.
    /// </summary>
    private bool HandleStep(BinaryReader reader, BinaryWriter writer)
    {
        try
        {
            // Read action
            int actionLen = reader.ReadInt32();
            float[] action = new float[actionLen];
            for (int i = 0; i < actionLen; i++)
                action[i] = reader.ReadSingle();

            // Queue step command for Unity main thread
            state.QueueStep(action);

            // Wait for Unity to complete step
            state.responseReady.WaitOne();

            // Send response
            byte[] response = state.GetResponse();
            writer.Write(response.Length);
            writer.Write(response);
            writer.Flush();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMLServer] Step error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends handshake packet with obs/action dimensions.
    /// </summary>
    private void SendHandshake(BinaryWriter writer)
    {
        try
        {
            writer.Write(MessageProtocol.OPCODE_HANDSHAKE);
            writer.Write(obsLen);
            writer.Write(actLen);
            writer.Flush();

            Debug.Log($"[UnityMLServer] Handshake sent: obs={obsLen}, act={actLen}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMLServer] Handshake error: {ex.Message}");
        }
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Message protocol constants.
/// Must match Python client opcodes.
/// </summary>
internal static class MessageProtocol
{
    public const int OPCODE_STEP = 0;
    public const int OPCODE_RESET = 1;
    public const int OPCODE_HANDSHAKE = 100;
}

/// <summary>
/// Server state and thread synchronization.
/// Manages communication between network and Unity threads.
/// </summary>
internal class ServerState
{
    // Environment controller reference
    public EnvironmentController envController;

    // Network state
    public TcpListener listener;
    public volatile bool running = false;

    // Thread synchronization
    public AutoResetEvent commandAvailable = new AutoResetEvent(false);
    public AutoResetEvent responseReady = new AutoResetEvent(false);

    // Command queue (single-command queue for simplicity)
    private volatile CommandType pendingCommandType = CommandType.None;
    private volatile float[] pendingAction = null;
    private volatile int pendingSeed = 0;
    private byte[] pendingResponse = null;

    public ServerState(EnvironmentController controller, int port)
    {
        this.envController = controller;
    }

    /// <summary>
    /// Queues a reset command for Unity main thread.
    /// Called from network thread.
    /// </summary>
    public void QueueReset(int seed)
    {
        pendingCommandType = CommandType.Reset;
        pendingSeed = seed;
        commandAvailable.Set();  // Wake Unity thread
    }

    /// <summary>
    /// Queues a step command for Unity main thread.
    /// Called from network thread.
    /// </summary>
    public void QueueStep(float[] action)
    {
        pendingCommandType = CommandType.Step;
        pendingAction = action;
        commandAvailable.Set();  // Wake Unity thread
    }

    /// <summary>
    /// Retrieves response data.
    /// Called from network thread after responseReady signal.
    /// </summary>
    public byte[] GetResponse()
    {
        byte[] response = pendingResponse;
        pendingResponse = null;
        return response ?? new byte[0];
    }

    /// <summary>
    /// Processes any pending commands on Unity main thread.
    /// Called from Update().
    /// </summary>
    public void ProcessPendingCommands()
    {
        // Check if command available (non-blocking)
        if (!commandAvailable.WaitOne(0))
            return;

        switch (pendingCommandType)
        {
            case CommandType.Reset:
                ProcessReset();
                break;

            case CommandType.Step:
                ProcessStep();
                break;
        }

        pendingCommandType = CommandType.None;
    }

    /// <summary>
    /// Processes reset command on Unity main thread.
    /// </summary>
    private void ProcessReset()
    {
        UnityEngine.Random.InitState(pendingSeed);
        envController.ResetEnvironment();

        // Signal completion to network thread
        responseReady.Set();
    }

    /// <summary>
    /// Processes step command on Unity main thread.
    /// </summary>
    private void ProcessStep()
    {
        // Execute step
        var (obs, reward, done) = envController.Step(pendingAction);

        // Serialize response
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write(obs.Length);
            foreach (float f in obs)
                writer.Write(f);
            writer.Write(reward);
            writer.Write(done ? (byte)1 : (byte)0);

            pendingResponse = ms.ToArray();
        }

        // Signal completion to network thread
        responseReady.Set();
    }

    /// <summary>
    /// Command type enumeration.
    /// </summary>
    private enum CommandType
    {
        None,
        Reset,
        Step
    }
}

#endregion
