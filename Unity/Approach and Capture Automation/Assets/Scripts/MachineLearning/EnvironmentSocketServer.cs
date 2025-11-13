using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using UnityEngine;

public class EnvironmentSocketServer : MonoBehaviour
{
    // This script was generated with assistance from OpenAI's GPT-5 model.
    // For details on the classes here that were new to me at the time of writing, see:
    //  Thread:         https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread?view=net-9.0
    //  TcpListener:    https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcplistener?view=net-9.0
    //  AutoResetEvent: https://learn.microsoft.com/en-us/dotnet/api/system.threading.autoresetevent?view=net-9.0
    //  NetworkStream:  https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=net-9.0
    //  MemoryStream:   https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream?view=net-9.0

    public EnvironmentController envController;
    public int port = 5005;
    private TcpListener listener;
    private Thread listenThread;
    private volatile bool running = false;

    // Synchronization primitives
    private AutoResetEvent actionAvailable = new AutoResetEvent(false);
    private AutoResetEvent responseReady = new AutoResetEvent(false);
    private float[] pendingAction = null;
    private byte[] pendingResponse = null;
    private volatile bool resetRequested;
    private int pendingSeed = 0;

    void Start()
    {
        // Validate the EnvironmentController reference
        if (envController == null) { Debug.LogError("EnvironmentController reference missing."); enabled = false; return; }
        // Initiate a new connection listener thread with the ListenForClient delegate
        listenThread = new Thread(ListenForClient);
        listenThread.IsBackground = true;
        listenThread.Start();
        // Record and report listener thread status
        running = true;
        Debug.Log($"EnvironmentSocketServer listening on port {port}");
    }

    void OnApplicationQuit()
    {
        running = false;
        try { listener?.Stop(); } catch { }
        actionAvailable.Set();
        responseReady.Set();
        listenThread?.Join(500);
    }

    // Network thread: accept a single client and serve requests serially
    private void ListenForClient()
    {
        try
        {
            // "Initializes a new instance of the TcpListener class that listens for incoming connection attempts on the specified local IP address and port number".
            listener = new TcpListener(IPAddress.Any, port);
            // "Starts listening for incoming connection requests."
            listener.Start();

            while (running)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream ns = client.GetStream())
                using (BinaryReader br = new BinaryReader(ns))
                using (BinaryWriter bw = new BinaryWriter(ns))
                {
                    // Set the time alloted for data reading and writing (ms).
                    ns.ReadTimeout = 1000;
                    ns.WriteTimeout = 1000;
                    Debug.Log("Client connected.");

                    while (running && client.Connected)
                    {
                        // Read opCode (int32): 0 = step, 1 = reset
                        int op;
                        try { op = br.ReadInt32(); }
                        catch (IOException) { break; }

                        if (op == 1)
                        {
                            // Reset: read seed (int32)
                            int seed = br.ReadInt32();
                            pendingSeed = seed;
                            resetRequested = true;

                            // Wait for Unity thread to finish reset
                            responseReady.WaitOne();

                            // Send an empty payload (obs_len = 0)
                            bw.Write(0);
                            bw.Flush();
                            continue;
                        }
                        else if (op == 0)
                        {
                            // Step: read action length and action floats
                            int actionLen = br.ReadInt32();
                            float[] action = new float[actionLen];
                            for (int i = 0; i < actionLen; i++) action[i] = br.ReadSingle();

                            // Pass action to main thread
                            pendingAction = action;
                            actionAvailable.Set();

                            // Wait for main thread to produce pendingResponse
                            // "Blocks the current thread until the current WaitHandle receives a signal."
                            responseReady.WaitOne();

                            // Send response bytes
                            int respLen = pendingResponse?.Length ?? 0;
                            bw.Write(respLen);
                            if (respLen > 0) bw.Write(pendingResponse, 0, respLen);
                            bw.Flush();

                            // Reset for next step
                            pendingAction = null;
                            pendingResponse = null;
                        }
                        else
                        {
                            Debug.LogWarning($"Unknown op code: {op}");
                            break;
                        }
                    } // client loop
                    Debug.Log("Client disconnected.");
                } // using client
            } // listener loop
        }
        catch (SocketException ex)
        {
            // Catch the expected exception caused when the simulation is ended (play mode exited). If the exception is not this, report it.
            if (!running && ex.SocketErrorCode == SocketError.Interrupted) { return; }
            Debug.LogError($"Socket server error: {ex}");
        }
    }

    // Called on main Unity thread in Update to process pending action
    void Update()
    {
        // Handle a pending Reset request from network thread
        if (resetRequested)
        {
            UnityEngine.Random.InitState(pendingSeed);
            envController.ResetEnvironment();

            resetRequested = false;
            responseReady.Set(); // let network thread continue
        }

        // Handle pending Step
        if (pendingAction != null)
        {
            // Run Step synchronously on main thread
            var (obs, reward, done) = envController.Step(pendingAction);
            // Pack response: [obs_len(int), obs floats..., reward(float), done(byte)]
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(obs.Length);
                foreach (float f in obs) { bw.Write(f); }
                bw.Write(reward);
                bw.Write(done ? (byte)1 : (byte)0);
                pendingResponse = ms.ToArray();
            }
            // Signal network thread
            responseReady.Set();
        }
        // Keep server alive (do not terminate the connection here).
    }
}
