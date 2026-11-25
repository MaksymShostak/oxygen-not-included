using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Tests.Fixtures;

internal static class TestDirectoryLink
{
    // The two command arguments are fixed relative fixture names. The temporary directory
    // itself is supplied as the working directory and never interpreted by the shell.
    internal static async Task<string> CreateAsync(string parent, string name, string relativeTarget)
    {
        string link = Path.Combine(parent, name);
        if (OperatingSystem.IsWindows())
        {
            var result = await new ExternalProcessRunner().RunAsync(new ProcessRequest(
                Path.Combine(Environment.SystemDirectory, "cmd.exe"), ["/d", "/c", "mklink", "/j", name, relativeTarget.Replace('/', '\\')],
                parent, new Dictionary<string, string>()), CancellationToken.None);
            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.StandardError);
        }
        else Directory.CreateSymbolicLink(link, Path.GetFullPath(relativeTarget, parent));
        return link;
    }
}
