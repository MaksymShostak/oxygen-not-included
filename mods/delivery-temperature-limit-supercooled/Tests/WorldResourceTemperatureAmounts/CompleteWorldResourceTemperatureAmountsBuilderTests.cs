using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;

[TestClass]
public sealed class CompleteWorldResourceTemperatureAmountsBuilderTests
{
    [TestMethod]
    public void Build_WhenResourceTagIsAbsentFromCandidate_PublishesACompleteMapWithoutThatTag()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));
        builder.AddTemperatureAmount(300.0f, 10.0f);
        builder.CompleteResourceTag();

        var amounts = builder.Build();

        Assert.IsTrue(amounts.TryGetSeries(new Tag("Iron"), out _));
        Assert.IsFalse(amounts.TryGetSeries(new Tag("Copper"), out _));
    }

    [TestMethod]
    public void BeginWorld_WhenAlreadyBuilding_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.BeginWorld(new WorldInventoryCollectionGeneration(5)));

        StringAssert.Contains(exception.Message, "already building");
    }

    [TestMethod]
    public void BeginResourceTag_WhenAnotherTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.BeginResourceTag(new Tag("Copper")));

        StringAssert.Contains(exception.Message, "resource tag is already open");
    }

    [TestMethod]
    public void CompleteResourceTag_WhenNoTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.CompleteResourceTag());

        StringAssert.Contains(exception.Message, "BeginResourceTag");
    }

    [TestMethod]
    public void BeginResourceTag_WhenTagRepeatsInOneWorld_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));
        builder.CompleteResourceTag();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.BeginResourceTag(new Tag("Iron")));

        StringAssert.Contains(exception.Message, "already been completed");
    }

    [TestMethod]
    public void Build_WhenTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.Build());

        StringAssert.Contains(exception.Message, "CompleteResourceTag");
    }

    [TestMethod]
    public void Build_WhenComplete_PublishesImmutableSeriesByTag()
    {
        var collectionGeneration = new WorldInventoryCollectionGeneration(4);
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(collectionGeneration);
        AddCompletedResourceTag(
            builder,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);
        AddCompletedResourceTag(
            builder,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);

        var publication = builder.Build();

        Assert.AreEqual(collectionGeneration, publication.CollectionGeneration);
        CollectionAssert.AreEquivalent(
            new[] { new Tag("Iron"), new Tag("Copper") },
            publication.ResourceTags.ToArray());
        Assert.IsTrue(
            publication.TryGetSeries(new Tag("Iron"), out var ironAmounts));
        Assert.IsTrue(
            publication.TryGetSeries(new Tag("Copper"), out var copperAmounts));
        Assert.AreEqual(10.0f, ironAmounts.TotalAmount);
        Assert.AreEqual(20.0f, copperAmounts.TotalAmount);
        Assert.IsFalse(publication.ResourceTags is Tag[]);
        Assert.IsFalse(
            publication.ResourceTags is ICollection<Tag> mutableTags &&
            !mutableTags.IsReadOnly);
    }

    [TestMethod]
    public void Build_WhenSourceBuilderIsReused_PreviousPublicationDoesNotChange()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        AddCompletedResourceTag(
            builder,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);
        var firstPublication = builder.Build();

        builder.BeginWorld(new WorldInventoryCollectionGeneration(5));
        AddCompletedResourceTag(
            builder,
            new Tag("Iron"),
            temperatureKelvin: 400.0f,
            amount: 100.0f);
        AddCompletedResourceTag(
            builder,
            new Tag("Copper"),
            temperatureKelvin: 500.0f,
            amount: 20.0f);
        var secondPublication = builder.Build();

        Assert.AreSequenceEqual(
            new[] { new Tag("Iron") },
            firstPublication.ResourceTags);
        Assert.IsTrue(firstPublication.TryGetSeries(
            new Tag("Iron"),
            out var firstIronAmounts));
        Assert.AreEqual(10.0f, firstIronAmounts.TotalAmount);
        Assert.IsFalse(firstPublication.TryGetSeries(new Tag("Copper"), out _));

        Assert.IsTrue(secondPublication.TryGetSeries(
            new Tag("Iron"),
            out var secondIronAmounts));
        Assert.AreEqual(100.0f, secondIronAmounts.TotalAmount);
        Assert.IsTrue(secondPublication.TryGetSeries(new Tag("Copper"), out _));
    }

    [TestMethod]
    public void Discard_WhenBuildIsIncomplete_ReleasesCandidateReferences()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        AddCompletedResourceTag(
            builder,
            new Tag("Iron"),
            temperatureKelvin: 300.0f,
            amount: 10.0f);
        builder.BeginResourceTag(new Tag("Copper"));
        builder.AddTemperatureAmount(400.0f, 20.0f);
        var openAccumulator = ReadPrivateField<TemperatureAmountAccumulator>(
            builder,
            "temperatureAmountAccumulator");

        builder.Discard();

        var candidateMap = ReadPrivateField<
            Dictionary<Tag, TemperatureAmountSeries>>(
                builder,
                "temperatureAmountsByResourceTag");
        Assert.IsEmpty(candidateMap);
        Assert.AreNotSame(
            openAccumulator,
            ReadPrivateField<TemperatureAmountAccumulator>(
                builder,
                "temperatureAmountAccumulator"));
        Assert.AreEqual(
            "Idle",
            ReadPrivateField<object>(builder, "state").ToString());

        // Reuse proves that Discard also closes the accumulator lifecycle; a
        // lingering open tag would make this otherwise-valid sequence fail.
        builder.BeginWorld(new WorldInventoryCollectionGeneration(5));
        AddCompletedResourceTag(
            builder,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);
        Assert.IsTrue(builder.Build().TryGetSeries(new Tag("Copper"), out _));
    }

    [TestMethod]
    public void Build_WhenPreviousCandidateExceededRetainedTagLimit_ReplacesMutableDictionary()
    {
        AssertRetentionBehavior(
            RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount,
            dictionaryShouldBeReplaced: false);
        AssertRetentionBehavior(
            RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount + 1,
            dictionaryShouldBeReplaced: true);
        AssertRetentionBehavior(
            RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount + 257,
            dictionaryShouldBeReplaced: true);
    }

    [TestMethod]
    public void Build_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        _ = builder.Build();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.Build());

        StringAssert.Contains(exception.Message, "already been built");
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenNoResourceTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.AddTemperatureAmount(300.0f, 10.0f));

        StringAssert.Contains(exception.Message, "BeginResourceTag");
    }

    private static void AssertRetentionBehavior(
        int resourceTagCount,
        bool dictionaryShouldBeReplaced)
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        var candidateMapBeforeBuild = ReadPrivateField<
            Dictionary<Tag, TemperatureAmountSeries>>(
                builder,
                "temperatureAmountsByResourceTag");
        var accumulatorBeforeBuild =
            ReadPrivateField<TemperatureAmountAccumulator>(
                builder,
                "temperatureAmountAccumulator");
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        for (var resourceTagIndex = 0;
             resourceTagIndex < resourceTagCount;
             resourceTagIndex++)
        {
            builder.BeginResourceTag(ResourceTag(resourceTagIndex));
            builder.CompleteResourceTag();
        }

        var publication = builder.Build();

        Assert.AreEqual(resourceTagCount, publication.ResourceTags.Count);
        for (var resourceTagIndex = 0;
             resourceTagIndex < resourceTagCount;
             resourceTagIndex++)
        {
            Assert.IsTrue(
                publication.TryGetSeries(
                    ResourceTag(resourceTagIndex),
                    out var series),
                $"Resource tag index {resourceTagIndex} was dropped.");
            Assert.AreSame(TemperatureAmountSeries.Empty, series);
        }

        var candidateMapAfterBuild = ReadPrivateField<
            Dictionary<Tag, TemperatureAmountSeries>>(
                builder,
                "temperatureAmountsByResourceTag");
        if (dictionaryShouldBeReplaced)
        {
            Assert.AreNotSame(candidateMapBeforeBuild, candidateMapAfterBuild);
        }
        else
        {
            Assert.AreSame(candidateMapBeforeBuild, candidateMapAfterBuild);
        }

        Assert.IsEmpty(candidateMapAfterBuild);
        Assert.AreSame(
            accumulatorBeforeBuild,
            ReadPrivateField<TemperatureAmountAccumulator>(
                builder,
                "temperatureAmountAccumulator"));
    }

    private static void AddCompletedResourceTag(
        CompleteWorldResourceTemperatureAmountsBuilder builder,
        Tag resourceTag,
        float temperatureKelvin,
        float amount)
    {
        builder.BeginResourceTag(resourceTag);
        builder.AddTemperatureAmount(temperatureKelvin, amount);
        builder.CompleteResourceTag();
    }

    private static Tag ResourceTag(int resourceTagIndex) =>
        new Tag($"T{resourceTagIndex:X4}");

    private static T ReadPrivateField<T>(object instance, string exactFieldName)
    {
        var field = instance.GetType().GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private field " +
            $"{instance.GetType().Name}.{exactFieldName}.");
        return Assert.IsInstanceOfType<T>(field.GetValue(instance));
    }
}
