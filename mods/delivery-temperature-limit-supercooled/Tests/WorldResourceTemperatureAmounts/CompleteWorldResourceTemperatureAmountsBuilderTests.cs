using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;
public sealed class CompleteWorldResourceTemperatureAmountsBuilderTests
{
    [Test]
    public async Task Build_WhenResourceTagIsAbsentFromCandidate_PublishesACompleteMapWithoutThatTag()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));
        builder.AddTemperatureAmount(300.0f, 10.0f);
        builder.CompleteResourceTag();

        var amounts = builder.Build();

        await Assert.That(amounts.TryGetSeries(new Tag("Iron"), out _)).IsTrue();
        await Assert.That(amounts.TryGetSeries(new Tag("Copper"), out _)).IsFalse();
    }

    [Test]
    public async Task BeginWorld_WhenAlreadyBuilding_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));

        var exception = (await Assert.That(() =>
            builder.BeginWorld(new WorldInventoryCollectionGeneration(5))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("already building");
    }

    [Test]
    public async Task BeginResourceTag_WhenAnotherTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));

        var exception = (await Assert.That(() =>
            builder.BeginResourceTag(new Tag("Copper"))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("resource tag is already open");
    }

    [Test]
    public async Task CompleteResourceTag_WhenNoTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));

        var exception = (await Assert.That(() =>
            builder.CompleteResourceTag()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("BeginResourceTag");
    }

    [Test]
    public async Task BeginResourceTag_WhenTagRepeatsInOneWorld_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));
        builder.CompleteResourceTag();

        var exception = (await Assert.That(() =>
            builder.BeginResourceTag(new Tag("Iron"))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("already been completed");
    }

    [Test]
    public async Task Build_WhenTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        builder.BeginResourceTag(new Tag("Iron"));

        var exception = (await Assert.That(() =>
            builder.Build()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("CompleteResourceTag");
    }

    [Test]
    public async Task Build_WhenComplete_PublishesImmutableSeriesByTag()
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

        await Assert.That(publication.CollectionGeneration).IsEqualTo(collectionGeneration);
        await Assert.That(publication.ResourceTags.ToArray()).IsEquivalentTo(new[] { new Tag("Iron"), new Tag("Copper") }, TUnit.Assertions.Enums.CollectionOrdering.Any);
        await Assert.That(publication.TryGetSeries(new Tag("Iron"), out var ironAmounts)).IsTrue();
        await Assert.That(publication.TryGetSeries(new Tag("Copper"), out var copperAmounts)).IsTrue();
        await Assert.That(ironAmounts.TotalAmount).IsEqualTo(10.0f);
        await Assert.That(copperAmounts.TotalAmount).IsEqualTo(20.0f);
        await Assert.That(publication.ResourceTags is Tag[]).IsFalse();
        await Assert.That(publication.ResourceTags is ICollection<Tag> mutableTags &&
            !mutableTags.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task Build_WhenSourceBuilderIsReused_PreviousPublicationDoesNotChange()
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

        await Assert.That(firstPublication.ResourceTags).IsEquivalentTo(new[] { new Tag("Iron") }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(firstPublication.TryGetSeries(
            new Tag("Iron"),
            out var firstIronAmounts)).IsTrue();
        await Assert.That(firstIronAmounts.TotalAmount).IsEqualTo(10.0f);
        await Assert.That(firstPublication.TryGetSeries(new Tag("Copper"), out _)).IsFalse();

        await Assert.That(secondPublication.TryGetSeries(
            new Tag("Iron"),
            out var secondIronAmounts)).IsTrue();
        await Assert.That(secondIronAmounts.TotalAmount).IsEqualTo(100.0f);
        await Assert.That(secondPublication.TryGetSeries(new Tag("Copper"), out _)).IsTrue();
    }

    [Test]
    public async Task Discard_WhenBuildIsIncomplete_ReleasesCandidateReferences()
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
        var openAccumulator = await ReadPrivateField<TemperatureAmountAccumulator>(
            builder,
            "temperatureAmountAccumulator");

        builder.Discard();

        var candidateMap = await ReadPrivateField<
            Dictionary<Tag, TemperatureAmountSeries>>(
                builder,
                "temperatureAmountsByResourceTag");
        await Assert.That(candidateMap).IsEmpty();
        await Assert.That((await ReadPrivateField<TemperatureAmountAccumulator>(
                builder,
                "temperatureAmountAccumulator"))).IsNotSameReferenceAs(openAccumulator);
        await Assert.That((await ReadPrivateField<object>(builder, "state")).ToString()).IsEqualTo("Idle");

        // Reuse proves that Discard also closes the accumulator lifecycle; a
        // lingering open tag would make this otherwise-valid sequence fail.
        builder.BeginWorld(new WorldInventoryCollectionGeneration(5));
        AddCompletedResourceTag(
            builder,
            new Tag("Copper"),
            temperatureKelvin: 400.0f,
            amount: 20.0f);
        await Assert.That(builder.Build().TryGetSeries(new Tag("Copper"), out _)).IsTrue();
    }

    [Test]
    public async Task Build_WhenPreviousCandidateExceededRetainedTagLimit_ReplacesMutableDictionary()
    {
        await AssertRetentionBehavior(
            RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount,
            dictionaryShouldBeReplaced: false);
        await AssertRetentionBehavior(
            RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount + 1,
            dictionaryShouldBeReplaced: true);
        await AssertRetentionBehavior(
            RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount + 257,
            dictionaryShouldBeReplaced: true);
    }

    [Test]
    public async Task Build_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
        _ = builder.Build();

        var exception = (await Assert.That(() =>
            builder.Build()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("already been built");
    }

    [Test]
    public async Task AddTemperatureAmount_WhenNoResourceTagIsOpen_ThrowsInvalidOperationException()
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(new WorldInventoryCollectionGeneration(4));

        var exception = (await Assert.That(() =>
            builder.AddTemperatureAmount(300.0f, 10.0f)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("BeginResourceTag");
    }

    private static async Task AssertRetentionBehavior(
        int resourceTagCount,
        bool dictionaryShouldBeReplaced)
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        var candidateMapBeforeBuild = await ReadPrivateField<
            Dictionary<Tag, TemperatureAmountSeries>>(
                builder,
                "temperatureAmountsByResourceTag");
        var accumulatorBeforeBuild =
            await ReadPrivateField<TemperatureAmountAccumulator>(
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

        await Assert.That(publication.ResourceTags.Count).IsEqualTo(resourceTagCount);
        for (var resourceTagIndex = 0;
             resourceTagIndex < resourceTagCount;
             resourceTagIndex++)
        {
            await Assert.That(publication.TryGetSeries(
                    ResourceTag(resourceTagIndex),
                    out var series)).IsTrue().Because($"Resource tag index {resourceTagIndex} was dropped.");
            await Assert.That(series).IsSameReferenceAs(TemperatureAmountSeries.Empty);
        }

        var candidateMapAfterBuild = await ReadPrivateField<
            Dictionary<Tag, TemperatureAmountSeries>>(
                builder,
                "temperatureAmountsByResourceTag");
        if (dictionaryShouldBeReplaced)
        {
            await Assert.That(candidateMapAfterBuild).IsNotSameReferenceAs(candidateMapBeforeBuild);
        }
        else
        {
            await Assert.That(candidateMapAfterBuild).IsSameReferenceAs(candidateMapBeforeBuild);
        }

        await Assert.That(candidateMapAfterBuild).IsEmpty();
        await Assert.That((await ReadPrivateField<TemperatureAmountAccumulator>(
                builder,
                "temperatureAmountAccumulator"))).IsSameReferenceAs(accumulatorBeforeBuild);
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

    private static async Task<T> ReadPrivateField<T>(object instance, string exactFieldName)
    {
        var field = instance.GetType().GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private field " +
            $"{instance.GetType().Name}.{exactFieldName}.");
        return ((await Assert.That(field.GetValue(instance)).IsAssignableTo<T>())!);
    }
}
