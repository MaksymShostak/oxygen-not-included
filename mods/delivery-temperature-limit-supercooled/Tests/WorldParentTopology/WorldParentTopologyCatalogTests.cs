using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldParentTopology;

[TestClass]
public sealed class WorldParentTopologyCatalogTests
{
    private const int ConcurrentWriterIterationCount = 5000;
    private const int ConcurrentReaderIterationCount = 20000;
    private const int ConcurrentReaderCount = 4;

    [TestMethod]
    public void RegisterWorld_WhenMappingIsNew_IncrementsVersionOnce()
    {
        var catalog = CreateCatalog();
        var initialSnapshot = catalog.CaptureSnapshot();

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var changedSnapshot = catalog.CaptureSnapshot();

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(7, change.WorldId);
        Assert.IsNull(change.PreviousParentWorldId);
        Assert.AreEqual(1, change.CurrentParentWorldId);
        Assert.AreNotSame(initialSnapshot, changedSnapshot);
        Assert.AreEqual(
            initialSnapshot.Version.Value + 1,
            changedSnapshot.Version.Value);
        Assert.AreEqual(
            initialSnapshot.GameSessionGeneration,
            changedSnapshot.GameSessionGeneration);
        Assert.IsTrue(changedSnapshot.TryResolveParentWorld(
            7,
            out var parentWorldId));
        Assert.AreEqual(1, parentWorldId);
    }

