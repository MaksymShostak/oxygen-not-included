using DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.HarmonyTranspilerInfrastructure;
public sealed class HarmonyPatchContractVerifierTests
{
    [Test]
    public async Task RequireInstanceMethod_WhenOneSignatureMatches_ReturnsThatMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(MethodFixture),
            "ExactInstanceTarget",
            DeclaredMemberVisibility.NonPublic,
            typeof(bool),
            [typeof(int), typeof(string)]);

        await Assert.That(method.Name).IsEqualTo("ExactInstanceTarget");
        await Assert.That(method.DeclaringType).IsSameReferenceAs(typeof(MethodFixture));
        await Assert.That(method.IsStatic).IsFalse();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenMethodIsMissing_ThrowsContractViolation()
    {
        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(MethodFixture),
                    "MissingInstanceTarget",
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    [typeof(int), typeof(string)])).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("MissingInstanceTarget");
        await Assert.That(exception.Message).Contains("0");
    }

    [Test]
    public async Task RequireInstanceMethod_WhenOnlyWrongParametersExist_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "WrongParameterInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenOnlyWrongReturnTypeExists_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "WrongReturnInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenStaticnessDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "StaticInsteadOfInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(MethodFixture),
            DuplicatedMemberCollection.Methods);

        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    ambiguousType,
                    "ExactInstanceTarget",
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    [typeof(int), typeof(string)])).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("2");
    }

    [Test]
    public async Task RequireInstanceMethod_WhenPublicVisibilityIsRequired_ReturnsPublicMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(MethodFixture),
            "PublicInstanceTarget",
            DeclaredMemberVisibility.Public,
            typeof(bool),
            [typeof(int), typeof(string)]);

        await Assert.That(method.IsPublic).IsTrue();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "PublicInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenOnlyInheritedMethodMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "InheritedInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireInstanceMethod_WhenOnlyGenericMethodMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "GenericInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenOneSignatureMatches_ReturnsThatMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireStaticMethod(
            typeof(MethodFixture),
            "ExactStaticTarget",
            DeclaredMemberVisibility.NonPublic,
            typeof(string),
            [typeof(long)]);

        await Assert.That(method.Name).IsEqualTo("ExactStaticTarget");
        await Assert.That(method.DeclaringType).IsSameReferenceAs(typeof(MethodFixture));
        await Assert.That(method.IsStatic).IsTrue();
    }

    [Test]
    public async Task RequireStaticMethod_WhenMethodIsMissing_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "MissingStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenOnlyWrongParametersExist_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "WrongParameterStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenOnlyWrongReturnTypeExists_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "WrongReturnStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenStaticnessDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "InstanceInsteadOfStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(MethodFixture),
            DuplicatedMemberCollection.Methods);

        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    ambiguousType,
                    "ExactStaticTarget",
                    DeclaredMemberVisibility.NonPublic,
                    typeof(string),
                    [typeof(long)])).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("2");
    }

    [Test]
    public async Task RequireStaticMethod_WhenPublicVisibilityIsRequired_ReturnsPublicMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireStaticMethod(
            typeof(MethodFixture),
            "PublicStaticTarget",
            DeclaredMemberVisibility.Public,
            typeof(string),
            [typeof(long)]);

        await Assert.That(method.IsPublic).IsTrue();
    }

    [Test]
    public async Task RequireStaticMethod_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "PublicStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenOnlyInheritedMethodMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "InheritedStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireStaticMethod_WhenOnlyGenericMethodMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "GenericStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireConstructor_WhenOneSignatureMatches_ReturnsThatConstructor()
    {
        var constructor = HarmonyPatchContractVerifier.RequireConstructor(
            typeof(ConstructorFixture),
            DeclaredMemberVisibility.NonPublic,
            [typeof(int), typeof(string)]);

        await Assert.That(constructor.DeclaringType).IsSameReferenceAs(typeof(ConstructorFixture));
        await Assert.That(constructor.IsPublic).IsFalse();
    }

    [Test]
    public async Task RequireConstructor_WhenConstructorIsMissing_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(ConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(decimal)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireConstructor_WhenParameterOrderDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(ReversedConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(int), typeof(string)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireConstructor_WhenPublicVisibilityIsRequired_ReturnsPublicConstructor()
    {
        var constructor = HarmonyPatchContractVerifier.RequireConstructor(
            typeof(ConstructorFixture),
            DeclaredMemberVisibility.Public,
            [typeof(string), typeof(int)]);

        await Assert.That(constructor.IsPublic).IsTrue();
    }

    [Test]
    public async Task RequireConstructor_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(ConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(string), typeof(int)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireConstructor_WhenOnlyBaseConstructorMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(DerivedConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(long)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireConstructor_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(ConstructorFixture),
            DuplicatedMemberCollection.Constructors);

        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireConstructor(
                    ambiguousType,
                    DeclaredMemberVisibility.NonPublic,
                    [typeof(int), typeof(string)])).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("2");
    }

    [Test]
    public async Task RequireField_WhenOneInstanceFieldMatches_ReturnsThatField()
    {
        var field = HarmonyPatchContractVerifier.RequireField(
            typeof(FieldFixture),
            "exactInstanceField",
            DeclaredMemberVisibility.NonPublic,
            FieldStorageKind.Instance,
            typeof(int));

        await Assert.That(field.DeclaringType).IsSameReferenceAs(typeof(FieldFixture));
        await Assert.That(field.IsStatic).IsFalse();
    }

    [Test]
    public async Task RequireField_WhenOneStaticFieldMatches_ReturnsThatField()
    {
        var field = HarmonyPatchContractVerifier.RequireField(
            typeof(FieldFixture),
            "exactStaticField",
            DeclaredMemberVisibility.NonPublic,
            FieldStorageKind.Static,
            typeof(string));

        await Assert.That(field.DeclaringType).IsSameReferenceAs(typeof(FieldFixture));
        await Assert.That(field.IsStatic).IsTrue();
    }

    [Test]
    public async Task RequireField_WhenFieldIsMissing_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "missingField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int))).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireField_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                nameof(FieldFixture.PublicInstanceField),
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int))).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireField_WhenStaticnessDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "staticInsteadOfInstanceField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int))).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireField_WhenFieldTypeDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "wrongTypeField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int))).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireField_WhenOnlyInheritedFieldMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "InheritedInstanceField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int))).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireField_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(FieldFixture),
            DuplicatedMemberCollection.Fields);

        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireField(
                    ambiguousType,
                    "exactInstanceField",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(int))).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("2");
    }

    [Test]
    public async Task RequireNestedType_WhenOneNonPublicTypeMatches_ReturnsThatType()
    {
        var nestedType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(NestedTypeFixture),
            "NonPublicNestedTarget",
            DeclaredMemberVisibility.NonPublic);

        await Assert.That(nestedType.Name).IsEqualTo("NonPublicNestedTarget");
        await Assert.That(nestedType.IsNestedPrivate).IsTrue();
    }

    [Test]
    public async Task RequireNestedType_WhenOnePublicTypeMatches_ReturnsThatType()
    {
        var nestedType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(NestedTypeFixture),
            "PublicNestedTarget",
            DeclaredMemberVisibility.Public);

        await Assert.That(nestedType.Name).IsEqualTo("PublicNestedTarget");
        await Assert.That(nestedType.IsNestedPublic).IsTrue();
    }

    [Test]
    public async Task RequireNestedType_WhenNestedTypeIsMissing_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireNestedType(
                typeof(NestedTypeFixture),
                "MissingNestedTarget",
                DeclaredMemberVisibility.NonPublic)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireNestedType_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireNestedType(
                typeof(NestedTypeFixture),
                "PublicNestedTarget",
                DeclaredMemberVisibility.NonPublic)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireNestedType_WhenOnlyInheritedNestedTypeMatches_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireNestedType(
                typeof(NestedTypeFixture),
                "InheritedNestedTarget",
                DeclaredMemberVisibility.NonPublic)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task RequireNestedType_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(NestedTypeFixture),
            DuplicatedMemberCollection.NestedTypes);

        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireNestedType(
                    ambiguousType,
                    "NonPublicNestedTarget",
                    DeclaredMemberVisibility.NonPublic)).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("2");
    }

    [Test]
    public async Task RequireSingleMatch_WhenNoInstructionMatches_ThrowsWithMatchCount()
    {
        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    new[] { "load", "return" },
                    instruction => instruction == "anchor",
                    "Fixture.Target anchor")).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("0");
        await Assert.That(exception.Message).Contains("Fixture.Target anchor");
    }

    [Test]
    public async Task RequireSingleMatch_WhenOneInstructionMatches_ReturnsThatInstruction()
    {
        var instruction = HarmonyPatchContractVerifier.RequireSingleMatch(
            new[] { "load", "anchor", "return" },
            candidate => candidate == "anchor",
            "Fixture.Target anchor");

        await Assert.That(instruction).IsEqualTo("anchor");
    }

    [Test]
    public async Task RequireSingleMatch_WhenTwoInstructionsMatch_ThrowsWithMatchCount()
    {
        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    new[] { "load", "anchor", "anchor", "return" },
                    instruction => instruction == "anchor",
                    "Fixture.Target anchor")).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("2");
        await Assert.That(exception.Message).Contains("Fixture.Target anchor");
    }

    [Test]
    public async Task RequireSingleMatch_WhenPredicateThrows_WrapsContractAndOriginalException()
    {
        var predicateFailure = new InvalidOperationException(
            "fixture predicate failure");

        var exception = (await Assert.That(() =>
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    new[] { "load", "anchor", "return" },
                    instruction => instruction == "anchor"
                        ? throw predicateFailure
                        : false,
                    "Fixture.Target anchor")).ThrowsExactly<
            HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("Fixture.Target anchor");
        await Assert.That(exception.InnerException).IsSameReferenceAs(predicateFailure);
    }

    [Test]
    public async Task RequireSingleMatch_WhenSeveralCandidatesExist_EvaluatesEachCandidateOnce()
    {
        var evaluationCountByCandidate = new Dictionary<string, int>();

        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireSingleMatch(
                new[] { "first", "anchor-one", "anchor-two", "last" },
                candidate =>
                {
                    evaluationCountByCandidate[candidate] =
                        evaluationCountByCandidate.GetValueOrDefault(candidate) + 1;
                    return candidate.StartsWith(
                        "anchor",
                        StringComparison.Ordinal);
                },
                "Fixture.Target anchor")).ThrowsExactly<HarmonyPatchContractViolationException>();

        await Assert.That(evaluationCountByCandidate).Count().IsEqualTo(4);
        await Assert.That(evaluationCountByCandidate.Values.All(count => count == 1)).IsTrue();
    }

    [Test]
    public async Task ActiveHarmonyPrefixDescriptor_WhenConstructed_PreservesExactMetadata()
    {
        var targetMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var patchMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix));

        var descriptor = new ActiveHarmonyPrefixDescriptor(
            targetMethod,
            patchMethod,
            "permitted.owner",
            priority: -123);

        await Assert.That(descriptor.TargetMethod).IsSameReferenceAs(targetMethod);
        await Assert.That(descriptor.PrefixMethod).IsSameReferenceAs(patchMethod);
        await Assert.That(descriptor.HarmonyOwner).IsEqualTo("permitted.owner");
        await Assert.That(descriptor.Priority).IsEqualTo(-123);
    }

    [Test]
    public async Task ActiveHarmonyPrefixDescriptor_WhenTargetMethodIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(async () =>
{new ActiveHarmonyPrefixDescriptor(
                null!,
                (await RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix))),
                "permitted.owner",
                priority: 0);}).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ActiveHarmonyPrefixDescriptor_WhenPatchMethodIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(async () =>
{new ActiveHarmonyPrefixDescriptor(
                (await RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.KleiTarget))),
                null!,
                "permitted.owner",
                priority: 0);}).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ActiveHarmonyPrefixDescriptor_WhenHarmonyOwnerIsBlank_ThrowsArgumentException()
    {
        await Assert.That(async () =>
{new ActiveHarmonyPrefixDescriptor(
                (await RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.KleiTarget))),
                (await RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix))),
                " ",
                priority: 0);}).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenNoSkippingPrefixExists_ReturnsTrue()
    {
        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            (await RequireFixtureMethod(nameof(HarmonyAuthorityFixture.KleiTarget))),
            Array.Empty<ActiveHarmonyPrefixDescriptor>(),
            Array.Empty<string>());

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenOnlyPermittedSkippingOwnerExists_ReturnsTrue()
    {
        var targetMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix),
                "permitted.owner",
                priority: 400)),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenForeignSkippingPrefixExists_ReturnsFalse()
    {
        var targetMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignSkippingPrefix),
                "foreign.owner",
                priority: 0)),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenForeignNonSkippingPrefixExists_ReturnsTrue()
    {
        var targetMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignObservingPrefix),
                "foreign.owner",
                priority: 0)),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            Array.Empty<string>());

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenSkippingPrefixTargetsAnotherMethod_ReturnsTrue()
    {
        var activePatches = new[]
        {
            (await CreateDescriptor(
                (await RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.OtherKleiTarget))),
                nameof(HarmonyAuthorityFixture.ForeignSkippingPrefix),
                "foreign.owner",
                priority: 0)),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            (await RequireFixtureMethod(nameof(HarmonyAuthorityFixture.KleiTarget))),
            activePatches,
            Array.Empty<string>());

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenSeveralPrioritiesExist_DoesNotInferAuthorityFromOrder()
    {
        var targetMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix),
                "permitted.owner",
                priority: int.MaxValue)),
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignObservingPrefix),
                "observer.owner",
                priority: 0)),
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignSkippingPrefix),
                "foreign.owner",
                priority: int.MinValue)),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyKleiAuthority_WhenOwnerDiffersOnlyByCase_ReturnsFalse()
    {
        var targetMethod = await RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            (await CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix),
                "PERMITTED.OWNER",
                priority: 0)),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task WorldInventoryUpdateContract_WhenInstalledShapeMatches_ReturnsExactPrivateInstanceMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(WorldInventoryUpdateTargetContractFixture),
            "Update",
            DeclaredMemberVisibility.NonPublic,
            typeof(void),
            Array.Empty<Type>());

        await AssertExactInstanceMethod(
            method,
            typeof(WorldInventoryUpdateTargetContractFixture),
            isPublic: false,
            Array.Empty<Type>());
    }

    [Test]
    public async Task FetchListStatusRenderContract_WhenInstalledShapeMatches_ReturnsExactPublicInstanceMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(FetchListStatusRenderTargetContractFixture),
            "Render200ms",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(float)]);

        await AssertExactInstanceMethod(
            method,
            typeof(FetchListStatusRenderTargetContractFixture),
            isPublic: true,
            [typeof(float)]);
    }

    [Test]
    public void FetchListStatusRenderContract_WhenInstalledOniAssemblyIsInspected_MatchesSingleFloatParameter()
    {
        string? managedAssemblyDirectory = Environment.GetEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        if (string.IsNullOrWhiteSpace(managedAssemblyDirectory))
        {
            throw new TUnit.Assertions.Exceptions.AssertionException(
                "The ONI mod pipeline must provide " +
                "ONI_MANAGED_ASSEMBLY_DIRECTORY.");
        }

        string assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                managedAssemblyDirectory,
                "Assembly-CSharp.dll");
        DeliveryTemperatureAssemblyMetadataReader
            .AssertPublicInstanceVoidMethodWithSingleFloatParameter(
                assemblyPath,
                "FetchListStatusItemUpdater",
                "Render200ms");
    }

    [Test]
    public async Task FetchListStatusRenderTargetContract_WhenSourceIsInspected_UsesSingleFloatParameter()
    {
        string adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "TemperatureStatusAvailabilityPatches.cs");
        string source = File.ReadAllText(adapterPath);
        const string expectedContractPattern =
            @"typeof\(FetchListStatusItemUpdater\),\s*" +
            @"""Render200ms"",\s*" +
            @"DeclaredMemberVisibility\.Public,\s*" +
            @"typeof\(void\),\s*" +
            @"new\[\]\s*\{\s*typeof\(float\)\s*\}\s*\)";

        await Assert.That(Regex.IsMatch(
                source,
                expectedContractPattern,
                RegexOptions.CultureInvariant)).IsTrue().Because("The Render200ms resolver must model the installed public instance " +
            "void method's single System.Single parameter.");
    }

    [Test]
    public async Task AuthoritativeFetchTargetContracts_WhenInstalledShapesMatch_ReturnExactMethods()
    {
        var addChore = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(GlobalChoreProviderFetchContractFixture),
            "AddChore",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(ChoreContractFixture)]);
        var removeChore = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(GlobalChoreProviderFetchContractFixture),
            "RemoveChore",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(ChoreContractFixture)]);
        var updateStorageFetchableBits =
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchContractFixture),
                "UpdateStorageFetchableBits",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());
        var clearableHasDestination =
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchContractFixture),
                "ClearableHasDestination",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                [typeof(PickupableContractFixture)]);
        var onTagsChanged = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(FetchChoreTagChangeContractFixture),
            "OnTagsChanged",
            DeclaredMemberVisibility.NonPublic,
            typeof(void),
            [typeof(object)]);

        await AssertExactInstanceMethod(
            addChore,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: true,
            [typeof(ChoreContractFixture)]);
        await AssertExactInstanceMethod(
            removeChore,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: true,
            [typeof(ChoreContractFixture)]);
        await AssertExactInstanceMethod(
            updateStorageFetchableBits,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: false,
            Array.Empty<Type>());
        await AssertExactInstanceMethod(
            clearableHasDestination,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: true,
            typeof(bool),
            [typeof(PickupableContractFixture)]);
        await AssertExactInstanceMethod(
            onTagsChanged,
            typeof(FetchChoreTagChangeContractFixture),
            isPublic: false,
            [typeof(object)]);
    }

    [Test]
    public async Task AuthoritativeFetchTargetContracts_WhenOnlyOverloadsArePresent_ThrowContractViolations()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "AddChore",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(ChoreContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "RemoveChore",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(ChoreContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "UpdateStorageFetchableBits",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>())).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "ClearableHasDestination",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                [typeof(PickupableContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task FetchChoreOnTagsChangedContract_WhenParameterTypeChanges_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchChoreChangedTagEventContractFixture),
                "OnTagsChanged",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                [typeof(object)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task AuthoritativeFetchTraversalInstructionContract_WhenCapturedInstalledShapeMatches_ResolvesParentAndSelectedChoreAnchorsOnce()
    {
        var anchors = RequireAuthoritativeFetchTraversalInstructionAnchors(
            CapturedAuthoritativeFetchTraversalInstructions());

        await Assert.That(anchors.ParentWorldSectionStartIndex).IsEqualTo(0);
        await Assert.That(anchors.SelectedFetchChoreIndex).IsEqualTo(13);
        await Assert.That(anchors.SortedWorldIdsLocalIndex).IsEqualTo(2);
        await Assert.That(anchors.SortedWorldIdIndexLocalIndex).IsEqualTo(3);
        await Assert.That(anchors.SelectedFetchChoreLocalIndex).IsEqualTo(6);
    }

    [Test]
    public async Task AuthoritativeFetchTraversalInstructionContract_WhenSecondFetchMapTraversalExists_ThrowsContractViolation()
    {
        var instructions = CapturedAuthoritativeFetchTraversalInstructions();
        instructions.AddRange(instructions.GetRange(0, 6));

        await Assert.That(() =>
            RequireAuthoritativeFetchTraversalInstructionAnchors(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task AuthoritativeFetchTraversalInstructionContract_WhenSelectedFetchChoreAnchorIsMissing_ThrowsContractViolation()
    {
        var instructions = CapturedAuthoritativeFetchTraversalInstructions();
        instructions[15] = FieldInstruction("FetchChore.unrelatedTags");

        await Assert.That(() =>
            RequireAuthoritativeFetchTraversalInstructionAnchors(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task AuthoritativeFetchAdapterSource_WhenInspected_ExposesManualFailClosedContractsOnly()
    {
        var adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiAuthoritativeFetchTemperatureEligibilityPatches.cs");
        await Assert.That(File.Exists(adapterPath)).IsTrue().Because($"Missing authoritative fetch adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);

        await Assert.That(source).Contains("ResolveGlobalChoreProviderAddChoreTarget");
        await Assert.That(source).Contains("ResolveGlobalChoreProviderRemoveChoreTarget");
        await Assert.That(source).Contains("ResolveFetchChoreOnTagsChangedTarget");
        await Assert.That(source).Contains("ResolveGlobalChoreProviderUpdateStorageFetchableBitsTarget");
        await Assert.That(source).Contains("ResolveGlobalChoreProviderClearableHasDestinationTarget");
        await Assert.That(source).Contains("UpdateStorageFetchableBitsTranspiler");
        await Assert.That(source).Contains("BeginParentWorldFetchMapSection");
        await Assert.That(source).Contains("RecordSelectedFetchChore");
        await Assert.That(source).Contains("TryPublishFetchTemperatureEligibility");
        await Assert.That(source).Contains("ClearableDestinationSweepEligibility.AllowsClearing");
        await Assert.That(source.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("operand.ToString", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("HashSet<Tag>[]", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("storageFetchableTagsPerTemperatureIndex", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AuthoritativeFetchMapFieldContract_WhenComparedWithInstalledOni_UsesCurrentPublicInstanceField()
    {
        string? managedAssemblyDirectory = Environment.GetEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        if (string.IsNullOrWhiteSpace(managedAssemblyDirectory))
        {
            throw new TUnit.Assertions.Exceptions.AssertionException(
                "The ONI mod pipeline must provide " +
                "ONI_MANAGED_ASSEMBLY_DIRECTORY.");
        }

        string assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                managedAssemblyDirectory,
                "Assembly-CSharp.dll");
        DeliveryTemperatureAssemblyMetadataReader.AssertPublicInstanceField(
            assemblyPath,
            "GlobalChoreProvider",
            "fetchMap");

        string adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiAuthoritativeFetchTemperatureEligibilityPatches.cs");
        string source = File.ReadAllText(adapterPath);
        const string expectedContractPattern =
            @"RequireField\(\s*" +
            @"typeof\(GlobalChoreProvider\),\s*" +
            @"""fetchMap"",\s*" +
            @"DeclaredMemberVisibility\.Public,\s*" +
            @"FieldStorageKind\.Instance,\s*" +
            @"typeof\(Dictionary<int,\s*List<FetchChore>>\)\s*\)";

        await Assert.That(Regex.IsMatch(
                source,
                expectedContractPattern,
                RegexOptions.CultureInvariant)).IsTrue().Because("The authoritative fetch-map reflection contract must use the " +
            "public instance visibility verified in the installed ONI assembly.");
    }

    [Test]
    public async Task KleiPickupGroupingTargetContracts_WhenInstalledShapesMatch_ReturnExactMethods()
    {
        var updatePickups = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(FetchablesByPrefabIdPickupGroupingContractFixture),
            "UpdatePickups",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(NavigatorPickupGroupingContractFixture), typeof(int)]);
        var comparerType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(FetchManagerPickupGroupingContractFixture),
            "PickupComparerIncludingPriority",
            DeclaredMemberVisibility.NonPublic);
        var compare = HarmonyPatchContractVerifier.RequireStaticMethod(
            comparerType,
            "Compare",
            DeclaredMemberVisibility.NonPublic,
            typeof(int),
            [
                typeof(PickupGroupingCandidateContractFixture),
                typeof(PickupGroupingCandidateContractFixture)
            ]);

        await AssertExactInstanceMethod(
            updatePickups,
            typeof(FetchablesByPrefabIdPickupGroupingContractFixture),
            isPublic: true,
            [typeof(NavigatorPickupGroupingContractFixture), typeof(int)]);
        await Assert.That(compare.DeclaringType).IsSameReferenceAs(comparerType);
        await Assert.That(compare.IsPrivate).IsTrue();
        await Assert.That(compare.IsStatic).IsTrue();
        await Assert.That(compare.ReturnType).IsSameReferenceAs(typeof(int));
        await Assert.That(compare.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray()).IsEquivalentTo(new[]
            {
                typeof(PickupGroupingCandidateContractFixture),
                typeof(PickupGroupingCandidateContractFixture)
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task KleiPickupGroupingCompareContract_WhenCandidateTypeChanges_ThrowsContractViolation()
    {
        var comparerType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(FetchManagerChangedPickupGroupingContractFixture),
            "PickupComparerIncludingPriority",
            DeclaredMemberVisibility.NonPublic);

        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                comparerType,
                "Compare",
                DeclaredMemberVisibility.NonPublic,
                typeof(int),
                [
                    typeof(PickupGroupingCandidateContractFixture),
                    typeof(PickupGroupingCandidateContractFixture)
                ])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiPickupGroupingInstructionContract_WhenCapturedInstalledShapesMatch_ResolvesComparatorAndSuppressionAnchorsOnce()
    {
        var anchors = RequireKleiPickupGroupingInstructionAnchors(
            CapturedPickupGroupingComparatorInstructions(),
            CapturedPickupDuplicateSuppressionInstructions());

        await Assert.That(anchors.ComparatorExtensionIndex).IsEqualTo(0);
        await Assert.That(anchors.DuplicateSuppressionExtensionIndex).IsEqualTo(6);
        await Assert.That(anchors.PreviousPickupLocalIndex).IsEqualTo(0);
        await Assert.That(anchors.CurrentPickupLocalIndex).IsEqualTo(6);
        await Assert.That(anchors.SharedSemanticKeyIdentity).IsEqualTo("TemperatureEligibilityClassKey");
    }

    [Test]
    public async Task KleiPickupGroupingInstructionContract_WhenComparatorAnchorIsDuplicated_ThrowsContractViolation()
    {
        var comparatorInstructions =
            CapturedPickupGroupingComparatorInstructions();
        comparatorInstructions.AddRange(
            CapturedPickupGroupingComparatorInstructions());

        await Assert.That(() =>
            RequireKleiPickupGroupingInstructionAnchors(
                comparatorInstructions,
                CapturedPickupDuplicateSuppressionInstructions())).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiPickupGroupingInstructionContract_WhenSuppressionAnchorIsMissing_ThrowsContractViolation()
    {
        var suppressionInstructions =
            CapturedPickupDuplicateSuppressionInstructions();
        suppressionInstructions[13] = OperationInstruction("ldc.i4.0");

        await Assert.That(() =>
            RequireKleiPickupGroupingInstructionAnchors(
                CapturedPickupGroupingComparatorInstructions(),
                suppressionInstructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiPickupGroupingInstructionContract_WhenCandidateFieldTypeChanges_ThrowsContractViolation()
    {
        var comparatorInstructions =
            CapturedPickupGroupingComparatorInstructions();
        comparatorInstructions[1] = FieldAddressInstruction(
            "ChangedPickupCandidate.masterPriority");

        await Assert.That(() =>
            RequireKleiPickupGroupingInstructionAnchors(
                comparatorInstructions,
                CapturedPickupDuplicateSuppressionInstructions())).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public void KleiPickupWorkerReadContract_WhenEveryReadIsVerifiedManagedState_AcceptsContract()
    {
        RequireVerifiedKleiPickupWorkerReadContract(
            CapturedVerifiedKleiPickupWorkerReads());
    }

    [Test]
    public async Task KleiPickupWorkerReadContract_WhenPickupIdentityRequiresUnityNativeCall_ThrowsContractViolation()
    {
        var workerReads = CapturedVerifiedKleiPickupWorkerReads();
        workerReads[4] = "UnityEngine.Object.GetInstanceID";

        await Assert.That(() =>
            RequireVerifiedKleiPickupWorkerReadContract(workerReads)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiPickupGroupingAdapterSource_WhenInspected_UsesOneFullKeyAndOnlyVerifiedManagedWorkerReads()
    {
        var adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiPickupTemperatureGroupingPatches.cs");
        await Assert.That(File.Exists(adapterPath)).IsTrue().Because($"Missing Klei pickup grouping adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);
        var sharedReadContractSource = File.ReadAllText(
            ResolveProductionSourcePath(
                "PickupTemperatureGroupingAdapters",
                "PickupTemperatureGroupingWorkerReadContractVerifier.cs"));

        await Assert.That(source).Contains("ResolveFetchablesByPrefabIdUpdatePickupsTarget");
        await Assert.That(source).Contains("ResolvePickupComparerIncludingPriorityCompareTarget");
        await Assert.That(source).Contains("VerifyKleiPickupGroupingPatchContracts");
        await Assert.That(source).Contains("PatchProcessor.GetOriginalInstructions");
        await Assert.That(source).Contains("UpdatePickupsPrefix");
        await Assert.That(source).Contains("UpdatePickupsTranspiler");
        await Assert.That(source).Contains("UpdatePickupsPostfix");
        await Assert.That(source).Contains("UpdatePickupsFinalizer");
        await Assert.That(source).Contains("PickupComparerTranspiler");
        await Assert.That(source).Contains("GetTemperatureEligibilityClassKey");
        await Assert.That(source).Contains("TemperatureEligibilityClassKey.CompareTo");
        await Assert.That(source).Contains("TemperatureEligibilityClassKey.Equals");
        await Assert.That(source).Contains("PickupTemperatureGroupingWorkerReadContractVerifier");
        await Assert.That(sharedReadContractSource).Contains("KPrefabID.HasTag");
        await Assert.That(sharedReadContractSource).Contains("PrimaryElement.InternalTemperature");
        await Assert.That(source).Contains("kPrefabId.InstanceID");
        await Assert.That(source).Contains("kPrefabId.PrefabTag");
        await Assert.That(source).Contains("ThreadConfinedSessionSlot");
        await Assert.That(CountOrdinalOccurrences(
                source,
                "GetTemperatureEligibilityClassKey(")).IsEqualTo(5);
        await Assert.That(source.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("operand.ToString", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains(".LocalIndex()", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("GetComponent", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("GetInstanceID", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("ClusterManager", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("PrimaryElement.Temperature", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AsyncLocal", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains(".PartitionDefinitionId", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains(".IntervalOrdinal", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task KleiPickupGroupingPrefix_WhenSourceIsInspected_BindsNavigatorByVerifiedPosition()
    {
        string adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiPickupTemperatureGroupingPatches.cs");
        string source = File.ReadAllText(adapterPath);

        await Assert.That(Regex.IsMatch(
                source,
                @"UpdatePickupsPrefix\s*\(\s*Navigator\s+__0\s*,",
                RegexOptions.CultureInvariant)).IsTrue().Because("The prefix must bind Harmony's verified first target argument by " +
            "position so metadata-only parameter renames cannot break patching.");
        await Assert.That(Regex.IsMatch(
                source,
                @"ResolveNavigatorParentWorldId\s*\(\s*__0\s*,",
                RegexOptions.CultureInvariant)).IsTrue().Because("The positionally injected Navigator must remain the parent-world " +
            "resolution input.");
    }

    [Test]
    public async Task KleiDirectEligibilityTargetContracts_WhenInstalledShapesMatch_ReturnExactMethods()
    {
        var isFetchablePickup =
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(FetchManagerDirectEligibilityContractFixture),
                "IsFetchablePickup",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                [
                    typeof(DirectPickupableContractFixture),
                    typeof(DirectFetchChoreContractFixture),
                    typeof(DirectStorageContractFixture)
                ]);
        var collectChores =
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClearableManagerDirectEligibilityContractFixture),
                "CollectChores",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [
                    typeof(List<DirectGlobalFetchContractFixture>),
                    typeof(DirectChoreConsumerStateContractFixture),
                    typeof(List<DirectChoreContextContractFixture>),
                    typeof(List<DirectChoreContextContractFixture>)
                ]);
        var begin = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture),
            "Begin",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(DirectChoreContextContractFixture)]);
        var candidateDelegate =
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchAreaCandidateClosureDirectEligibilityContractFixture),
                "EvaluateCandidate",
                DeclaredMemberVisibility.NonPublic,
                typeof(DirectIterationInstructionContractFixture),
                [typeof(object), typeof(object)]);
        var closureOwnerField = HarmonyPatchContractVerifier.RequireField(
            typeof(FetchAreaCandidateClosureDirectEligibilityContractFixture),
            "StatesInstance",
            DeclaredMemberVisibility.Public,
            FieldStorageKind.Instance,
            typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture));

        await Assert.That(isFetchablePickup.IsStatic).IsTrue();
        await Assert.That(isFetchablePickup.IsPublic).IsTrue();
        await Assert.That(isFetchablePickup.ReturnType).IsSameReferenceAs(typeof(bool));
        await AssertExactInstanceMethod(
            collectChores,
            typeof(ClearableManagerDirectEligibilityContractFixture),
            isPublic: true,
            [
                typeof(List<DirectGlobalFetchContractFixture>),
                typeof(DirectChoreConsumerStateContractFixture),
                typeof(List<DirectChoreContextContractFixture>),
                typeof(List<DirectChoreContextContractFixture>)
            ]);
        await AssertExactInstanceMethod(
            begin,
            typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture),
            isPublic: true,
            [typeof(DirectChoreContextContractFixture)]);
        await AssertExactInstanceMethod(
            candidateDelegate,
            typeof(FetchAreaCandidateClosureDirectEligibilityContractFixture),
            isPublic: false,
            typeof(DirectIterationInstructionContractFixture),
            [typeof(object), typeof(object)]);
        await Assert.That(closureOwnerField.FieldType).IsSameReferenceAs(typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture));
    }

    [Test]
    public async Task KleiDirectEligibilityIsFetchablePickupContract_WhenReturnTypeChanges_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(FetchManagerChangedDirectEligibilityContractFixture),
                "IsFetchablePickup",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                [
                    typeof(DirectPickupableContractFixture),
                    typeof(DirectFetchChoreContractFixture),
                    typeof(DirectStorageContractFixture)
                ])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiDirectEligibilityDelegateContract_WhenClosureOwnerFieldChanges_ThrowsContractViolation()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FetchAreaChangedCandidateClosureDirectEligibilityContractFixture),
                "StatesInstance",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture))).ThrowsExactly<HarmonyPatchContractViolationException>();

        var instructions = CapturedFetchAreaCandidateDelegateInstructions();
        instructions[1] = FieldInstruction(
            "FetchAreaCandidateClosure.changedStatesInstance");
        await Assert.That(() =>
            RequireKleiDirectEligibilityInstructionAnchors(
                CapturedClearableCollectChoresInstructions(),
                CapturedFetchAreaBeginInstructions(),
                instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiDirectEligibilityInstructionContract_WhenCapturedInstalledShapesMatch_ResolvesEveryTypedAnchorOnce()
    {
        var anchors = RequireKleiDirectEligibilityInstructionAnchors(
            CapturedClearableCollectChoresInstructions(),
            CapturedFetchAreaBeginInstructions(),
            CapturedFetchAreaCandidateDelegateInstructions());

        await Assert.That(anchors.ClearablePickupableLocalIndex).IsEqualTo(5);
        await Assert.That(anchors.ClearableFetchLocalIndex).IsEqualTo(10);
        await Assert.That(anchors.CandidateFetchChoreLocalIndex).IsEqualTo(14);
        await Assert.That(anchors.DelegatePickupableLocalIndex).IsEqualTo(0);
        await Assert.That(anchors.ClearableEligibilityExtensionIndex).IsEqualTo(20);
        await Assert.That(anchors.FetchChoreContainmentExtensionIndex).IsEqualTo(6);
        await Assert.That(anchors.DelegateCanReachCallIndex).IsEqualTo(6);
    }

    [Test]
    public async Task KleiDirectEligibilityInstructionContract_WhenTwoCanReachCallsMatch_ThrowsContractViolation()
    {
        var delegateInstructions =
            CapturedFetchAreaCandidateDelegateInstructions();
        delegateInstructions.AddRange(
            CapturedFetchAreaCandidateDelegateInstructions());

        await Assert.That(() =>
            RequireKleiDirectEligibilityInstructionAnchors(
                CapturedClearableCollectChoresInstructions(),
                CapturedFetchAreaBeginInstructions(),
                delegateInstructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiDirectEligibilityInstructionContract_WhenDirectResultBranchIsMissing_ThrowsContractViolation()
    {
        var delegateInstructions =
            CapturedFetchAreaCandidateDelegateInstructions();
        delegateInstructions[7] = BranchInstruction("brfalse.s");

        await Assert.That(() =>
            RequireKleiDirectEligibilityInstructionAnchors(
                CapturedClearableCollectChoresInstructions(),
                CapturedFetchAreaBeginInstructions(),
                delegateInstructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task KleiDirectEligibilityAdapterSource_WhenInspected_UsesCentralAllocationFreeChecksAndManualFailClosedContracts()
    {
        var adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiDirectDeliveryEligibilityPatches.cs");
        await Assert.That(File.Exists(adapterPath)).IsTrue().Because($"Missing Klei direct delivery eligibility adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);

        await Assert.That(source).Contains("ResolveFetchManagerIsFetchablePickupTarget");
        await Assert.That(source).Contains("ResolveClearableManagerCollectChoresTarget");
        await Assert.That(source).Contains("ResolveFetchAreaChoreStatesInstanceBeginTarget");
        await Assert.That(source).Contains("ResolveFetchAreaChoreCandidateDelegateTarget");
        await Assert.That(source).Contains("VerifyKleiDirectDeliveryEligibilityPatchContracts");
        await Assert.That(source).Contains("IsFetchablePickupPostfix");
        await Assert.That(source).Contains("ClearableManagerCollectChoresTranspiler");
        await Assert.That(source).Contains("FetchAreaChoreBeginTranspiler");
        await Assert.That(source).Contains("FetchAreaCandidateDelegateTranspiler");
        await Assert.That(source).Contains("IsPickupAllowedForDestination");
        await Assert.That(source).Contains("FetchChoreTemperatureConstraintContainment.CanCombine");
        await Assert.That(source).Contains("TemperatureLimitComponents.TryGetConstraint");
        await Assert.That(source).Contains("constraint.Allows");
        await Assert.That(CountOrdinalOccurrences(source, "constraint.Allows(")).IsEqualTo(1).Because("The direct adapter must delegate one canonical temperature decision rather than duplicating its bounds.");
        await Assert.That(CountOrdinalOccurrences(source, "primaryElement.Temperature")).IsEqualTo(1).Because("The direct adapter must read a candidate's live temperature exactly once in its shared check.");
        await Assert.That(source).Contains("if (!__result)");
        await Assert.That(source).Contains("if (!consumer.CanReach(approachable))");
        await Assert.That(source).Contains("PatchProcessor.GetOriginalInstructions");
        await Assert.That(source.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("operand.ToString", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("<>c__DisplayClass", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("GetComponent", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("CaptureSnapshot", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("CurrentFetchTemperatureEligibility", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("HashSet<Tag>[]", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("System.Linq", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("TemperatureLimit.Get", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("ValueTuple", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Tuple<", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task WorldInventoryInstructionContract_WhenCapturedInstalledShapeMatches_ResolvesEverySemanticAnchorOnce()
    {
        var anchors = RequireWorldInventoryInstructionAnchors(
            CapturedWorldInventoryUpdateInstructions());

        await Assert.That(anchors.InventoryEntryCaptureIndex <
            anchors.ResourceTagStartIndex).IsTrue();
        await Assert.That(anchors.ResourceTagStartIndex <
            anchors.FilteredPickupContributionIndex).IsTrue();
        await Assert.That(anchors.FilteredPickupContributionIndex <
            anchors.ResourceTagCompletionIndex).IsTrue();
        await Assert.That(anchors.ResourceTagLocalIndex).IsEqualTo(4);
        await Assert.That(anchors.AccumulatedAmountLocalIndex).IsEqualTo(5);
        await Assert.That(anchors.PickupableLocalIndex).IsEqualTo(7);
    }

    [Test]
    public async Task WorldInventoryEnumeratorContract_WhenSourceIsInspected_UsesInstalledInterfaceDispatch()
    {
        string adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiWorldInventoryTemperaturePatches.cs");
        string source = File.ReadAllText(adapterPath);
        const string expectedContractPattern =
            @"RequireInstanceMethod\(\s*" +
            @"typeof\(IEnumerable<Pickupable>\),\s*" +
            @"""GetEnumerator"",\s*" +
            @"DeclaredMemberVisibility\.Public,\s*" +
            @"typeof\(IEnumerator<Pickupable>\),\s*" +
            @"Array\.Empty<Type>\(\)\s*\)";

        await Assert.That(Regex.IsMatch(
                source,
                expectedContractPattern,
                RegexOptions.CultureInvariant)).IsTrue().Because("WorldInventory.Update dispatches pickup enumeration through " +
            "IEnumerable<Pickupable>.GetEnumerator in the installed ONI runtime.");
    }

    [Test]
    public async Task WorldInventoryInstructionContract_WhenResourceTagStartIsMissing_ThrowsContractViolation()
    {
        var instructions = CapturedWorldInventoryUpdateInstructions();
        instructions[2] = CallInstruction("Wrong.get_Key");

        await Assert.That(() =>
            RequireWorldInventoryInstructionAnchors(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task WorldInventoryInstructionContract_WhenFilteredPickupContributionIsDuplicated_ThrowsContractViolation()
    {
        var instructions = CapturedWorldInventoryUpdateInstructions();
        instructions.AddRange(instructions.GetRange(6, 10));

        await Assert.That(() =>
            RequireWorldInventoryInstructionAnchors(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task WorldInventoryInstructionContract_WhenPickupGetterIsNotTotalAmount_ThrowsContractViolation()
    {
        var instructions = CapturedWorldInventoryUpdateInstructions();
        instructions[13] = CallInstruction(
            "Pickupable.get_FetchTotalAmount");

        await Assert.That(() =>
            RequireWorldInventoryInstructionAnchors(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task FetchListStatusInstructionContract_WhenCapturedInstalledShapeMatches_ResolvesEarlyBranchAndAssignmentOnce()
    {
        var anchor = RequireFetchListStatusInstructionAnchor(
            CapturedFetchListStatusRenderInstructions());

        await Assert.That(anchor.WorldIdLocalIndex).IsEqualTo(1);
        await Assert.That(anchor.FetchListLocalIndex).IsEqualTo(28);
        await Assert.That(anchor.ResourceTagLocalIndex).IsEqualTo(33);
        await Assert.That(anchor.RemainingAmountLocalIndex).IsEqualTo(34);
        await Assert.That(anchor.FetchableAmountLocalIndex).IsEqualTo(37);
        await Assert.That(anchor.MinimumRequiredAmountLocalIndex).IsEqualTo(38);
    }

    [Test]
    public async Task FetchListStatusInstructionContract_WhenFetchableAssignmentIsMissing_ThrowsContractViolation()
    {
        var instructions = CapturedFetchListStatusRenderInstructions();
        instructions[15] = StoreLocalInstruction(99);

        await Assert.That(() =>
            RequireFetchListStatusInstructionAnchor(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task FetchListStatusInstructionContract_WhenEarlyInsufficientBranchIsReordered_ThrowsContractViolation()
    {
        var instructions = CapturedFetchListStatusRenderInstructions();
        (instructions[20], instructions[21]) =
            (instructions[21], instructions[20]);

        await Assert.That(() =>
            RequireFetchListStatusInstructionAnchor(instructions)).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task InventoryAndStatusAdapterSources_WhenInspected_ExposeManualFailClosedContractsOnly()
    {
        var inventoryAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiWorldInventoryTemperaturePatches.cs");
        var statusAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "TemperatureStatusAvailabilityPatches.cs");
        await Assert.That(File.Exists(inventoryAdapterPath)).IsTrue().Because($"Missing Klei inventory adapter source {inventoryAdapterPath}.");
        await Assert.That(File.Exists(statusAdapterPath)).IsTrue().Because($"Missing status adapter source {statusAdapterPath}.");
        var inventorySource = File.ReadAllText(inventoryAdapterPath);
        var statusSource = File.ReadAllText(statusAdapterPath);
        var combinedSource =
            inventorySource + Environment.NewLine + statusSource;

        await Assert.That(inventorySource).Contains("ResolveWorldInventoryUpdateTarget");
        await Assert.That(inventorySource).Contains("WorldInventoryUpdateTranspiler");
        await Assert.That(inventorySource).Contains("WorldInventoryUpdatePrefix");
        await Assert.That(inventorySource).Contains("WorldInventoryUpdatePostfix");
        await Assert.That(inventorySource).Contains("WorldInventoryUpdateFinalizer");
        await Assert.That(inventorySource).Contains("BeginResourceTagEnumeration");
        await Assert.That(inventorySource).Contains("IsTemperatureCollectionActive");
        await Assert.That(inventorySource).Contains("ObserveResourceTagForCoverage");
        await Assert.That(inventorySource).Contains("RecordFilteredPickupTemperatureAmount");
        await Assert.That(inventorySource).Contains("CompleteResourceTagEnumeration");
        await Assert.That(inventorySource).Contains("PublishWorldResourceTagCoverage");
        await Assert.That(inventorySource).Contains("PublishWorldResourceTemperatureSeries");
        await Assert.That(inventorySource).Contains("___firstUpdate");
        await Assert.That(statusSource).Contains("ResolveFetchListStatusItemUpdaterRender200msTarget");
        await Assert.That(statusSource).Contains("Render200msTranspiler");
        await Assert.That(statusSource).Contains("ReplaceFetchableAmountWhenInventoryIsComplete");
        await Assert.That(statusSource).Contains("TemperatureStatusAvailabilityDecision.ShouldTryReplacement");
        await Assert.That(statusSource).Contains("GetTemperatureConstrainedAmountAvailability");
        await Assert.That(CountOrdinalOccurrences(
                combinedSource,
                "HarmonyPatchContractVerifier.RequireSingleMatch") >= 5).IsTrue();
        await Assert.That(CountOrdinalOccurrences(
                inventorySource,
                "HarmonyPatchContractVerifier.RequireInstanceMethod") >= 1).IsTrue();
        await Assert.That(CountOrdinalOccurrences(
                statusSource,
                "HarmonyPatchContractVerifier.RequireInstanceMethod") >= 1).IsTrue();
        await Assert.That(combinedSource.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("CheckTemperatureForStatusItems", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("WorldContainers", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("worldAmounts", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("FastTrack", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task FastTrackCompatibilityInspectorSource_WhenInspected_CentralizesExactBuildPolicyAndSeparatesIdentityIo()
    {
        var inspectorPath = ResolveProductionSourcePath(
            Path.Combine(
                "FastTrackCompatibility",
                "FeatureContractVerification"),
            "FastTrackCompatibilityInspector.cs");
        var identityReaderPath = ResolveProductionSourcePath(
            Path.Combine(
                "FastTrackCompatibility",
                "FeatureContractVerification"),
            "FastTrackAssemblyFileIdentityReader.cs");
        var supportedBuildCatalogPath = ResolveProductionSourcePath(
            Path.Combine(
                "FastTrackCompatibility",
                "FeatureContractVerification"),
            "FastTrackSupportedAssemblyBuildCatalog.cs");
        await Assert.That(File.Exists(inspectorPath)).IsTrue();
        await Assert.That(File.Exists(identityReaderPath)).IsTrue();
        await Assert.That(File.Exists(supportedBuildCatalogPath)).IsTrue();
        var inspectorSource = File.ReadAllText(inspectorPath);
        var identityReaderSource = File.ReadAllText(identityReaderPath);
        var supportedBuildCatalogSource =
            File.ReadAllText(supportedBuildCatalogPath);

        await Assert.That(inspectorSource).Contains("HarmonyPatchContractVerifier.RequireSingleMatch");
        await Assert.That(inspectorSource).Contains("IFastTrackAssemblyFileIdentityReader");
        await Assert.That(inspectorSource).Contains("FastTrackSupportedAssemblyBuildCatalog");
        await Assert.That(inspectorSource).Contains("supportedAssemblyBuildCatalog.Contains(");
        await Assert.That(CountOrdinalOccurrences(
                inspectorSource,
                "assemblyFileIdentityReader.Read(")).IsEqualTo(1).Because("One loaded FastTrack assembly must produce one shared physical-file identity read.");
        await Assert.That(inspectorSource.Contains("using HarmonyLib", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("using UnityEngine", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("using PeterHan", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("Type.GetType", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("Assembly.GetAssemblies", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("File.Open", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("FileVersionInfo", StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains("SHA256", StringComparison.Ordinal)).IsFalse();

        await Assert.That(inspectorSource.Contains(
            "new Version(0, 18, 4, 0)",
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains(
            "new Version(0, 18, 5, 0)",
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains(
            "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD",
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(inspectorSource.Contains(
            "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B",
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(supportedBuildCatalogSource.Contains(
            "System.IO",
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(supportedBuildCatalogSource.Contains(
            "System.Net",
            StringComparison.Ordinal)).IsFalse();

        string supportedBuildCatalogDirectory =
            Path.GetDirectoryName(supportedBuildCatalogPath)!;
        string productionSourceDirectory = Directory.GetParent(
            Directory.GetParent(supportedBuildCatalogDirectory)!.FullName)!
            .FullName;

        string gameplayActivationCoreDirectory = Path.Combine(
            productionSourceDirectory,
            "GameplayActivation",
            "Core");
        string gameplayActivationCoreSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    gameplayActivationCoreDirectory,
                    "*.cs",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        await Assert.That(gameplayActivationCoreSource.Contains(
                "FastTrackSupportedAssemblyBuildCatalog",
                StringComparison.Ordinal)).IsFalse();

        await Assert.That(identityReaderSource).Contains("fastTrackAssembly.Location");
        await Assert.That(identityReaderSource).Contains("FileVersionInfo.GetVersionInfo");
        await Assert.That(identityReaderSource).Contains("SHA256.Create()");
        await Assert.That(identityReaderSource).Contains("ComputeHash(stream)");
        await Assert.That(identityReaderSource.Contains("using HarmonyLib", StringComparison.Ordinal)).IsFalse();
        await Assert.That(identityReaderSource.Contains("using UnityEngine", StringComparison.Ordinal)).IsFalse();
        await Assert.That(identityReaderSource.Contains("using PeterHan", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task FastTrackWorldInventoryAdapterSource_WhenInspected_UsesBoundContractsAndIncrementalPublicationWithoutHotPathDiscovery()
    {
        var adapterPath = ResolveProductionSourcePath(
            Path.Combine(
                "FastTrackCompatibility",
                "InventoryUpdateAdapters"),
            "FastTrackWorldInventoryTemperaturePatches.cs");
        await Assert.That(File.Exists(adapterPath)).IsTrue().Because($"Missing FastTrack world-inventory adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);

        await Assert.That(source).Contains("BindVerifiedWorldInventoryFeature");
        await Assert.That(source).Contains("ResolveBackgroundWorldInventoryRunUpdateTarget");
        await Assert.That(source).Contains("ResolveBackgroundWorldInventorySumTotalTarget");
        await Assert.That(source).Contains("BackgroundWorldInventoryRunUpdatePrefix");
        await Assert.That(source).Contains("BackgroundWorldInventoryRunUpdateTranspiler");
        await Assert.That(source).Contains("BackgroundWorldInventoryRunUpdatePostfix");
        await Assert.That(source).Contains("BackgroundWorldInventoryRunUpdateFinalizer");
        await Assert.That(source).Contains("BackgroundWorldInventorySumTotalTranspiler");
        await Assert.That(source).Contains("FastTrackWorldInventoryPublicationSession");
        await Assert.That(source).Contains("FastTrackVerifiedMember.WorldInventoryInventoryField");
        await Assert.That(source).Contains("CreateWorldInventoryEntriesReader");
        await Assert.That(source).Contains("DynamicMethod");
        await Assert.That(source).Contains("ThreadConfinedSessionSlot");
        await Assert.That(source).Contains("BeginResourceTagEnumeration");
        await Assert.That(source).Contains("RecordFilteredPickupTemperatureAmount");
        await Assert.That(source).Contains("GetCurrentPublicationSession");
        await Assert.That(source).Contains("CompleteResourceTagEnumeration");
        await Assert.That(source).Contains("GetWorldResourceTagCoverageRequirementState");
        await Assert.That(source).Contains("PublishCompleteWorldResourceAmounts");
        await Assert.That(source).Contains("PublishWorldResourceTagCoverage");
        await Assert.That(source).Contains("PublishWorldResourceTemperatureSeries");
        await Assert.That(source).Contains("___firstUpdate");
        await Assert.That(source).Contains("___worldContainer");
        await Assert.That(source).Contains("___worldInventory");
        await Assert.That(source).Contains("PrimaryElement primaryElement = pickupable.PrimaryElement");
        await Assert.That(CountOrdinalOccurrences(
                source,
                "GetWorldResourceTagCoverageRequirementState(")).IsEqualTo(1);
        await Assert.That(CountOrdinalOccurrences(source, "worldInventoryEntries.Keys")).IsEqualTo(1);
        await Assert.That(source.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Type.GetType", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Assembly.GetAssemblies", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("FileVersionInfo", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("SHA256", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("operand.ToString", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("GetComponent", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("ClusterManager", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("WorldContainers", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("StartUpdateAll", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("System.Linq", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("FastTrackOptions", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("FieldInfo.GetValue", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("MethodInfo.Invoke", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("DynamicInvoke", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains(
                "private static bool IsTemperatureCollectionActive",
                StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AddTemperatureAmount_WhenPrimaryElementIsMissing_IsNotCalledByAdapter()
    {
        var adapterPath = ResolveProductionSourcePath(
            Path.Combine(
                "FastTrackCompatibility",
                "InventoryUpdateAdapters"),
            "FastTrackWorldInventoryTemperaturePatches.cs");
        await Assert.That(File.Exists(adapterPath)).IsTrue();
        var source = File.ReadAllText(adapterPath);
        int primaryElementCaptureIndex = source.IndexOf(
            "PrimaryElement primaryElement = pickupable.PrimaryElement",
            StringComparison.Ordinal);
        int missingPrimaryElementGuardIndex = source.IndexOf(
            "if (primaryElement != null)",
            primaryElementCaptureIndex,
            StringComparison.Ordinal);
        int addTemperatureAmountIndex = source.IndexOf(
            ".AddTemperatureAmount(",
            missingPrimaryElementGuardIndex,
            StringComparison.Ordinal);

        await Assert.That(primaryElementCaptureIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(primaryElementCaptureIndex < missingPrimaryElementGuardIndex).IsTrue().Because("The cached PrimaryElement must be checked before accumulation.");
        await Assert.That(missingPrimaryElementGuardIndex < addTemperatureAmountIndex).IsTrue().Because("The null guard must dominate AddTemperatureAmount.");
    }

    [Test]
    public async Task RecordFilteredPickupTemperatureAmount_WhenInspected_UsesPassedSessionWithoutPerPickupThreadLocalLookup()
    {
        var adapterPath = ResolveProductionSourcePath(
            Path.Combine(
                "FastTrackCompatibility",
                "InventoryUpdateAdapters"),
            "FastTrackWorldInventoryTemperaturePatches.cs");
        string source = File.ReadAllText(adapterPath);
        int hookStartIndex = source.IndexOf(
            "private static float RecordFilteredPickupTemperatureAmount(",
            StringComparison.Ordinal);
        int nextMethodIndex = source.IndexOf(
            "private static void CompleteResourceTagEnumeration(",
            hookStartIndex,
            StringComparison.Ordinal);

        await Assert.That(hookStartIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(nextMethodIndex > hookStartIndex).IsTrue().Because("The exact pickup hook body must have a bounded source region.");
        string hookSource = source.Substring(
            hookStartIndex,
            nextMethodIndex - hookStartIndex);
        await Assert.That(hookSource).Contains("FastTrackWorldInventoryPublicationSession publicationSession");
        await Assert.That(hookSource).Contains("PrimaryElement primaryElement = pickupable.PrimaryElement");
        await Assert.That(hookSource.Contains("TryGetCurrent", StringComparison.Ordinal)).IsFalse().Because("The accepted-pickup hook must consume its method-local cached " +
            "session instead of rereading thread-local state.");
    }

    [Test]
    public async Task GameDestroyInstancesContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(GameDestroyInstancesContractFixture),
            "DestroyInstances",
            DeclaredMemberVisibility.NonPublic,
            typeof(void),
            Array.Empty<Type>());

        await AssertExactInstanceMethod(
            method,
            typeof(GameDestroyInstancesContractFixture),
            isPublic: false,
            Array.Empty<Type>());
    }

    [Test]
    public async Task GameOnPrefabInitContract_WhenFixtureShapeMatches_ReturnsExactProtectedMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(GameOnPrefabInitContractFixture),
            "OnPrefabInit",
            DeclaredMemberVisibility.NonPublic,
            typeof(void),
            Array.Empty<Type>());

        await AssertExactInstanceMethod(
            method,
            typeof(GameOnPrefabInitContractFixture),
            isPublic: false,
            Array.Empty<Type>());
        await Assert.That(method.IsFamily).IsTrue().Because("The fixture must model ONI's exact protected lifecycle boundary.");
    }

    [Test]
    public void GameOnPrefabInitContract_WhenInstalledOniAssemblyIsInspected_MatchesProtectedInstanceVoidMethodWithoutParameters()
    {
        string? managedAssemblyDirectory = Environment.GetEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        if (string.IsNullOrWhiteSpace(managedAssemblyDirectory))
        {
            throw new TUnit.Assertions.Exceptions.AssertionException(
                "The ONI mod pipeline must provide " +
                "ONI_MANAGED_ASSEMBLY_DIRECTORY.");
        }
        string assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                managedAssemblyDirectory,
                "Assembly-CSharp.dll");

        DeliveryTemperatureAssemblyMetadataReader
            .AssertProtectedInstanceVoidMethodWithoutParameters(
                assemblyPath,
                "Game",
                "OnPrefabInit");
    }

    [Test]
    public async Task GameLoadAuthorityAdapterSource_WhenInspected_DelegatesOnlyToRuntimeInstaller()
    {
        string adapterPath = ResolveProductionSourcePath(
            "RuntimePatchInstallation",
            "DeliveryTemperatureGameLoadAuthorityPatches.cs");
        string source = File.ReadAllText(adapterPath);

        await Assert.That(source).Contains("ResolveGameOnPrefabInitTarget");
        await Assert.That(source).Contains("typeof(Game)");
        await Assert.That(source).Contains("\"OnPrefabInit\"");
        await Assert.That(source).Contains("DeclaredMemberVisibility.NonPublic");
        await Assert.That(source).Contains("typeof(void)");
        await Assert.That(source).Contains("Array.Empty<Type>()");
        await Assert.That(CountOrdinalOccurrences(
                source,
                "TryStartAuthorizedGameSession(__instance)")).IsEqualTo(1).Because("The game-load prefix must make exactly one installer delegation.");
        await Assert.That(source.Contains("EnsureGameSession", StringComparison.Ordinal)).IsFalse().Because("Only the verified installer success branch may publish a session.");
        await Assert.That(source.Contains("[Harmony", StringComparison.Ordinal)).IsFalse().Because("The adapter is installed explicitly and must remain undiscoverable.");
    }

    [Test]
    public async Task ClusterManagerRegisterWorldContainerContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(ClusterManagerWorldLifecycleContractFixture),
            "RegisterWorldContainer",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(WorldContainerLifecycleContractFixture)]);

        await AssertExactInstanceMethod(
            method,
            typeof(ClusterManagerWorldLifecycleContractFixture),
            isPublic: true,
            [typeof(WorldContainerLifecycleContractFixture)]);
    }

    [Test]
    public async Task ClusterManagerUnregisterWorldContainerContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(ClusterManagerWorldLifecycleContractFixture),
            "UnregisterWorldContainer",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(WorldContainerLifecycleContractFixture)]);

        await AssertExactInstanceMethod(
            method,
            typeof(ClusterManagerWorldLifecycleContractFixture),
            isPublic: true,
            [typeof(WorldContainerLifecycleContractFixture)]);
    }

    [Test]
    public async Task WorldContainerSetParentIdxContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(WorldContainerLifecycleContractFixture),
            "SetParentIdx",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(int)]);

        await AssertExactInstanceMethod(
            method,
            typeof(WorldContainerLifecycleContractFixture),
            isPublic: true,
            [typeof(int)]);
    }

    [Test]
    public async Task LifecycleTargetContracts_WhenOnlyOverloadsMatch_ThrowContractViolations()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GameDestroyInstancesOverloadOnlyFixture),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>())).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleOverloadOnlyFixture),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleOverloadOnlyFixture),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainerSetParentIdxOverloadOnlyFixture),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(int)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task LifecycleTargetContracts_WhenReturnTypesChange_ThrowContractViolations()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GameDestroyInstancesChangedReturnFixture),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>())).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleChangedReturnFixture),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleChangedReturnFixture),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainerSetParentIdxChangedReturnFixture),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(int)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task LifecycleTargetContracts_WhenStaticnessChanges_ThrowContractViolations()
    {
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GameDestroyInstancesStaticFixture),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>())).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleStaticFixture),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleStaticFixture),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)])).ThrowsExactly<HarmonyPatchContractViolationException>();
        await Assert.That(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainerSetParentIdxStaticFixture),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(int)])).ThrowsExactly<HarmonyPatchContractViolationException>();
    }

    [Test]
    public async Task LifecycleAdapterSources_WhenInspected_DeclareExactResolversWithoutPatchDiscoveryAttributes()
    {
        var shutdownAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "DeliveryTemperatureGameSessionShutdownPatches.cs");
        var worldTopologyAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "WorldParentTopologyPatches.cs");
        await Assert.That(File.Exists(shutdownAdapterPath)).IsTrue().Because($"Missing lifecycle adapter source {shutdownAdapterPath}.");
        await Assert.That(File.Exists(worldTopologyAdapterPath)).IsTrue().Because($"Missing world-topology adapter source {worldTopologyAdapterPath}.");
        var combinedSource =
            File.ReadAllText(shutdownAdapterPath) +
            Environment.NewLine +
            File.ReadAllText(worldTopologyAdapterPath);

        await Assert.That(CountOrdinalOccurrences(
                combinedSource,
                "HarmonyPatchContractVerifier.RequireInstanceMethod")).IsEqualTo(4);
        await Assert.That(combinedSource).Contains("ResolveGameDestroyInstancesTarget");
        await Assert.That(combinedSource).Contains("ResolveClusterManagerRegisterWorldContainerTarget");
        await Assert.That(combinedSource).Contains("ResolveClusterManagerUnregisterWorldContainerTarget");
        await Assert.That(combinedSource).Contains("ResolveWorldContainerSetParentIdxTarget");
        await Assert.That(combinedSource).Contains("typeof(Game)");
        await Assert.That(combinedSource).Contains("\"DestroyInstances\"");
        await Assert.That(combinedSource).Contains("Array.Empty<Type>()");
        await Assert.That(CountOrdinalOccurrences(
                combinedSource,
                "new[] { typeof(WorldContainer) }")).IsEqualTo(2);
        await Assert.That(combinedSource).Contains("\"RegisterWorldContainer\"");
        await Assert.That(combinedSource).Contains("\"UnregisterWorldContainer\"");
        await Assert.That(combinedSource).Contains("\"SetParentIdx\"");
        await Assert.That(combinedSource).Contains("new[] { typeof(int) }");
        await Assert.That(CountOrdinalOccurrences(combinedSource, "typeof(void)")).IsEqualTo(4);
        await Assert.That(combinedSource).Contains("DeclaredMemberVisibility.NonPublic");
        await Assert.That(combinedSource).Contains("DeclaredMemberVisibility.Public");
        await Assert.That(combinedSource.Contains("AccessTools", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("[HarmonyPatch", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("[HarmonyPrefix", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("[HarmonyPostfix", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("[HarmonyFinalizer", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("OnLoadLevel", StringComparison.Ordinal)).IsFalse();
        await Assert.That(combinedSource.Contains("OnPrefabInit", StringComparison.Ordinal)).IsFalse();
    }

    private static KleiDirectEligibilityInstructionAnchors
        RequireKleiDirectEligibilityInstructionAnchors(
            IReadOnlyList<TranspilerInstructionFixture>
                clearableCollectChoresInstructions,
            IReadOnlyList<TranspilerInstructionFixture>
                fetchAreaBeginInstructions,
            IReadOnlyList<TranspilerInstructionFixture>
                fetchAreaCandidateDelegateInstructions)
    {
        var clearableCandidateIndices = Enumerable.Range(
            0,
            clearableCollectChoresInstructions.Count).ToArray();
        int pickupableCaptureIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                clearableCandidateIndices,
                index => MatchesWindow(
                    clearableCollectChoresInstructions,
                    index,
                    FieldInstruction("ClearableManager.sortedClearables"),
                    LoadLocalInstruction(localIndex: null),
                    CallInstruction("List<SortedClearable>.get_Item"),
                    OperationInstruction("dup"),
                    FieldInstruction("SortedClearable.pickupable"),
                    StoreLocalInstruction(localIndex: null)),
                "ClearableManager.CollectChores typed pickupable capture");
        int clearablePickupableLocalIndex =
            clearableCollectChoresInstructions[pickupableCaptureIndex + 5]
                .LocalIndex!.Value;

        int fetchCaptureIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                clearableCandidateIndices,
                index => MatchesWindow(
                    clearableCollectChoresInstructions,
                    index,
                    LoadArgumentInstruction(1),
                    LoadLocalInstruction(localIndex: null),
                    CallInstruction("List<GlobalFetch>.get_Item"),
                    StoreLocalInstruction(localIndex: null)),
                "ClearableManager.CollectChores typed fetch capture");
        int clearableFetchLocalIndex =
            clearableCollectChoresInstructions[fetchCaptureIndex + 3]
                .LocalIndex!.Value;

        int clearableEligibilityAnchorIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                clearableCandidateIndices,
                index => MatchesWindow(
                    clearableCollectChoresInstructions,
                    index,
                    LoadLocalInstruction(localIndex: null),
                    LoadLocalInstruction(clearableFetchLocalIndex),
                    FieldInstruction("GlobalFetch.chore"),
                    FieldInstruction("FetchChore.tagsFirst"),
                    CallInstruction("KPrefabID.HasTag"),
                    BranchInstruction("br.s"),
                    OperationInstruction("ldc.i4.0"),
                    BranchInstruction("br.s"),
                    OperationInstruction("ldc.i4.1"),
                    BranchInstruction("brfalse.s")),
                "ClearableManager.CollectChores direct eligibility extension");

        var beginCandidateIndices = Enumerable.Range(
            0,
            fetchAreaBeginInstructions.Count).ToArray();
        int fetchChoreContainmentAnchorIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                beginCandidateIndices,
                index => MatchesWindow(
                    fetchAreaBeginInstructions,
                    index,
                    LoadLocalInstruction(localIndex: null),
                    FieldInstruction("FetchChore.forbidHash"),
                    LoadArgumentInstruction(0),
                    FieldInstruction("FetchAreaStatesInstance.rootChore"),
                    FieldInstruction("FetchChore.forbidHash"),
                    BranchInstruction("bne.un.s")),
                "FetchAreaChore.StatesInstance.Begin fetch-chore containment " +
                "extension");
        int candidateFetchChoreLocalIndex =
            fetchAreaBeginInstructions[fetchChoreContainmentAnchorIndex]
                .LocalIndex!.Value;

        var delegateCandidateIndices = Enumerable.Range(
            0,
            fetchAreaCandidateDelegateInstructions.Count).ToArray();
        int delegateCanReachAnchorIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                delegateCandidateIndices,
                index => MatchesWindow(
                    fetchAreaCandidateDelegateInstructions,
                    index,
                    LoadArgumentInstruction(0),
                    FieldInstruction("FetchAreaCandidateClosure.statesInstance"),
                    FieldAddressInstruction(
                        "FetchAreaStatesInstance.rootContext"),
                    FieldInstruction("ChoreContext.consumerState"),
                    FieldInstruction("ChoreConsumerState.consumer"),
                    LoadLocalInstruction(localIndex: null),
                    CallInstruction("ChoreConsumer.CanReach"),
                    BranchInstruction("brtrue.s"),
                    OperationInstruction("ldc.i4.0"),
                    OperationInstruction("ret")),
                "FetchAreaChore candidate delegate direct CanReach result");
        int delegatePickupableLocalIndex =
            fetchAreaCandidateDelegateInstructions[
                delegateCanReachAnchorIndex + 5].LocalIndex!.Value;

        return new KleiDirectEligibilityInstructionAnchors(
            clearablePickupableLocalIndex,
            clearableFetchLocalIndex,
            candidateFetchChoreLocalIndex,
            delegatePickupableLocalIndex,
            clearableEligibilityAnchorIndex + 10,
            fetchChoreContainmentAnchorIndex + 6,
            delegateCanReachAnchorIndex + 6);
    }

    private static KleiPickupGroupingInstructionAnchors
        RequireKleiPickupGroupingInstructionAnchors(
            IReadOnlyList<TranspilerInstructionFixture>
                comparatorInstructions,
            IReadOnlyList<TranspilerInstructionFixture>
                duplicateSuppressionInstructions)
    {
        var comparatorCandidateIndices = Enumerable.Range(
            0,
            comparatorInstructions.Count).ToArray();
        int comparatorExtensionIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                comparatorCandidateIndices,
                index => MatchesWindow(
                    comparatorInstructions,
                    index,
                    LoadArgumentAddressInstruction(1),
                    FieldAddressInstruction(
                        "FetchManager.Pickup.masterPriority"),
                    LoadArgumentInstruction(0),
                    FieldInstruction(
                        "FetchManager.Pickup.masterPriority"),
                    CallInstruction("System.Int32.CompareTo"),
                    StoreLocalInstruction(localIndex: null),
                    LoadLocalInstruction(localIndex: null),
                    BranchInstruction("brfalse.s"),
                    LoadLocalInstruction(localIndex: null),
                    OperationInstruction("ret")) &&
                    comparatorInstructions[index + 5].LocalIndex ==
                        comparatorInstructions[index + 6].LocalIndex &&
                    comparatorInstructions[index + 5].LocalIndex ==
                        comparatorInstructions[index + 8].LocalIndex,
                "FetchManager.PickupComparerIncludingPriority.Compare " +
                "post-priority extension anchor");

        var suppressionCandidateIndices = Enumerable.Range(
            0,
            duplicateSuppressionInstructions.Count).ToArray();
        int duplicateSuppressionExtensionIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                suppressionCandidateIndices,
                index => MatchesWindow(
                    duplicateSuppressionInstructions,
                    index,
                    LoadLocalInstruction(localIndex: null),
                    FieldInstruction(
                        "FetchManager.Pickup.masterPriority"),
                    LoadLocalInstruction(localIndex: null),
                    FieldInstruction(
                        "FetchManager.Pickup.masterPriority"),
                    BranchInstruction("bne.un.s"),
                    LoadLocalInstruction(localIndex: null),
                    LoadLocalInstruction(localIndex: null),
                    BranchInstruction("bne.un.s"),
                    OperationInstruction("ldc.i4.1"),
                    StoreLocalInstruction(localIndex: null)),
                "FetchManager.FetchablesByPrefabId.UpdatePickups duplicate " +
                "suppression extension anchor");

        int previousPickupLocalIndex = duplicateSuppressionInstructions[
            duplicateSuppressionExtensionIndex].LocalIndex!.Value;
        int currentPickupLocalIndex = duplicateSuppressionInstructions[
            duplicateSuppressionExtensionIndex + 2].LocalIndex!.Value;
        int currentTagHashLocalIndex = duplicateSuppressionInstructions[
            duplicateSuppressionExtensionIndex + 5].LocalIndex!.Value;
        int previousTagHashLocalIndex = duplicateSuppressionInstructions[
            duplicateSuppressionExtensionIndex + 6].LocalIndex!.Value;

        _ = HarmonyPatchContractVerifier.RequireSingleMatch(
            suppressionCandidateIndices,
            index => MatchesWindow(
                duplicateSuppressionInstructions,
                index,
                LoadLocalInstruction(previousPickupLocalIndex),
                FieldInstruction("FetchManager.Pickup.tagBitsHash"),
                StoreLocalInstruction(previousTagHashLocalIndex)),
            "UpdatePickups previous pickup tag-hash capture");
        _ = HarmonyPatchContractVerifier.RequireSingleMatch(
            suppressionCandidateIndices,
            index => MatchesWindow(
                duplicateSuppressionInstructions,
                index,
                LoadLocalInstruction(currentPickupLocalIndex),
                FieldInstruction("FetchManager.Pickup.tagBitsHash"),
                StoreLocalInstruction(currentTagHashLocalIndex)),
            "UpdatePickups current pickup tag-hash capture");

        return new KleiPickupGroupingInstructionAnchors(
            comparatorExtensionIndex,
            duplicateSuppressionExtensionIndex,
            previousPickupLocalIndex,
            currentPickupLocalIndex,
            "TemperatureEligibilityClassKey");
    }

    private static void RequireVerifiedKleiPickupWorkerReadContract(
        IReadOnlyList<string> observedWorkerReads)
    {
        string[] expectedWorkerReads =
        [
            "Navigator.GetAnchorCell",
            "Grid.WorldIdx",
            "FetchManager.Pickup.pickupable",
            "Pickupable.KPrefabID",
            "KPrefabID.InstanceID",
            "KPrefabID.PrefabTag",
            "KPrefabID.HasTag",
            "Pickupable.get_PrimaryElement",
            "PrimaryElement.get_InternalTemperature"
        ];
        if (!observedWorkerReads.SequenceEqual(
                expectedWorkerReads,
                StringComparer.Ordinal))
        {
            throw new HarmonyPatchContractViolationException(
                "The Klei pickup grouping worker read contract contains an " +
                "unverified managed or Unity/native access.");
        }
    }

    private static AuthoritativeFetchTraversalInstructionAnchors
        RequireAuthoritativeFetchTraversalInstructionAnchors(
            IReadOnlyList<TranspilerInstructionFixture> instructions)
    {
        var candidateIndices = Enumerable.Range(0, instructions.Count).ToArray();
        int parentWorldSectionStartIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesWindow(
                    instructions,
                    index,
                    FieldInstruction("GlobalChoreProvider.fetchMap"),
                    LoadLocalInstruction(localIndex: null),
                    LoadLocalInstruction(localIndex: null),
                    CallInstruction("List<int>.get_Item"),
                    LoadLocalAddressInstruction(localIndex: null),
                    CallInstruction(
                        "Dictionary<int,List<FetchChore>>.TryGetValue")),
                "GlobalChoreProvider.UpdateStorageFetchableBits parent-world " +
                "fetch-map section start");
        int selectedFetchChoreIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesWindow(
                    instructions,
                    index,
                    FieldInstruction("GlobalChoreProvider.storageFetchableTags"),
                    LoadLocalInstruction(localIndex: null),
                    FieldInstruction("FetchChore.tags"),
                    CallInstruction("HashSet<Tag>.UnionWith")),
                "GlobalChoreProvider.UpdateStorageFetchableBits selected " +
                "FetchChore traversal");

        return new AuthoritativeFetchTraversalInstructionAnchors(
            parentWorldSectionStartIndex,
            selectedFetchChoreIndex,
            instructions[parentWorldSectionStartIndex + 1].LocalIndex!.Value,
            instructions[parentWorldSectionStartIndex + 2].LocalIndex!.Value,
            instructions[selectedFetchChoreIndex + 1].LocalIndex!.Value);
    }

    private static WorldInventoryInstructionAnchors
        RequireWorldInventoryInstructionAnchors(
            IReadOnlyList<TranspilerInstructionFixture> instructions)
    {
        var candidateIndices = Enumerable.Range(0, instructions.Count).ToArray();
        var inventoryEntryCaptureIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesWindow(
                    instructions,
                    index,
                    CallInstruction(
                        "Dictionary<Tag,HashSet<Pickupable>>.Enumerator.get_Current"),
                    StoreLocalInstruction(localIndex: null)),
                "WorldInventory.Update inventory-entry capture");
        var resourceTagStartIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesWindow(
                    instructions,
                    index,
                    CallInstruction(
                        "KeyValuePair<Tag,HashSet<Pickupable>>.get_Key"),
                    StoreLocalInstruction(localIndex: null),
                    CallInstruction(
                        "KeyValuePair<Tag,HashSet<Pickupable>>.get_Value"),
                    CallInstruction("IEnumerable<Pickupable>.GetEnumerator")),
                "WorldInventory.Update resource-tag enumeration start");
        int resourceTagLocalIndex =
            instructions[resourceTagStartIndex + 1].LocalIndex!.Value;

        var filteredPickupContributionIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesWindow(
                    instructions,
                    index,
                    LoadLocalInstruction(localIndex: null),
                    FieldInstruction("Pickupable.KPrefabID"),
                    StaticFieldInstruction("GameTags.StoredPrivate"),
                    CallInstruction("KPrefabID.HasTag"),
                    BranchInstruction("brtrue.s"),
                    LoadLocalInstruction(localIndex: null),
                    LoadLocalInstruction(localIndex: null),
                    CallInstruction("Pickupable.get_TotalAmount"),
                    OperationInstruction("add"),
                    StoreLocalInstruction(localIndex: null)) &&
                    HasMatchingWorldInventoryContributionLocals(
                        instructions,
                        index),
                "WorldInventory.Update filtered Pickupable.TotalAmount contribution");
        int pickupableLocalIndex =
            instructions[filteredPickupContributionIndex].LocalIndex!.Value;
        int accumulatedAmountLocalIndex =
            instructions[filteredPickupContributionIndex + 5]
                .LocalIndex!.Value;

        var resourceTagCompletionIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesWindow(
                    instructions,
                    index,
                    FieldInstruction("WorldInventory.accessibleAmounts"),
                    LoadLocalInstruction(resourceTagLocalIndex),
                    LoadLocalInstruction(accumulatedAmountLocalIndex),
                    CallInstruction("Dictionary<Tag,float>.set_Item")),
                "WorldInventory.Update resource-tag enumeration completion");

        return new WorldInventoryInstructionAnchors(
            inventoryEntryCaptureIndex,
            resourceTagStartIndex,
            filteredPickupContributionIndex,
            resourceTagCompletionIndex,
            resourceTagLocalIndex,
            accumulatedAmountLocalIndex,
            pickupableLocalIndex);
    }

    private static FetchListStatusInstructionAnchor
        RequireFetchListStatusInstructionAnchor(
            IReadOnlyList<TranspilerInstructionFixture> instructions)
    {
        var candidateIndices = Enumerable.Range(0, instructions.Count).ToArray();
        var worldIdentityIndex = HarmonyPatchContractVerifier.RequireSingleMatch(
            candidateIndices,
            index => MatchesWindow(
                instructions,
                index,
                CallInstruction("List<WorldContainer>.Enumerator.get_Current"),
                FieldInstruction("WorldContainer.id"),
                StoreLocalInstruction(localIndex: null)),
            "FetchListStatusItemUpdater.Render200ms world identity");
        int worldIdLocalIndex = instructions[worldIdentityIndex + 2]
            .LocalIndex!.Value;

        var minimumAmountCallIndex =
            HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesStatusAvailabilityWindow(instructions, index),
                "FetchListStatusItemUpdater.Render200ms early-insufficient " +
                "branch and fetchable assignment");

        return new FetchListStatusInstructionAnchor(
            worldIdLocalIndex,
            instructions[minimumAmountCallIndex - 2].LocalIndex!.Value,
            instructions[minimumAmountCallIndex - 1].LocalIndex!.Value,
            instructions[minimumAmountCallIndex - 9].LocalIndex!.Value,
            instructions[minimumAmountCallIndex - 3].LocalIndex!.Value,
            instructions[minimumAmountCallIndex + 1].LocalIndex!.Value);
    }

    private static bool MatchesStatusAvailabilityWindow(
        IReadOnlyList<TranspilerInstructionFixture> instructions,
        int minimumAmountCallIndex)
    {
        if (!MatchesWindow(
                instructions,
                minimumAmountCallIndex - 12,
                LoadLocalInstruction(localIndex: null),
                LoadLocalInstruction(localIndex: null),
                CallInstruction("Dictionary<Tag,float>.get_Item"),
                LoadLocalInstruction(localIndex: null),
                LoadLocalInstruction(localIndex: null),
                CallInstruction("Mathf.Min"),
                StoreLocalInstruction(localIndex: null),
                LoadLocalInstruction(localIndex: null),
                OperationInstruction("add"),
                StoreLocalInstruction(localIndex: null),
                LoadLocalInstruction(localIndex: null),
                LoadLocalInstruction(localIndex: null),
                CallInstruction("FetchList2.GetMinimumAmount"),
                StoreLocalInstruction(localIndex: null),
                OperationInstruction("dup"),
                LoadLocalInstruction(localIndex: null),
                OperationInstruction("add"),
                LoadLocalInstruction(localIndex: null),
                BranchInstruction("bge.un.s")))
        {
            return false;
        }

        int resourceTagLocalIndex =
            instructions[minimumAmountCallIndex - 11].LocalIndex!.Value;
        int interimFetchableAmountLocalIndex =
            instructions[minimumAmountCallIndex - 6].LocalIndex!.Value;
        int fetchableAmountLocalIndex =
            instructions[minimumAmountCallIndex - 3].LocalIndex!.Value;
        int minimumRequiredAmountLocalIndex =
            instructions[minimumAmountCallIndex + 1].LocalIndex!.Value;

        return instructions[minimumAmountCallIndex - 1].LocalIndex ==
                resourceTagLocalIndex &&
            instructions[minimumAmountCallIndex - 5].LocalIndex ==
                interimFetchableAmountLocalIndex &&
            instructions[minimumAmountCallIndex + 3].LocalIndex ==
                fetchableAmountLocalIndex &&
            instructions[minimumAmountCallIndex + 5].LocalIndex ==
                minimumRequiredAmountLocalIndex;
    }

    private static bool HasMatchingWorldInventoryContributionLocals(
        IReadOnlyList<TranspilerInstructionFixture> instructions,
        int contributionStartIndex) =>
        instructions[contributionStartIndex].LocalIndex ==
            instructions[contributionStartIndex + 6].LocalIndex &&
        instructions[contributionStartIndex + 5].LocalIndex ==
            instructions[contributionStartIndex + 9].LocalIndex;

    private static bool MatchesWindow(
        IReadOnlyList<TranspilerInstructionFixture> instructions,
        int startIndex,
        params TranspilerInstructionFixture[] expectedWindow)
    {
        if (startIndex < 0 ||
            startIndex + expectedWindow.Length > instructions.Count)
        {
            return false;
        }

        for (var relativeIndex = 0;
             relativeIndex < expectedWindow.Length;
             relativeIndex++)
        {
            var observed = instructions[startIndex + relativeIndex];
            var expected = expectedWindow[relativeIndex];
            if (!string.Equals(
                    observed.Operation,
                    expected.Operation,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observed.MemberIdentity,
                    expected.MemberIdentity,
                    StringComparison.Ordinal) ||
                expected.LocalIndex.HasValue &&
                observed.LocalIndex != expected.LocalIndex)
            {
                return false;
            }
        }

        return true;
    }

    private static List<TranspilerInstructionFixture>
        CapturedClearableCollectChoresInstructions() =>
        [
            FieldInstruction("ClearableManager.sortedClearables"),
            LoadLocalInstruction(4),
            CallInstruction("List<SortedClearable>.get_Item"),
            OperationInstruction("dup"),
            FieldInstruction("SortedClearable.pickupable"),
            StoreLocalInstruction(5),
            LoadArgumentInstruction(1),
            LoadLocalInstruction(9),
            CallInstruction("List<GlobalFetch>.get_Item"),
            StoreLocalInstruction(10),
            LoadLocalInstruction(8),
            LoadLocalInstruction(10),
            FieldInstruction("GlobalFetch.chore"),
            FieldInstruction("FetchChore.tagsFirst"),
            CallInstruction("KPrefabID.HasTag"),
            BranchInstruction("br.s"),
            OperationInstruction("ldc.i4.0"),
            BranchInstruction("br.s"),
            OperationInstruction("ldc.i4.1"),
            BranchInstruction("brfalse.s"),
            LoadLocalAddressInstruction(7)
        ];

    private static List<TranspilerInstructionFixture>
        CapturedFetchAreaBeginInstructions() =>
        [
            LoadLocalInstruction(14),
            FieldInstruction("FetchChore.forbidHash"),
            LoadArgumentInstruction(0),
            FieldInstruction("FetchAreaStatesInstance.rootChore"),
            FieldInstruction("FetchChore.forbidHash"),
            BranchInstruction("bne.un.s"),
            LoadLocalInstruction(14),
            CallInstruction("FetchChore.get_originalAmount")
        ];

    private static List<TranspilerInstructionFixture>
        CapturedFetchAreaCandidateDelegateInstructions() =>
        [
            LoadArgumentInstruction(0),
            FieldInstruction("FetchAreaCandidateClosure.statesInstance"),
            FieldAddressInstruction("FetchAreaStatesInstance.rootContext"),
            FieldInstruction("ChoreContext.consumerState"),
            FieldInstruction("ChoreConsumerState.consumer"),
            LoadLocalInstruction(0),
            CallInstruction("ChoreConsumer.CanReach"),
            BranchInstruction("brtrue.s"),
            OperationInstruction("ldc.i4.0"),
            OperationInstruction("ret"),
            LoadLocalInstruction(1),
            StaticFieldInstruction("GameTags.MarkedForMove")
        ];

    private static List<TranspilerInstructionFixture>
        CapturedPickupGroupingComparatorInstructions() =>
        [
            LoadArgumentAddressInstruction(1),
            FieldAddressInstruction(
                "FetchManager.Pickup.masterPriority"),
            LoadArgumentInstruction(0),
            FieldInstruction("FetchManager.Pickup.masterPriority"),
            CallInstruction("System.Int32.CompareTo"),
            StoreLocalInstruction(0),
            LoadLocalInstruction(0),
            BranchInstruction("brfalse.s"),
            LoadLocalInstruction(0),
            OperationInstruction("ret"),
            LoadArgumentAddressInstruction(0),
            FieldAddressInstruction("FetchManager.Pickup.PathCost")
        ];

    private static List<TranspilerInstructionFixture>
        CapturedPickupDuplicateSuppressionInstructions() =>
        [
            LoadLocalInstruction(0),
            FieldInstruction("FetchManager.Pickup.tagBitsHash"),
            StoreLocalInstruction(1),
            LoadLocalInstruction(6),
            FieldInstruction("FetchManager.Pickup.tagBitsHash"),
            StoreLocalInstruction(7),
            LoadLocalInstruction(0),
            FieldInstruction("FetchManager.Pickup.masterPriority"),
            LoadLocalInstruction(6),
            FieldInstruction("FetchManager.Pickup.masterPriority"),
            BranchInstruction("bne.un.s"),
            LoadLocalInstruction(7),
            LoadLocalInstruction(1),
            BranchInstruction("bne.un.s"),
            OperationInstruction("ldc.i4.1"),
            StoreLocalInstruction(5)
        ];

    private static List<string> CapturedVerifiedKleiPickupWorkerReads() =>
        [
            "Navigator.GetAnchorCell",
            "Grid.WorldIdx",
            "FetchManager.Pickup.pickupable",
            "Pickupable.KPrefabID",
            "KPrefabID.InstanceID",
            "KPrefabID.PrefabTag",
            "KPrefabID.HasTag",
            "Pickupable.get_PrimaryElement",
            "PrimaryElement.get_InternalTemperature"
        ];

    private static List<TranspilerInstructionFixture>
        CapturedAuthoritativeFetchTraversalInstructions() =>
        [
            FieldInstruction("GlobalChoreProvider.fetchMap"),
            LoadLocalInstruction(2),
            LoadLocalInstruction(3),
            CallInstruction("List<int>.get_Item"),
            LoadLocalAddressInstruction(4),
            CallInstruction("Dictionary<int,List<FetchChore>>.TryGetValue"),
            LoadLocalInstruction(6),
            CallInstruction("Chore.get_choreType"),
            LoadLocalInstruction(0),
            BranchInstruction("beq.s"),
            LoadLocalInstruction(6),
            CallInstruction("FetchChore.get_destination"),
            BranchInstruction("brfalse.s"),
            FieldInstruction("GlobalChoreProvider.storageFetchableTags"),
            LoadLocalInstruction(6),
            FieldInstruction("FetchChore.tags"),
            CallInstruction("HashSet<Tag>.UnionWith")
        ];

    private static List<TranspilerInstructionFixture>
        CapturedWorldInventoryUpdateInstructions() =>
        [
            CallInstruction(
                "Dictionary<Tag,HashSet<Pickupable>>.Enumerator.get_Current"),
            StoreLocalInstruction(3),
            CallInstruction(
                "KeyValuePair<Tag,HashSet<Pickupable>>.get_Key"),
            StoreLocalInstruction(4),
            CallInstruction(
                "KeyValuePair<Tag,HashSet<Pickupable>>.get_Value"),
            CallInstruction("IEnumerable<Pickupable>.GetEnumerator"),
            LoadLocalInstruction(7),
            FieldInstruction("Pickupable.KPrefabID"),
            StaticFieldInstruction("GameTags.StoredPrivate"),
            CallInstruction("KPrefabID.HasTag"),
            BranchInstruction("brtrue.s"),
            LoadLocalInstruction(5),
            LoadLocalInstruction(7),
            CallInstruction("Pickupable.get_TotalAmount"),
            OperationInstruction("add"),
            StoreLocalInstruction(5),
            FieldInstruction("WorldInventory.accessibleAmounts"),
            LoadLocalInstruction(4),
            LoadLocalInstruction(5),
            CallInstruction("Dictionary<Tag,float>.set_Item")
        ];

    private static List<TranspilerInstructionFixture>
        CapturedFetchListStatusRenderInstructions() =>
        [
            CallInstruction("List<WorldContainer>.Enumerator.get_Current"),
            FieldInstruction("WorldContainer.id"),
            StoreLocalInstruction(1),
            LoadLocalInstruction(18),
            LoadLocalInstruction(33),
            CallInstruction("Dictionary<Tag,float>.get_Item"),
            LoadLocalInstruction(5),
            LoadLocalInstruction(33),
            CallInstruction("Dictionary<Tag,float>.get_Item"),
            LoadLocalInstruction(34),
            LoadLocalInstruction(35),
            CallInstruction("Mathf.Min"),
            StoreLocalInstruction(36),
            LoadLocalInstruction(36),
            OperationInstruction("add"),
            StoreLocalInstruction(37),
            LoadLocalInstruction(28),
            LoadLocalInstruction(33),
            CallInstruction("FetchList2.GetMinimumAmount"),
            StoreLocalInstruction(38),
            OperationInstruction("dup"),
            LoadLocalInstruction(37),
            OperationInstruction("add"),
            LoadLocalInstruction(38),
            BranchInstruction("bge.un.s")
        ];

    private static TranspilerInstructionFixture CallInstruction(
        string memberIdentity) =>
        new("call", memberIdentity, null);

    private static TranspilerInstructionFixture FieldInstruction(
        string memberIdentity) =>
        new("field", memberIdentity, null);

    private static TranspilerInstructionFixture FieldAddressInstruction(
        string memberIdentity) =>
        new("field-address", memberIdentity, null);

    private static TranspilerInstructionFixture StaticFieldInstruction(
        string memberIdentity) =>
        new("static-field", memberIdentity, null);

    private static TranspilerInstructionFixture LoadLocalInstruction(
        int? localIndex) =>
        new("load-local", null, localIndex);

    private static TranspilerInstructionFixture LoadArgumentInstruction(
        int? argumentIndex) =>
        new("load-argument", null, argumentIndex);

    private static TranspilerInstructionFixture LoadArgumentAddressInstruction(
        int? argumentIndex) =>
        new("load-argument-address", null, argumentIndex);

    private static TranspilerInstructionFixture StoreLocalInstruction(
        int? localIndex) =>
        new("store-local", null, localIndex);

    private static TranspilerInstructionFixture LoadLocalAddressInstruction(
        int? localIndex) =>
        new("load-local-address", null, localIndex);

    private static TranspilerInstructionFixture OperationInstruction(
        string operation) =>
        new(operation, null, null);

    private static TranspilerInstructionFixture BranchInstruction(
        string operation) =>
        new(operation, null, null);

    private static async Task AssertExactInstanceMethod(
        MethodInfo method,
        Type expectedDeclaringType,
        bool isPublic,
        IReadOnlyList<Type> expectedParameterTypes) =>
        await AssertExactInstanceMethod(
            method,
            expectedDeclaringType,
            isPublic,
            typeof(void),
            expectedParameterTypes);

    private static async Task AssertExactInstanceMethod(
        MethodInfo method,
        Type expectedDeclaringType,
        bool isPublic,
        Type expectedReturnType,
        IReadOnlyList<Type> expectedParameterTypes)
    {
        await Assert.That(method.DeclaringType).IsSameReferenceAs(expectedDeclaringType);
        await Assert.That(method.IsPublic).IsEqualTo(isPublic);
        await Assert.That(method.IsStatic).IsFalse();
        await Assert.That(method.ReturnType).IsSameReferenceAs(expectedReturnType);
        await Assert.That(method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray()).IsEquivalentTo(expectedParameterTypes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static string ResolveProductionSourcePath(
        string semanticDirectoryName,
        string sourceFileName)
    {
        var configuredRepositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var repositoryRoot = string.IsNullOrWhiteSpace(configuredRepositoryRoot)
            ? FindRepositoryRoot(AppContext.BaseDirectory)
            : Path.GetFullPath(configuredRepositoryRoot);
        return Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            semanticDirectoryName,
            sourceFileName);
    }

    private static string FindRepositoryRoot(string startingDirectory)
    {
        for (DirectoryInfo? candidate =
                 new DirectoryInfo(Path.GetFullPath(startingDirectory));
             candidate != null;
             candidate = candidate.Parent)
        {
            if (Directory.Exists(Path.Combine(
                candidate.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source")))
            {
                return candidate.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the oxygen-not-included repository root from " +
            startingDirectory +
            ".");
    }

    private static int CountOrdinalOccurrences(string source, string value)
    {
        int occurrenceCount = 0;
        int searchIndex = 0;
        while ((searchIndex = source.IndexOf(
                   value,
                   searchIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            occurrenceCount++;
            searchIndex += value.Length;
        }

        return occurrenceCount;
    }

    private static async Task<ActiveHarmonyPrefixDescriptor> CreateDescriptor(
        MethodBase targetMethod,
        string patchMethodName,
        string harmonyOwner,
        int priority) =>
        new ActiveHarmonyPrefixDescriptor(
            targetMethod,
            (await RequireFixtureMethod(patchMethodName)),
            harmonyOwner,
            priority);

    private static async Task<MethodInfo> RequireFixtureMethod(string methodName)
    {
        var method = typeof(HarmonyAuthorityFixture).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method;
    }

    private sealed record TranspilerInstructionFixture(
        string Operation,
        string? MemberIdentity,
        int? LocalIndex);

    private sealed record KleiDirectEligibilityInstructionAnchors(
        int ClearablePickupableLocalIndex,
        int ClearableFetchLocalIndex,
        int CandidateFetchChoreLocalIndex,
        int DelegatePickupableLocalIndex,
        int ClearableEligibilityExtensionIndex,
        int FetchChoreContainmentExtensionIndex,
        int DelegateCanReachCallIndex);

    private sealed record KleiPickupGroupingInstructionAnchors(
        int ComparatorExtensionIndex,
        int DuplicateSuppressionExtensionIndex,
        int PreviousPickupLocalIndex,
        int CurrentPickupLocalIndex,
        string SharedSemanticKeyIdentity);

    private sealed record AuthoritativeFetchTraversalInstructionAnchors(
        int ParentWorldSectionStartIndex,
        int SelectedFetchChoreIndex,
        int SortedWorldIdsLocalIndex,
        int SortedWorldIdIndexLocalIndex,
        int SelectedFetchChoreLocalIndex);

    private sealed record WorldInventoryInstructionAnchors(
        int InventoryEntryCaptureIndex,
        int ResourceTagStartIndex,
        int FilteredPickupContributionIndex,
        int ResourceTagCompletionIndex,
        int ResourceTagLocalIndex,
        int AccumulatedAmountLocalIndex,
        int PickupableLocalIndex);

    private sealed record FetchListStatusInstructionAnchor(
        int WorldIdLocalIndex,
        int FetchListLocalIndex,
        int ResourceTagLocalIndex,
        int RemainingAmountLocalIndex,
        int FetchableAmountLocalIndex,
        int MinimumRequiredAmountLocalIndex);

    private class MethodFixtureBase
    {
        protected bool InheritedInstanceTarget(int number, string text) =>
            number > text.Length;

        protected static string InheritedStaticTarget(long number) =>
            number.ToString();
    }

    private sealed class MethodFixture : MethodFixtureBase
    {
        private bool ExactInstanceTarget(int number, string text) =>
            number > text.Length;

        private bool WrongParameterInstanceTarget(string text, int number) =>
            text.Length > number;

        private int WrongReturnInstanceTarget(int number, string text) =>
            number + text.Length;

        private static bool StaticInsteadOfInstanceTarget(
            int number,
            string text) =>
            number > text.Length;

        public bool PublicInstanceTarget(int number, string text) =>
            number > text.Length;

        private bool GenericInstanceTarget<T>(int number, string text) =>
            number > text.Length && typeof(T) == typeof(object);

        private static string ExactStaticTarget(long number) =>
            number.ToString();

        private static string WrongParameterStaticTarget(int number) =>
            number.ToString();

        private static long WrongReturnStaticTarget(long number) => number;

        private string InstanceInsteadOfStaticTarget(long number) =>
            number.ToString();

        public static string PublicStaticTarget(long number) =>
            number.ToString();

        private static string GenericStaticTarget<T>(long number) =>
            typeof(T).Name + number;
    }

    private sealed class ConstructorFixture
    {
        private ConstructorFixture(int number, string text)
        {
            _ = number;
            _ = text;
        }

        public ConstructorFixture(string text, int number)
        {
            _ = text;
            _ = number;
        }
    }

    private sealed class ReversedConstructorFixture
    {
        private ReversedConstructorFixture(string text, int number)
        {
            _ = text;
            _ = number;
        }
    }

    private class BaseConstructorFixture
    {
        protected BaseConstructorFixture()
        {
        }

        protected BaseConstructorFixture(long number)
        {
            _ = number;
        }
    }

    private sealed class DerivedConstructorFixture : BaseConstructorFixture
    {
        public DerivedConstructorFixture()
        {
        }
    }

    private class FieldFixtureBase
    {
        protected int InheritedInstanceField = 0;

        protected int ReadInheritedInstanceField() => InheritedInstanceField;
    }

    private sealed class FieldFixture : FieldFixtureBase
    {
        private int exactInstanceField = 0;
        private static string exactStaticField = string.Empty;
        private static int staticInsteadOfInstanceField = 0;
        private string wrongTypeField = string.Empty;

        public int PublicInstanceField = 0;

        internal int ReadPrivateFields() =>
            exactInstanceField +
            exactStaticField.Length +
            staticInsteadOfInstanceField +
            wrongTypeField.Length +
            PublicInstanceField +
            ReadInheritedInstanceField();
    }

    private class NestedTypeFixtureBase
    {
        protected sealed class InheritedNestedTarget
        {
        }
    }

    private sealed class NestedTypeFixture : NestedTypeFixtureBase
    {
        private sealed class NonPublicNestedTarget
        {
        }

        public sealed class PublicNestedTarget
        {
        }
    }

    private sealed class ChoreContractFixture
    {
    }

    private sealed class PickupableContractFixture
    {
    }

    private sealed class NavigatorPickupGroupingContractFixture
    {
    }

    private sealed class DirectPickupableContractFixture
    {
    }

    private sealed class DirectFetchChoreContractFixture
    {
    }

    private sealed class DirectStorageContractFixture
    {
    }

    private sealed class DirectGlobalFetchContractFixture
    {
    }

    private sealed class DirectChoreConsumerStateContractFixture
    {
    }

    private sealed class DirectChoreContextContractFixture
    {
    }

    private enum DirectIterationInstructionContractFixture
    {
        Continue
    }

    private static class FetchManagerDirectEligibilityContractFixture
    {
        public static bool IsFetchablePickup(
            DirectPickupableContractFixture pickup,
            DirectFetchChoreContractFixture chore,
            DirectStorageContractFixture destination) =>
            pickup != null && chore != null && destination != null;
    }

    private static class FetchManagerChangedDirectEligibilityContractFixture
    {
        public static int IsFetchablePickup(
            DirectPickupableContractFixture pickup,
            DirectFetchChoreContractFixture chore,
            DirectStorageContractFixture destination) =>
            pickup != null && chore != null && destination != null ? 1 : 0;
    }

    private sealed class ClearableManagerDirectEligibilityContractFixture
    {
        public void CollectChores(
            List<DirectGlobalFetchContractFixture> fetches,
            DirectChoreConsumerStateContractFixture consumerState,
            List<DirectChoreContextContractFixture> succeededContexts,
            List<DirectChoreContextContractFixture> failedContexts)
        {
            _ = fetches;
            _ = consumerState;
            _ = succeededContexts;
            _ = failedContexts;
        }
    }

    private sealed class FetchAreaStatesInstanceDirectEligibilityContractFixture
    {
        public void Begin(DirectChoreContextContractFixture context)
        {
            _ = context;
        }
    }

    private sealed class FetchAreaCandidateClosureDirectEligibilityContractFixture
    {
        public FetchAreaStatesInstanceDirectEligibilityContractFixture
            StatesInstance = new();

        private DirectIterationInstructionContractFixture EvaluateCandidate(
            object candidate,
            object context)
        {
            _ = candidate;
            _ = context;
            _ = StatesInstance;
            return DirectIterationInstructionContractFixture.Continue;
        }
    }

    private sealed class FetchAreaChangedCandidateClosureDirectEligibilityContractFixture
    {
        public FetchAreaStatesInstanceDirectEligibilityContractFixture
            ChangedStatesInstance = new();

        private DirectIterationInstructionContractFixture EvaluateCandidate(
            object candidate,
            object context)
        {
            _ = candidate;
            _ = context;
            _ = ChangedStatesInstance;
            return DirectIterationInstructionContractFixture.Continue;
        }
    }

    private readonly struct PickupGroupingCandidateContractFixture
    {
        internal PickupGroupingCandidateContractFixture(int groupingValue)
        {
            GroupingValue = groupingValue;
        }

        internal int GroupingValue { get; }
    }

    private readonly struct ChangedPickupGroupingCandidateContractFixture
    {
        internal ChangedPickupGroupingCandidateContractFixture(
            int groupingValue)
        {
            GroupingValue = groupingValue;
        }

        internal int GroupingValue { get; }
    }

    private sealed class FetchablesByPrefabIdPickupGroupingContractFixture
    {
        public void UpdatePickups(
            NavigatorPickupGroupingContractFixture worker_navigator,
            int worker)
        {
            _ = worker_navigator;
            _ = worker;
        }
    }

    private sealed class FetchManagerPickupGroupingContractFixture
    {
        private static class PickupComparerIncludingPriority
        {
            private static int Compare(
                PickupGroupingCandidateContractFixture firstCandidate,
                PickupGroupingCandidateContractFixture secondCandidate) =>
                firstCandidate.GroupingValue.CompareTo(
                    secondCandidate.GroupingValue);
        }
    }

    private sealed class FetchManagerChangedPickupGroupingContractFixture
    {
        private static class PickupComparerIncludingPriority
        {
            private static int Compare(
                ChangedPickupGroupingCandidateContractFixture firstCandidate,
                ChangedPickupGroupingCandidateContractFixture secondCandidate) =>
                firstCandidate.GroupingValue.CompareTo(
                    secondCandidate.GroupingValue);
        }
    }

    private sealed class GlobalChoreProviderFetchContractFixture
    {
        public void AddChore(ChoreContractFixture chore)
        {
        }

        public void RemoveChore(ChoreContractFixture chore)
        {
        }

        private void UpdateStorageFetchableBits()
        {
        }

        public bool ClearableHasDestination(PickupableContractFixture pickupable) =>
            pickupable != null;
    }

    private sealed class FetchChoreTagChangeContractFixture
    {
        private void OnTagsChanged(object eventData)
        {
        }
    }

    private sealed class GlobalChoreProviderFetchOverloadOnlyFixture
    {
        public void AddChore(object chore)
        {
        }

        public void RemoveChore(object chore)
        {
        }

        private void UpdateStorageFetchableBits(bool forceUpdate)
        {
        }

        public bool ClearableHasDestination(object pickupable) =>
            pickupable != null;
    }

    private sealed class FetchChoreChangedTagEventContractFixture
    {
        private void OnTagsChanged(string eventData)
        {
        }
    }

    private sealed class WorldInventoryUpdateTargetContractFixture
    {
        private void Update()
        {
        }
    }

    private sealed class FetchListStatusRenderTargetContractFixture
    {
        public void Render200ms(float deltaTime)
        {
            _ = deltaTime;
        }
    }

    private sealed class GameDestroyInstancesContractFixture
    {
        private void DestroyInstances()
        {
        }
    }

    private class GameOnPrefabInitContractFixture
    {
        protected void OnPrefabInit()
        {
        }
    }

    private sealed class ClusterManagerWorldLifecycleContractFixture
    {
        public void RegisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer)
        {
            _ = worldContainer;
        }

        public void UnregisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer)
        {
            _ = worldContainer;
        }
    }

    private sealed class WorldContainerLifecycleContractFixture
    {
        public void SetParentIdx(int parentWorldId)
        {
            _ = parentWorldId;
        }
    }

    private sealed class GameDestroyInstancesOverloadOnlyFixture
    {
        private void DestroyInstances(int unusedArgument)
        {
            _ = unusedArgument;
        }
    }

    private sealed class ClusterManagerWorldLifecycleOverloadOnlyFixture
    {
        public void RegisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer,
            int unusedArgument)
        {
            _ = worldContainer;
            _ = unusedArgument;
        }

        public void UnregisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer,
            int unusedArgument)
        {
            _ = worldContainer;
            _ = unusedArgument;
        }
    }

    private sealed class WorldContainerSetParentIdxOverloadOnlyFixture
    {
        public void SetParentIdx(long parentWorldId)
        {
            _ = parentWorldId;
        }
    }

    private sealed class GameDestroyInstancesChangedReturnFixture
    {
        private bool DestroyInstances() => true;
    }

    private sealed class ClusterManagerWorldLifecycleChangedReturnFixture
    {
        public bool RegisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer) =>
            worldContainer != null;

        public bool UnregisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer) =>
            worldContainer != null;
    }

    private sealed class WorldContainerSetParentIdxChangedReturnFixture
    {
        public bool SetParentIdx(int parentWorldId) => parentWorldId >= 0;
    }

    private static class GameDestroyInstancesStaticFixture
    {
        private static void DestroyInstances()
        {
        }
    }

    private static class ClusterManagerWorldLifecycleStaticFixture
    {
        public static void RegisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer)
        {
            _ = worldContainer;
        }

        public static void UnregisterWorldContainer(
            WorldContainerLifecycleContractFixture worldContainer)
        {
            _ = worldContainer;
        }
    }

    private static class WorldContainerSetParentIdxStaticFixture
    {
        public static void SetParentIdx(int parentWorldId)
        {
            _ = parentWorldId;
        }
    }

    private static class HarmonyAuthorityFixture
    {
        internal static void KleiTarget()
        {
        }

        internal static void OtherKleiTarget()
        {
        }

        internal static bool PermittedSkippingPrefix() => true;

        internal static bool ForeignSkippingPrefix() => true;

        internal static void ForeignObservingPrefix()
        {
        }
    }

    /// <summary>
    /// Repeats reflection results to prove every verifier primitive rejects an
    /// ambiguous metadata result instead of silently selecting the first item.
    /// The production path receives ordinary runtime <see cref="Type"/> values;
    /// this delegator isolates the cardinality invariant without relying on
    /// invalid CLR metadata.
    /// </summary>
    private sealed class DuplicateMemberReportingType : TypeDelegator
    {
        private readonly DuplicatedMemberCollection duplicatedCollection;

        internal DuplicateMemberReportingType(
            Type delegatedType,
            DuplicatedMemberCollection duplicatedCollection)
            : base(delegatedType)
        {
            this.duplicatedCollection = duplicatedCollection;
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) =>
            duplicatedCollection == DuplicatedMemberCollection.Methods
                ? Duplicate(base.GetMethods(bindingAttr))
                : base.GetMethods(bindingAttr);

        public override ConstructorInfo[] GetConstructors(
            BindingFlags bindingAttr) =>
            duplicatedCollection == DuplicatedMemberCollection.Constructors
                ? Duplicate(base.GetConstructors(bindingAttr))
                : base.GetConstructors(bindingAttr);

        public override FieldInfo[] GetFields(BindingFlags bindingAttr) =>
            duplicatedCollection == DuplicatedMemberCollection.Fields
                ? Duplicate(base.GetFields(bindingAttr))
                : base.GetFields(bindingAttr);

        public override Type[] GetNestedTypes(BindingFlags bindingAttr) =>
            duplicatedCollection == DuplicatedMemberCollection.NestedTypes
                ? Duplicate(base.GetNestedTypes(bindingAttr))
                : base.GetNestedTypes(bindingAttr);

        private static T[] Duplicate<T>(IReadOnlyList<T> source)
        {
            var duplicated = new T[source.Count * 2];
            for (var sourceIndex = 0;
                 sourceIndex < source.Count;
                 sourceIndex++)
            {
                duplicated[sourceIndex * 2] = source[sourceIndex];
                duplicated[(sourceIndex * 2) + 1] = source[sourceIndex];
            }

            return duplicated;
        }
    }

    private enum DuplicatedMemberCollection
    {
        Methods,
        Constructors,
        Fields,
        NestedTypes,
    }
}
