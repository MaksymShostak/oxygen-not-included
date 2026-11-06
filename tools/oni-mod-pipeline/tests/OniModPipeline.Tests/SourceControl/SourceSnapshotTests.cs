using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;
public sealed class SourceSnapshotTests
{
    [Test]
    public async Task Capture_WhenFileBytesChange_ReportsCanonicalPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.GetPath("input.txt");
        File.WriteAllText(filePath, "before");
        var before = SourceSnapshot.Capture([filePath]);
        File.WriteAllText(filePath, "after!");

        var later = SourceSnapshot.Capture([filePath]);

        await Assert.That(before.ChangedPathsComparedWith(later).ToArray()).IsEquivalentTo(new[] { Path.GetFullPath(filePath) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CaptureTree_WhenFileIsAddedAndAnotherRemoved_ReportsBothPaths()
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

        await Assert.That(before.ChangedPathsComparedWith(later).ToArray()).IsEquivalentTo(new[] { Path.GetFullPath(addedPath), Path.GetFullPath(removedPath) }
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Capture_WhenOnlyTimestampChanges_ReportsNoChangedPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.GetPath("input.txt");
        File.WriteAllText(filePath, "unchanged bytes");
        var before = SourceSnapshot.Capture([filePath]);
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddHours(-1));

        var later = SourceSnapshot.Capture([filePath]);

        await Assert.That(before.ChangedPathsComparedWith(later).Count).IsEqualTo(0);
    }
}
