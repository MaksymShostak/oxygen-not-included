using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Processes;

[TestClass]
public sealed class ExternalProcessRunnerTests
{
    private static TemporaryDirectory? fixtureDirectory;
    private static string? fixtureAssemblyPath;

    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
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
            throw new AssertFailedException("The process fixture build exceeded 30 seconds.");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"Fixture build failed.{Environment.NewLine}{standardOutput}{standardError}");

        fixtureAssemblyPath = fixtureDirectory.GetPath(
            "bin",
            "Release",
            "net10.0",
            "ProcessFixture.dll");
        Assert.IsTrue(File.Exists(fixtureAssemblyPath));
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        fixtureDirectory?.Dispose();
    }

    [TestMethod]
    public async Task RunAsync_WhenArgumentContainsShellCharacters_PreservesOneLiteralArgument()
    {
        const string literalArgument = "value with spaces \"quotes\" & $dollar; semicolon";
        var runner = new ExternalProcessRunner();
        var request = CreateRequest("arguments", literalArgument);

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { literalArgument },
            JsonSerializer.Deserialize<string[]>(result.StandardOutput));
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
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

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(variableValue, result.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_WhenChildWritesBothStreams_ReturnsExactTextAndExitCode()
    {
        var runner = new ExternalProcessRunner();
        var request = CreateRequest("streams");

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual("standard-output", result.StandardOutput);
        Assert.AreEqual("standard-error", result.StandardError);
    }

    [TestMethod]
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
            var cancellationAssertion = Assert.ThrowsExactlyAsync<OperationCanceledException>(
                async () => await runTask);
            var completedTask = await Task.WhenAny(
                cancellationAssertion,
                Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.AreSame(
                cancellationAssertion,
                completedTask,
                "The external process runner did not complete cancellation within 10 seconds.");
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

        return args[0] switch
        {
            "arguments" => WriteArguments(args[1..]),
            "environment" => WriteEnvironment(args[1]),
            "streams" => WriteStreams(),
            "wait-tree" => await WaitWithGrandchildAsync(args[1]),
            "grandchild" => await WaitAsGrandchildAsync(args[1]),
            _ => 64
        };

        static int WriteArguments(string[] arguments)
        {
            Console.Out.Write(JsonSerializer.Serialize(arguments));
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
