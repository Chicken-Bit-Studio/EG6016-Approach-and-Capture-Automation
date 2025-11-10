using Unity.Mathematics;

public static class SignalDynamics
{
    private const float cutoff = 0.0001f;
    
    public static class RCS
    {
        // The time constant of the signal response in seconds. Recall work on capacitor discharge in A-Level physics.
        // 1tau -> 63.2%
        // 2tau -> 86.5%
        // 3tau -> 95.0%
        // 4tau -> 98.2%
        // 5tau -> 99.3%
        public const float tau = 0.05f;
        // The governing equation for first-order systems.
        // Note: "(input - output) * deltaTime / tau" returns the *difference* in signal strength.
        public static float Response(float input, float output, float deltaTime)
        {
            float calc = output + (input - output) * deltaTime / tau;
            if (math.abs(calc) <= cutoff) { calc = 0; }
            return calc;
        }
    }
    public static class ClawActuator
    {
        // The time constant of the signal response in seconds. Recall work on capacitor discharge in A-Level physics.
        // 1tau -> 63.2%
        // 2tau -> 86.5%
        // 3tau -> 95.0%
        // 4tau -> 98.2%
        // 5tau -> 99.3%
        public const float tau = 0.2f;
        // The governing equation for first-order systems.
        // Note: "(input - output) * deltaTime / tau" returns the *difference* in signal strength.
        public static float Response(float input, float output, float deltaTime)
        {
            float calc = output + (input - output) * deltaTime / tau;
            if (math.abs(calc) <= cutoff) { calc = 0; }
            return calc;
        }
    }
}
