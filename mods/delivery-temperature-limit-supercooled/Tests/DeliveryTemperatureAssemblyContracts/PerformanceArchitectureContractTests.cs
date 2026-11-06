#nullable enable

using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class PerformanceArchitectureContractTests
{
    private enum BackEdgePolicy
    {
        None,
        OccupiedItemsOnly,
        BinarySearchOnly,
        NotApplicable
    }

    private sealed record MethodPerformanceContract(
        string ContractId,
        string DeclaringTypeName,
        string MethodName,
        IReadOnlyList<string> ExactParameterTypeNames,
        string RelativeSourcePath,
        string DeclarationMarker,
        IReadOnlyList<string> PermittedDirectCallNames,
        IReadOnlyList<string> ForbiddenDirectCallOrFieldNames,
        BackEdgePolicy BackEdgePolicy,
        Type? LinkedDeclaringType = null,
        Type[]? LinkedParameterTypes = null);

    // Declare each inspected method once. Individual tests select an ID from this
    // table, so parameter identity and the permitted/forbidden edge policy cannot
    // silently diverge between assertions.
    private static readonly MethodPerformanceContract[] MethodContracts =
    [
        new(
            "constraint-read",
            "DeliveryTemperatureLimit.TemperatureConstraintRegistry",
            "CaptureSnapshot",
            [],
            "TemperatureConstraints/TemperatureConstraintRegistry.cs",
            "internal ActiveTemperatureConstraintSnapshot CaptureSnapshot()",
            ["System.Threading.Volatile.Read"],
            [
                "System.Linq.Enumerable.Sort",
                "System.Linq.Enumerable.Distinct",
                "DeliveryTemperatureLimit.TemperatureConstraintRegistry.CreateSortedDecisionEndpointView"
            ],
            BackEdgePolicy.None,
            typeof(TemperatureConstraintRegistry),
            Type.EmptyTypes),
        new(
            "status-query",
            "DeliveryTemperatureLimit.TemperatureStatusAvailabilityPatches",
            "ReplaceFetchableAmountWhenInventoryIsComplete",
            ["System.Single", "FetchList2", "System.Int32", "Tag", "System.Single&", "System.Single", "System.Single"],
            "KleiImplementationAdapters/TemperatureStatusAvailabilityPatches.cs",
            "private static void ReplaceFetchableAmountWhenInventoryIsComplete(",
            [
                "TemperatureStatusAvailabilityDecision.ShouldTryReplacement",
                "DeliveryTemperatureGameSessionHost.TryCaptureCurrent",
                "TemperatureLimitComponentIndex.TryGetConstraint",
                "WorldParentTopologySnapshot.TryResolveParentWorld",
                "WorldResourceTemperatureAmountCatalog.GetTemperatureConstrainedAmountAvailability",
                "TemperatureStatusAvailabilityDecision.TryCalculateReplacementFetchable"
            ],
            ["ClusterManager", "WorldContainers", "GetWorld", "GetComponent"],
            BackEdgePolicy.None),
        new(
            "klei-inventory-pickup",
            "DeliveryTemperatureLimit.KleiWorldInventoryTemperaturePatches",
            "RecordFilteredPickupTemperatureAmount",
            ["Pickupable", "System.Single"],
            "KleiImplementationAdapters/KleiWorldInventoryTemperaturePatches.cs",
            "private static float RecordFilteredPickupTemperatureAmount(",
            ["CompleteWorldResourceTemperatureAmountsBuilder.AddTemperatureAmount"],
            ["GetEnumerator", "ToArray", "ToList", "TemperatureDecisionBucket.BucketCount"],
            BackEdgePolicy.None),
        new(
            "direct-eligibility",
            "DeliveryTemperatureLimit.KleiDirectDeliveryEligibilityPatches",
            "IsPickupAllowedForDestination",
            ["Pickupable", "Storage"],
            "KleiImplementationAdapters/KleiDirectDeliveryEligibilityPatches.cs",
            "private static bool IsPickupAllowedForDestination(",
            [
                "DeliveryTemperatureGameSessionHost.TryCaptureCurrent",
                "TemperatureLimitComponentIndex.TryGetConstraint",
                "DeliveryTemperatureConstraint.Allows"
            ],
            [
                "System.Reflection",
                "Activator",
                "CaptureSnapshot",
                "CreateSortedDecisionEndpointView",
                "new Dictionary",
                "new List",
                "new HashSet"
            ],
            BackEdgePolicy.None),
        new(
            "pickup-comparator",
            "DeliveryTemperatureLimit.KleiPickupTemperatureGroupingPatches",
            "CompareTemperatureEligibilityClasses",
            ["FetchManager.Pickup", "FetchManager.Pickup"],
            "KleiImplementationAdapters/KleiPickupTemperatureGroupingPatches.cs",
            "private static int CompareTemperatureEligibilityClasses(",
            [
                "ThreadConfinedSessionSlot.TryGetCurrent",
                "KleiPickupTemperatureGroupingPatches.GetTemperatureEligibilityClassKey",
                "TemperatureEligibilityClassKey.CompareTo"
            ],
            [
                "CaptureSnapshot",
                "new Dictionary",
                "new List",
                "new HashSet",
                "new TemperaturePartitionDefinition"
            ],
            BackEdgePolicy.None),
        new(
            "pickup-retention",
            "DeliveryTemperatureLimit.PickupTemperatureGroupingSession",
            "CompleteOrDiscard",
            [],
            "FetchTemperatureEligibility/PickupTemperatureGroupingSession.cs",
            "private void CompleteOrDiscard()",
            [],
            [],
            BackEdgePolicy.None,
            typeof(PickupTemperatureGroupingSession),
            Type.EmptyTypes),
        new(
            "fasttrack-key-retention",
            "DeliveryTemperatureLimit.FastTrackPickupGroupingKeyAllocator",
            "CompleteOrDiscard",
            [],
            "FastTrackCompatibility/PickupGroupingAdapters/FastTrackPickupGroupingKeyAllocator.cs",
            "private void CompleteOrDiscard()",
            [],
            [],
            BackEdgePolicy.None,
            typeof(FastTrackPickupGroupingKeyAllocator),
            Type.EmptyTypes),
        new(
            "fetch-retention",
            "DeliveryTemperatureLimit.FetchTemperatureEligibilityBuilder",
            "CompleteOrDiscardBuild",
            ["System.Int32"],
            "FetchTemperatureEligibility/FetchTemperatureEligibilityBuilder.cs",
            "private void CompleteOrDiscardBuild(int priorEntryCount)",
            [],
            [],
            BackEdgePolicy.None,
            typeof(FetchTemperatureEligibilityBuilder),
            [typeof(int)]),
        new(
            "world-tag-retention",
            "DeliveryTemperatureLimit.CompleteWorldResourceTemperatureAmountsBuilder",
            "ReleaseCandidateMap",
            [],
            "WorldResourceTemperatureAmounts/CompleteWorldResourceTemperatureAmountsBuilder.cs",
            "private void ReleaseCandidateMap()",
            [],
            [],
            BackEdgePolicy.None,
            typeof(CompleteWorldResourceTemperatureAmountsBuilder),
            Type.EmptyTypes),
        new(
            "constraint-decision",
            "DeliveryTemperatureLimit.DeliveryTemperatureConstraint",
            "Allows",
            ["System.Single"],
            "TemperatureConstraints/DeliveryTemperatureConstraint.cs",
            "internal bool Allows(float temperatureKelvin)",
            [],
            ["TemperatureDecisionBucket.BucketCount"],
            BackEdgePolicy.None,
            typeof(DeliveryTemperatureConstraint),
            [typeof(float)]),
        new(
            "amount-add",
            "DeliveryTemperatureLimit.TemperatureAmountAccumulator",
            "AddTemperatureAmount",
            ["System.Single", "System.Single"],
            "WorldResourceTemperatureAmounts/TemperatureAmountAccumulator.cs",
            "internal void AddTemperatureAmount(",
            [],
            ["TemperatureDecisionBucket.BucketCount"],
            BackEdgePolicy.None,
            typeof(TemperatureAmountAccumulator),
            [typeof(float), typeof(float)]),
        new(
            "amount-query",
            "DeliveryTemperatureLimit.TemperatureAmountSeries",
            "GetAmountAllowedBy",
            ["DeliveryTemperatureLimit.DeliveryTemperatureConstraint"],
            "WorldResourceTemperatureAmounts/TemperatureAmountSeries.cs",
            "internal float GetAmountAllowedBy(",
            [],
            ["TemperatureDecisionBucket.BucketCount"],
            BackEdgePolicy.None,
            typeof(TemperatureAmountSeries),
            [typeof(DeliveryTemperatureConstraint)]),
        new(
            "amount-build",
            "DeliveryTemperatureLimit.TemperatureAmountAccumulator",
            "BuildSeries",
            [],
            "WorldResourceTemperatureAmounts/TemperatureAmountAccumulator.cs",
            "internal TemperatureAmountSeries BuildSeries()",
            [],
            ["TemperatureDecisionBucket.BucketCount"],
            BackEdgePolicy.OccupiedItemsOnly),
        new(
            "partition-classification",
            "DeliveryTemperatureLimit.TemperaturePartitionDefinition",
            "Classify",
            ["DeliveryTemperatureLimit.TemperatureDecisionBucket"],
            "FetchTemperatureEligibility/TemperaturePartitionDefinition.cs",
            "internal int Classify(TemperatureDecisionBucket bucket)",
            [],
            ["TemperatureDecisionBucket.BucketCount"],
            BackEdgePolicy.BinarySearchOnly)
    ];

    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(operation => operation.Value);

    [Test]
    public async Task MethodContracts_WhenInspected_DeclareUniqueExactPerformanceSubjects()
    {
        await Assert.That(MethodContracts
                .Select(contract => contract.ContractId)
                .Distinct(StringComparer.Ordinal)).Count().IsEqualTo(MethodContracts.Length);
        foreach (MethodPerformanceContract contract in MethodContracts)
        {
            await Assert.That(string.IsNullOrWhiteSpace(contract.DeclaringTypeName)).IsFalse();
            await Assert.That(string.IsNullOrWhiteSpace(contract.MethodName)).IsFalse();
            await Assert.That(string.IsNullOrWhiteSpace(contract.RelativeSourcePath)).IsFalse();
            await Assert.That(string.IsNullOrWhiteSpace(contract.DeclarationMarker)).IsFalse();
        }
    }

    [Test]
    public async Task ConstraintReadPath_WhenInspected_DoesNotCallSortDistinctOrRegistryRebuild()
    {
        MethodPerformanceContract contract = await RequireContract("constraint-read");
        MethodInfo method = await RequireLinkedMethod(contract);
        IReadOnlyList<string> directCalls = ReadDirectCallNames(method);

        await Assert.That(directCalls.Distinct(StringComparer.Ordinal).ToArray()).IsEquivalentTo(contract.PermittedDirectCallNames, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because($"Unexpected direct call from {FormatSubject(contract)}.");
        await AssertContainsNoForbiddenReferences(contract, directCalls);
        await Assert.That(ReadBackEdgeTargets(method)).IsEmpty();
    }

    [Test]
    public async Task StatusQueryPath_WhenInspected_DoesNotReferenceClusterManagerOrWorldContainers()
    {
        MethodPerformanceContract contract = await RequireContract("status-query");
        string methodBody = await ReadAndValidateSourceMethod(contract);

        await AssertContainsEveryPermittedSourceCall(contract, methodBody);
        await AssertContainsNoForbiddenReferences(contract, [methodBody]);
        await AssertSourceHasNoLoop(contract, methodBody);
    }

    [Test]
    public async Task KleiInventoryPublication_WhenOneUpdateRuns_EnumeratesEachContributingPickupableOnce()
    {
        MethodPerformanceContract contract = await RequireContract("klei-inventory-pickup");
        string methodBody = await ReadAndValidateSourceMethod(contract);

        await Assert.That(CountOccurrences(methodBody, ".AddTemperatureAmount(")).IsEqualTo(1).Because("The one hook invocation injected at the verified original " +
            "Pickupable.TotalAmount contribution must publish exactly once.");
        await AssertContainsNoForbiddenReferences(contract, [methodBody]);
        await AssertSourceHasNoLoop(contract, methodBody);
    }

    [Test]
    public async Task FastTrackIncrementalPublication_WhenOneTagRuns_DoesNotConstructCompleteWorldPublication()
    {
        var publicationSession = new FastTrackWorldInventoryPublicationSession();
        FieldInfo completeWorldBuilderField = await RequirePrivateInstanceField(
            typeof(FastTrackWorldInventoryPublicationSession),
            "completeWorldResourceTemperatureAmountsBuilder");
        await Assert.That(completeWorldBuilderField.GetValue(publicationSession)).IsNull();

        publicationSession.BeginIncrementalResourceTagUpdateRequiringCoverage(
            new GameSessionGeneration(1),
            new WorldInventoryCollectionGeneration(1),
            [new Tag("Iron")]);
        publicationSession.BeginResourceTag(new Tag("Iron"));
        publicationSession.AddTemperatureAmount(300.0f, 17.0f);
        publicationSession.CompleteResourceTag();
        FastTrackWorldInventoryPublicationResult result =
            publicationSession.Complete();

        await Assert.That(completeWorldBuilderField.GetValue(publicationSession)).IsNull().Because("The ordinary incremental path must leave the lazy complete-world " +
            "builder unconstructed.");
        await Assert.That(result.TryGetCompleteWorldResourceTemperatureAmounts(out _)).IsFalse();
        await Assert.That(result.Kind).IsEqualTo(FastTrackWorldInventoryPublicationKind
                .ResourceTagCoverageAndTemperatureSeries);
    }

    [Test]
    public async Task FastTrackIncrementalPublication_WhenOneTagRuns_RebuildsOneParentTagAggregate()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var generation = new WorldInventoryCollectionGeneration(1);
        var catalog = new WorldResourceTemperatureAmountCatalog();
        catalog.RegisterWorld(worldId: 1, parentWorldId: 10);
        catalog.RegisterWorld(worldId: 2, parentWorldId: 10);
        catalog.RegisterWorld(worldId: 3, parentWorldId: 20);
        await Assert.That(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(generation, (iron, 10.0f), (copper, 20.0f)))).IsTrue();
        await Assert.That(catalog.PublishCompleteWorldResourceAmounts(
            2,
            CompleteWorld(generation, (iron, 30.0f), (copper, 40.0f)))).IsTrue();
        await Assert.That(catalog.PublishCompleteWorldResourceAmounts(
            3,
            CompleteWorld(generation, (iron, 50.0f), (copper, 60.0f)))).IsTrue();
        object targetBefore = await ReadAggregateReference(catalog, 10, iron);
        object sameParentOtherTagBefore =
            await ReadAggregateReference(catalog, 10, copper);
        object otherParentSameTagBefore =
            await ReadAggregateReference(catalog, 20, iron);

        await Assert.That(catalog.PublishWorldResourceTemperatureSeries(
            1,
            new WorldResourceTemperatureSeriesPublication(
                generation,
                iron,
                Series(300.0f, 70.0f)))).IsTrue();

        await Assert.That((await ReadAggregateReference(catalog, 10, iron))).IsNotSameReferenceAs(targetBefore);
        await Assert.That((await ReadAggregateReference(catalog, 10, copper))).IsSameReferenceAs(sameParentOtherTagBefore);
        await Assert.That((await ReadAggregateReference(catalog, 20, iron))).IsSameReferenceAs(otherParentSameTagBefore);

        MethodPerformanceContract sourceContract = new(
            "single-tag-catalog-publication",
            "DeliveryTemperatureLimit.WorldResourceTemperatureAmountCatalog",
            "PublishWorldResourceTemperatureSeries",
            ["System.Int32", "DeliveryTemperatureLimit.WorldResourceTemperatureSeriesPublication"],
            "WorldResourceTemperatureAmounts/WorldResourceTemperatureAmountCatalog.cs",
            "internal bool PublishWorldResourceTemperatureSeries(",
            ["WorldResourceTemperatureAmountCatalog.RebuildOneParentResourceTagAggregate"],
            ["RebuildAffectedParentResourceTagAggregates"],
            BackEdgePolicy.None);
        string methodBody = await ReadAndValidateSourceMethod(sourceContract);
        await AssertContainsEveryPermittedSourceCall(sourceContract, methodBody);
        await AssertContainsNoForbiddenReferences(sourceContract, [methodBody]);
    }

    [Test]
    public async Task DirectEligibilityPath_WhenInspected_CallsNoAllocatorReflectionOrSnapshotRebuild()
    {
        MethodPerformanceContract contract = await RequireContract("direct-eligibility");
        string methodBody = await ReadAndValidateSourceMethod(contract);

        await AssertContainsEveryPermittedSourceCall(contract, methodBody);
        await AssertContainsNoForbiddenReferences(contract, [methodBody]);
        await AssertSourceHasNoLoop(contract, methodBody);
    }

    [Test]
    public async Task PickupComparator_WhenInspected_CapturesNoNewSnapshotAndCreatesNoCollection()
    {
        MethodPerformanceContract contract = await RequireContract("pickup-comparator");
        string methodBody = await ReadAndValidateSourceMethod(contract);

        await AssertContainsEveryPermittedSourceCall(contract, methodBody);
        await AssertContainsNoForbiddenReferences(contract, [methodBody]);
        await AssertSourceHasNoLoop(contract, methodBody);
    }

    [Test]
    public async Task StatusOptionDisabled_WhenActivationPlanIsInspected_ContainsNoInventoryOrStatusPatchGroup()
    {
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                RuntimePatchCapabilitySelectionFixture
                    .CreateKleiBaselineSelection());
        string[] prohibitedGroupIds =
        [
            "klei-world-inventory-temperature-publication",
            "fast-track-world-inventory-temperature-publication",
            "temperature-status-availability"
        ];

        foreach (string prohibitedGroupId in prohibitedGroupIds)
        {
            await Assert.That(plan.OrderedPatchGroupIds.Any(group => string.Equals(
                    group.Value,
                    prohibitedGroupId,
                    StringComparison.Ordinal))).IsFalse().Because($"Disabled status integration selected {prohibitedGroupId}.");
        }
    }

    [Test]
    public async Task RetainedCollections_WhenHighWaterLimitWasExceeded_ReplaceVariableCapacityStorage()
    {
        await AssertRetentionContract(
            (await RequireContract("pickup-retention")),
            "MaximumRetainedPickupClassificationCount",
            "temperatureClassesByPickupInstanceId =",
            "new Dictionary<int, TemperatureEligibilityClassKey>()");
        await AssertRetentionContract(
            (await RequireContract("fasttrack-key-retention")),
            "MaximumRetainedFastTrackGroupingKeyCount",
            "allocatedGroupingKeysByCompositeIdentity =",
            "new Dictionary<");
        await AssertRetentionContract(
            (await RequireContract("fetch-retention")),
            "MaximumRetainedFetchEligibilityEntryCount",
            "destinationRequirementsByParentWorldAndRequestedTag =",
            "new Dictionary<");
        await AssertRetentionContract(
            (await RequireContract("world-tag-retention")),
            "MaximumRetainedWorldResourceTagCount",
            "temperatureAmountsByResourceTag =",
            "new Dictionary<Tag, TemperatureAmountSeries>()");
    }

    [Test]
    public async Task UnusedDecisionBuckets_WhenHotMethodsAreInspected_CauseNoCompleteRangeLoop()
    {
        foreach (string contractId in new[]
                 {
                     "constraint-decision",
                     "amount-add",
                     "amount-query"
                 })
        {
            MethodPerformanceContract contract = await RequireContract(contractId);
            MethodInfo method = await RequireLinkedMethod(contract);
            await Assert.That(ReadBackEdgeTargets(method)).IsEmpty().Because($"Hot method {FormatSubject(contract)} contains a loop.");
            string sourceBody = await ReadAndValidateSourceMethod(contract);
            await AssertContainsNoForbiddenReferences(contract, [sourceBody]);
        }

        MethodPerformanceContract buildContract = await RequireContract("amount-build");
        string buildBody = await ReadAndValidateSourceMethod(buildContract);
        await Assert.That(buildBody.Contains(
            "touchedBucketIndex < touchedBucketCount",
            StringComparison.Ordinal)).IsTrue();
        await Assert.That(buildBody.Contains(
            "occupiedBucketIndex < occupiedBucketCount",
            StringComparison.Ordinal)).IsTrue();
        await AssertContainsNoForbiddenReferences(buildContract, [buildBody]);

        MethodPerformanceContract partitionContract =
            await RequireContract("partition-classification");
        string partitionBody = await ReadAndValidateSourceMethod(partitionContract);
        await Assert.That(partitionBody.Contains(
            "while (lowerEndpointIndex < upperEndpointIndex)",
            StringComparison.Ordinal)).IsTrue();
        await AssertContainsNoForbiddenReferences(partitionContract, [partitionBody]);
    }

    [Test]
    public async Task TemperatureAmountAccumulator_WhenOneBucketIsObserved_TouchesOnlyThatBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(5017.75f, 23.0f);
        int currentStamp = await ReadPrivateField<int>(accumulator, "stamp");
        int touchedBucketCount =
            await ReadPrivateField<int>(accumulator, "touchedBucketCount");
        int[] touchedOrdinals =
            await ReadPrivateField<int[]>(accumulator, "touchedBucketOrdinals");
        int[] stamps = await ReadPrivateField<int[]>(accumulator, "stampsByBucket");
        float[] amounts =
            await ReadPrivateField<float[]>(accumulator, "amountsByBucket");
        int expectedOrdinal =
            TemperatureDecisionBucket.FromTemperature(5017.75f).Ordinal;

        await Assert.That(touchedBucketCount).IsEqualTo(1);
        await Assert.That(touchedOrdinals[0]).IsEqualTo(expectedOrdinal);
        await Assert.That(stamps.Count(stamp => stamp == currentStamp)).IsEqualTo(1);
        await Assert.That(amounts.Count(amount => amount != 0.0f)).IsEqualTo(1);
        await Assert.That(stamps[expectedOrdinal]).IsEqualTo(currentStamp);
        await Assert.That(amounts[expectedOrdinal]).IsEqualTo(23.0f);
    }

    [Test]
    public async Task FastTrackFixture_WhenPackageBoundaryIsInspected_IsNeverPackaged()
    {
        string modRoot = ResolveModRoot();
        string profilePath = Path.Combine(modRoot, "oni-mod-pipeline.toml");
        string profile = File.ReadAllText(profilePath);
        MatchCollection packageMappings = Regex.Matches(
            profile,
            @"(?m)^\[\[package-files\]\]$",
            RegexOptions.CultureInvariant);

        await Assert.That(packageMappings).Count().IsEqualTo(4);
        await Assert.That(profile.Contains("FastTrack.dll", StringComparison.Ordinal)).IsFalse().Because("The static FastTrack fixture must not enter a package mapping.");
        await Assert.That(profile.Contains("Fixtures/", StringComparison.Ordinal)).IsFalse().Because("No test-fixture directory may enter a package mapping.");
        string[] expectedDestinations =
        [
            "destination = \"mod.yaml\"",
            "destination = \"mod_info.yaml\"",
            "destination = \"DeliveryTemperatureLimit.dll\"",
            "destination = \"translations\""
        ];
        foreach (string destination in expectedDestinations)
        {
            await Assert.That(CountOccurrences(profile, destination)).IsEqualTo(1);
        }

        await Assert.That(typeof(PerformanceArchitectureContractTests).Assembly
                .GetManifestResourceNames()
                .Any(resourceName => resourceName.Contains(
                    "FastTrack",
                    StringComparison.OrdinalIgnoreCase))).IsFalse().Because("The fixture must not be embedded as a production resource.");
    }

    private static async Task AssertRetentionContract(
        MethodPerformanceContract contract,
        string retentionLimitName,
        string replacementAssignment,
        string replacementConstruction)
    {
        string methodBody = await ReadAndValidateSourceMethod(contract);
        int comparisonIndex = methodBody.IndexOf(
            retentionLimitName,
            StringComparison.Ordinal);
        int assignmentIndex = methodBody.IndexOf(
            replacementAssignment,
            StringComparison.Ordinal);
        int constructionIndex = methodBody.IndexOf(
            replacementConstruction,
            assignmentIndex,
            StringComparison.Ordinal);

        await Assert.That(comparisonIndex).IsGreaterThanOrEqualTo(0).Because($"{FormatSubject(contract)} does not use {retentionLimitName}.");
        await Assert.That(assignmentIndex).IsGreaterThan(comparisonIndex).Because($"{FormatSubject(contract)} replaces storage before its high-water comparison.");
        await Assert.That(constructionIndex).IsGreaterThanOrEqualTo(assignmentIndex).Because($"{FormatSubject(contract)} does not construct replacement storage.");
        await Assert.That(methodBody.Substring(0, comparisonIndex).Contains(">", StringComparison.Ordinal)).IsTrue().Because($"{FormatSubject(contract)} must replace only after strictly exceeding the limit.");
        await Assert.That(methodBody.Substring(0, comparisonIndex).Contains(">=", StringComparison.Ordinal)).IsFalse().Because($"{FormatSubject(contract)} must retain storage exactly at the limit.");
    }

    private static async Task<MethodPerformanceContract> RequireContract(string contractId)
    {
        MethodPerformanceContract[] matches = MethodContracts
            .Where(contract => string.Equals(
                contract.ContractId,
                contractId,
                StringComparison.Ordinal))
            .ToArray();
        await Assert.That(matches).Count().IsEqualTo(1).Because($"Performance contract ID {contractId} is not unique.");
        return matches[0];
    }

    private static async Task<MethodInfo> RequireLinkedMethod(
        MethodPerformanceContract contract)
    {
        if (contract.LinkedDeclaringType is null) throw new TUnit.Assertions.Exceptions.AssertionException($"{FormatSubject(contract)} is not linked into the test assembly.");
        MethodInfo? method = contract.LinkedDeclaringType.GetMethod(
            contract.MethodName,
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static,
            binder: null,
            contract.LinkedParameterTypes ?? Type.EmptyTypes,
            modifiers: null);
        if (method is null) throw new TUnit.Assertions.Exceptions.AssertionException($"Missing exact linked method {FormatSubject(contract)}.");
        return method;
    }

    private static async Task<string> ReadAndValidateSourceMethod(
        MethodPerformanceContract contract)
    {
        string sourcePath = Path.Combine(
            ResolveSourceRoot(),
            contract.RelativeSourcePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        string source = File.ReadAllText(sourcePath);
        string methodBody = await ExtractMethodBody(source, contract.DeclarationMarker);
        int signatureEndIndex = methodBody.IndexOfAny(['{', '=']);
        await Assert.That(signatureEndIndex).IsGreaterThan(0).Because($"Could not isolate signature for {FormatSubject(contract)}.");
        string signature = methodBody.Substring(0, signatureEndIndex);
        int priorParameterIndex = -1;
        foreach (string parameterTypeName in contract.ExactParameterTypeNames)
        {
            string sourceTypeName = parameterTypeName
                .Replace("System.Single&", "ref float", StringComparison.Ordinal)
                .Replace("System.Single", "float", StringComparison.Ordinal)
                .Replace("System.Int32", "int", StringComparison.Ordinal)
                .Replace("DeliveryTemperatureLimit.", string.Empty, StringComparison.Ordinal);
            int parameterIndex = signature.IndexOf(
                sourceTypeName,
                priorParameterIndex + 1,
                StringComparison.Ordinal);
            await Assert.That(parameterIndex).IsGreaterThan(priorParameterIndex).Because($"Exact parameter type {parameterTypeName} is absent or out of " +
                $"order in {FormatSubject(contract)}: {signature.Trim()}.");
            priorParameterIndex = parameterIndex;
        }

        return methodBody;
    }

    private static async Task<string> ExtractMethodBody(
        string source,
        string declarationMarker)
    {
        int declarationIndex = source.IndexOf(
            declarationMarker,
            StringComparison.Ordinal);
        await Assert.That(declarationIndex).IsGreaterThanOrEqualTo(0).Because($"Missing exact method declaration {declarationMarker}.");
        await Assert.That(source.LastIndexOf(declarationMarker, StringComparison.Ordinal)).IsEqualTo(declarationIndex).Because($"Method declaration is not unique: {declarationMarker}.");
        int openingBraceIndex = source.IndexOf(
            '{',
            declarationIndex + declarationMarker.Length);
        int expressionBodyIndex = source.IndexOf(
            "=>",
            declarationIndex + declarationMarker.Length,
            StringComparison.Ordinal);
        if (expressionBodyIndex >= 0 &&
            (openingBraceIndex < 0 || expressionBodyIndex < openingBraceIndex))
        {
            int semicolonIndex = source.IndexOf(';', expressionBodyIndex + 2);
            await Assert.That(semicolonIndex).IsGreaterThan(expressionBodyIndex);
            return source.Substring(
                declarationIndex,
                semicolonIndex - declarationIndex + 1);
        }

        await Assert.That(openingBraceIndex).IsGreaterThanOrEqualTo(0).Because($"Method declaration has no body: {declarationMarker}.");
        int nestingDepth = 0;
        for (int characterIndex = openingBraceIndex;
             characterIndex < source.Length;
             characterIndex++)
        {
            if (source[characterIndex] == '{')
            {
                nestingDepth++;
            }
            else if (source[characterIndex] == '}')
            {
                nestingDepth--;
                if (nestingDepth == 0)
                {
                    return source.Substring(
                        declarationIndex,
                        characterIndex - declarationIndex + 1);
                }
            }
        }

        Assert.Fail($"Method body is unbalanced: {declarationMarker}.");
        return string.Empty;
    }

    private static async Task AssertContainsEveryPermittedSourceCall(
        MethodPerformanceContract contract,
        string methodBody)
    {
        foreach (string permittedCall in contract.PermittedDirectCallNames)
        {
            string simpleCallName = permittedCall[
                (permittedCall.LastIndexOf(".", StringComparison.Ordinal) + 1)..];
            await Assert.That(methodBody.Contains(simpleCallName + "(", StringComparison.Ordinal)).IsTrue().Because($"Required direct call {permittedCall} is absent from " +
                $"{FormatSubject(contract)}.");
        }
    }

    private static async Task AssertContainsNoForbiddenReferences(
        MethodPerformanceContract contract,
        IReadOnlyList<string> observedReferences)
    {
        foreach (string forbiddenReference in
                 contract.ForbiddenDirectCallOrFieldNames)
        {
            string? matchingReference = observedReferences.FirstOrDefault(
                observed => observed.Contains(
                    forbiddenReference,
                    StringComparison.Ordinal));
            await Assert.That(matchingReference).IsNull().Because($"Forbidden member/reference {forbiddenReference} was observed " +
                $"in {FormatSubject(contract)}: {matchingReference}.");
        }
    }

    private static async Task AssertSourceHasNoLoop(
        MethodPerformanceContract contract,
        string methodBody)
    {
        await Assert.That(contract.BackEdgePolicy).IsEqualTo(BackEdgePolicy.None);
        string[] loopMarkers = ["for (", "foreach (", "while (", "do\r\n", "do\n"];
        foreach (string loopMarker in loopMarkers)
        {
            await Assert.That(methodBody.Contains(loopMarker, StringComparison.Ordinal)).IsFalse().Because($"No-back-edge method {FormatSubject(contract)} contains " +
                $"loop marker '{loopMarker}'.");
        }
    }

    private static IReadOnlyList<string> ReadDirectCallNames(MethodInfo method) =>
        DecodeMethod(method)
            .Where(instruction =>
                instruction.Operation == OpCodes.Call ||
                instruction.Operation == OpCodes.Callvirt ||
                instruction.Operation == OpCodes.Newobj)
            .Select(instruction => instruction.ResolvedMember)
            .Where(member => member is not null)
            .Select(member => StableMemberName(member!))
            .ToArray();

    private static IReadOnlyList<int> ReadBackEdgeTargets(MethodInfo method) =>
        DecodeMethod(method)
            .SelectMany(instruction => instruction.BranchTargets.Select(
                target => (instruction.Offset, Target: target)))
            .Where(edge => edge.Target <= edge.Offset)
            .Select(edge => edge.Target)
            .ToArray();

    private static IReadOnlyList<DecodedInstruction> DecodeMethod(
        MethodInfo method)
    {
        byte[] methodBytes = method.GetMethodBody()?.GetILAsByteArray() ??
            throw new InvalidOperationException(
                $"Method {StableMemberName(method)} has no managed body.");
        var instructions = new List<DecodedInstruction>();
        int byteIndex = 0;
        while (byteIndex < methodBytes.Length)
        {
            int offset = byteIndex;
            OpCode operation = ReadOpCode(methodBytes, ref byteIndex);
            MemberInfo? resolvedMember = null;
            int[] branchTargets = [];
            switch (operation.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    byteIndex += 1;
                    break;
                case OperandType.InlineVar:
                    byteIndex += 2;
                    break;
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                {
                    int tokenOrValue = BitConverter.ToInt32(methodBytes, byteIndex);
                    byteIndex += 4;
                    if (operation.OperandType is OperandType.InlineField or
                        OperandType.InlineMethod or OperandType.InlineTok)
                    {
                        resolvedMember = method.Module.ResolveMember(
                            tokenOrValue,
                            method.DeclaringType?.GetGenericArguments(),
                            method.GetGenericArguments());
                    }

                    break;
                }
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    byteIndex += 8;
                    break;
                case OperandType.ShortInlineBrTarget:
                {
                    sbyte delta = unchecked((sbyte)methodBytes[byteIndex]);
                    byteIndex += 1;
                    branchTargets = [byteIndex + delta];
                    break;
                }
                case OperandType.InlineBrTarget:
                {
                    int delta = BitConverter.ToInt32(methodBytes, byteIndex);
                    byteIndex += 4;
                    branchTargets = [byteIndex + delta];
                    break;
                }
                case OperandType.InlineSwitch:
                {
                    int targetCount = BitConverter.ToInt32(methodBytes, byteIndex);
                    byteIndex += 4;
                    var deltas = new int[targetCount];
                    for (int targetIndex = 0;
                         targetIndex < targetCount;
                         targetIndex++)
                    {
                        deltas[targetIndex] =
                            BitConverter.ToInt32(methodBytes, byteIndex);
                        byteIndex += 4;
                    }

                    int instructionEnd = byteIndex;
                    branchTargets = deltas
                        .Select(delta => instructionEnd + delta)
                        .ToArray();
                    break;
                }
                default:
                    throw new InvalidDataException(
                        $"Unsupported IL operand {operation.OperandType} in " +
                        $"{StableMemberName(method)} at IL_{offset:X4}.");
            }

            instructions.Add(new(
                offset,
                operation,
                resolvedMember,
                branchTargets));
        }

        return instructions;
    }

    private static OpCode ReadOpCode(byte[] methodBytes, ref int byteIndex)
    {
        byte firstByte = methodBytes[byteIndex++];
        short encodedValue;
        if (firstByte == 0xFE)
        {
            byte secondByte = methodBytes[byteIndex++];
            encodedValue = unchecked((short)(0xFE00 | secondByte));
        }
        else
        {
            encodedValue = firstByte;
        }

        if (!OpCodesByValue.TryGetValue(encodedValue, out OpCode operation))
        {
            throw new InvalidDataException(
                $"Unknown IL operation 0x{unchecked((ushort)encodedValue):X4}.");
        }

        return operation;
    }

    private static string StableMemberName(MemberInfo member) =>
        (member.DeclaringType?.FullName ?? "<global>") + "." + member.Name;

    private static string FormatSubject(MethodPerformanceContract contract) =>
        contract.DeclaringTypeName + "." + contract.MethodName + "(" +
        string.Join(", ", contract.ExactParameterTypeNames) + ")";

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int searchIndex = 0;
        while ((searchIndex = source.IndexOf(
                   value,
                   searchIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += value.Length;
        }

        return count;
    }

    private static async Task<FieldInfo> RequirePrivateInstanceField(
        Type declaringType,
        string fieldName)
    {
        FieldInfo? field = declaringType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"Missing exact private field {declaringType.FullName}.{fieldName}.");
        return field;
    }

    private static async Task<T> ReadPrivateField<T>(object instance, string fieldName) =>
        (T)(await RequirePrivateInstanceField(instance.GetType(), fieldName))
            .GetValue(instance)!;

    private static CompleteWorldResourceTemperatureAmounts CompleteWorld(
        WorldInventoryCollectionGeneration generation,
        params (Tag ResourceTag, float Amount)[] resourceAmounts)
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(generation);
        foreach ((Tag resourceTag, float amount) in resourceAmounts)
        {
            builder.BeginResourceTag(resourceTag);
            builder.AddTemperatureAmount(300.0f, amount);
            builder.CompleteResourceTag();
        }

        return builder.Build();
    }

    private static TemperatureAmountSeries Series(
        float temperatureKelvin,
        float amount)
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(temperatureKelvin, amount);
        return accumulator.BuildSeries();
    }

    private static async Task<object> ReadAggregateReference(
        WorldResourceTemperatureAmountCatalog catalog,
        int parentWorldId,
        Tag resourceTag)
    {
        FieldInfo aggregateMapField = await RequirePrivateInstanceField(
            typeof(WorldResourceTemperatureAmountCatalog),
            "aggregatesByParentWorldAndResourceTag");
        IDictionary aggregateMap = (await Assert.That(aggregateMapField.GetValue(catalog)).IsAssignableTo<IDictionary>())!;
        foreach (DictionaryEntry entry in aggregateMap)
        {
            Assert.NotNull(entry.Key);
            object key = entry.Key;
            FieldInfo keyParentField = await RequirePrivateInstanceField(
                key.GetType(),
                "parentWorldId");
            FieldInfo keyTagField = await RequirePrivateInstanceField(
                key.GetType(),
                "resourceTag");
            if ((int)keyParentField.GetValue(key)! == parentWorldId &&
                ((Tag)keyTagField.GetValue(key)!).Equals(resourceTag))
            {
                Assert.NotNull(entry.Value);
                return entry.Value;
            }
        }

        Assert.Fail(
            $"No parent/tag aggregate exists for parent {parentWorldId} and " +
            $"tag hash {resourceTag.GetHashCode()}.");
        return new object();
    }

    private static string ResolveSourceRoot() =>
        Path.Combine(ResolveModRoot(), "Source");

    private static string ResolveModRoot()
    {
        string? repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return Path.Combine(
                repositoryRoot,
                "mods",
                "delivery-temperature-limit-supercooled");
        }

        DirectoryInfo? candidateDirectory = new(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            string modRoot = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled");
            if (File.Exists(Path.Combine(
                    modRoot,
                    "Source",
                    "DeliveryTemperatureLimit.csproj")))
            {
                return modRoot;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        throw new InvalidOperationException(
            "The Delivery Temperature Limit mod root could not be resolved.");
    }

    private sealed record DecodedInstruction(
        int Offset,
        OpCode Operation,
        MemberInfo? ResolvedMember,
        IReadOnlyList<int> BranchTargets);
}
