using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MLTimers : MonoBehaviour
{
    public Text timer_total_real;
    public Text timer_total_simulated;
    public Text timer_episode_real;
    public Text timer_episode_simulated;
    public Text effective_time_scale;

    private float currentEffectiveTimeScale = 1f;
    private EnvironmentController envController;

    // For tracking real-time elapsed
    private float episodeRealTimeStart;
    private float totalRealTimeStart;
    private float totalSimulatedTime = 0f;

    // For efficient time scale calculation (20 second rolling average)
    private Queue<float> recentTimeScales = new Queue<float>();
    private float timeScaleSampleInterval = 0.5f;  // Sample every 0.5 seconds
    private float lastTimeScaleSample = 0f;
    private const int maxSamples = 40;  // 40 samples * 0.5s = 20 seconds

    void Start()
    {
        envController = FindObjectOfType<EnvironmentController>();
        if (envController == null)
        {
            Debug.LogWarning("EnvironmentController not found in the scene.");
            this.enabled = false;
            return;
        }

        // Initialize timers
        episodeRealTimeStart = Time.realtimeSinceStartup;
        totalRealTimeStart = Time.realtimeSinceStartup;
    }

    void Update()
    {
        if (envController == null) return;

        // Update episode simulated time
        float episodeSimulated = envController.episodeSettings.elapsedTime;
        timer_episode_simulated.text = FormatTime(episodeSimulated);

        // Update episode real time
        float episodeReal = Time.realtimeSinceStartup - episodeRealTimeStart;
        timer_episode_real.text = FormatTime(episodeReal);

        // Update total simulated time
        timer_total_simulated.text = FormatTime(totalSimulatedTime + episodeSimulated);

        // Update total real time
        float totalReal = Time.realtimeSinceStartup - totalRealTimeStart;
        timer_total_real.text = FormatTime(totalReal);

        // Update time scale calculation (every 0.5s)
        if (Time.realtimeSinceStartup - lastTimeScaleSample >= timeScaleSampleInterval)
        {
            UpdateTimeScale(episodeSimulated, episodeReal);
            lastTimeScaleSample = Time.realtimeSinceStartup;
        }

        // Update effective time scale display
        effective_time_scale.text = $"{currentEffectiveTimeScale:F2}x";
    }

    /// <summary>
    /// Detects episode reset and updates total time tracking.
    /// Called by monitoring the episode done flag.
    /// </summary>
    void LateUpdate()
    {
        if (envController == null) return;

        // Detect episode reset (elapsed time goes back to near zero)
        if (envController.episodeSettings.elapsedTime < 0.1f && totalSimulatedTime > 0f)
        {
            // Episode just reset - add previous episode to total
            totalSimulatedTime += envController.episodeSettings.elapsedTime;
            episodeRealTimeStart = Time.realtimeSinceStartup;
        }
    }

    /// <summary>
    /// Updates the rolling average time scale (simulated/real time ratio).
    /// </summary>
    private void UpdateTimeScale(float simTime, float realTime)
    {
        if (realTime > 0.1f)  // Avoid division by zero and initial startup
        {
            float instantTimeScale = simTime / realTime;

            // Add to rolling queue
            recentTimeScales.Enqueue(instantTimeScale);

            // Remove oldest sample if we exceed max
            if (recentTimeScales.Count > maxSamples)
            {
                recentTimeScales.Dequeue();
            }

            // Calculate average
            if (recentTimeScales.Count > 0)
            {
                float sum = 0f;
                foreach (float ts in recentTimeScales)
                {
                    sum += ts;
                }
                currentEffectiveTimeScale = sum / recentTimeScales.Count;
            }
        }
    }

    /// <summary>
    /// Formats time in MM:SS.ss format.
    /// </summary>
    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;
        return $"{minutes:00}:{remainingSeconds:00.00}";
    }
}
