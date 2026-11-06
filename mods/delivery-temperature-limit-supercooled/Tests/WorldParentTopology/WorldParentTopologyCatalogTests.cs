using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldParentTopology;
public sealed class WorldParentTopologyCatalogTests
{
    private const int ConcurrentWriterIterationCount = 5000;
    private const int ConcurrentReaderIterationCount = 20000;
    private const int ConcurrentReaderCount = 4;

    [Test]
    public async Task RegisterWorld_WhenMappingIsNew_IncrementsVersionOnce()
    {
        var catalog = CreateCatalog();
        var initialSnapshot = catalog.CaptureSnapshot();

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var changedSnapshot = catalog.CaptureSnapshot();

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(change.WorldId).IsEqualTo(7);
        await Assert.That(change.PreviousParentWorldId).IsNull();
        await Assert.That(change.CurrentParentWorldId).IsEqualTo(1);
        await Assert.That(changedSnapshot).IsNotSameReferenceAs(initialSnapshot);
        await Assert.That(changedSnapshot.Version.Value).IsEqualTo(initialSnapshot.Version.Value + 1);
        await Assert.That(changedSnapshot.GameSessionGeneration).IsEqualTo(initialSnapshot.GameSessionGeneration);
        await Assert.That(changedSnapshot.TryResolveParentWorld(
            7,
            out var parentWorldId)).IsTrue();
        await Assert.That(parentWorldId).IsEqualTo(1);
    }

