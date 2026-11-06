using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackWorldInventoryPublicationSessionTests
{
    private static readonly GameSessionGeneration FirstGameSessionGeneration =
        new(41);
    private static readonly WorldInventoryCollectionGeneration
        FirstCollectionGeneration = new(73);

    [Test]
    public async Task BeginCompleteWorldUpdate_WhenTwoTagsComplete_ProducesOneCompleteWorldPublication()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginCompleteWorldUpdate(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);
        AddCompletedResourceTag(
            session,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        await Assert.That(result.Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind.CompleteWorldAmounts);
        await Assert.That(result.TryGetCompleteWorldResourceTemperatureAmounts(
                out CompleteWorldResourceTemperatureAmounts completeWorldAmounts)).IsTrue();
        await Assert.That(completeWorldAmounts.CollectionGeneration).IsEqualTo(FirstCollectionGeneration);
        await Assert.That(completeWorldAmounts.ResourceTags).IsEquivalentTo(new[] { new Tag("Iron"), new Tag("Copper") }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await AssertSeriesTotal(completeWorldAmounts, new Tag("Iron"), 10.0f);
        await AssertSeriesTotal(completeWorldAmounts, new Tag("Copper"), 20.0f);
        await Assert.That(result.TryGetWorldResourceTagCoverage(out _)).IsFalse();
        await Assert.That(result.TryGetWorldResourceTemperatureSeriesPublication(out _)).IsFalse();
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenOneTagCompletes_ProducesResourceTemperatureSeries()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        await Assert.That(result.Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind.ResourceTemperatureSeries);
        await Assert.That(result.TryGetWorldResourceTemperatureSeriesPublication(
                out WorldResourceTemperatureSeriesPublication publication)).IsTrue();
        await Assert.That(publication.CollectionGeneration).IsEqualTo(FirstCollectionGeneration);
        await Assert.That(publication.ResourceTag).IsEqualTo(new Tag("Iron"));
        await Assert.That(publication.TemperatureAmounts.TotalAmount).IsEqualTo(10.0f);
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenSecondTagBegins_ThrowsLifecycleViolation()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);

        InvalidOperationException exception =
            (await Assert.That(() =>
                session.BeginResourceTag(new Tag("Copper"))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("exactly one resource tag");
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateRequiringCoverage_WhenOneTagCompletes_ProducesCoverageAndTemperatureSeries()
    {
        var presentResourceTags = new CountingTagCollection(
            new Tag("Iron"),
            new Tag("Copper"));
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            presentResourceTags);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        await Assert.That(presentResourceTags.EnumerationCount).IsEqualTo(1);
        await Assert.That(result.Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind
                .ResourceTagCoverageAndTemperatureSeries);
        await Assert.That(result.TryGetWorldResourceTagCoverage(
            out WorldResourceTagCoverage coverage)).IsTrue();
        await Assert.That(coverage.PresentResourceTags).IsEquivalentTo(new[] { new Tag("Iron"), new Tag("Copper") }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.TryGetWorldResourceTemperatureSeriesPublication(
                out WorldResourceTemperatureSeriesPublication publication)).IsTrue();
        await Assert.That(publication.ResourceTag).IsEqualTo(new Tag("Iron"));
        await Assert.That(publication.TemperatureAmounts.TotalAmount).IsEqualTo(10.0f);
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenOneTagCompletes_ProducesOnlyTemperatureSeries()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        await Assert.That(result.TryGetWorldResourceTagCoverage(out _)).IsFalse();
        await Assert.That(result.TryGetCompleteWorldResourceTemperatureAmounts(out _)).IsFalse();
        await Assert.That(result.TryGetWorldResourceTemperatureSeriesPublication(out _)).IsTrue();
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateRequiringCoverage_WhenInventoryHasNoTags_ProducesCoverageOnly()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            Array.Empty<Tag>());

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        await Assert.That(result.Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind.ResourceTagCoverageOnly);
        await Assert.That(result.TryGetWorldResourceTagCoverage(
            out WorldResourceTagCoverage coverage)).IsTrue();
        await Assert.That(coverage.PresentResourceTags).IsEmpty();
        await Assert.That(result.TryGetWorldResourceTemperatureSeriesPublication(out _)).IsFalse();
    }

    [Test]
    public async Task Complete_WhenResourceTagIsStillOpen_ThrowsLifecycleViolation()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        session.BeginResourceTag(new Tag("Iron"));

        InvalidOperationException exception =
            (await Assert.That(() =>
                session.Complete()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("CompleteResourceTag");
    }

    [Test]
    public async Task Discard_AfterException_ReleasesCoverageTagsAndAccumulatorReferences()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            new[] { new Tag("Iron"), new Tag("Copper") });
        session.BeginResourceTag(new Tag("Iron"));
        session.AddTemperatureAmount(300.0f, 10.0f);
        TemperatureAmountAccumulator accumulatorBeforeDiscard =
            await ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator");

        session.Discard();

        await Assert.That((await ReadPrivateField<object?>(
            session,
            "resourceTagCoverage"))).IsNull();
        await Assert.That((await ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator"))).IsNotSameReferenceAs(accumulatorBeforeDiscard);

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            new WorldInventoryCollectionGeneration(74));
        AddCompletedResourceTag(
            session,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);
        await Assert.That((await RequireSeriesPublication(session.Complete()))
                .TemperatureAmounts.TotalAmount).IsEqualTo(20.0f);
    }

    [Test]
    public async Task Begin_WhenGameSessionGenerationChanges_DiscardsRetainedOldSessionState()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            new[] { new Tag("Iron") });
        session.BeginResourceTag(new Tag("Iron"));
        session.AddTemperatureAmount(300.0f, 10.0f);
        TemperatureAmountAccumulator oldAccumulator =
            await ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator");

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            new GameSessionGeneration(42),
            new WorldInventoryCollectionGeneration(81));
        AddCompletedResourceTag(
            session,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);

        WorldResourceTemperatureSeriesPublication publication =
            await RequireSeriesPublication(session.Complete());
        await Assert.That((await ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator"))).IsNotSameReferenceAs(oldAccumulator);
        await Assert.That(publication.ResourceTag).IsEqualTo(new Tag("Copper"));
        await Assert.That(publication.TemperatureAmounts.TotalAmount).IsEqualTo(20.0f);
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenUnrelatedCollectionMutates_DoesNotRetainOrPublishIt()
    {
        var unrelatedInventory = new List<Tag> { new("Copper") };
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        unrelatedInventory.Add(new Tag("Gold"));
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        WorldResourceTemperatureSeriesPublication publication =
            await RequireSeriesPublication(result);
        await Assert.That(publication.ResourceTag).IsEqualTo(new Tag("Iron"));
        await Assert.That(result.TryGetWorldResourceTagCoverage(out _)).IsFalse();
        await Assert.That((await ReadPrivateField<object?>(session, "resourceTagCoverage"))).IsNull();
    }

    [Test]
    public async Task BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenFreshSessionBegins_DoesNotAllocateCompleteWorldBuilder()
    {
        var session = new FastTrackWorldInventoryPublicationSession();

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);

        await Assert.That((await ReadPrivateField<object?>(
            session,
            "completeWorldResourceTemperatureAmountsBuilder"))).IsNull();
    }

    [Test]
    public async Task Begin_WhenSameGameSessionAlreadyHasActiveUpdate_ThrowsLifecycleViolation()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginCompleteWorldUpdate(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);

        InvalidOperationException exception =
            (await Assert.That(() =>
                session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
                    FirstGameSessionGeneration,
                    FirstCollectionGeneration)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("already active");
    }

    [Test]
    public async Task CompleteThenThreeIncrementalUpdates_WhenNextGenerationRequiresCoverage_EnumeratesKeysOnceAndProducesOnlySingleTagSeries()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        var results = new List<FastTrackWorldInventoryPublicationResult>();
        session.BeginCompleteWorldUpdate(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);
        results.Add(session.Complete());
        CompleteWorldResourceTemperatureAmountsBuilder completeWorldBuilder =
            await ReadPrivateField<CompleteWorldResourceTemperatureAmountsBuilder>(
                session,
                "completeWorldResourceTemperatureAmountsBuilder");

        var nextCollectionGeneration =
            new WorldInventoryCollectionGeneration(74);
        var presentResourceTags = new CountingTagCollection(
            new Tag("Iron"),
            new Tag("Copper"),
            new Tag("Gold"));
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            nextCollectionGeneration,
            presentResourceTags);
        AddCompletedResourceTag(
            session,
            new Tag("Iron"),
            temperatureKelvin: 301.0f,
            amount: 11.0f);
        results.Add(session.Complete());

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            nextCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Copper"),
            temperatureKelvin: 401.0f,
            amount: 21.0f);
        results.Add(session.Complete());

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            nextCollectionGeneration);
        AddCompletedResourceTag(
            session,
            new Tag("Gold"),
            temperatureKelvin: 501.0f,
            amount: 31.0f);
        results.Add(session.Complete());

        await Assert.That(presentResourceTags.EnumerationCount).IsEqualTo(1);
        await Assert.That(results[0].Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind.CompleteWorldAmounts);
        await Assert.That(results[1].Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind
                .ResourceTagCoverageAndTemperatureSeries);
        await Assert.That(results[2].Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind.ResourceTemperatureSeries);
        await Assert.That(results[3].Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind.ResourceTemperatureSeries);
        await Assert.That((await Task.WhenAll(results.Skip(1)
                .Select(RequireSeriesPublication)))
                .Select(publication => publication.ResourceTag)).IsEquivalentTo(new[] { new Tag("Iron"), new Tag("Copper"), new Tag("Gold") }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That((await ReadPrivateField<CompleteWorldResourceTemperatureAmountsBuilder>(
                session,
                "completeWorldResourceTemperatureAmountsBuilder"))).IsSameReferenceAs(completeWorldBuilder);
        foreach (FastTrackWorldInventoryPublicationResult incrementalResult in
                 results.Skip(1))
        {
            await Assert.That(incrementalResult
                    .TryGetCompleteWorldResourceTemperatureAmounts(out _)).IsFalse();
        }
    }

    private static void AddCompletedResourceTag(
        FastTrackWorldInventoryPublicationSession session,
        Tag resourceTag,
        float temperatureKelvin,
        float amount)
    {
        session.BeginResourceTag(resourceTag);
        session.AddTemperatureAmount(temperatureKelvin, amount);
        session.CompleteResourceTag();
    }

    private static async Task AssertSeriesTotal(
        CompleteWorldResourceTemperatureAmounts completeWorldAmounts,
        Tag resourceTag,
        float expectedTotal)
    {
        await Assert.That(completeWorldAmounts.TryGetSeries(
            resourceTag,
            out TemperatureAmountSeries series)).IsTrue();
        await Assert.That(series.TotalAmount).IsEqualTo(expectedTotal);
    }

    private static async Task<WorldResourceTemperatureSeriesPublication>
        RequireSeriesPublication(FastTrackWorldInventoryPublicationResult result)
    {
        await Assert.That(result.TryGetWorldResourceTemperatureSeriesPublication(
                out WorldResourceTemperatureSeriesPublication publication)).IsTrue();
        return publication;
    }

    private static async Task<T> ReadPrivateField<T>(
        object instance,
        string exactFieldName)
    {
        FieldInfo? field = instance.GetType().GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private field " +
            $"{instance.GetType().Name}.{exactFieldName}.");
        return (T)field.GetValue(instance)!;
    }

    private sealed class CountingTagCollection : IReadOnlyCollection<Tag>
    {
        private readonly Tag[] resourceTags;

        internal CountingTagCollection(params Tag[] resourceTags)
        {
            this.resourceTags = resourceTags;
        }

        internal int EnumerationCount { get; private set; }

        public int Count => resourceTags.Length;

        public IEnumerator<Tag> GetEnumerator()
        {
            EnumerationCount++;
            return ((IEnumerable<Tag>)resourceTags).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
