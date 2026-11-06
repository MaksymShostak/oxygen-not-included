namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;
public sealed class WorldResourceTemperaturePublicationTests
{
    [Test]
    public async Task CreateCoverage_WhenInputContainsDuplicateTags_PublishesFirstSeenUniquePresentTags()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            new[] { iron, copper, iron, copper });

        await Assert.That(coverage.PresentResourceTags).IsEquivalentTo(new[] { iron, copper }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CreateCoverage_WhenSourceCollectionMutates_PublishedCoverageDoesNotChange()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var sourcePresentResourceTags = new List<Tag> { iron };
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            sourcePresentResourceTags);

        sourcePresentResourceTags[0] = copper;
        sourcePresentResourceTags.Add(copper);

        await Assert.That(coverage.PresentResourceTags).IsEquivalentTo(new[] { iron }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(coverage.Contains(iron)).IsTrue();
        await Assert.That(coverage.Contains(copper)).IsFalse();
        await Assert.That(coverage.PresentResourceTags is Tag[]).IsFalse();
        await Assert.That(coverage.PresentResourceTags is ICollection<Tag> mutableTags &&
            !mutableTags.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task Contains_WhenTagWasPresent_ReturnsTrue()
    {
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            new[] { new Tag("Iron"), new Tag("Copper") });

        await Assert.That(coverage.Contains(new Tag("Iron"))).IsTrue();
    }

    [Test]
    public async Task Contains_WhenTagWasAbsent_ReturnsFalse()
    {
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            new[] { new Tag("Iron") });

        await Assert.That(coverage.Contains(new Tag("Copper"))).IsFalse();
    }

    [Test]
    public async Task CreateCoverage_WhenPresentTagsIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            WorldResourceTagCoverage.Create(
                new WorldInventoryCollectionGeneration(4),
                null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task CreateSeriesPublication_WhenSeriesIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            new WorldResourceTemperatureSeriesPublication(
                new WorldInventoryCollectionGeneration(4),
                new Tag("Iron"),
                null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task CreateSeriesPublication_WhenConstructed_PreservesGenerationTagAndSeriesIdentity()
    {
        var collectionGeneration = new WorldInventoryCollectionGeneration(4);
        var resourceTag = new Tag("Iron");
        var temperatureAmounts = CreateSeries(300.0f, 10.0f);

        var publication = new WorldResourceTemperatureSeriesPublication(
            collectionGeneration,
            resourceTag,
            temperatureAmounts);

        await Assert.That(publication.CollectionGeneration).IsEqualTo(collectionGeneration);
        await Assert.That(publication.ResourceTag).IsEqualTo(resourceTag);
        await Assert.That(publication.TemperatureAmounts).IsSameReferenceAs(temperatureAmounts);
    }

    [Test]
    public async Task CollectionGeneration_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() =>
            new WorldInventoryCollectionGeneration(0)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() =>
            new WorldInventoryCollectionGeneration(-1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    private static TemperatureAmountSeries CreateSeries(
        float temperatureKelvin,
        float amount)
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(temperatureKelvin, amount);
        return accumulator.BuildSeries();
    }
}
