using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.ModTest;

internal sealed class AutomatedTestRunner
{
    private readonly IExternalProcessRunner processRunner;
    private readonly string managedAssemblyDirectory;
    private readonly string repositoryRoot;

    internal AutomatedTestRunner(
        IExternalProcessRunner processRunner,
        string managedAssemblyDirectory,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedAssemblyDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        this.processRunner = processRunner;
        this.managedAssemblyDirectory = Path.GetFullPath(managedAssemblyDirectory);
        this.repositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    internal async Task<OperationResult<IReadOnlyList<AutomatedTestResult>>> RunAsync(
        ModProfile profile,
        string resultsRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedResultsRoot = Path.GetFullPath(resultsRoot);
        var validationFailure = ValidateRunInputs(profile, resolvedResultsRoot);
        if (validationFailure is not null)
        {
            return Failure([], validationFailure);
        }

        var resolvedProjects = new List<(TestProjectProfile Profile, string Path)>();
        foreach (var testProject in profile.TestProjects)
        {
            var resolved = ContainedPathResolver.ResolveExistingFile(
                profile.ModRoot,
                testProject.Path);
            if (!resolved.IsSuccess)
            {
                if (!testProject.Required)
                {
                    continue;
                }

                return Failure(
                    [],
                    DiagnosticCatalog.AutomatedTestFailed(
                        testProject.Id,
                        $"Required test project '{testProject.Path}' is missing or unsafe beneath mod root '{profile.ModRoot}'."));
            }

            resolvedProjects.Add((testProject, resolved.Value!));
        }

        Directory.CreateDirectory(resolvedResultsRoot);
        var results = new List<AutomatedTestResult>(resolvedProjects.Count);
        foreach (var (testProject, projectPath) in resolvedProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var restore = await processRunner.RunAsync(
                new ProcessRequest(
                    "dotnet",
                    ["restore", projectPath, "--locked-mode"],
                    profile.ModRoot,
                    EmptyEnvironment),
                cancellationToken).ConfigureAwait(false);
            if (restore.ExitCode != 0)
            {
                return Failure(
                    results,
                    DiagnosticCatalog.AutomatedTestFailed(
                        testProject.Id,
                        ProcessEvidence("Locked restore failed", restore)));
            }

            var trxPath = Path.Combine(resolvedResultsRoot, $"{testProject.Id}.trx");
            var test = await processRunner.RunAsync(
                new ProcessRequest(
                    "dotnet",
                    [
                        "test",
                        "--project",
                        projectPath,
                        "--no-restore",
                        "--configuration",
                        "Release",
                        "--results-directory",
                        resolvedResultsRoot,
                        "--",
                        "--report-trx",
                        "--report-trx-filename",
                        $"{testProject.Id}.trx"
                    ],
                    profile.ModRoot,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ONI_MANAGED_ASSEMBLY_DIRECTORY"] = managedAssemblyDirectory,
                        ["ONI_MOD_PIPELINE_REPOSITORY_ROOT"] = repositoryRoot
                    }),
                cancellationToken).ConfigureAwait(false);
            var passed = test.ExitCode == 0 && File.Exists(trxPath);
            var automatedResult = new AutomatedTestResult(
                testProject.Id,
                projectPath,
                trxPath,
                test.ExitCode,
                test.StandardOutput,
                test.StandardError,
                passed);
            results.Add(automatedResult);

            if (!passed && testProject.Required)
            {
                var evidence = test.ExitCode == 0
                    ? $"The test process exited 0 but did not create exact TRX evidence '{trxPath}'."
                    : ProcessEvidence("The test process failed", test);
                return Failure(
                    results,
                    DiagnosticCatalog.AutomatedTestFailed(testProject.Id, evidence));
            }
        }

        var intendedTrxPaths = results
            .Select(result => Path.GetFullPath(result.TrxPath))
            .OrderBy(path => path, PathComparer)
            .ToArray();
        var actualTrxPaths = Directory
            .EnumerateFiles(resolvedResultsRoot, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".trx",
                StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, PathComparer)
            .ToArray();
        if (!intendedTrxPaths.SequenceEqual(actualTrxPaths, PathComparer))
        {
            return Failure(
                results,
                DiagnosticCatalog.AutomatedTestFailed(
                    "automated-test-results",
                    "The results directory does not contain exactly one intended TRX file per executed project."));
        }

        return new OperationResult<IReadOnlyList<AutomatedTestResult>>(
            results,
            [],
            PipelineExitCode.Success);
    }

    private static Diagnostic? ValidateRunInputs(ModProfile profile, string resultsRoot)
    {
        if (File.Exists(resultsRoot) || Directory.Exists(resultsRoot))
        {
            return DiagnosticCatalog.AutomatedTestFailed(
                "automated-test-results",
                $"Run-specific results root '{resultsRoot}' already exists; existing evidence is never deleted or reused.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var testProject in profile.TestProjects)
        {
            if (string.IsNullOrWhiteSpace(testProject.Id) ||
                testProject.Id.Any(char.IsControl) ||
                !string.Equals(Path.GetFileName(testProject.Id), testProject.Id, StringComparison.Ordinal) ||
                !ids.Add(testProject.Id))
            {
                return DiagnosticCatalog.AutomatedTestFailed(
                    string.IsNullOrWhiteSpace(testProject.Id) ? "<invalid-id>" : testProject.Id,
                    "Declared automated-test IDs must be unique safe filename stems.");
            }
        }

        return null;
    }

    private static string ProcessEvidence(string summary, ProcessResult result)
    {
        var standardError = string.IsNullOrWhiteSpace(result.StandardError)
            ? "<empty>"
            : result.StandardError.Trim();
        var standardOutput = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? "<empty>"
            : result.StandardOutput.Trim();
        return $"{summary} with exit code {result.ExitCode}. Standard error: {standardError}. Standard output: {standardOutput}.";
    }

    private static OperationResult<IReadOnlyList<AutomatedTestResult>> Failure(
        IReadOnlyList<AutomatedTestResult> results,
        Diagnostic diagnostic) =>
        new(results, [diagnostic], PipelineExitCode.BuildOrTestFailed);

    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironment =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