    [Test]
    public async Task RegisterWorld_WhenMappingIsIdentical_DoesNotChangeVersionOrSnapshotReference()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var snapshotBeforeRepeat = catalog.CaptureSnapshot();

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 1);

        await Assert.That(change.HasChanged).IsFalse();
        await Assert.That(change.WorldId).IsEqualTo(7);
        await Assert.That(change.PreviousParentWorldId).IsEqualTo(1);
        await Assert.That(change.CurrentParentWorldId).IsEqualTo(1);
        await Assert.That(catalog.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeRepeat);
        await Assert.That(catalog.CaptureSnapshot().Version).IsEqualTo(snapshotBeforeRepeat.Version);
    }

    [Test]
    public async Task RegisterWorld_WhenExistingWorldChangesParent_ReturnsBothAffectedParents()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 2);

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(change.WorldId).IsEqualTo(7);
        await Assert.That(change.PreviousParentWorldId).IsEqualTo(1);
        await Assert.That(change.CurrentParentWorldId).IsEqualTo(2);
        await Assert.That(catalog.CaptureSnapshot().TryResolveParentWorld(
            7,
            out var parentWorldId)).IsTrue();
        await Assert.That(parentWorldId).IsEqualTo(2);
    }

    [Test]
    public async Task RegisterWorld_WhenWorldIsItsOwnParent_PreservesSelfParentMapping()
    {
        var catalog = CreateCatalog();

        var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 7);
        var snapshot = catalog.CaptureSnapshot();

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(change.CurrentParentWorldId).IsEqualTo(7);
        await Assert.That(snapshot.TryResolveParentWorld(7, out var parentWorldId)).IsTrue();
        await Assert.That(parentWorldId).IsEqualTo(7);
        await AssertMemberWorldIds(snapshot, parentWorldId: 7, 7);
    }

    [Test]
    public async Task RegisterWorld_WhenWorldIdIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var catalog = CreateCatalog();
        var snapshotBeforeFailure = catalog.CaptureSnapshot();

        var exception = (await Assert.That(() =>
            catalog.RegisterWorld(worldId: -1, parentWorldId: 0)).ThrowsExactly<ArgumentOutOfRangeException>())!;

        await Assert.That(exception.ParamName).IsEqualTo("worldId");
        await Assert.That(catalog.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeFailure);
    }

    [Test]
    public async Task RegisterWorld_WhenParentWorldIdIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var catalog = CreateCatalog();
        var snapshotBeforeFailure = catalog.CaptureSnapshot();

        var exception = (await Assert.That(() =>
            catalog.RegisterWorld(worldId: 0, parentWorldId: -1)).ThrowsExactly<ArgumentOutOfRangeException>())!;

        await Assert.That(exception.ParamName).IsEqualTo("parentWorldId");
        await Assert.That(catalog.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeFailure);
    }

    [Test]
    public async Task RemoveWorld_WhenKnown_ReturnsPreviousParentAndRemovesMapping()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 2);
        var snapshotBeforeRemoval = catalog.CaptureSnapshot();

        var change = catalog.RemoveWorld(worldId: 7);
        var snapshotAfterRemoval = catalog.CaptureSnapshot();

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(change.WorldId).IsEqualTo(7);
        await Assert.That(change.PreviousParentWorldId).IsEqualTo(2);
        await Assert.That(change.CurrentParentWorldId).IsNull();
        await Assert.That(snapshotAfterRemoval.Version.Value).IsEqualTo(snapshotBeforeRemoval.Version.Value + 1);
        await Assert.That(snapshotAfterRemoval.TryResolveParentWorld(7, out _)).IsFalse();
        await Assert.That(snapshotAfterRemoval.GetMemberWorldIds(2)).IsEmpty();
    }

    [Test]
    public async Task RemoveWorld_WhenUnknown_IsIdempotent()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 2);
        var snapshotBeforeUnknownRemoval = catalog.CaptureSnapshot();

        var change = catalog.RemoveWorld(worldId: 8);

        await Assert.That(change.HasChanged).IsFalse();
        await Assert.That(change.WorldId).IsEqualTo(8);
        await Assert.That(change.PreviousParentWorldId).IsNull();
        await Assert.That(change.CurrentParentWorldId).IsNull();
        await Assert.That(catalog.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeUnknownRemoval);
    }

    [Test]
    public async Task GetMemberWorldIds_WhenParentHasSeveralWorlds_ReturnsSortedImmutableIds()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 9, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 2, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var snapshot = catalog.CaptureSnapshot();

        var memberWorldIds = snapshot.GetMemberWorldIds(parentWorldId: 1);

        await Assert.That(memberWorldIds.ToArray()).IsEquivalentTo(new[] { 2, 7, 9 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(memberWorldIds is int[]).IsFalse();
        var mutableListView = (await Assert.That(memberWorldIds).IsAssignableTo<IList<int>>())!;
        await Assert.That(() =>
            mutableListView[0] = 99).ThrowsExactly<NotSupportedException>();
        await AssertMemberWorldIds(snapshot, parentWorldId: 1, 2, 7, 9);
    }

    [Test]
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

        await Assert.That(invalidSnapshotCount).IsEqualTo(0);
    }

    [Test]
    public async Task TryResolveParentWorld_WhenWorldIsUnknown_ReturnsFalseWithoutFallback()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 0, parentWorldId: 0);

        var found = catalog.CaptureSnapshot().TryResolveParentWorld(
            worldId: 999,
            out var parentWorldId);

        await Assert.That(found).IsFalse();
        await Assert.That(parentWorldId).IsEqualTo(default);
        await Assert.That(catalog.CaptureSnapshot().GetMemberWorldIds(999)).IsEmpty();
    }

    [Test]
    public async Task RegisterWorld_WhenTopologyVersionIsExhausted_ThrowsWithoutChangingSnapshot()
    {
        var catalog = CreateCatalog();
        catalog.RegisterWorld(worldId: 7, parentWorldId: 1);
        var snapshotBeforeFailure = catalog.CaptureSnapshot();
        var versionField = await RequirePrivateInt64Field(
            typeof(WorldParentTopologyCatalog),
            "version");
        versionField.SetValue(catalog, long.MaxValue);

        var exception = (await Assert.That(() =>
            catalog.RegisterWorld(worldId: 7, parentWorldId: 2)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("topology version");
        await Assert.That(catalog.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeFailure);
        await Assert.That(catalog.CaptureSnapshot().TryResolveParentWorld(
            7,
            out var retainedParentWorldId)).IsTrue();
        await Assert.That(retainedParentWorldId).IsEqualTo(1);
        await Assert.That(((await Assert.That(versionField.GetValue(catalog)).IsAssignableTo<long>())!)).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task Catalog_WhenBaseGameContentModeShapeIsRegistered_PreservesReportedTopology()
    {
        var catalog = CreateCatalog();

        catalog.RegisterWorld(worldId: 0, parentWorldId: 0);
        var snapshot = catalog.CaptureSnapshot();

        await Assert.That(snapshot.TryResolveParentWorld(0, out var parentWorldId)).IsTrue();
        await Assert.That(parentWorldId).IsEqualTo(0);
        await AssertMemberWorldIds(snapshot, parentWorldId: 0, 0);
    }

    [Test]
    public async Task Catalog_WhenSpacedOutContentModeShapeIsRegistered_PreservesEveryReportedWorld()
    {
        var catalog = CreateCatalog();

        catalog.RegisterWorld(worldId: 0, parentWorldId: 0);
        catalog.RegisterWorld(worldId: 1, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 10, parentWorldId: 1);
        catalog.RegisterWorld(worldId: 11, parentWorldId: 1);
        var snapshot = catalog.CaptureSnapshot();

        await Assert.That(snapshot.TryResolveParentWorld(0, out var firstParentWorldId)).IsTrue();
        await Assert.That(firstParentWorldId).IsEqualTo(0);
        await Assert.That(snapshot.TryResolveParentWorld(1, out var secondParentWorldId)).IsTrue();
        await Assert.That(secondParentWorldId).IsEqualTo(1);
        await Assert.That(snapshot.TryResolveParentWorld(10, out var thirdParentWorldId)).IsTrue();
        await Assert.That(thirdParentWorldId).IsEqualTo(1);
        await Assert.That(snapshot.TryResolveParentWorld(11, out var fourthParentWorldId)).IsTrue();
        await Assert.That(fourthParentWorldId).IsEqualTo(1);
        await AssertMemberWorldIds(snapshot, parentWorldId: 0, 0);
        await AssertMemberWorldIds(snapshot, parentWorldId: 1, 1, 10, 11);
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

    private static async Task AssertMemberWorldIds(
        WorldParentTopologySnapshot snapshot,
        int parentWorldId,
        params int[] expectedMemberWorldIds)
    {
        await Assert.That(snapshot.GetMemberWorldIds(parentWorldId).ToArray()).IsEquivalentTo(expectedMemberWorldIds, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static async Task<FieldInfo> RequirePrivateInt64Field(
        Type declaringType,
        string exactFieldName)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private field " +
            $"{declaringType.Name}.{exactFieldName}.");
        await Assert.That(field.FieldType).IsEqualTo(typeof(long));
        return field;
    }
}
