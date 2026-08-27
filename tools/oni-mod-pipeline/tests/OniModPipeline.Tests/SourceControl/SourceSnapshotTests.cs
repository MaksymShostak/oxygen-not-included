using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;

[TestClass]
public sealed class SourceSnapshotTests
{
    [TestMethod]
    public void Capture_WhenFileBytesChange_ReportsCanonicalPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.GetPath("input.txt");
        File.WriteAllText(filePath, "before");
        var before = SourceSnapshot.Capture([filePath]);
        File.WriteAllText(filePath, "after!");

        var later = SourceSnapshot.Capture([filePath]);

        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(filePath) },
            before.ChangedPathsComparedWith(later).ToArray());
    }

    [TestMethod]
    public void CaptureTree_WhenFileIsAddedAndAnotherRemoved_ReportsBothPaths()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var removedPath = temporaryDirectory.GetPath("removed.txt");
        var unchangedPath = temporaryDirectory.GetPath("unchanged.txt");
        File.WriteAllText(removedPath, "removed");
        File.WriteAllText(unchangedPath, "unchanged");
        var before = SourceSnapshot.CaptureTree(temporaryDirectory.Path);
        File.Delete(removedPath);
        var addedPath = temporaryDirectory.GetPath("added.txt");
        File.WriteAllText(addedPath, "added");

        var later = SourceSnapshot.CaptureTree(temporaryDirectory.Path);

        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(addedPath), Path.GetFullPath(removedPath) }
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            before.ChangedPathsComparedWith(later).ToArray());
    }

    [TestMethod]
    public void Capture_WhenOnlyTimestampChanges_ReportsNoChangedPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.GetPath("input.txt");
        File.WriteAllText(filePath, "unchanged bytes");
        var before = SourceSnapshot.Capture([filePath]);
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddHours(-1));

        var later = SourceSnapshot.Capture([filePath]);

        Assert.AreEqual(0, before.ChangedPathsComparedWith(later).Count);
    }
}
