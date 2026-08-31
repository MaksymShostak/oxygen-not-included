namespace DeliveryTemperatureLimit;

/// <summary>
/// Keeps linked pure production owners isolated from the Unity/Klei reporting
/// adapter while exercising their behavior in the test target.
/// </summary>
internal static class DeliveryTemperatureSupportReporter
{
    internal static void Record(
        string code,
        SupportDiagnosticSeverity severity,
        string message,
        Exception? exception = null)
    {
    }
}
