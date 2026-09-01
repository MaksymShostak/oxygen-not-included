#nullable enable

using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

/// <summary>
/// Pins the structural reasons the rewrite scales with observed colony work
/// rather than the complete configured temperature range. These are call-edge,
/// control-flow, immutable-reference, and semantic-count contracts—not timing or
/// allocation benchmarks.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void MethodContracts_WhenInspected_DeclareUniqueExactPerformanceSubjects()
    {
        Assert.HasCount(
            MethodContracts.Length,
            MethodContracts
                .Select(contract => contract.ContractId)
                .Distinct(StringComparer.Ordinal));
        foreach (MethodPerformanceContract contract in MethodContracts)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.DeclaringTypeName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.MethodName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.RelativeSourcePath));
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.DeclarationMarker));
        }
    }

    [TestMethod]
    public void ConstraintReadPath_WhenInspected_DoesNotCallSortDistinctOrRegistryRebuild()
    {
        MethodPerformanceContract contract = RequireContract("constraint-read");
        MethodInfo method = RequireLinkedMethod(contract);
        IReadOnlyList<string> directCalls = ReadDirectCallNames(method);

        Assert.AreSequenceEqual(
            contract.PermittedDirectCallNames,
            directCalls.Distinct(StringComparer.Ordinal).ToArray(),
            $"Unexpected direct call from {FormatSubject(contract)}.");
        AssertContainsNoForbiddenReferences(contract, directCalls);
        Assert.IsEmpty(ReadBackEdgeTargets(method));
    }

    [TestMethod]
    public void StatusQueryPath_WhenInspected_DoesNotReferenceClusterManagerOrWorldContainers()
    {
        MethodPerformanceContract contract = RequireContract("status-query");
        string methodBody = ReadAndValidateSourceMethod(contract);

        AssertContainsEveryPermittedSourceCall(contract, methodBody);
        AssertContainsNoForbiddenReferences(contract, [methodBody]);
        AssertSourceHasNoLoop(contract, methodBody);
    }

    [TestMethod]
    public void KleiInventoryPublication_WhenOneUpdateRuns_EnumeratesEachContributingPickupableOnce()
    {
        MethodPerformanceContract contract = RequireContract("klei-inventory-pickup");
        string methodBody = ReadAndValidateSourceMethod(contract);

        Assert.AreEqual(
            1,
            CountOccurrences(methodBody, ".AddTemperatureAmount("),
            "The one hook invocation injected at the verified original " +
            "Pickupable.TotalAmount contribution must publish exactly once.");
        AssertContainsNoForbiddenReferences(contract, [methodBody]);
        AssertSourceHasNoLoop(contract, methodBody);
    }

    [TestMethod]
    public void FastTrackIncrementalPublication_WhenOneTagRuns_DoesNotConstructCompleteWorldPublication()
    {
        var publicationSession = new FastTrackWorldInventoryPublicationSession();
        FieldInfo completeWorldBuilderField = RequirePrivateInstanceField(
            typeof(FastTrackWorldInventoryPublicationSession),
            "completeWorldResourceTemperatureAmountsBuilder");
        Assert.IsNull(completeWorldBuilderField.GetValue(publicationSession));

        publicationSession.BeginIncrementalResourceTagUpdateRequiringCoverage(
            new GameSessionGeneration(1),
            new WorldInventoryCollectionGeneration(1),
            [new Tag("Iron")]);
        publicationSession.BeginResourceTag(new Tag("Iron"));
        publicationSession.AddTemperatureAmount(300.0f, 17.0f);
        publicationSession.CompleteResourceTag();
        FastTrackWorldInventoryPublicationResult result =
            publicationSession.Complete();

        Assert.IsNull(
            completeWorldBuilderField.GetValue(publicationSession),
            "The ordinary incremental path must leave the lazy complete-world " +
            "builder unconstructed.");
        Assert.IsFalse(
            result.TryGetCompleteWorldResourceTemperatureAmounts(out _));
        Assert.AreEqual(
            FastTrackWorldInventoryPublicationKind
                .ResourceTagCoverageAndTemperatureSeries,
            result.Kind);
    }

    [TestMethod]
    public void FastTrackIncrementalPublication_WhenOneTagRuns_RebuildsOneParentTagAggregate()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var generation = new WorldInventoryCollectionGeneration(1);
        var catalog = new WorldResourceTemperatureAmountCatalog();
        catalog.RegisterWorld(worldId: 1, parentWorldId: 10);
        catalog.RegisterWorld(worldId: 2, parentWorldId: 10);
        catalog.RegisterWorld(worldId: 3, parentWorldId: 20);
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            1,
            CompleteWorld(generation, (iron, 10.0f), (copper, 20.0f))));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            2,
            CompleteWorld(generation, (iron, 30.0f), (copper, 40.0f))));
        Assert.IsTrue(catalog.PublishCompleteWorldResourceAmounts(
            3,
            CompleteWorld(generation, (iron, 50.0f), (copper, 60.0f))));
        object targetBefore = ReadAggregateReference(catalog, 10, iron);
        object sameParentOtherTagBefore =
            ReadAggregateReference(catalog, 10, copper);
        object otherParentSameTagBefore =
            ReadAggregateReference(catalog, 20, iron);

        Assert.IsTrue(catalog.PublishWorldResourceTemperatureSeries(
            1,
            new WorldResourceTemperatureSeriesPublication(
                generation,
                iron,
                Series(300.0f, 70.0f))));

        Assert.AreNotSame(
            targetBefore,
            ReadAggregateReference(catalog, 10, iron));
        Assert.AreSame(
            sameParentOtherTagBefore,
            ReadAggregateReference(catalog, 10, copper));
        Assert.AreSame(
            otherParentSameTagBefore,
            ReadAggregateReference(catalog, 20, iron));

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
        string methodBody = ReadAndValidateSourceMethod(sourceContract);
        AssertContainsEveryPermittedSourceCall(sourceContract, methodBody);
        AssertContainsNoForbiddenReferences(sourceContract, [methodBody]);
    }

    [TestMethod]
    public void DirectEligibilityPath_WhenInspected_CallsNoAllocatorReflectionOrSnapshotRebuild()
    {
        MethodPerformanceContract contract = RequireContract("direct-eligibility");
        string methodBody = ReadAndValidateSourceMethod(contract);

        AssertContainsEveryPermittedSourceCall(contract, methodBody);
        AssertContainsNoForbiddenReferences(contract, [methodBody]);
        AssertSourceHasNoLoop(contract, methodBody);
    }

    [TestMethod]
    public void PickupComparator_WhenInspected_CapturesNoNewSnapshotAndCreatesNoCollection()
    {
        MethodPerformanceContract contract = RequireContract("pickup-comparator");
        string methodBody = ReadAndValidateSourceMethod(contract);

        AssertContainsEveryPermittedSourceCall(contract, methodBody);
        AssertContainsNoForbiddenReferences(contract, [methodBody]);
        AssertSourceHasNoLoop(contract, methodBody);
    }

    [TestMethod]
    public void StatusOptionDisabled_WhenActivationPlanIsInspected_ContainsNoInventoryOrStatusPatchGroup()
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
            Assert.IsFalse(
                plan.OrderedPatchGroupIds.Any(group => string.Equals(
                    group.Value,
                    prohibitedGroupId,
                    StringComparison.Ordinal)),
                $"Disabled status integration selected {prohibitedGroupId}.");
        }
    }

    [TestMethod]
    public void RetainedCollections_WhenHighWaterLimitWasExceeded_ReplaceVariableCapacityStorage()
    {
        AssertRetentionContract(
            RequireContract("pickup-retention"),
            "MaximumRetainedPickupClassificationCount",
            "temperatureClassesByPickupInstanceId =",
            "new Dictionary<int, TemperatureEligibilityClassKey>()");
        AssertRetentionContract(
            RequireContract("fasttrack-key-retention"),
            "MaximumRetainedFastTrackGroupingKeyCount",
            "allocatedGroupingKeysByCompositeIdentity =",
            "new Dictionary<");
        AssertRetentionContract(
            RequireContract("fetch-retention"),
            "MaximumRetainedFetchEligibilityEntryCount",
            "destinationRequirementsByParentWorldAndRequestedTag =",
            "new Dictionary<");
        AssertRetentionContract(
            RequireContract("world-tag-retention"),
            "MaximumRetainedWorldResourceTagCount",
            "temperatureAmountsByResourceTag =",
            "new Dictionary<Tag, TemperatureAmountSeries>()");
    }

    [TestMethod]
    public void UnusedDecisionBuckets_WhenHotMethodsAreInspected_CauseNoCompleteRangeLoop()
    {
        foreach (string contractId in new[]
                 {
                     "constraint-decision",
                     "amount-add",
                     "amount-query"
                 })
        {
            MethodPerformanceContract contract = RequireContract(contractId);
            MethodInfo method = RequireLinkedMethod(contract);
            Assert.IsEmpty(
                ReadBackEdgeTargets(method),
                $"Hot method {FormatSubject(contract)} contains a loop.");
            string sourceBody = ReadAndValidateSourceMethod(contract);
            AssertContainsNoForbiddenReferences(contract, [sourceBody]);
        }

        MethodPerformanceContract buildContract = RequireContract("amount-build");
        string buildBody = ReadAndValidateSourceMethod(buildContract);
        Assert.IsTrue(buildBody.Contains(
            "touchedBucketIndex < touchedBucketCount",
            StringComparison.Ordinal));
        Assert.IsTrue(buildBody.Contains(
            "occupiedBucketIndex < occupiedBucketCount",
            StringComparison.Ordinal));
        AssertContainsNoForbiddenReferences(buildContract, [buildBody]);

        MethodPerformanceContract partitionContract =
            RequireContract("partition-classification");
        string partitionBody = ReadAndValidateSourceMethod(partitionContract);
        Assert.IsTrue(partitionBody.Contains(
            "while (lowerEndpointIndex < upperEndpointIndex)",
            StringComparison.Ordinal));
        AssertContainsNoForbiddenReferences(partitionContract, [partitionBody]);
    }

    [TestMethod]
    public void TemperatureAmountAccumulator_WhenOneBucketIsObserved_TouchesOnlyThatBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(5017.75f, 23.0f);
        int currentStamp = ReadPrivateField<int>(accumulator, "stamp");
        int touchedBucketCount =
            ReadPrivateField<int>(accumulator, "touchedBucketCount");
        int[] touchedOrdinals =
            ReadPrivateField<int[]>(accumulator, "touchedBucketOrdinals");
        int[] stamps = ReadPrivateField<int[]>(accumulator, "stampsByBucket");
        float[] amounts =
            ReadPrivateField<float[]>(accumulator, "amountsByBucket");
        int expectedOrdinal =
            TemperatureDecisionBucket.FromTemperature(5017.75f).Ordinal;

        Assert.AreEqual(1, touchedBucketCount);
        Assert.AreEqual(expectedOrdinal, touchedOrdinals[0]);
        Assert.AreEqual(1, stamps.Count(stamp => stamp == currentStamp));
        Assert.AreEqual(1, amounts.Count(amount => amount != 0.0f));
        Assert.AreEqual(currentStamp, stamps[expectedOrdinal]);
        Assert.AreEqual(23.0f, amounts[expectedOrdinal]);
    }

    [TestMethod]
    public void FastTrackFixture_WhenPackageBoundaryIsInspected_IsNeverPackaged()
    {
        string modRoot = ResolveModRoot();
        string profilePath = Path.Combine(modRoot, "oni-mod-pipeline.toml");
        string profile = File.ReadAllText(profilePath);
        MatchCollection packageMappings = Regex.Matches(
            profile,
            @"(?m)^\[\[package-files\]\]$",
            RegexOptions.CultureInvariant);

        Assert.HasCount(3, packageMappings);
        Assert.IsFalse(
            profile.Contains("FastTrack.dll", StringComparison.Ordinal),
            "The static FastTrack fixture must not enter a package mapping.");
        Assert.IsFalse(
            profile.Contains("Fixtures/", StringComparison.Ordinal),
            "No test-fixture directory may enter a package mapping.");
        string[] expectedDestinations =
        [
            "destination = \"mod.yaml\"",
            "destination = \"mod_info.yaml\"",
            "destination = \"DeliveryTemperatureLimit.dll\""
        ];
        foreach (string destination in expectedDestinations)
        {
            Assert.AreEqual(1, CountOccurrences(profile, destination));
        }

        Assert.IsFalse(
            typeof(PerformanceArchitectureContractTests).Assembly
                .GetManifestResourceNames()
                .Any(resourceName => resourceName.Contains(
                    "FastTrack",
                    StringComparison.OrdinalIgnoreCase)),
            "The fixture must not be embedded as a production resource.");
    }

    private static void AssertRetentionContract(
        MethodPerformanceContract contract,
        string retentionLimitName,
        string replacementAssignment,
        string replacementConstruction)
    {
        string methodBody = ReadAndValidateSourceMethod(contract);
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

        Assert.IsGreaterThanOrEqualTo(
            0,
            comparisonIndex,
            $"{FormatSubject(contract)} does not use {retentionLimitName}.");
        Assert.IsGreaterThan(
            comparisonIndex,
            assignmentIndex,
            $"{FormatSubject(contract)} replaces storage before its high-water comparison.");
        Assert.IsGreaterThanOrEqualTo(
            assignmentIndex,
            constructionIndex,
            $"{FormatSubject(contract)} does not construct replacement storage.");
        Assert.IsTrue(
            methodBody.Substring(0, comparisonIndex).Contains(">", StringComparison.Ordinal),
            $"{FormatSubject(contract)} must replace only after strictly exceeding the limit.");
        Assert.IsFalse(
            methodBody.Substring(0, comparisonIndex).Contains(">=", StringComparison.Ordinal),
            $"{FormatSubject(contract)} must retain storage exactly at the limit.");
    }

    private static MethodPerformanceContract RequireContract(string contractId)
    {
        MethodPerformanceContract[] matches = MethodContracts
            .Where(contract => string.Equals(
                contract.ContractId,
                contractId,
                StringComparison.Ordinal))
            .ToArray();
        Assert.HasCount(1, matches, $"Performance contract ID {contractId} is not unique.");
        return matches[0];
    }

    private static MethodInfo RequireLinkedMethod(
        MethodPerformanceContract contract)
    {
        Assert.IsNotNull(
            contract.LinkedDeclaringType,
            $"{FormatSubject(contract)} is not linked into the test assembly.");
        MethodInfo? method = contract.LinkedDeclaringType.GetMethod(
            contract.MethodName,
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static,
            binder: null,
            contract.LinkedParameterTypes ?? Type.EmptyTypes,
            modifiers: null);
        Assert.IsNotNull(
            method,
            $"Missing exact linked method {FormatSubject(contract)}.");
        return method;
    }

    private static string ReadAndValidateSourceMethod(
        MethodPerformanceContract contract)
    {
        string sourcePath = Path.Combine(
            ResolveSourceRoot(),
            contract.RelativeSourcePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        string source = File.ReadAllText(sourcePath);
        string methodBody = ExtractMethodBody(source, contract.DeclarationMarker);
        int signatureEndIndex = methodBody.IndexOfAny(['{', '=']);
        Assert.IsGreaterThan(
            0,
            signatureEndIndex,
            $"Could not isolate signature for {FormatSubject(contract)}.");
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
            Assert.IsGreaterThan(
                priorParameterIndex,
                parameterIndex,
                $"Exact parameter type {parameterTypeName} is absent or out of " +
                $"order in {FormatSubject(contract)}: {signature.Trim()}.");
            priorParameterIndex = parameterIndex;
        }

        return methodBody;
    }

    private static string ExtractMethodBody(
        string source,
        string declarationMarker)
    {
        int declarationIndex = source.IndexOf(
            declarationMarker,
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(
            0,
            declarationIndex,
            $"Missing exact method declaration {declarationMarker}.");
        Assert.AreEqual(
            declarationIndex,
            source.LastIndexOf(declarationMarker, StringComparison.Ordinal),
            $"Method declaration is not unique: {declarationMarker}.");
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
            Assert.IsGreaterThan(expressionBodyIndex, semicolonIndex);
            return source.Substring(
                declarationIndex,
                semicolonIndex - declarationIndex + 1);
        }

        Assert.IsGreaterThanOrEqualTo(
            0,
            openingBraceIndex,
            $"Method declaration has no body: {declarationMarker}.");
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

    private static void AssertContainsEveryPermittedSourceCall(
        MethodPerformanceContract contract,
        string methodBody)
    {
        foreach (string permittedCall in contract.PermittedDirectCallNames)
        {
            string simpleCallName = permittedCall[
                (permittedCall.LastIndexOf(".", StringComparison.Ordinal) + 1)..];
            Assert.IsTrue(
                methodBody.Contains(simpleCallName + "(", StringComparison.Ordinal),
                $"Required direct call {permittedCall} is absent from " +
                $"{FormatSubject(contract)}.");
        }
    }

    private static void AssertContainsNoForbiddenReferences(
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
            Assert.IsNull(
                matchingReference,
                $"Forbidden member/reference {forbiddenReference} was observed " +
                $"in {FormatSubject(contract)}: {matchingReference}.");
        }
    }

    private static void AssertSourceHasNoLoop(
        MethodPerformanceContract contract,
        string methodBody)
    {
        Assert.AreEqual(BackEdgePolicy.None, contract.BackEdgePolicy);
        string[] loopMarkers = ["for (", "foreach (", "while (", "do\r\n", "do\n"];
        foreach (string loopMarker in loopMarkers)
        {
            Assert.IsFalse(
                methodBody.Contains(loopMarker, StringComparison.Ordinal),
                $"No-back-edge method {FormatSubject(contract)} contains " +
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

    private static FieldInfo RequirePrivateInstanceField(
        Type declaringType,
        string fieldName)
    {
        FieldInfo? field = declaringType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"Missing exact private field {declaringType.FullName}.{fieldName}.");
        return field;
    }

    private static T ReadPrivateField<T>(object instance, string fieldName) =>
        (T)RequirePrivateInstanceField(instance.GetType(), fieldName)
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

    private static object ReadAggregateReference(
        WorldResourceTemperatureAmountCatalog catalog,
        int parentWorldId,
        Tag resourceTag)
    {
        FieldInfo aggregateMapField = RequirePrivateInstanceField(
            typeof(WorldResourceTemperatureAmountCatalog),
            "aggregatesByParentWorldAndResourceTag");
        IDictionary aggregateMap = Assert.IsInstanceOfType<IDictionary>(
            aggregateMapField.GetValue(catalog));
        foreach (DictionaryEntry entry in aggregateMap)
        {
            Assert.IsNotNull(entry.Key);
            object key = entry.Key;
            FieldInfo keyParentField = RequirePrivateInstanceField(
                key.GetType(),
                "parentWorldId");
            FieldInfo keyTagField = RequirePrivateInstanceField(
                key.GetType(),
                "resourceTag");
            if ((int)keyParentField.GetValue(key)! == parentWorldId &&
                ((Tag)keyTagField.GetValue(key)!).Equals(resourceTag))
            {
                Assert.IsNotNull(entry.Value);
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
