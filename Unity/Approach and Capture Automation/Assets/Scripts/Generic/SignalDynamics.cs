public static class SignalDynamics
{
    public static class SignalRCS
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
            return output + (input - output) * deltaTime / tau;
        }
    }
}
