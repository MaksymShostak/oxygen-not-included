using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackWorldInventoryPublicationSessionTests
{
    private static readonly GameSessionGeneration FirstGameSessionGeneration =
        new(41);
    private static readonly WorldInventoryCollectionGeneration
        FirstCollectionGeneration = new(73);

    [TestMethod]
    public void BeginCompleteWorldUpdate_WhenTwoTagsComplete_ProducesOneCompleteWorldPublication()
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

        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind.CompleteWorldAmounts,
            result.Kind);
        Assert.IsTrue(
            result.TryGetCompleteWorldResourceTemperatureAmounts(
                out CompleteWorldResourceTemperatureAmounts completeWorldAmounts));
        Assert.AreEqual(
            FirstCollectionGeneration,
            completeWorldAmounts.CollectionGeneration);
        Assert.AreSequenceEqual(
            new[] { new Tag("Iron"), new Tag("Copper") },
            completeWorldAmounts.ResourceTags);
        AssertSeriesTotal(completeWorldAmounts, new Tag("Iron"), 10.0f);
        AssertSeriesTotal(completeWorldAmounts, new Tag("Copper"), 20.0f);
        Assert.IsFalse(result.TryGetWorldResourceTagCoverage(out _));
        Assert.IsFalse(
            result.TryGetWorldResourceTemperatureSeriesPublication(out _));
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenOneTagCompletes_ProducesResourceTemperatureSeries()
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

        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind.ResourceTemperatureSeries,
            result.Kind);
        Assert.IsTrue(
            result.TryGetWorldResourceTemperatureSeriesPublication(
                out WorldResourceTemperatureSeriesPublication publication));
        Assert.AreEqual(FirstCollectionGeneration, publication.CollectionGeneration);
        Assert.AreEqual(new Tag("Iron"), publication.ResourceTag);
        Assert.AreEqual(10.0f, publication.TemperatureAmounts.TotalAmount);
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenSecondTagBegins_ThrowsLifecycleViolation()
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
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.BeginResourceTag(new Tag("Copper")));

        StringAssert.Contains(exception.Message, "exactly one resource tag");
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateRequiringCoverage_WhenOneTagCompletes_ProducesCoverageAndTemperatureSeries()
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

        Assert.AreEqual(1, presentResourceTags.EnumerationCount);
        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind
                .ResourceTagCoverageAndTemperatureSeries,
            result.Kind);
        Assert.IsTrue(result.TryGetWorldResourceTagCoverage(
            out WorldResourceTagCoverage coverage));
        Assert.AreSequenceEqual(
            new[] { new Tag("Iron"), new Tag("Copper") },
            coverage.PresentResourceTags);
        Assert.IsTrue(
            result.TryGetWorldResourceTemperatureSeriesPublication(
                out WorldResourceTemperatureSeriesPublication publication));
        Assert.AreEqual(new Tag("Iron"), publication.ResourceTag);
        Assert.AreEqual(10.0f, publication.TemperatureAmounts.TotalAmount);
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenOneTagCompletes_ProducesOnlyTemperatureSeries()
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

        Assert.IsFalse(result.TryGetWorldResourceTagCoverage(out _));
        Assert.IsFalse(
            result.TryGetCompleteWorldResourceTemperatureAmounts(out _));
        Assert.IsTrue(
            result.TryGetWorldResourceTemperatureSeriesPublication(out _));
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateRequiringCoverage_WhenInventoryHasNoTags_ProducesCoverageOnly()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            Array.Empty<Tag>());

        FastTrackWorldInventoryPublicationResult result = session.Complete();

        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind.ResourceTagCoverageOnly,
            result.Kind);
        Assert.IsTrue(result.TryGetWorldResourceTagCoverage(
            out WorldResourceTagCoverage coverage));
        Assert.IsEmpty(coverage.PresentResourceTags);
        Assert.IsFalse(
            result.TryGetWorldResourceTemperatureSeriesPublication(out _));
    }

    [TestMethod]
    public void Complete_WhenResourceTagIsStillOpen_ThrowsLifecycleViolation()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);
        session.BeginResourceTag(new Tag("Iron"));

        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.Complete());

        StringAssert.Contains(exception.Message, "CompleteResourceTag");
    }

    [TestMethod]
    public void Discard_AfterException_ReleasesCoverageTagsAndAccumulatorReferences()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            new[] { new Tag("Iron"), new Tag("Copper") });
        session.BeginResourceTag(new Tag("Iron"));
        session.AddTemperatureAmount(300.0f, 10.0f);
        TemperatureAmountAccumulator accumulatorBeforeDiscard =
            ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator");

        session.Discard();

        Assert.IsNull(ReadPrivateField<object?>(
            session,
            "resourceTagCoverage"));
        Assert.AreNotSame(
            accumulatorBeforeDiscard,
            ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator"));

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            new WorldInventoryCollectionGeneration(74));
        AddCompletedResourceTag(
            session,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);
        Assert.AreEqual(
            20.0f,
            RequireSeriesPublication(session.Complete())
                .TemperatureAmounts.TotalAmount);
    }

    [TestMethod]
    public void Begin_WhenGameSessionGenerationChanges_DiscardsRetainedOldSessionState()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginIncrementalResourceTagUpdateRequiringCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration,
            new[] { new Tag("Iron") });
        session.BeginResourceTag(new Tag("Iron"));
        session.AddTemperatureAmount(300.0f, 10.0f);
        TemperatureAmountAccumulator oldAccumulator =
            ReadPrivateField<TemperatureAmountAccumulator>(
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
            RequireSeriesPublication(session.Complete());
        Assert.AreNotSame(
            oldAccumulator,
            ReadPrivateField<TemperatureAmountAccumulator>(
                session,
                "temperatureAmountAccumulator"));
        Assert.AreEqual(new Tag("Copper"), publication.ResourceTag);
        Assert.AreEqual(20.0f, publication.TemperatureAmounts.TotalAmount);
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenUnrelatedCollectionMutates_DoesNotRetainOrPublishIt()
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
            RequireSeriesPublication(result);
        Assert.AreEqual(new Tag("Iron"), publication.ResourceTag);
        Assert.IsFalse(result.TryGetWorldResourceTagCoverage(out _));
        Assert.IsNull(ReadPrivateField<object?>(session, "resourceTagCoverage"));
    }

    [TestMethod]
    public void BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenFreshSessionBegins_DoesNotAllocateCompleteWorldBuilder()
    {
        var session = new FastTrackWorldInventoryPublicationSession();

        session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);

        Assert.IsNull(ReadPrivateField<object?>(
            session,
            "completeWorldResourceTemperatureAmountsBuilder"));
    }

    [TestMethod]
    public void Begin_WhenSameGameSessionAlreadyHasActiveUpdate_ThrowsLifecycleViolation()
    {
        var session = new FastTrackWorldInventoryPublicationSession();
        session.BeginCompleteWorldUpdate(
            FirstGameSessionGeneration,
            FirstCollectionGeneration);

        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.BeginIncrementalResourceTagUpdateWithCurrentCoverage(
                    FirstGameSessionGeneration,
                    FirstCollectionGeneration));

        StringAssert.Contains(exception.Message, "already active");
    }

    [TestMethod]
    public void CompleteThenThreeIncrementalUpdates_WhenNextGenerationRequiresCoverage_EnumeratesKeysOnceAndProducesOnlySingleTagSeries()
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
            ReadPrivateField<CompleteWorldResourceTemperatureAmountsBuilder>(
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

        Assert.AreEqual(1, presentResourceTags.EnumerationCount);
        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind.CompleteWorldAmounts,
            results[0].Kind);
        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind
                .ResourceTagCoverageAndTemperatureSeries,
            results[1].Kind);
        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind.ResourceTemperatureSeries,
            results[2].Kind);
        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind.ResourceTemperatureSeries,
            results[3].Kind);
        Assert.AreSequenceEqual(
            new[] { new Tag("Iron"), new Tag("Copper"), new Tag("Gold") },
            results.Skip(1)
                .Select(RequireSeriesPublication)
                .Select(publication => publication.ResourceTag));
        Assert.AreSame(
            completeWorldBuilder,
            ReadPrivateField<CompleteWorldResourceTemperatureAmountsBuilder>(
                session,
                "completeWorldResourceTemperatureAmountsBuilder"));
        foreach (FastTrackWorldInventoryPublicationResult incrementalResult in
                 results.Skip(1))
        {
            Assert.IsFalse(
                incrementalResult
                    .TryGetCompleteWorldResourceTemperatureAmounts(out _));
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

    private static void AssertSeriesTotal(
        CompleteWorldResourceTemperatureAmounts completeWorldAmounts,
        Tag resourceTag,
        float expectedTotal)
    {
        Assert.IsTrue(completeWorldAmounts.TryGetSeries(
            resourceTag,
            out TemperatureAmountSeries series));
        Assert.AreEqual(expectedTotal, series.TotalAmount);
    }

    private static WorldResourceTemperatureSeriesPublication
        RequireSeriesPublication(FastTrackWorldInventoryPublicationResult result)
    {
        Assert.IsTrue(
            result.TryGetWorldResourceTemperatureSeriesPublication(
                out WorldResourceTemperatureSeriesPublication publication));
        return publication;
    }

    private static T ReadPrivateField<T>(
        object instance,
        string exactFieldName)
    {
        FieldInfo? field = instance.GetType().GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private field " +
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
