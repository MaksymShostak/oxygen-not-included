using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Processes;
public sealed class ExternalProcessRunnerTests
{
    private static TemporaryDirectory? fixtureDirectory;
    private static string? fixtureAssemblyPath;

    [Before(HookType.Class)]
    public static async Task Initialize()    {
        fixtureDirectory = new TemporaryDirectory();
        var projectPath = fixtureDirectory.GetPath("ProcessFixture.csproj");
        File.WriteAllText(projectPath, FixtureProject);
        File.WriteAllText(fixtureDirectory.GetPath("Program.cs"), FixtureProgram);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = fixtureDirectory.Path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The process fixture build did not start.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var buildCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(buildCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("The process fixture build exceeded 30 seconds.");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        await Assert.That(process.ExitCode).IsEqualTo(0).Because($"Fixture build failed.{Environment.NewLine}{standardOutput}{standardError}");

        fixtureAssemblyPath = fixtureDirectory.GetPath(
            "bin",
            "Release",
            "net10.0",
            "ProcessFixture.dll");
        await Assert.That(File.Exists(fixtureAssemblyPath)).IsTrue();
    }

    [After(HookType.Class)]
    public static void Cleanup()
    {
        fixtureDirectory?.Dispose();
    }

    [Test]
    public async Task RunAsync_TransportsLiteralStandardInputWithoutAScratchFile()
    {
        const string input = "雪 and shell characters & $literal\n";
        var result = await new ExternalProcessRunner().RunAsync(CreateRequest("stdin") with { StandardInput = input }, CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo(input);
    }

    [Test]
    public async Task RunAsync_RejectsOutputThatExceedsTheRequestedBoundary()
    {
        await Assert.That(async () => {await new ExternalProcessRunner().RunAsync(
            CreateRequest("streams") with { OutputLimitCharacters = 8 }, CancellationToken.None);}).Throws<InvalidDataException>();
    }

    [Test]
    public async Task RunAsync_ObservesEarlyInputRejectionAndRetainsDiagnostics()
    {
        var result = await new ExternalProcessRunner().RunAsync(
            CreateRequest("reject-input") with { StandardInput = new string('x', 2_097_152) }, CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(2);
        await Assert.That(result.StandardError).IsEqualTo("request rejected");
    }

    [Test]
    public async Task RunAsync_WhenCancelledDuringInput_ReportsCancellationAndCompletes()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.That(async () => {await new ExternalProcessRunner().RunAsync(
            CreateRequest("wait-input") with { StandardInput = new string('x', 2_097_152) }, cancellation.Token);}).ThrowsExactly<OperationCanceledException>();
    }

    [Test]
    public async Task RunAsync_WhenArgumentContainsShellCharacters_PreservesOneLiteralArgument()
    {
        const string literalArgument = "value with spaces \"quotes\" & $dollar; semicolon";
        var runner = new ExternalProcessRunner();
        var request = CreateRequest("arguments", literalArgument);

        var result = await runner.RunAsync(request, CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(JsonSerializer.Deserialize<string[]>(result.StandardOutput)).IsEquivalentTo(new[] { literalArgument }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.StandardError).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task RunAsync_WhenEnvironmentVariableIsProvided_PassesExactValue()
    {
        const string variableName = "ONI_MOD_PIPELINE_TEST_VALUE";
        const string variableValue = "literal value & $not-expanded";
        var runner = new ExternalProcessRunner();
        var request = CreateRequest("environment", variableName) with
        {
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [variableName] = variableValue
            }
        };

        var result = await runner.RunAsync(request, CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo(variableValue);
    }

    [Test]
    public async Task RunAsync_WhenChildWritesBothStreams_ReturnsExactTextAndExitCode()
    {
        var runner = new ExternalProcessRunner();
        var request = CreateRequest("streams");

        var result = await runner.RunAsync(request, CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(7);
        await Assert.That(result.StandardOutput).IsEqualTo("standard-output");
        await Assert.That(result.StandardError).IsEqualTo("standard-error");
    }

    [Test]
    public async Task RunAsync_WhenCancelled_KillsProcessTreeAndThrowsOperationCancelledException()
    {
        var grandchildPidPath = fixtureDirectory!.GetPath("grandchild.pid");
        var runner = new ExternalProcessRunner();
        using var cancellation = new CancellationTokenSource();
        var runTask = runner.RunAsync(
            CreateRequest("wait-tree", grandchildPidPath),
            cancellation.Token);
        int? grandchildPid = null;
        int? parentPid = null;

        try
        {
            await WaitForFileAsync(grandchildPidPath, TimeSpan.FromSeconds(10));
            grandchildPid = int.Parse(
                File.ReadAllText(grandchildPidPath),
                CultureInfo.InvariantCulture);
            parentPid = int.Parse(
                File.ReadAllText($"{grandchildPidPath}.parent"),
                CultureInfo.InvariantCulture);
            cancellation.Cancel();
            var cancellationAssertion = VerifyCancellationAsync();
            async Task VerifyCancellationAsync() =>
                await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => { await runTask; });
            var completedTask = await Task.WhenAny(
                cancellationAssertion,
                Task.Delay(TimeSpan.FromSeconds(10)));
            await Assert.That(completedTask).IsSameReferenceAs(cancellationAssertion).Because("The external process runner did not complete cancellation within 10 seconds.");
            await cancellationAssertion;
            await WaitForProcessExitAsync(grandchildPid.Value, TimeSpan.FromSeconds(10));
        }
        finally
        {
            cancellation.Cancel();
            parentPid ??= TryReadProcessId($"{grandchildPidPath}.parent");
            grandchildPid ??= TryReadProcessId(grandchildPidPath);
            if (parentPid is { } activeParentPid)
            {
                TerminateProcessTree(activeParentPid);
            }

            if (grandchildPid is { } activeGrandchildPid)
            {
                TerminateProcessTree(activeGrandchildPid);
            }

            await ObserveCompletionAsync(runTask, TimeSpan.FromSeconds(5));
        }
    }

    private static ProcessRequest CreateRequest(params string[] arguments) =>
        new(
            "dotnet",
            [fixtureAssemblyPath!, .. arguments],
            fixtureDirectory!.Path,
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!File.Exists(path))
        {
            await Task.Delay(25, cancellation.Token);
        }
    }

    private static async Task WaitForProcessExitAsync(int processId, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (true)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(25, cancellation.Token);
        }
    }

    private static int? TryReadProcessId(string path)
    {
        try
        {
            return File.Exists(path) && int.TryParse(
                File.ReadAllText(path),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var processId)
                ? processId
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task ObserveCompletionAsync(Task task, TimeSpan timeout)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask != task)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected while cleaning up a cancelled process fixture.
        }
    }

    private static void TerminateProcessTree(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 5000);
            }
        }
        catch (ArgumentException)
        {
            // The process already exited and its identifier is no longer active.
        }
    }

    private const string FixtureProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
          </PropertyGroup>
        </Project>
        """;

    private const string FixtureProgram = """
        using System.Diagnostics;
        using System.Globalization;
        using System.Reflection;
        using System.Text.Json;

        Console.InputEncoding = new System.Text.UTF8Encoding(false);
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);

        return args[0] switch
        {
            "arguments" => WriteArguments(args[1..]),
            "environment" => WriteEnvironment(args[1]),
            "streams" => WriteStreams(),
            "stdin" => await EchoInputAsync(),
            "reject-input" => RejectInput(),
            "wait-input" => await WaitWithoutInputAsync(),
            "wait-tree" => await WaitWithGrandchildAsync(args[1]),
            "grandchild" => await WaitAsGrandchildAsync(args[1]),
            _ => 64
        };

        static int WriteArguments(string[] arguments)
        {
            Console.Out.Write(JsonSerializer.Serialize(arguments));
            return 0;
        }

        static async Task<int> EchoInputAsync()
        {
            Console.Out.Write(await Console.In.ReadToEndAsync());
            return 0;
        }

        static int RejectInput()
        {
            Console.Error.Write("request rejected");
            return 2;
        }

        static async Task<int> WaitWithoutInputAsync()
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        static int WriteEnvironment(string name)
        {
            Console.Out.Write(Environment.GetEnvironmentVariable(name));
            return 0;
        }

        static int WriteStreams()
        {
            Console.Out.Write("standard-output");
            Console.Error.Write("standard-error");
            return 7;
        }

        static async Task<int> WaitWithGrandchildAsync(string pidPath)
        {
            await PublishProcessIdAsync($"{pidPath}.parent");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            startInfo.ArgumentList.Add("grandchild");
            startInfo.ArgumentList.Add(pidPath);
            using var grandchild = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Grandchild did not start.");
            await Console.In.ReadLineAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        static async Task<int> WaitAsGrandchildAsync(string pidPath)
        {
            await PublishProcessIdAsync(pidPath);
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        static async Task PublishProcessIdAsync(string path)
        {
            var temporaryPath = $"{path}.{Environment.ProcessId}.tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            File.Move(temporaryPath, path, overwrite: true);
        }
        """;
}
