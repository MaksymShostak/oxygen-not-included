namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal interface IAcceptanceConsole
{
    bool IsInteractive { get; }

    void WriteLine(string value);

    string ReadRequired(string prompt);

    AcceptanceOutcome ReadOutcome(string prompt);

    string? ReadOptional(string prompt);
}

internal sealed class SystemAcceptanceConsole : IAcceptanceConsole
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public void WriteLine(string value) => Console.WriteLine(value);

    public string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var value = Console.ReadLine();
            if (value is null)
            {
                throw new EndOfStreamException(
                    "Interactive acceptance input ended before a required value was read.");
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Console.WriteLine("A nonempty value is required.");
        }
    }

    public AcceptanceOutcome ReadOutcome(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var value = Console.ReadLine();
            if (value is null)
            {
                throw new EndOfStreamException(
                    "Interactive acceptance input ended before an outcome was read.");
            }

            var normalized = value.Trim();
            if (string.Equals(
                normalized,
                "passed",
                StringComparison.OrdinalIgnoreCase))
            {
                return AcceptanceOutcome.Passed;
            }

            if (string.Equals(
                normalized,
                "failed",
                StringComparison.OrdinalIgnoreCase))
            {
                return AcceptanceOutcome.Failed;
            }

            Console.WriteLine("Enter only 'passed' or 'failed'; checks cannot be skipped.");
        }
    }

    public string? ReadOptional(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }
}