    [TestMethod]
    public void RegisterWorld_WhenMappingIsIdentical_DoesNotChangeVersionOrSnapshotReference()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var snapshotBeforeRepeat = catalog.CaptureSnapshot();

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 1);

        Assert.IsFalse(change.HasChanged);
        Assert.AreEqual(7, change.WorldId);
        Assert.AreEqual(1, change.PreviousParentWorldId);
        Assert.AreEqual(1, change.CurrentParentWorldId);
        Assert.AreSame(snapshotBeforeRepeat, catalog.CaptureSnapshot());
        Assert.AreEqual(
            snapshotBeforeRepeat.Version,
            catalog.CaptureSnapshot().Version);
    }

    [TestMethod]
    public void RegisterWorld_WhenExistingWorldChangesParent_ReturnsBothAffectedParents()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 2);

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(7, change.WorldId);
        Assert.AreEqual(1, change.PreviousParentWorldId);
        Assert.AreEqual(2, change.CurrentParentWorldId);
        Assert.IsTrue(catalog.CaptureSnapshot().TryResolveParentWorld(
            7,
            out var parentWorldId));
        Assert.AreEqual(2, parentWorldId);
    }

    [TestMethod]
    public void RegisterWorld_WhenWorldIsItsOwnParent_PreservesSelfParentMapping()
    {
        var catalog = CreateCatalog();

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 7);
        var snapshot = catalog.CaptureSnapshot();

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(7, change.CurrentParentWorldId);
        Assert.IsTrue(snapshot.TryResolveParentWorld(7, out var parentWorldId));
        Assert.AreEqual(7, parentWorldId);
        AssertMemberWorldIds(snapshot, parentWorldId: 7, 7);
    }

    [TestMethod]
    public void RegisterWorld_WhenWorldIdIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var catalog = CreateCatalog();
        var snapshotBeforeFailure = catalog.CaptureSnapshot();

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            catalog.RegisterWorld(worldId: -1, parentWorldId: 0));

        Assert.AreEqual("worldId", exception.ParamName);
        Assert.AreSame(snapshotBeforeFailure, catalog.CaptureSnapshot());
    }

    [TestMethod]
    public void RegisterWorld_WhenParentWorldIdIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var catalog = CreateCatalog();
        var snapshotBeforeFailure = catalog.CaptureSnapshot();

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            catalog.RegisterWorld(worldId: 0, parentWorldId: -1));

        Assert.AreEqual("parentWorldId", exception.ParamName);
        Assert.AreSame(snapshotBeforeFailure, catalog.CaptureSnapshot());
    }

    [TestMethod]
    public void RemoveWorld_WhenKnown_ReturnsPreviousParentAndRemovesMapping()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 2);
        var snapshotBeforeRemoval = catalog.CaptureSnapshot();

        var change = catalog.RemoveWorld(worldId: 7);
        var snapshotAfterRemoval = catalog.CaptureSnapshot();

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(7, change.WorldId);
        Assert.AreEqual(2, change.PreviousParentWorldId);
        Assert.IsNull(change.CurrentParentWorldId);
        Assert.AreEqual(
            snapshotBeforeRemoval.Version.Value + 1,
            snapshotAfterRemoval.Version.Value);
        Assert.IsFalse(snapshotAfterRemoval.TryResolveParentWorld(7, out _));
        Assert.IsEmpty(snapshotAfterRemoval.GetMemberWorldIds(2));
    }

    [TestMethod]
    public void RemoveWorld_WhenUnknown_IsIdempotent()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 2);
        var snapshotBeforeUnknownRemoval = catalog.CaptureSnapshot();

        var change = catalog.RemoveWorld(worldId: 8);

        Assert.IsFalse(change.HasChanged);
        Assert.AreEqual(8, change.WorldId);
        Assert.IsNull(change.PreviousParentWorldId);
        Assert.IsNull(change.CurrentParentWorldId);
        Assert.AreSame(
            snapshotBeforeUnknownRemoval,
            catalog.CaptureSnapshot());
    }

    [TestMethod]
    public void GetMemberWorldIds_WhenParentHasSeveralWorlds_ReturnsSortedImmutableIds()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 9, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 2, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var snapshot = catalog.CaptureSnapshot();

        var memberWorldIds = snapshot.GetMemberWorldIds(parentWorldId: 1);

        Assert.AreSequenceEqual(new[] { 2, 7, 9 }, memberWorldIds.ToArray());
        Assert.IsFalse(memberWorldIds is int[]);
        var mutableListView = Assert.IsInstanceOfType<IList<int>>(memberWorldIds);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            mutableListView[0] = 99);
        AssertMemberWorldIds(snapshot, parentWorldId: 1, 2, 7, 9);
    }

    [TestMethod]
    public async Task CaptureSnapshot_WhenMappingChanges_ReaderSeesCompleteOldOrCompleteNewMapping()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 1, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 2, parentWorldId: 1);
        using var startSignal = new ManualResetEventSlim(initialState: false);
        var invalidSnapshotCount = 0;

        var writer = Task.Run(() =>
        {
            startSignal.Wait();
            for (var iteration = 0;
                 iteration < ConcurrentWriterIterationCount;
                 iteration++)
            {
                catalog.RegisterWorld(
                    worldId: 2,
                    parentWorldId: (iteration & 1) == 0 ? 2 : 1);
                if ((iteration & 63) == 0)
                {
                    Thread.Yield();
                }
            }
        });

        var readers = new Task[ConcurrentReaderCount];
        for (var readerIndex = 0;
             readerIndex < readers.Length;
             readerIndex++)
        {
            readers[readerIndex] = Task.Run(() =>
            {
                startSignal.Wait();
                for (var iteration = 0;
                     iteration < ConcurrentReaderIterationCount;
                     iteration++)
                {
                    var snapshot = catalog.CaptureSnapshot();
                    if (!IsCompleteOldOrNewSnapshot(snapshot))
                    {
                        Interlocked.Increment(ref invalidSnapshotCount);
                    }

                    if ((iteration & 255) == 0)
                    {
                        Thread.Yield();
                    }
                }
            });
        }

        startSignal.Set();
        await writer;
        await Task.WhenAll(readers);

        Assert.AreEqual(0, invalidSnapshotCount);
    }

    [TestMethod]
    public void TryResolveParentWorld_WhenWorldIsUnknown_ReturnsFalseWithoutFallback()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 0, parentWorldId: 0);

        var found = catalog.CaptureSnapshot().TryResolveParentWorld(
            worldId: 999,
            out var parentWorldId);

        Assert.IsFalse(found);
        Assert.AreEqual(default, parentWorldId);
        Assert.IsEmpty(catalog.CaptureSnapshot().GetMemberWorldIds(999));
    }

    [TestMethod]
    public void RegisterWorld_WhenTopologyVersionIsExhausted_ThrowsWithoutChangingSnapshot()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var snapshotBeforeFailure = catalog.CaptureSnapshot();
        var versionField = RequirePrivateInt64Field(
            typeof(WorldParentTopologyCatalog),
            "version");
        versionField.SetValue(catalog, long.MaxValue);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            catalog.RegisterWorld(worldId: 7, parentWorldId: 2));

        StringAssert.Contains(exception.Message, "topology version");
        Assert.AreSame(snapshotBeforeFailure, catalog.CaptureSnapshot());
        Assert.IsTrue(catalog.CaptureSnapshot().TryResolveParentWorld(
            7,
            out var retainedParentWorldId));
        Assert.AreEqual(1, retainedParentWorldId);
        Assert.AreEqual(
            long.MaxValue,
            Assert.IsInstanceOfType<long>(versionField.GetValue(catalog)));
    }

    [TestMethod]
    public void Catalog_WhenBaseGameContentModeShapeIsRegistered_PreservesReportedTopology()
    {
        var catalog = CreateCatalog();

        catalog.RegisterWorld(worldId: 0, parentWorldId: 0);
        var snapshot = catalog.CaptureSnapshot();

        Assert.IsTrue(snapshot.TryResolveParentWorld(0, out var parentWorldId));
        Assert.AreEqual(0, parentWorldId);
        AssertMemberWorldIds(snapshot, parentWorldId: 0, 0);
    }

    [TestMethod]
    public void Catalog_WhenSpacedOutContentModeShapeIsRegistered_PreservesEveryReportedWorld()
    {
        var catalog = CreateCatalog();

        catalog.RegisterWorld(worldId: 0, parentWorldId: 0);
        catalog.RegisterWorld(worldId: 1, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 10, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 11, parentWorldId: 1);
        var snapshot = catalog.CaptureSnapshot();

        Assert.IsTrue(snapshot.TryResolveParentWorld(0, out var firstParentWorldId));
        Assert.AreEqual(0, firstParentWorldId);
        Assert.IsTrue(snapshot.TryResolveParentWorld(1, out var secondParentWorldId));
        Assert.AreEqual(1, secondParentWorldId);
        Assert.IsTrue(snapshot.TryResolveParentWorld(10, out var thirdParentWorldId));
        Assert.AreEqual(1, thirdParentWorldId);
        Assert.IsTrue(snapshot.TryResolveParentWorld(11, out var fourthParentWorldId));
        Assert.AreEqual(1, fourthParentWorldId);
        AssertMemberWorldIds(snapshot, parentWorldId: 0, 0);
        AssertMemberWorldIds(snapshot, parentWorldId: 1, 1, 10, 11);
    }

    private static WorldParentTopologyCatalog CreateCatalog() =>
        new WorldParentTopologyCatalog(new GameSessionGeneration(1001));

    private static bool IsCompleteOldOrNewSnapshot(
        WorldParentTopologySnapshot snapshot)
    {
        if (!snapshot.TryResolveParentWorld(1, out var firstParentWorldId) ||
            !snapshot.TryResolveParentWorld(2, out var secondParentWorldId) ||
            firstParentWorldId != 1)
        {
            return false;
        }

        var firstParentMembers = snapshot.GetMemberWorldIds(1);
        var secondParentMembers = snapshot.GetMemberWorldIds(2);
        bool isOldState =
            secondParentWorldId == 1 &&
            HasExactValues(firstParentMembers, 1, 2) &&
            secondParentMembers.Count == 0;
        bool isNewState =
            secondParentWorldId == 2 &&
            HasExactValues(firstParentMembers, 1) &&
            HasExactValues(secondParentMembers, 2);
        return isOldState || isNewState;
    }

    private static bool HasExactValues(
        IReadOnlyList<int> observedValues,
        params int[] expectedValues)
    {
        if (observedValues.Count != expectedValues.Length)
        {
            return false;
        }

        for (var valueIndex = 0;
             valueIndex < expectedValues.Length;
             valueIndex++)
        {
            if (observedValues[valueIndex] != expectedValues[valueIndex])
            {
                return false;
            }
        }

        return true;
    }

    private static void AssertMemberWorldIds(
        WorldParentTopologySnapshot snapshot,
        int parentWorldId,
        params int[] expectedMemberWorldIds)
    {
        Assert.AreSequenceEqual(
            expectedMemberWorldIds,
            snapshot.GetMemberWorldIds(parentWorldId).ToArray());
    }

    private static FieldInfo RequirePrivateInt64Field(
        Type declaringType,
        string exactFieldName)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private field " +
            $"{declaringType.Name}.{exactFieldName}.");
        Assert.AreEqual(typeof(long), field.FieldType);
        return field;
    }
}
