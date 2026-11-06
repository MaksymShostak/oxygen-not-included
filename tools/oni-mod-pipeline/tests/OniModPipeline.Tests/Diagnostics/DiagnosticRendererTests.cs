using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using System.Globalization;

namespace MaksymShostak.OniModPipeline.Tests.Diagnostics;
public sealed class DiagnosticRendererTests
{
    [Test]
    public async Task Render_WhenInvalidProfileUsesJson_WritesStableMachineReadableFields()
    {
        var diagnostic = DiagnosticCatalog.UnsupportedSchemaVersion(2, "profile.toml");
        var result = new OperationResult<object>(
            null,
            [diagnostic],
            PipelineExitCode.InvalidInput);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = DiagnosticRenderer.Render(result, OutputFormat.Json, output, error);

        await Assert.That(exitCode).IsEqualTo(2);
        await Assert.That(output.ToString()).Contains("\"id\": \"ONIP1001\"");
        await Assert.That(output.ToString()).Contains("\"exitCode\": 2");
        await Assert.That(error.ToString()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Render_WhenHumanFailure_WritesRemedyToStandardError()
    {
        var result = new OperationResult<object>(
            null,
            [DiagnosticCatalog.UnsupportedSchemaVersion(2, "profile.toml")],
            PipelineExitCode.InvalidInput);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = DiagnosticRenderer.Render(result, OutputFormat.Human, output, error);

        await Assert.That(exitCode).IsEqualTo(2);
        await Assert.That(output.ToString()).IsEqualTo(string.Empty);
        await Assert.That(error.ToString()).Contains("ONIP1001");
        await Assert.That(error.ToString()).Contains("Use schema-version = 1");
    }

    [Test]
    public async Task InvokeAsync_WhenCommandCannotBeParsed_ReturnsInvalidInputExitCode()
    {
        var exitCode = await CliApplication.InvokeAsync(
            ["--not-a-real-option"],
            CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(2);
    }
}
