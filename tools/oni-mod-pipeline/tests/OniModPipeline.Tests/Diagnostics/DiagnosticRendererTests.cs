using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.Diagnostics;
using System.Globalization;

namespace MaksymShostak.OniModPipeline.Tests.Diagnostics;

[TestClass]
public sealed class DiagnosticRendererTests
{
    [TestMethod]
    public void Render_WhenInvalidProfileUsesJson_WritesStableMachineReadableFields()
    {
        var diagnostic = DiagnosticCatalog.UnsupportedSchemaVersion(2, "profile.toml");
        var result = new OperationResult<object>(
            null,
            [diagnostic],
            PipelineExitCode.InvalidInput);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = DiagnosticRenderer.Render(result, OutputFormat.Json, output, error);

        Assert.AreEqual(2, exitCode);
        StringAssert.Contains(output.ToString(), "\"id\": \"ONIP1001\"");
        StringAssert.Contains(output.ToString(), "\"exitCode\": 2");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void Render_WhenHumanFailure_WritesRemedyToStandardError()
    {
        var result = new OperationResult<object>(
            null,
            [DiagnosticCatalog.UnsupportedSchemaVersion(2, "profile.toml")],
            PipelineExitCode.InvalidInput);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = DiagnosticRenderer.Render(result, OutputFormat.Human, output, error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "ONIP1001");
        StringAssert.Contains(error.ToString(), "Use schema-version = 1");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenCommandCannotBeParsed_ReturnsInvalidInputExitCode()
    {
        var exitCode = await CliApplication.InvokeAsync(
            ["--not-a-real-option"],
            CancellationToken.None);

        Assert.AreEqual(2, exitCode);
    }
}
