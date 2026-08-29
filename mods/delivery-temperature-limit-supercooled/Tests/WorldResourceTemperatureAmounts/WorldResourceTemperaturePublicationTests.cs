namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;

[TestClass]
public sealed class WorldResourceTemperaturePublicationTests
{
    [TestMethod]
    public void CreateCoverage_WhenInputContainsDuplicateTags_PublishesFirstSeenUniquePresentTags()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            new[] { iron, copper, iron, copper });

        Assert.AreSequenceEqual(
            new[] { iron, copper },
            coverage.PresentResourceTags);
    }

    [TestMethod]
    public void CreateCoverage_WhenSourceCollectionMutates_PublishedCoverageDoesNotChange()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var sourcePresentResourceTags = new List<Tag> { iron };
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            sourcePresentResourceTags);

        sourcePresentResourceTags[0] = copper;
        sourcePresentResourceTags.Add(copper);

        Assert.AreSequenceEqual(
            new[] { iron },
            coverage.PresentResourceTags);
        Assert.IsTrue(coverage.Contains(iron));
        Assert.IsFalse(coverage.Contains(copper));
        Assert.IsFalse(coverage.PresentResourceTags is Tag[]);
        Assert.IsFalse(
            coverage.PresentResourceTags is ICollection<Tag> mutableTags &&
            !mutableTags.IsReadOnly);
    }

    [TestMethod]
    public void Contains_WhenTagWasPresent_ReturnsTrue()
    {
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            new[] { new Tag("Iron"), new Tag("Copper") });

        Assert.IsTrue(coverage.Contains(new Tag("Iron")));
    }

    [TestMethod]
    public void Contains_WhenTagWasAbsent_ReturnsFalse()
    {
        var coverage = WorldResourceTagCoverage.Create(
            new WorldInventoryCollectionGeneration(4),
            new[] { new Tag("Iron") });

        Assert.IsFalse(coverage.Contains(new Tag("Copper")));
    }

    [TestMethod]
    public void CreateCoverage_WhenPresentTagsIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            WorldResourceTagCoverage.Create(
                new WorldInventoryCollectionGeneration(4),
                null!));
    }

    [TestMethod]
    public void CreateSeriesPublication_WhenSeriesIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new WorldResourceTemperatureSeriesPublication(
                new WorldInventoryCollectionGeneration(4),
                new Tag("Iron"),
                null!));
    }

    [TestMethod]
    public void CreateSeriesPublication_WhenConstructed_PreservesGenerationTagAndSeriesIdentity()
    {
        var collectionGeneration = new WorldInventoryCollectionGeneration(4);
        var resourceTag = new Tag("Iron");
        var temperatureAmounts = CreateSeries(300.0f, 10.0f);

        var publication = new WorldResourceTemperatureSeriesPublication(
            collectionGeneration,
            resourceTag,
            temperatureAmounts);

        Assert.AreEqual(
            collectionGeneration,
            publication.CollectionGeneration);
        Assert.AreEqual(resourceTag, publication.ResourceTag);
        Assert.AreSame(temperatureAmounts, publication.TemperatureAmounts);
    }

    [TestMethod]
    public void CollectionGeneration_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new WorldInventoryCollectionGeneration(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new WorldInventoryCollectionGeneration(-1));
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
