using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackAssemblyFileIdentityReaderTests
{
    [Test]
    public async Task Read_WhenAssemblyIsDynamic_ReturnsDynamicAssemblyWithoutFileMetadata()
    {
        Assembly dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"DynamicFastTrack.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);
        var reader = new FastTrackAssemblyFileIdentityReader();

        FastTrackAssemblyFileIdentity result = reader.Read(dynamicAssembly);

        await Assert.That(result.ReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.DynamicAssembly);
        await Assert.That(result.FileVersion).IsNull();
        await Assert.That(result.AssemblySha256).IsNull();
        Assert.NotNull(result.FailureMessage);
    }

    [Test]
    public async Task Read_WhenAssemblyLocationIsUnavailable_ReturnsLocationUnavailable()
    {
        byte[] assemblyBytes = File.ReadAllBytes(
            typeof(FastTrackAssemblyFileIdentityReaderTests).Assembly.Location);
        Assembly locationlessAssembly = System.Reflection.Assembly.Load(assemblyBytes);
        var reader = new FastTrackAssemblyFileIdentityReader();

        FastTrackAssemblyFileIdentity result = reader.Read(locationlessAssembly);

        await Assert.That(result.ReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.LocationUnavailable);
        await Assert.That(result.FileVersion).IsNull();
        await Assert.That(result.AssemblySha256).IsNull();
    }

    [Test]
    public async Task Read_WhenAssemblyFileNoLongerExists_ReturnsAssemblyFileMissing()
    {
        using var assemblyCopy = TemporaryAssemblyCopy.Create();
        Assembly loadedAssembly = new AssemblyWithSpecifiedLocation(
            assemblyCopy.AssemblyPath);
        assemblyCopy.DeleteAssemblyFile();
        var reader = new FastTrackAssemblyFileIdentityReader();

        FastTrackAssemblyFileIdentity result = reader.Read(loadedAssembly);

        await Assert.That(result.ReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.AssemblyFileMissing);
        await Assert.That(result.FileVersion).IsNull();
        await Assert.That(result.AssemblySha256).IsNull();
    }

    [Test]
    public async Task Read_WhenAssemblyFileCannotBeOpened_ReturnsReadFailedWithoutCatchingUnrelatedFailures()
    {
        using var assemblyCopy = TemporaryAssemblyCopy.Create();
        Assembly loadedAssembly = new AssemblyWithSpecifiedLocation(
            assemblyCopy.AssemblyPath);
        using FileStream exclusiveLease = new(
            assemblyCopy.AssemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        var reader = new FastTrackAssemblyFileIdentityReader();

        FastTrackAssemblyFileIdentity result = reader.Read(loadedAssembly);

        await Assert.That(result.ReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.ReadFailed);
        await Assert.That(result.FileVersion).IsNull();
        await Assert.That(result.AssemblySha256).IsNull();
        Assert.NotNull(result.FailureMessage);
    }

    [Test]
    public async Task Read_WhenPhysicalAssemblyIsReadable_ReturnsExactFileVersionAndUppercaseSha256()
    {
        Assembly assembly =
            typeof(FastTrackAssemblyFileIdentityReaderTests).Assembly;
        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(
            assembly.Location);
        var expectedVersion = new Version(
            versionInfo.FileMajorPart,
            versionInfo.FileMinorPart,
            versionInfo.FileBuildPart,
            versionInfo.FilePrivatePart);
        string expectedSha256 = ComputeUppercaseSha256(assembly.Location);
        var reader = new FastTrackAssemblyFileIdentityReader();

        FastTrackAssemblyFileIdentity result = reader.Read(assembly);

        await Assert.That(result.ReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.Success);
        await Assert.That(result.FileVersion).IsEqualTo(expectedVersion);
        await Assert.That(result.AssemblySha256).IsEqualTo(expectedSha256);
        await Assert.That(result.AssemblySha256!.ToUpperInvariant()).IsEqualTo(result.AssemblySha256);
        await Assert.That(result.FailureMessage).IsNull();
    }

    [Test]
    public async Task Constructor_WhenSuccessOmitsFileVersion_ThrowsArgumentException()
    {
        await Assert.That(() =>
            new FastTrackAssemblyFileIdentity(
                FastTrackAssemblyFileIdentityReadState.Success,
                fileVersion: null,
                assemblySha256: "ABCDEF",
                failureMessage: null)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WhenFailureCarriesDigest_ThrowsArgumentException()
    {
        await Assert.That(() =>
            new FastTrackAssemblyFileIdentity(
                FastTrackAssemblyFileIdentityReadState.ReadFailed,
                fileVersion: null,
                assemblySha256: "ABCDEF",
                failureMessage: "Read failed.")).ThrowsExactly<ArgumentException>();
    }

    private static string ComputeUppercaseSha256(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using SHA256 algorithm = SHA256.Create();
        byte[] digest = algorithm.ComputeHash(stream);
        return string.Concat(digest.Select(value => value.ToString("X2")));
    }

    private sealed class TemporaryAssemblyCopy : IDisposable
    {
        private TemporaryAssemblyCopy(string directoryPath, string assemblyPath)
        {
            DirectoryPath = directoryPath;
            AssemblyPath = assemblyPath;
        }

        private string DirectoryPath { get; }

        internal string AssemblyPath { get; }

        internal static TemporaryAssemblyCopy Create()
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "DeliveryTemperatureLimit.FastTrackIdentityTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            string sourceAssemblyPath =
                typeof(FastTrackAssemblyFileIdentityReaderTests).Assembly.Location;
            string assemblyPath = Path.Combine(
                directoryPath,
                $"FastTrackIdentityFixture.{Guid.NewGuid():N}.dll");
            File.Copy(sourceAssemblyPath, assemblyPath);
            return new TemporaryAssemblyCopy(directoryPath, assemblyPath);
        }

        internal void DeleteAssemblyFile()
        {
            File.Delete(AssemblyPath);
        }

        public void Dispose()
        {
            if (File.Exists(AssemblyPath))
            {
                File.Delete(AssemblyPath);
            }

            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: false);
            }
        }
    }

    /// <summary>
    /// Supplies a deterministic physical location without asking the runtime to
    /// load and lock the copied test assembly. The production reader consumes
    /// only Assembly.IsDynamic and Assembly.Location on these failure paths.
    /// </summary>
    private sealed class AssemblyWithSpecifiedLocation : Assembly
    {
        private readonly string location;

        internal AssemblyWithSpecifiedLocation(string location)
        {
            this.location = location;
        }

        public override bool IsDynamic => false;

        public override string Location => location;
    }
}
