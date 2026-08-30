using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;

[TestClass]
public sealed class WorldResourceTemperatureAmountCatalogTests
{
    private const int MixedPublicationSeed = 0xC47A109;
    private const int MixedPublicationOperationCount = 10000;

    private static readonly Tag Iron = new("Iron");
    private static readonly Tag Copper = new("Copper");
    private static readonly Tag Gold = new("Gold");
    private static readonly Tag[] StressResourceTags = [Iron, Copper, Gold];

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenCoverageContainsTagButSeriesHasNotArrived_ReturnsInventoryIncomplete()
    {
        var catalog = new WorldResourceTemperatureAmountCatalog();
        catalog.RegisterWorld(worldId: 1, parentWorldId: 1);
        var generation = new WorldInventoryCollectionGeneration(9);
        catalog.PublishWorldResourceTagCoverage(
            1,
            WorldResourceTagCoverage.Create(generation, new[] { new Tag("Iron") }));

        var availability = catalog.GetTemperatureConstrainedAmountAvailability(
            parentWorldId: 1,
            resourceTag: new Tag("Iron"),
            constraint: Constraint(250, 350),
            expectedCollectionGeneration: generation);

        Assert.AreEqual(
            TemperatureConstrainedAmountAvailabilityState.InventoryIncomplete,
            availability.State);
        Assert.IsFalse(availability.TryGetCompleteAvailableAmount(out _));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenEveryMemberHasCompleteWorldPublication_ReturnsCompleteParentAndChildSum()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, 10.0f))));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            2,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 320.0f, 20.0f))));

        AssertCompleteAmount(
            30.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenTagIsAbsentFromCompleteWorld_ReturnsCompleteKnownZeroContribution()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, 10.0f))));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            2,
            CompleteWorld(
                generation,
                ResourceAmount(Copper, 300.0f, 50.0f))));

        AssertCompleteAmount(
            10.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenEveryCoverageExcludesTag_ReturnsCompleteZero()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Copper)));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            2,
            Coverage(generation, Copper)));

        AssertCompleteAmount(
            0.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenOneMemberCoverageIsMissing_ReturnsInventoryIncomplete()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Copper)));

        AssertUnavailable(
            TemperatureConstrainedAmountAvailabilityState.InventoryIncomplete,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenCoverageContainsTagAndCurrentSeriesExists_ReturnsCompleteAmount()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));

        AssertCompleteAmount(
            10.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenOnePresentMemberSeriesIsPending_ReturnsInventoryIncompleteRatherThanZero()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            2,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));

        AssertUnavailable(
            TemperatureConstrainedAmountAvailabilityState.InventoryIncomplete,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void PublishCompleteWorldResourceAmounts_WhenWorldIsUnknown_ReturnsFalse()
    {
        var catalog = new WorldResourceTemperatureAmountCatalog();

        Assert.IsFalse(catalog.PublishCompleteWorldResourceAmounts(
            99,
            CompleteWorld(
                Generation(),
                ResourceAmount(Iron, 300.0f, 10.0f))));
    }

    [TestMethod]
    public void PublishWorldResourceTagCoverage_WhenGenerationIsOlder_RejectsLatePublication()
    {
        var catalog = CatalogWithWorlds((1, 1));
        var currentGeneration = new WorldInventoryCollectionGeneration(10);
        var olderGeneration = new WorldInventoryCollectionGeneration(9);
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(currentGeneration, Iron)));

        Assert.IsFalse(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(olderGeneration, Copper)));
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageCurrent,
            catalog.GetWorldResourceTagCoverageRequirementState(
                1,
                currentGeneration));
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState
                .UnknownWorldOrCollectionGeneration,
            catalog.GetWorldResourceTagCoverageRequirementState(
                1,
                olderGeneration));
    }

    [TestMethod]
    public void PublishWorldResourceTemperatureSeries_WhenNoCurrentCoverageOrCompletePublicationExists_ReturnsFalse()
    {
        var catalog = CatalogWithWorlds((1, 1));

        Assert.IsFalse(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(Generation(), Iron, 300.0f, 10.0f)));
    }

    [TestMethod]
    public void PublishWorldResourceTagCoverage_AfterCompletePublicationForSameGeneration_RejectsSemanticDowngrade()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, 10.0f))));

        Assert.IsFalse(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Copper)));
        AssertCompleteAmount(
            10.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void PublishCompleteWorldResourceAmounts_AfterCoveragePublication_UpgradesToCompleteWorldState()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));

        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, 10.0f))));
        AssertCompleteAmount(
            10.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void GetWorldResourceTagCoverageRequirementState_WhenWorldOrGenerationIsUnknown_ReturnsUnknownWorldOrCollectionGeneration()
    {
        var catalog = CatalogWithWorlds((1, 1));
        var currentGeneration = new WorldInventoryCollectionGeneration(10);
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(currentGeneration, Iron)));

        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState
                .UnknownWorldOrCollectionGeneration,
            catalog.GetWorldResourceTagCoverageRequirementState(
                99,
                currentGeneration));
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState
                .UnknownWorldOrCollectionGeneration,
            catalog.GetWorldResourceTagCoverageRequirementState(
                1,
                new WorldInventoryCollectionGeneration(9)));
    }

    [TestMethod]
    public void GetWorldResourceTagCoverageRequirementState_WhenGenerationHasNoCoverage_ReturnsCoverageRequired()
    {
        var catalog = CatalogWithWorlds((1, 1));

        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageRequired,
            catalog.GetWorldResourceTagCoverageRequirementState(
                1,
                Generation()));
    }

    [TestMethod]
    public void GetWorldResourceTagCoverageRequirementState_WhenCoverageOrCompletePublicationIsCurrent_ReturnsCoverageCurrent()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            2,
            CompleteWorld(generation)));

        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageCurrent,
            catalog.GetWorldResourceTagCoverageRequirementState(1, generation));
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageCurrent,
            catalog.GetWorldResourceTagCoverageRequirementState(2, generation));
    }

    [TestMethod]
    public void PublishWorldResourceTemperatureSeries_WhenSameTagRepeats_ReplacesOnlyThatWorldTagContribution()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            2,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            2,
            SeriesPublication(generation, Iron, 300.0f, 20.0f)));

        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 40.0f)));

        AssertCompleteAmount(
            60.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void PublishWorldResourceTemperatureSeries_WhenTagWasAbsentFromCoverage_AddsPresentCurrentTagAtomically()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Copper)));

        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));

        AssertCompleteAmount(
            10.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void PublishWorldResourceTemperatureSeries_WhenDifferentTagChanges_DoesNotRebuildUnaffectedParentTag()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron, Copper)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Copper, 300.0f, 20.0f)));
        var ironAggregateBeforeCopperReplacement =
            ReadAggregateReference(catalog, parentWorldId: 1, Iron);

        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Copper, 300.0f, 40.0f)));

        Assert.AreSame(
            ironAggregateBeforeCopperReplacement,
            ReadAggregateReference(catalog, parentWorldId: 1, Iron));
        AssertCompleteAmount(
            10.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void PublishWorldResourceTagCoverage_WhenSameGenerationSetChanges_RecomputesOnlyChangedTagCompleteness()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron, Copper)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));
        var ironAggregateBeforeCoverageReplacement =
            ReadAggregateReference(catalog, parentWorldId: 1, Iron);
        AssertUnavailable(
            TemperatureConstrainedAmountAvailabilityState.InventoryIncomplete,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Copper,
                Constraint(250, 350),
                generation));

        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));

        Assert.AreSame(
            ironAggregateBeforeCoverageReplacement,
            ReadAggregateReference(catalog, parentWorldId: 1, Iron));
        AssertCompleteAmount(
            0.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Copper,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void RegisterWorld_WhenWorldMovesParent_InvalidatesOldAndNewParentMembershipVersions()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 2), (3, 1));
        PublishIronCompleteWorld(catalog, 1, generation, 10.0f);
        PublishIronCompleteWorld(catalog, 2, generation, 20.0f);
        PublishIronCompleteWorld(catalog, 3, generation, 5.0f);
        var oldParentAggregateBeforeMove =
            ReadAggregateReference(catalog, parentWorldId: 1, Iron);
        var newParentAggregateBeforeMove =
            ReadAggregateReference(catalog, parentWorldId: 2, Iron);

        catalog.RegisterWorld(worldId: 1, parentWorldId: 2);

        AssertCompleteAmount(
            5.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
        AssertCompleteAmount(
            30.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                2,
                Iron,
                Constraint(250, 350),
                generation));
        Assert.AreNotSame(
            oldParentAggregateBeforeMove,
            ReadAggregateReference(catalog, parentWorldId: 1, Iron));
        Assert.AreNotSame(
            newParentAggregateBeforeMove,
            ReadAggregateReference(catalog, parentWorldId: 2, Iron));
    }

    [TestMethod]
    public void RemoveWorld_WhenKnown_RemovesItsContributionAndRecomputesAffectedAggregates()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1), (2, 1));
        PublishIronCompleteWorld(catalog, 1, generation, 10.0f);
        PublishIronCompleteWorld(catalog, 2, generation, 20.0f);

        catalog.RemoveWorld(1);

        AssertCompleteAmount(
            20.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
    }

    [TestMethod]
    public void RemoveWorld_WhenLatePublicationArrives_RejectsIt()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        catalog.RemoveWorld(1);

        Assert.IsFalse(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, 10.0f))));
        Assert.IsFalse(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));
        Assert.IsFalse(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 10.0f)));
    }

    [TestMethod]
    public void ClearForGameSession_WhenCalledTwice_IsIdempotent()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        PublishIronCompleteWorld(catalog, 1, generation, 10.0f);

        catalog.ClearForGameSession();
        catalog.ClearForGameSession();

        Assert.IsFalse(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, 20.0f))));
        AssertUnavailable(
            TemperatureConstrainedAmountAvailabilityState.InventoryIncomplete,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(250, 350),
                generation));
        Assert.IsEmpty(ReadAggregateMap(catalog));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenConstraintIsEmpty_ReturnsCompleteZeroWithoutSeriesSearch()
    {
        var catalog = CatalogWithWorlds((1, 1));

        AssertCompleteAmount(
            0.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(300, 300),
                Generation()));
        Assert.IsEmpty(ReadAggregateMap(catalog));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenConstraintIsEmptyAndWorldIsUnknown_ReturnsCompleteZeroWithoutCatalogLookup()
    {
        var catalog = new WorldResourceTemperatureAmountCatalog();

        AssertCompleteAmount(
            0.0f,
            catalog.GetTemperatureConstrainedAmountAvailability(
                parentWorldId: 991,
                resourceTag: Iron,
                constraint: Constraint(300, 300),
                expectedCollectionGeneration: Generation()));
        Assert.IsEmpty(ReadAggregateMap(catalog));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenConstraintIsDisabled_ReturnsTemperatureConstraintDisabled()
    {
        var catalog = new WorldResourceTemperatureAmountCatalog();

        AssertUnavailable(
            TemperatureConstrainedAmountAvailabilityState
                .TemperatureConstraintDisabled,
            catalog.GetTemperatureConstrainedAmountAvailability(
                1,
                Iron,
                Constraint(300, 0),
                Generation()));
    }

    [TestMethod]
    public void GetTemperatureConstrainedAmountAvailability_WhenSingleTagPublicationIsConcurrent_ReturnsWholeOldOrNewAggregate()
    {
        var generation = Generation();
        var catalog = CatalogWithWorlds((1, 1));
        Assert.IsTrue(catalog.PublishWorldResourceTagCoverage(
            1,
            Coverage(generation, Iron)));
        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            SeriesPublication(generation, Iron, 300.0f, 1.0f)));
        using var start = new ManualResetEventSlim();
        var publisher = Task.Run(() =>
        {
            start.Wait();
            for (var publicationIndex = 0;
                 publicationIndex < 2000;
                 publicationIndex++)
            {
                var amount = publicationIndex % 2 == 0 ? 2.0f : 3.0f;
                Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
                    1,
                    SeriesPublication(
                        generation,
                        Iron,
                        300.0f,
                        amount)));
            }
        });

        start.Set();
        while (!publisher.IsCompleted)
        {
            var availability =
                catalog.GetTemperatureConstrainedAmountAvailability(
                    1,
                    Iron,
                    Constraint(250, 350),
                    generation);
            Assert.AreEqual(
                TemperatureConstrainedAmountAvailabilityState.Complete,
                availability.State);
            Assert.IsTrue(
                availability.TryGetCompleteAvailableAmount(out var amount));
            Assert.IsTrue(
                amount == 1.0f || amount == 2.0f || amount == 3.0f,
                $"Observed torn aggregate amount {amount}.");
        }

        publisher.GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MixedPublications_WhenTenThousandSeededOperationsRun_MatchIndependentThreeProofReferenceModel()
    {
        var random = new Random(MixedPublicationSeed);
        var generation = Generation();
        var catalog = new WorldResourceTemperatureAmountCatalog();
        var referenceWorlds = new Dictionary<int, ReferenceWorldState>();

        for (var operationIndex = 0;
             operationIndex < MixedPublicationOperationCount;
             operationIndex++)
        {
            ApplyRandomOperation(
                random,
                catalog,
                referenceWorlds,
                generation,
                operationIndex);

            var parentWorldId = random.Next(1, 4);
            var resourceTag = StressResourceTags[
                random.Next(StressResourceTags.Length)];
            var expected = ReferenceAvailability(
                referenceWorlds,
                parentWorldId,
                resourceTag);
            var observed = catalog.GetTemperatureConstrainedAmountAvailability(
                parentWorldId,
                resourceTag,
                Constraint(250, 350),
                generation);

            Assert.AreEqual(
                expected.State,
                observed.State,
                StressMessage(operationIndex, parentWorldId, resourceTag));
            if (expected.State ==
                TemperatureConstrainedAmountAvailabilityState.Complete)
            {
                Assert.IsTrue(
                    expected.TryGetCompleteAvailableAmount(
                        out var expectedAmount));
                Assert.IsTrue(
                    observed.TryGetCompleteAvailableAmount(
                        out var observedAmount));
                Assert.AreEqual(
                    expectedAmount,
                    observedAmount,
                    StressMessage(operationIndex, parentWorldId, resourceTag));
            }
            else
            {
                Assert.IsFalse(observed.TryGetCompleteAvailableAmount(out _));
            }
        }
    }

    [TestMethod]
    public void ProductionSource_WhenSingleTagAndQueryPathsAreInspected_UseOnlyNamedSparsePrimitives()
    {
        var source = File.ReadAllText(ResolveCatalogSourcePath());
        var singleTagPublicationSource = ExtractMethodRegion(
            source,
            "internal bool PublishWorldResourceTemperatureSeries(",
            "internal WorldResourceTagCoverageRequirementState");
        Assert.IsTrue(singleTagPublicationSource.Contains(
            "RebuildOneParentResourceTagAggregate(",
            StringComparison.Ordinal));
        Assert.IsFalse(singleTagPublicationSource.Contains(
            "RebuildAffectedParentResourceTagAggregates(",
            StringComparison.Ordinal));
        Assert.IsFalse(singleTagPublicationSource.Contains(
            "WorldContainer",
            StringComparison.Ordinal));
        Assert.IsFalse(singleTagPublicationSource.Contains(
            "TemperatureDecisionBucket.BucketCount",
            StringComparison.Ordinal));

        var querySource = ExtractMethodRegion(
            source,
            "internal TemperatureConstrainedAmountAvailability GetTemperatureConstrainedAmountAvailability(",
            "internal void RemoveWorld(");
        Assert.IsFalse(querySource.Contains("WorldContainer", StringComparison.Ordinal));
        Assert.IsFalse(querySource.Contains(
            "TemperatureDecisionBucket.BucketCount",
            StringComparison.Ordinal));
        Assert.IsFalse(querySource.Contains("foreach (", StringComparison.Ordinal));
        Assert.IsFalse(querySource.Contains("for (", StringComparison.Ordinal));
    }

    private static void ApplyRandomOperation(
        Random random,
        WorldResourceTemperatureAmountCatalog catalog,
        Dictionary<int, ReferenceWorldState> referenceWorlds,
        WorldInventoryCollectionGeneration generation,
        int operationIndex)
    {
        var worldId = random.Next(1, 9);
        switch (operationIndex % 8)
        {
            case 0:
            case 7:
            {
                var parentWorldId = random.Next(1, 4);
                catalog.RegisterWorld(worldId, parentWorldId);
                if (!referenceWorlds.TryGetValue(worldId, out var world))
                {
                    world = new ReferenceWorldState();
                    referenceWorlds.Add(worldId, world);
                }

                world.ParentWorldId = parentWorldId;
                break;
            }

            case 1:
            {
                var resourceAmounts = RandomResourceAmounts(random);
                var accepted = catalog.PublishCompleteWorldResourceAmounts(
                    worldId,
                    CompleteWorld(generation, resourceAmounts));
                var expectedAccepted = referenceWorlds.TryGetValue(
                    worldId,
                    out var world);
                Assert.AreEqual(expectedAccepted, accepted);
                if (expectedAccepted)
                {
                    world!.PublicationStrength =
                        ReferencePublicationStrength.CompleteWorld;
                    world.PresentResourceTags.Clear();
                    world.AmountByResourceTag.Clear();
                    foreach (var resourceAmount in resourceAmounts)
                    {
                        world.PresentResourceTags.Add(resourceAmount.ResourceTag);
                        world.AmountByResourceTag[resourceAmount.ResourceTag] =
                            resourceAmount.Amount;
                    }
                }

                break;
            }

            case 2:
            case 5:
            {
                var presentResourceTags = RandomResourceTagSet(random);
                var accepted = catalog.PublishWorldResourceTagCoverage(
                    worldId,
                    WorldResourceTagCoverage.Create(
                        generation,
                        presentResourceTags));
                var expectedAccepted = referenceWorlds.TryGetValue(
                    worldId,
                    out var world) &&
                    world.PublicationStrength !=
                        ReferencePublicationStrength.CompleteWorld;
                Assert.AreEqual(expectedAccepted, accepted);
                if (expectedAccepted)
                {
                    world!.PublicationStrength =
                        ReferencePublicationStrength.TagCoverage;
                    world.PresentResourceTags.IntersectWith(
                        presentResourceTags);
                    world.PresentResourceTags.UnionWith(
                        presentResourceTags);
                    var removedTags = world.AmountByResourceTag.Keys
                        .Where(resourceTag =>
                            !world.PresentResourceTags.Contains(resourceTag))
                        .ToArray();
                    foreach (var removedTag in removedTags)
                    {
                        world.AmountByResourceTag.Remove(removedTag);
                    }
                }

                break;
            }

            case 3:
            case 4:
            {
                var resourceTag = operationIndex % 8 == 4 &&
                    referenceWorlds.TryGetValue(worldId, out var observedWorld)
                    ? FirstAbsentOrRandomTag(random, observedWorld)
                    : StressResourceTags[random.Next(StressResourceTags.Length)];
                var amount = random.Next(0, 21);
                var accepted = catalog.PublishWorldResourceTemperatureSeries(
                    worldId,
                    SeriesPublication(
                        generation,
                        resourceTag,
                        300.0f,
                        amount));
                var expectedAccepted = referenceWorlds.TryGetValue(
                    worldId,
                    out var world) &&
                    world.PublicationStrength !=
                        ReferencePublicationStrength.NoCoverage;
                Assert.AreEqual(expectedAccepted, accepted);
                if (expectedAccepted)
                {
                    world!.PresentResourceTags.Add(resourceTag);
                    world.AmountByResourceTag[resourceTag] = amount;
                }

                break;
            }

            case 6:
                catalog.RemoveWorld(worldId);
                referenceWorlds.Remove(worldId);
                break;
        }
    }

    private static TemperatureConstrainedAmountAvailability ReferenceAvailability(
        IReadOnlyDictionary<int, ReferenceWorldState> referenceWorlds,
        int parentWorldId,
        Tag resourceTag)
    {
        var memberWorlds = referenceWorlds.Values
            .Where(world => world.ParentWorldId == parentWorldId)
            .ToArray();
        if (memberWorlds.Length == 0)
        {
            return TemperatureConstrainedAmountAvailability.InventoryIncomplete();
        }

        var completeAmount = 0.0f;
        foreach (var memberWorld in memberWorlds)
        {
            if (memberWorld.PublicationStrength ==
                ReferencePublicationStrength.NoCoverage)
            {
                return TemperatureConstrainedAmountAvailability
                    .InventoryIncomplete();
            }

            if (!memberWorld.PresentResourceTags.Contains(resourceTag))
            {
                continue;
            }

            if (!memberWorld.AmountByResourceTag.TryGetValue(
                resourceTag,
                out var memberAmount))
            {
                return TemperatureConstrainedAmountAvailability
                    .InventoryIncomplete();
            }

            completeAmount += memberAmount;
        }

        return TemperatureConstrainedAmountAvailability.Complete(completeAmount);
    }

    private static ResourceTagTemperatureAmount[] RandomResourceAmounts(
        Random random)
    {
        var resourceAmounts = new List<ResourceTagTemperatureAmount>();
        foreach (var resourceTag in StressResourceTags)
        {
            if (random.Next(2) == 0)
            {
                resourceAmounts.Add(ResourceAmount(
                    resourceTag,
                    300.0f,
                    random.Next(0, 21)));
            }
        }

        return resourceAmounts.ToArray();
    }

    private static Tag[] RandomResourceTagSet(Random random) =>
        StressResourceTags
            .Where(_ => random.Next(2) == 0)
            .ToArray();

    private static Tag FirstAbsentOrRandomTag(
        Random random,
        ReferenceWorldState world)
    {
        foreach (var resourceTag in StressResourceTags)
        {
            if (!world.PresentResourceTags.Contains(resourceTag))
            {
                return resourceTag;
            }
        }

        return StressResourceTags[random.Next(StressResourceTags.Length)];
    }

    private static string StressMessage(
        int operationIndex,
        int parentWorldId,
        Tag resourceTag) =>
        $"Seed=0x{MixedPublicationSeed:X}; operation={operationIndex}; " +
        $"parent={parentWorldId}; tagHash={resourceTag.GetHashCode()}.";

    private static WorldResourceTemperatureAmountCatalog CatalogWithWorlds(
        params (int WorldId, int ParentWorldId)[] registrations)
    {
        var catalog = new WorldResourceTemperatureAmountCatalog();
        foreach (var registration in registrations)
        {
            catalog.RegisterWorld(
                registration.WorldId,
                registration.ParentWorldId);
        }

        return catalog;
    }

    private static void PublishIronCompleteWorld(
        WorldResourceTemperatureAmountCatalog catalog,
        int worldId,
        WorldInventoryCollectionGeneration generation,
        float amount)
    {
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            worldId,
            CompleteWorld(
                generation,
                ResourceAmount(Iron, 300.0f, amount))));
    }

    private static CompleteWorldResourceTemperatureAmounts CompleteWorld(
        WorldInventoryCollectionGeneration generation,
        params ResourceTagTemperatureAmount[] resourceAmounts)
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(generation);
        foreach (var resourceAmount in resourceAmounts)
        {
            builder.BeginResourceTag(resourceAmount.ResourceTag);
            builder.AddTemperatureAmount(
                resourceAmount.TemperatureKelvin,
                resourceAmount.Amount);
            builder.CompleteResourceTag();
        }

        return builder.Build();
    }

    private static WorldResourceTagCoverage Coverage(
        WorldInventoryCollectionGeneration generation,
        params Tag[] presentResourceTags) =>
        WorldResourceTagCoverage.Create(generation, presentResourceTags);

    private static WorldResourceTemperatureSeriesPublication SeriesPublication(
        WorldInventoryCollectionGeneration generation,
        Tag resourceTag,
        float temperatureKelvin,
        float amount) =>
        new WorldResourceTemperatureSeriesPublication(
            generation,
            resourceTag,
            Series(temperatureKelvin, amount));

    private static TemperatureAmountSeries Series(
        float temperatureKelvin,
        float amount)
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(temperatureKelvin, amount);
        return accumulator.BuildSeries();
    }

    private static ResourceTagTemperatureAmount ResourceAmount(
        Tag resourceTag,
        float temperatureKelvin,
        float amount) =>
        new(resourceTag, temperatureKelvin, amount);

    private static WorldInventoryCollectionGeneration Generation() => new(9);

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static void AssertCompleteAmount(
        float expectedAmount,
        TemperatureConstrainedAmountAvailability availability)
    {
        Assert.AreEqual(
            TemperatureConstrainedAmountAvailabilityState.Complete,
            availability.State);
        Assert.IsTrue(
            availability.TryGetCompleteAvailableAmount(out var observedAmount));
        Assert.AreEqual(expectedAmount, observedAmount);
    }

    private static void AssertUnavailable(
        TemperatureConstrainedAmountAvailabilityState expectedState,
        TemperatureConstrainedAmountAvailability availability)
    {
        Assert.AreNotEqual(
            TemperatureConstrainedAmountAvailabilityState.Complete,
            expectedState);
        Assert.AreEqual(expectedState, availability.State);
        Assert.IsFalse(availability.TryGetCompleteAvailableAmount(out _));
    }

    private static object ReadAggregateReference(
        WorldResourceTemperatureAmountCatalog catalog,
        int parentWorldId,
        Tag resourceTag)
    {
        foreach (DictionaryEntry entry in ReadAggregateMap(catalog))
        {
            Assert.IsNotNull(entry.Key);
            var key = entry.Key;
            var keyType = key.GetType();
            var parentWorldIdField = keyType.GetField(
                "parentWorldId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resourceTagField = keyType.GetField(
                "resourceTag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(parentWorldIdField);
            Assert.IsNotNull(resourceTagField);
            if (Assert.IsInstanceOfType<int>(
                    parentWorldIdField.GetValue(key)) == parentWorldId &&
                Assert.IsInstanceOfType<Tag>(
                    resourceTagField.GetValue(key)).Equals(resourceTag))
            {
                Assert.IsNotNull(entry.Value);
                return entry.Value;
            }
        }

        Assert.Fail(
            $"No aggregate exists for parent {parentWorldId} and tag hash " +
            $"{resourceTag.GetHashCode()}.");
        return new object();
    }

    private static IDictionary ReadAggregateMap(
        WorldResourceTemperatureAmountCatalog catalog)
    {
        var field = typeof(WorldResourceTemperatureAmountCatalog).GetField(
            "aggregatesByParentWorldAndResourceTag",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            "The optimized aggregate-reuse contract requires the exact private " +
            "field aggregatesByParentWorldAndResourceTag.");
        return Assert.IsInstanceOfType<IDictionary>(field.GetValue(catalog));
    }

    private static string ResolveCatalogSourcePath()
    {
        var repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return Path.Combine(
                repositoryRoot,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source",
                "WorldResourceTemperatureAmounts",
                "WorldResourceTemperatureAmountCatalog.cs");
        }

        var candidateDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            var candidatePath = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source",
                "WorldResourceTemperatureAmounts",
                "WorldResourceTemperatureAmountCatalog.cs");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        Assert.Fail("Could not locate WorldResourceTemperatureAmountCatalog.cs.");
        return string.Empty;
    }

    private static string ExtractMethodRegion(
        string source,
        string startMarker,
        string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsTrue(startIndex >= 0);
        var endIndex = source.IndexOf(
            endMarker,
            startIndex + startMarker.Length,
            StringComparison.Ordinal);
        Assert.IsTrue(endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private readonly record struct ResourceTagTemperatureAmount(
        Tag ResourceTag,
        float TemperatureKelvin,
        float Amount);

    private enum ReferencePublicationStrength
    {
        NoCoverage,
        TagCoverage,
        CompleteWorld
    }

    private sealed class ReferenceWorldState
    {
        internal int ParentWorldId { get; set; }

        internal ReferencePublicationStrength PublicationStrength { get; set; }

        internal HashSet<Tag> PresentResourceTags { get; } = [];

        internal Dictionary<Tag, float> AmountByResourceTag { get; } = [];
    }
}
