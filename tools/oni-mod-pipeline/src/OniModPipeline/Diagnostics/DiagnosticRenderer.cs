using System.Globalization;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Diagnostics;

internal static class DiagnosticRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static int Render<T>(
        OperationResult<T> result,
        OutputFormat format,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (format == OutputFormat.Json)
        {
            WriteJson(result, output);
        }
        else
        {
            WriteHuman(result, output, error);
        }

        return (int)result.ExitCode;
    }

    private static void WriteJson<T>(OperationResult<T> result, TextWriter output)
    {
        var document = new
        {
            result.Value,
            Diagnostics = result.Diagnostics.Select(diagnostic => new
            {
                diagnostic.Id,
                Severity = diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.Summary,
                diagnostic.Evidence,
                diagnostic.NextAction
            }),
            ExitCode = (int)result.ExitCode
        };

        output.Write(JsonSerializer.Serialize(document, JsonOptions));
        output.Write('\n');
    }

    private static void WriteHuman<T>(
        OperationResult<T> result,
        TextWriter output,
        TextWriter error)
    {
        if (result.IsSuccess)
        {
            WriteHumanValue(result.Value, output);
        }

        var diagnosticWriter = result.IsSuccess ? output : error;
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnosticWriter.Write(diagnostic.Id);
            diagnosticWriter.Write(" [");
            diagnosticWriter.Write(diagnostic.Severity.ToString().ToLowerInvariant());
            diagnosticWriter.Write("]: ");
            diagnosticWriter.Write(diagnostic.Summary);
            diagnosticWriter.Write('\n');
            diagnosticWriter.Write("Evidence: ");
            diagnosticWriter.Write(diagnostic.Evidence);
            diagnosticWriter.Write('\n');
            diagnosticWriter.Write("Next action: ");
            diagnosticWriter.Write(diagnostic.NextAction);
            diagnosticWriter.Write('\n');
        }
    }

    private static void WriteHumanValue<T>(T? value, TextWriter output)
    {
        if (value is null)
        {
            return;
        }

        var renderedValue = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();

        if (renderedValue is null)
        {
            return;
        }

        output.Write(renderedValue);
        output.Write('\n');
    }
}
