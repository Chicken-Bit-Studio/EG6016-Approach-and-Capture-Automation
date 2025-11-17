/*
 * Acts as the TCP server bridging Unity and an external ML client (Python).
 * 
 * Key responsibilities:
 *   - Listen for a single client connection.
 *   - Transmit basic metadata (observation/action vector lengths) once on connect, and again after each environment reset.
 *   - Support opcode-based communication protocol:
 *          0  =>  Step      (expects an action float array)
 *          1  =>  Reset     (expects a random seed int)
 *   - Perform thread‑safe exchange of data between the network thread and Unity’s main thread.
 * 
 * Revision notes:
 *   - Introduced dynamic handshake including obs/action sizes.
 *   - Retained multi‑thread design for Unity API isolation.
 *   - Added detailed lifecycle logging for clarity.
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using UnityEngine;

public class EnvironmentSocketServer : MonoBehaviour
{
    [Header("Unity Environment Link")]
    [Tooltip("Script managing simulation stepping and resetting.")]
    public EnvironmentController envController;

    [Header("Network Settings")]
    [Tooltip("TCP port to listen on for Python clients.")]
    public int port = 5005;

    // ===== Internal server state =====
    private TcpListener listener;
    private Thread listenThread;
    private volatile bool running = false;

    // ===== Thread‑synchronisation objects =====
    private AutoResetEvent actionAvailable = new AutoResetEvent(false);
    private AutoResetEvent responseReady = new AutoResetEvent(false);

    // ===== Data exchange buffers =====
    private float[] pendingAction = null;             // action array waiting for execution
    private byte[] pendingResponse = null;            // serialized observation+reward+done to return
    private volatile bool resetRequested = false;     // flag: network requested environment reset
    private int pendingSeed = 0;                      // seed accompanying pending reset
    private volatile bool handshakeRequested = false; // flag: send obs/action sizes to Python

    // =============================================================================================

    void Start()
    {
        if (envController == null)
        {
            Debug.LogError("EnvironmentController reference missing.");
            enabled = false;
            return;
        }

        // Spawn dedicated network listener thread
        listenThread = new Thread(ListenForClient);
        listenThread.IsBackground = true;
        listenThread.Start();

        running = true;
        Debug.Log($"[SocketServer] Listening on port {port}");
    }

    void OnApplicationQuit()
    {
        running = false;
        try { listener?.Stop(); } catch { }
        actionAvailable.Set();
        responseReady.Set();
        listenThread?.Join(500);       // Attempt graceful shutdown within 0.5s
    }

    // =============================================================================================
    // NETWORK THREAD — handles all socket I/O
    // =============================================================================================

    private void ListenForClient()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            while (running)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream ns = client.GetStream())
                using (BinaryReader br = new BinaryReader(ns))
                using (BinaryWriter bw = new BinaryWriter(ns))
                {
                    ns.ReadTimeout = 2000;
                    ns.WriteTimeout = 2000;
                    Debug.Log("[SocketServer] Client connected.");

                    // === Initial handshake ===
                    SendHandshake(bw);
                    
                    while (running && client.Connected)
                    {
                        int opCode;
                        try { opCode = br.ReadInt32(); }
                        catch (IOException)
                        {
                            Debug.LogWarning("[SocketServer] Lost client or invalid opcode read.");
                            break;
                        }

                        if (opCode == 1) // ---- RESET ----
                        {
                            int seed = br.ReadInt32();
                            pendingSeed = seed;
                            resetRequested = true;
                            handshakeRequested = true;       // signal new handshake after reset
                            actionAvailable.Set();           // wake Unity thread

                            responseReady.WaitOne();         // wait for Unity main thread
                            bw.Write(0);                     // keep legacy empty ack for backward compat
                            bw.Flush();
                            continue;
                        }
                        else if (opCode == 0) // ---- STEP ----
                        {
                            int actionLen = br.ReadInt32();
                            float[] action = new float[actionLen];
                            for (int i = 0; i < actionLen; i++)
                                action[i] = br.ReadSingle();

                            pendingAction = action;
                            actionAvailable.Set();

                            // Wait for result
                            responseReady.WaitOne();

                            int respLen = pendingResponse?.Length ?? 0;
                            bw.Write(respLen);
                            if (respLen > 0) bw.Write(pendingResponse, 0, respLen);
                            bw.Flush();

                            pendingAction = null;
                            pendingResponse = null;
                        }
                        else
                        {
                            Debug.LogWarning($"[SocketServer] Unknown opcode ({opCode}). Disconnecting.");
                            break;
                        }

                        // If a reset triggered handshake, perform it now
                        if (handshakeRequested)
                        {
                            SendHandshake(bw);
                            handshakeRequested = false;
                        }
                    }

                    Debug.Log("[SocketServer] Client disconnected.");
                }
            }
        }
        catch (SocketException ex)
        {
            // Common benign case: play‑mode exit interrupts socket unexpectedly
            if (!running && ex.SocketErrorCode == SocketError.Interrupted) return;
            Debug.LogError($"[SocketServer] Socket error: {ex}");
        }
    }

    // =============================================================================================
    // MAIN UNITY THREAD — processes actions and resets
    // =============================================================================================

    void Update()
    {
        // ---- Handle a reset request ----
        if (resetRequested)
        {
            UnityEngine.Random.InitState(pendingSeed);
            envController.ResetEnvironment();

            resetRequested = false;
            responseReady.Set(); // Allow network thread to proceed
        }

        // ---- Handle a simulation step ----
        if (pendingAction != null)
        {
            var (obs, reward, done) = envController.Step(pendingAction);

            // Serialize step result: [obs_len(int32)] [obs floats] [reward(float32)] [done(byte)]
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(obs.Length);
                foreach (float f in obs) bw.Write(f);
                bw.Write(reward);
                bw.Write(done ? (byte)1 : (byte)0);
                pendingResponse = ms.ToArray();
            }

            responseReady.Set(); // wake network thread
        }
    }

    // =============================================================================================
    // HANDSHAKE STRUCTURE
    // =============================================================================================

    /// <summary>
    /// Sends the current observation and action vector lengths to the client.
    /// Called on connection and after each reset to support dynamic model variations.
    /// </summary>
    private void SendHandshake(BinaryWriter bw)
    {
        try
        {
            int obsLen = envController.CollectObservations().Length;    // TODO: inefficient to call this method just for this. Plus, it's supposed to be a private method
            int actLen = envController.references.satelliteModelInterface.plants.plantCount;

            // Handshake packet format:
            // [int32: opcode=100]  [int32: obsLen]  [int32: actLen]
            bw.Write(100);
            bw.Write(obsLen);
            bw.Write(actLen);
            bw.Flush();

            Debug.Log($"[SocketServer] Handshake sent (obs={obsLen}, act={actLen})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SocketServer] Handshake failed: {ex}");
        }
    }
}