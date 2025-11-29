using UnityEngine;
using UnityEngine.UI;

public class MLDataReadouts : MonoBehaviour
{
    private EnvironmentController envController;
    public Text timer_total_real;
    public Text timer_total_simulated;
    public Text timer_episode_real;
    public Text timer_episode_simulated;
    public Text effective_time_scale;
    public Text episode_number;
    public Text episode_reward;

    void Start()
    {
        envController = FindObjectOfType<EnvironmentController>();
        if (envController == null)
        {
            Debug.LogWarning("EnvironmentController not found in the scene.");
            this.enabled = false;
            return;
        }
    }

    void Update()
    {
        timer_total_real.text = envController.curriculumTracking.totalRealTime;
        timer_total_simulated.text = envController.curriculumTracking.totalSimulatedTime;
        timer_episode_real.text = envController.curriculumTracking.episodeRealTime;
        timer_episode_simulated.text = envController.curriculumTracking.episodeSimulatedTime;
        effective_time_scale.text = envController.curriculumTracking.effectiveTimeScale.ToString("F2") + "x";
        episode_number.text = envController.curriculumTracking.episodeNumber.ToString();
        episode_reward.text = envController.curriculumTracking.episodeReward.ToString("F3");
    }
}
