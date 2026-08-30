using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.HarmonyTranspilerInfrastructure;

[TestClass]
public sealed class HarmonyPatchContractVerifierTests
{
    [TestMethod]
    public void RequireInstanceMethod_WhenOneSignatureMatches_ReturnsThatMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(MethodFixture),
            "ExactInstanceTarget",
            DeclaredMemberVisibility.NonPublic,
            typeof(bool),
            [typeof(int), typeof(string)]);

        Assert.AreEqual("ExactInstanceTarget", method.Name);
        Assert.AreSame(typeof(MethodFixture), method.DeclaringType);
        Assert.IsFalse(method.IsStatic);
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenMethodIsMissing_ThrowsContractViolation()
    {
        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(MethodFixture),
                    "MissingInstanceTarget",
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    [typeof(int), typeof(string)]));

        StringAssert.Contains(exception.Message, "MissingInstanceTarget");
        StringAssert.Contains(exception.Message, "0");
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenOnlyWrongParametersExist_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "WrongParameterInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenOnlyWrongReturnTypeExists_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "WrongReturnInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenStaticnessDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "StaticInsteadOfInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(MethodFixture),
            DuplicatedMemberCollection.Methods);

        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    ambiguousType,
                    "ExactInstanceTarget",
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    [typeof(int), typeof(string)]));

        StringAssert.Contains(exception.Message, "2");
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenPublicVisibilityIsRequired_ReturnsPublicMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(MethodFixture),
            "PublicInstanceTarget",
            DeclaredMemberVisibility.Public,
            typeof(bool),
            [typeof(int), typeof(string)]);

        Assert.IsTrue(method.IsPublic);
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "PublicInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenOnlyInheritedMethodMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "InheritedInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireInstanceMethod_WhenOnlyGenericMethodMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MethodFixture),
                "GenericInstanceTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(bool),
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenOneSignatureMatches_ReturnsThatMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireStaticMethod(
            typeof(MethodFixture),
            "ExactStaticTarget",
            DeclaredMemberVisibility.NonPublic,
            typeof(string),
            [typeof(long)]);

        Assert.AreEqual("ExactStaticTarget", method.Name);
        Assert.AreSame(typeof(MethodFixture), method.DeclaringType);
        Assert.IsTrue(method.IsStatic);
    }

    [TestMethod]
    public void RequireStaticMethod_WhenMethodIsMissing_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "MissingStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenOnlyWrongParametersExist_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "WrongParameterStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenOnlyWrongReturnTypeExists_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "WrongReturnStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenStaticnessDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "InstanceInsteadOfStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(MethodFixture),
            DuplicatedMemberCollection.Methods);

        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    ambiguousType,
                    "ExactStaticTarget",
                    DeclaredMemberVisibility.NonPublic,
                    typeof(string),
                    [typeof(long)]));

        StringAssert.Contains(exception.Message, "2");
    }

    [TestMethod]
    public void RequireStaticMethod_WhenPublicVisibilityIsRequired_ReturnsPublicMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireStaticMethod(
            typeof(MethodFixture),
            "PublicStaticTarget",
            DeclaredMemberVisibility.Public,
            typeof(string),
            [typeof(long)]);

        Assert.IsTrue(method.IsPublic);
    }

    [TestMethod]
    public void RequireStaticMethod_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "PublicStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenOnlyInheritedMethodMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "InheritedStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireStaticMethod_WhenOnlyGenericMethodMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(MethodFixture),
                "GenericStaticTarget",
                DeclaredMemberVisibility.NonPublic,
                typeof(string),
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireConstructor_WhenOneSignatureMatches_ReturnsThatConstructor()
    {
        var constructor = HarmonyPatchContractVerifier.RequireConstructor(
            typeof(ConstructorFixture),
            DeclaredMemberVisibility.NonPublic,
            [typeof(int), typeof(string)]);

        Assert.AreSame(typeof(ConstructorFixture), constructor.DeclaringType);
        Assert.IsFalse(constructor.IsPublic);
    }

    [TestMethod]
    public void RequireConstructor_WhenConstructorIsMissing_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(ConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(decimal)]));
    }

    [TestMethod]
    public void RequireConstructor_WhenParameterOrderDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(ReversedConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(int), typeof(string)]));
    }

    [TestMethod]
    public void RequireConstructor_WhenPublicVisibilityIsRequired_ReturnsPublicConstructor()
    {
        var constructor = HarmonyPatchContractVerifier.RequireConstructor(
            typeof(ConstructorFixture),
            DeclaredMemberVisibility.Public,
            [typeof(string), typeof(int)]);

        Assert.IsTrue(constructor.IsPublic);
    }

    [TestMethod]
    public void RequireConstructor_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(ConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(string), typeof(int)]));
    }

    [TestMethod]
    public void RequireConstructor_WhenOnlyBaseConstructorMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireConstructor(
                typeof(DerivedConstructorFixture),
                DeclaredMemberVisibility.NonPublic,
                [typeof(long)]));
    }

    [TestMethod]
    public void RequireConstructor_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(ConstructorFixture),
            DuplicatedMemberCollection.Constructors);

        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireConstructor(
                    ambiguousType,
                    DeclaredMemberVisibility.NonPublic,
                    [typeof(int), typeof(string)]));

        StringAssert.Contains(exception.Message, "2");
    }

    [TestMethod]
    public void RequireField_WhenOneInstanceFieldMatches_ReturnsThatField()
    {
        var field = HarmonyPatchContractVerifier.RequireField(
            typeof(FieldFixture),
            "exactInstanceField",
            DeclaredMemberVisibility.NonPublic,
            FieldStorageKind.Instance,
            typeof(int));

        Assert.AreSame(typeof(FieldFixture), field.DeclaringType);
        Assert.IsFalse(field.IsStatic);
    }

    [TestMethod]
    public void RequireField_WhenOneStaticFieldMatches_ReturnsThatField()
    {
        var field = HarmonyPatchContractVerifier.RequireField(
            typeof(FieldFixture),
            "exactStaticField",
            DeclaredMemberVisibility.NonPublic,
            FieldStorageKind.Static,
            typeof(string));

        Assert.AreSame(typeof(FieldFixture), field.DeclaringType);
        Assert.IsTrue(field.IsStatic);
    }

    [TestMethod]
    public void RequireField_WhenFieldIsMissing_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "missingField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int)));
    }

    [TestMethod]
    public void RequireField_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                nameof(FieldFixture.PublicInstanceField),
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int)));
    }

    [TestMethod]
    public void RequireField_WhenStaticnessDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "staticInsteadOfInstanceField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int)));
    }

    [TestMethod]
    public void RequireField_WhenFieldTypeDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "wrongTypeField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int)));
    }

    [TestMethod]
    public void RequireField_WhenOnlyInheritedFieldMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FieldFixture),
                "InheritedInstanceField",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(int)));
    }

    [TestMethod]
    public void RequireField_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(FieldFixture),
            DuplicatedMemberCollection.Fields);

        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireField(
                    ambiguousType,
                    "exactInstanceField",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(int)));

        StringAssert.Contains(exception.Message, "2");
    }

    [TestMethod]
    public void RequireNestedType_WhenOneNonPublicTypeMatches_ReturnsThatType()
    {
        var nestedType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(NestedTypeFixture),
            "NonPublicNestedTarget",
            DeclaredMemberVisibility.NonPublic);

        Assert.AreEqual("NonPublicNestedTarget", nestedType.Name);
        Assert.IsTrue(nestedType.IsNestedPrivate);
    }

    [TestMethod]
    public void RequireNestedType_WhenOnePublicTypeMatches_ReturnsThatType()
    {
        var nestedType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(NestedTypeFixture),
            "PublicNestedTarget",
            DeclaredMemberVisibility.Public);

        Assert.AreEqual("PublicNestedTarget", nestedType.Name);
        Assert.IsTrue(nestedType.IsNestedPublic);
    }

    [TestMethod]
    public void RequireNestedType_WhenNestedTypeIsMissing_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireNestedType(
                typeof(NestedTypeFixture),
                "MissingNestedTarget",
                DeclaredMemberVisibility.NonPublic));
    }

    [TestMethod]
    public void RequireNestedType_WhenVisibilityDiffers_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireNestedType(
                typeof(NestedTypeFixture),
                "PublicNestedTarget",
                DeclaredMemberVisibility.NonPublic));
    }

    [TestMethod]
    public void RequireNestedType_WhenOnlyInheritedNestedTypeMatches_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireNestedType(
                typeof(NestedTypeFixture),
                "InheritedNestedTarget",
                DeclaredMemberVisibility.NonPublic));
    }

    [TestMethod]
    public void RequireNestedType_WhenSeveralExactCandidatesExist_ThrowsContractViolation()
    {
        var ambiguousType = new DuplicateMemberReportingType(
            typeof(NestedTypeFixture),
            DuplicatedMemberCollection.NestedTypes);

        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireNestedType(
                    ambiguousType,
                    "NonPublicNestedTarget",
                    DeclaredMemberVisibility.NonPublic));

        StringAssert.Contains(exception.Message, "2");
    }

    [TestMethod]
    public void RequireSingleMatch_WhenNoInstructionMatches_ThrowsWithMatchCount()
    {
        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    new[] { "load", "return" },
                    instruction => instruction == "anchor",
                    "Fixture.Target anchor"));

        StringAssert.Contains(exception.Message, "0");
        StringAssert.Contains(exception.Message, "Fixture.Target anchor");
    }

    [TestMethod]
    public void RequireSingleMatch_WhenOneInstructionMatches_ReturnsThatInstruction()
    {
        var instruction = HarmonyPatchContractVerifier.RequireSingleMatch(
            new[] { "load", "anchor", "return" },
            candidate => candidate == "anchor",
            "Fixture.Target anchor");

        Assert.AreEqual("anchor", instruction);
    }

    [TestMethod]
    public void RequireSingleMatch_WhenTwoInstructionsMatch_ThrowsWithMatchCount()
    {
        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    new[] { "load", "anchor", "anchor", "return" },
                    instruction => instruction == "anchor",
                    "Fixture.Target anchor"));

        StringAssert.Contains(exception.Message, "2");
        StringAssert.Contains(exception.Message, "Fixture.Target anchor");
    }

    [TestMethod]
    public void RequireSingleMatch_WhenPredicateThrows_WrapsContractAndOriginalException()
    {
        var predicateFailure = new InvalidOperationException(
            "fixture predicate failure");

        var exception = Assert.ThrowsExactly<
            HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    new[] { "load", "anchor", "return" },
                    instruction => instruction == "anchor"
                        ? throw predicateFailure
                        : false,
                    "Fixture.Target anchor"));

        StringAssert.Contains(exception.Message, "Fixture.Target anchor");
        Assert.AreSame(predicateFailure, exception.InnerException);
    }

    [TestMethod]
    public void RequireSingleMatch_WhenSeveralCandidatesExist_EvaluatesEachCandidateOnce()
    {
        var evaluationCountByCandidate = new Dictionary<string, int>();

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
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
                "Fixture.Target anchor"));

        Assert.HasCount(4, evaluationCountByCandidate);
        Assert.IsTrue(evaluationCountByCandidate.Values.All(count => count == 1));
    }

    [TestMethod]
    public void ActiveHarmonyPatchDescriptor_WhenConstructed_PreservesExactMetadata()
    {
        var targetMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var patchMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix));

        var descriptor = new ActiveHarmonyPatchDescriptor(
            targetMethod,
            patchMethod,
            "permitted.owner",
            priority: -123);

        Assert.AreSame(targetMethod, descriptor.TargetMethod);
        Assert.AreSame(patchMethod, descriptor.PatchMethod);
        Assert.AreEqual("permitted.owner", descriptor.HarmonyOwner);
        Assert.AreEqual(-123, descriptor.Priority);
    }

    [TestMethod]
    public void ActiveHarmonyPatchDescriptor_WhenTargetMethodIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ActiveHarmonyPatchDescriptor(
                null!,
                RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix)),
                "permitted.owner",
                priority: 0));
    }

    [TestMethod]
    public void ActiveHarmonyPatchDescriptor_WhenPatchMethodIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ActiveHarmonyPatchDescriptor(
                RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.KleiTarget)),
                null!,
                "permitted.owner",
                priority: 0));
    }

    [TestMethod]
    public void ActiveHarmonyPatchDescriptor_WhenHarmonyOwnerIsBlank_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ActiveHarmonyPatchDescriptor(
                RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.KleiTarget)),
                RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix)),
                " ",
                priority: 0));
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenNoSkippingPrefixExists_ReturnsTrue()
    {
        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            RequireFixtureMethod(nameof(HarmonyAuthorityFixture.KleiTarget)),
            Array.Empty<ActiveHarmonyPatchDescriptor>(),
            Array.Empty<string>());

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenOnlyPermittedSkippingOwnerExists_ReturnsTrue()
    {
        var targetMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix),
                "permitted.owner",
                priority: 400),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenForeignSkippingPrefixExists_ReturnsFalse()
    {
        var targetMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignSkippingPrefix),
                "foreign.owner",
                priority: 0),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenForeignNonSkippingPrefixExists_ReturnsTrue()
    {
        var targetMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignObservingPrefix),
                "foreign.owner",
                priority: 0),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            Array.Empty<string>());

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenSkippingPrefixTargetsAnotherMethod_ReturnsTrue()
    {
        var activePatches = new[]
        {
            CreateDescriptor(
                RequireFixtureMethod(
                    nameof(HarmonyAuthorityFixture.OtherKleiTarget)),
                nameof(HarmonyAuthorityFixture.ForeignSkippingPrefix),
                "foreign.owner",
                priority: 0),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            RequireFixtureMethod(nameof(HarmonyAuthorityFixture.KleiTarget)),
            activePatches,
            Array.Empty<string>());

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenSeveralPrioritiesExist_DoesNotInferAuthorityFromOrder()
    {
        var targetMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix),
                "permitted.owner",
                priority: int.MaxValue),
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignObservingPrefix),
                "observer.owner",
                priority: 0),
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.ForeignSkippingPrefix),
                "foreign.owner",
                priority: int.MinValue),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void VerifyKleiAuthority_WhenOwnerDiffersOnlyByCase_ReturnsFalse()
    {
        var targetMethod = RequireFixtureMethod(
            nameof(HarmonyAuthorityFixture.KleiTarget));
        var activePatches = new[]
        {
            CreateDescriptor(
                targetMethod,
                nameof(HarmonyAuthorityFixture.PermittedSkippingPrefix),
                "PERMITTED.OWNER",
                priority: 0),
        };

        var result = HarmonyPatchContractVerifier.VerifyKleiAuthority(
            targetMethod,
            activePatches,
            new[] { "permitted.owner" });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void WorldInventoryUpdateContract_WhenInstalledShapeMatches_ReturnsExactPrivateInstanceMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(WorldInventoryUpdateTargetContractFixture),
            "Update",
            DeclaredMemberVisibility.NonPublic,
            typeof(void),
            Array.Empty<Type>());

        AssertExactInstanceMethod(
            method,
            typeof(WorldInventoryUpdateTargetContractFixture),
            isPublic: false,
            Array.Empty<Type>());
    }

    [TestMethod]
    public void FetchListStatusRenderContract_WhenInstalledShapeMatches_ReturnsExactPublicInstanceMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(FetchListStatusRenderTargetContractFixture),
            "Render200ms",
            DeclaredMemberVisibility.Public,
            typeof(void),
            Array.Empty<Type>());

        AssertExactInstanceMethod(
            method,
            typeof(FetchListStatusRenderTargetContractFixture),
            isPublic: true,
            Array.Empty<Type>());
    }

    [TestMethod]
    public void AuthoritativeFetchTargetContracts_WhenInstalledShapesMatch_ReturnExactMethods()
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

        AssertExactInstanceMethod(
            addChore,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: true,
            [typeof(ChoreContractFixture)]);
        AssertExactInstanceMethod(
            removeChore,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: true,
            [typeof(ChoreContractFixture)]);
        AssertExactInstanceMethod(
            updateStorageFetchableBits,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: false,
            Array.Empty<Type>());
        AssertExactInstanceMethod(
            clearableHasDestination,
            typeof(GlobalChoreProviderFetchContractFixture),
            isPublic: true,
            typeof(bool),
            [typeof(PickupableContractFixture)]);
        AssertExactInstanceMethod(
            onTagsChanged,
            typeof(FetchChoreTagChangeContractFixture),
            isPublic: false,
            [typeof(object)]);
    }

    [TestMethod]
    public void AuthoritativeFetchTargetContracts_WhenOnlyOverloadsArePresent_ThrowContractViolations()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "AddChore",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(ChoreContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "RemoveChore",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(ChoreContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "UpdateStorageFetchableBits",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>()));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProviderFetchOverloadOnlyFixture),
                "ClearableHasDestination",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                [typeof(PickupableContractFixture)]));
    }

    [TestMethod]
    public void FetchChoreOnTagsChangedContract_WhenParameterTypeChanges_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchChoreChangedTagEventContractFixture),
                "OnTagsChanged",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                [typeof(object)]));
    }

    [TestMethod]
    public void AuthoritativeFetchTraversalInstructionContract_WhenCapturedInstalledShapeMatches_ResolvesParentAndSelectedChoreAnchorsOnce()
    {
        var anchors = RequireAuthoritativeFetchTraversalInstructionAnchors(
            CapturedAuthoritativeFetchTraversalInstructions());

        Assert.AreEqual(0, anchors.ParentWorldSectionStartIndex);
        Assert.AreEqual(13, anchors.SelectedFetchChoreIndex);
        Assert.AreEqual(2, anchors.SortedWorldIdsLocalIndex);
        Assert.AreEqual(3, anchors.SortedWorldIdIndexLocalIndex);
        Assert.AreEqual(6, anchors.SelectedFetchChoreLocalIndex);
    }

    [TestMethod]
    public void AuthoritativeFetchTraversalInstructionContract_WhenSecondFetchMapTraversalExists_ThrowsContractViolation()
    {
        var instructions = CapturedAuthoritativeFetchTraversalInstructions();
        instructions.AddRange(instructions.GetRange(0, 6));

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireAuthoritativeFetchTraversalInstructionAnchors(instructions));
    }

    [TestMethod]
    public void AuthoritativeFetchTraversalInstructionContract_WhenSelectedFetchChoreAnchorIsMissing_ThrowsContractViolation()
    {
        var instructions = CapturedAuthoritativeFetchTraversalInstructions();
        instructions[15] = FieldInstruction("FetchChore.unrelatedTags");

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireAuthoritativeFetchTraversalInstructionAnchors(instructions));
    }

    [TestMethod]
    public void AuthoritativeFetchAdapterSource_WhenInspected_ExposesManualFailClosedContractsOnly()
    {
        var adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiAuthoritativeFetchTemperatureEligibilityPatches.cs");
        Assert.IsTrue(
            File.Exists(adapterPath),
            $"Missing authoritative fetch adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);

        StringAssert.Contains(source, "ResolveGlobalChoreProviderAddChoreTarget");
        StringAssert.Contains(source, "ResolveGlobalChoreProviderRemoveChoreTarget");
        StringAssert.Contains(source, "ResolveFetchChoreOnTagsChangedTarget");
        StringAssert.Contains(
            source,
            "ResolveGlobalChoreProviderUpdateStorageFetchableBitsTarget");
        StringAssert.Contains(
            source,
            "ResolveGlobalChoreProviderClearableHasDestinationTarget");
        StringAssert.Contains(source, "UpdateStorageFetchableBitsTranspiler");
        StringAssert.Contains(source, "BeginParentWorldFetchMapSection");
        StringAssert.Contains(source, "RecordSelectedFetchChore");
        StringAssert.Contains(source, "TryPublishFetchTemperatureEligibility");
        StringAssert.Contains(
            source,
            "ClearableDestinationSweepEligibility.AllowsClearing");
        Assert.IsFalse(source.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("operand.ToString", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("HashSet<Tag>[]", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("storageFetchableTagsPerTemperatureIndex", StringComparison.Ordinal));
    }

    [TestMethod]
    public void KleiPickupGroupingTargetContracts_WhenInstalledShapesMatch_ReturnExactMethods()
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

        AssertExactInstanceMethod(
            updatePickups,
            typeof(FetchablesByPrefabIdPickupGroupingContractFixture),
            isPublic: true,
            [typeof(NavigatorPickupGroupingContractFixture), typeof(int)]);
        Assert.AreSame(comparerType, compare.DeclaringType);
        Assert.IsTrue(compare.IsPrivate);
        Assert.IsTrue(compare.IsStatic);
        Assert.AreSame(typeof(int), compare.ReturnType);
        Assert.AreSequenceEqual(
            new[]
            {
                typeof(PickupGroupingCandidateContractFixture),
                typeof(PickupGroupingCandidateContractFixture)
            },
            compare.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [TestMethod]
    public void KleiPickupGroupingCompareContract_WhenCandidateTypeChanges_ThrowsContractViolation()
    {
        var comparerType = HarmonyPatchContractVerifier.RequireNestedType(
            typeof(FetchManagerChangedPickupGroupingContractFixture),
            "PickupComparerIncludingPriority",
            DeclaredMemberVisibility.NonPublic);

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                comparerType,
                "Compare",
                DeclaredMemberVisibility.NonPublic,
                typeof(int),
                [
                    typeof(PickupGroupingCandidateContractFixture),
                    typeof(PickupGroupingCandidateContractFixture)
                ]));
    }

    [TestMethod]
    public void KleiPickupGroupingInstructionContract_WhenCapturedInstalledShapesMatch_ResolvesComparatorAndSuppressionAnchorsOnce()
    {
        var anchors = RequireKleiPickupGroupingInstructionAnchors(
            CapturedPickupGroupingComparatorInstructions(),
            CapturedPickupDuplicateSuppressionInstructions());

        Assert.AreEqual(0, anchors.ComparatorExtensionIndex);
        Assert.AreEqual(6, anchors.DuplicateSuppressionExtensionIndex);
        Assert.AreEqual(0, anchors.PreviousPickupLocalIndex);
        Assert.AreEqual(6, anchors.CurrentPickupLocalIndex);
        Assert.AreEqual(
            "TemperatureEligibilityClassKey",
            anchors.SharedSemanticKeyIdentity);
    }

    [TestMethod]
    public void KleiPickupGroupingInstructionContract_WhenComparatorAnchorIsDuplicated_ThrowsContractViolation()
    {
        var comparatorInstructions =
            CapturedPickupGroupingComparatorInstructions();
        comparatorInstructions.AddRange(
            CapturedPickupGroupingComparatorInstructions());

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireKleiPickupGroupingInstructionAnchors(
                comparatorInstructions,
                CapturedPickupDuplicateSuppressionInstructions()));
    }

    [TestMethod]
    public void KleiPickupGroupingInstructionContract_WhenSuppressionAnchorIsMissing_ThrowsContractViolation()
    {
        var suppressionInstructions =
            CapturedPickupDuplicateSuppressionInstructions();
        suppressionInstructions[13] = OperationInstruction("ldc.i4.0");

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireKleiPickupGroupingInstructionAnchors(
                CapturedPickupGroupingComparatorInstructions(),
                suppressionInstructions));
    }

    [TestMethod]
    public void KleiPickupGroupingInstructionContract_WhenCandidateFieldTypeChanges_ThrowsContractViolation()
    {
        var comparatorInstructions =
            CapturedPickupGroupingComparatorInstructions();
        comparatorInstructions[1] = FieldAddressInstruction(
            "ChangedPickupCandidate.masterPriority");

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireKleiPickupGroupingInstructionAnchors(
                comparatorInstructions,
                CapturedPickupDuplicateSuppressionInstructions()));
    }

    [TestMethod]
    public void KleiPickupWorkerReadContract_WhenEveryReadIsVerifiedManagedState_AcceptsContract()
    {
        RequireVerifiedKleiPickupWorkerReadContract(
            CapturedVerifiedKleiPickupWorkerReads());
    }

    [TestMethod]
    public void KleiPickupWorkerReadContract_WhenPickupIdentityRequiresUnityNativeCall_ThrowsContractViolation()
    {
        var workerReads = CapturedVerifiedKleiPickupWorkerReads();
        workerReads[4] = "UnityEngine.Object.GetInstanceID";

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireVerifiedKleiPickupWorkerReadContract(workerReads));
    }

    [TestMethod]
    public void KleiPickupGroupingAdapterSource_WhenInspected_UsesOneFullKeyAndOnlyVerifiedManagedWorkerReads()
    {
        var adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiPickupTemperatureGroupingPatches.cs");
        Assert.IsTrue(
            File.Exists(adapterPath),
            $"Missing Klei pickup grouping adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);

        StringAssert.Contains(
            source,
            "ResolveFetchablesByPrefabIdUpdatePickupsTarget");
        StringAssert.Contains(
            source,
            "ResolvePickupComparerIncludingPriorityCompareTarget");
        StringAssert.Contains(
            source,
            "VerifyKleiPickupGroupingPatchContracts");
        StringAssert.Contains(source, "PatchProcessor.GetOriginalInstructions");
        StringAssert.Contains(source, "UpdatePickupsPrefix");
        StringAssert.Contains(source, "UpdatePickupsTranspiler");
        StringAssert.Contains(source, "UpdatePickupsPostfix");
        StringAssert.Contains(source, "UpdatePickupsFinalizer");
        StringAssert.Contains(source, "PickupComparerTranspiler");
        StringAssert.Contains(source, "GetTemperatureEligibilityClassKey");
        StringAssert.Contains(source, "TemperatureEligibilityClassKey.CompareTo");
        StringAssert.Contains(source, "TemperatureEligibilityClassKey.Equals");
        StringAssert.Contains(source, "KPrefabID.HasTag");
        StringAssert.Contains(source, "PrimaryElement.InternalTemperature");
        StringAssert.Contains(source, "kPrefabId.InstanceID");
        StringAssert.Contains(source, "kPrefabId.PrefabTag");
        StringAssert.Contains(source, "ThreadConfinedSessionSlot");
        Assert.AreEqual(
            5,
            CountOrdinalOccurrences(
                source,
                "GetTemperatureEligibilityClassKey("));
        Assert.IsFalse(source.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("operand.ToString", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(".LocalIndex()", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GetComponent", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GetInstanceID", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ClusterManager", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("PrimaryElement.Temperature", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AsyncLocal", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(".PartitionDefinitionId", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(".IntervalOrdinal", StringComparison.Ordinal));
    }

    [TestMethod]
    public void KleiDirectEligibilityTargetContracts_WhenInstalledShapesMatch_ReturnExactMethods()
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

        Assert.IsTrue(isFetchablePickup.IsStatic);
        Assert.IsTrue(isFetchablePickup.IsPublic);
        Assert.AreSame(typeof(bool), isFetchablePickup.ReturnType);
        AssertExactInstanceMethod(
            collectChores,
            typeof(ClearableManagerDirectEligibilityContractFixture),
            isPublic: true,
            [
                typeof(List<DirectGlobalFetchContractFixture>),
                typeof(DirectChoreConsumerStateContractFixture),
                typeof(List<DirectChoreContextContractFixture>),
                typeof(List<DirectChoreContextContractFixture>)
            ]);
        AssertExactInstanceMethod(
            begin,
            typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture),
            isPublic: true,
            [typeof(DirectChoreContextContractFixture)]);
        AssertExactInstanceMethod(
            candidateDelegate,
            typeof(FetchAreaCandidateClosureDirectEligibilityContractFixture),
            isPublic: false,
            typeof(DirectIterationInstructionContractFixture),
            [typeof(object), typeof(object)]);
        Assert.AreSame(
            typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture),
            closureOwnerField.FieldType);
    }

    [TestMethod]
    public void KleiDirectEligibilityIsFetchablePickupContract_WhenReturnTypeChanges_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(FetchManagerChangedDirectEligibilityContractFixture),
                "IsFetchablePickup",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                [
                    typeof(DirectPickupableContractFixture),
                    typeof(DirectFetchChoreContractFixture),
                    typeof(DirectStorageContractFixture)
                ]));
    }

    [TestMethod]
    public void KleiDirectEligibilityDelegateContract_WhenClosureOwnerFieldChanges_ThrowsContractViolation()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FetchAreaChangedCandidateClosureDirectEligibilityContractFixture),
                "StatesInstance",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(FetchAreaStatesInstanceDirectEligibilityContractFixture)));

        var instructions = CapturedFetchAreaCandidateDelegateInstructions();
        instructions[1] = FieldInstruction(
            "FetchAreaCandidateClosure.changedStatesInstance");
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireKleiDirectEligibilityInstructionAnchors(
                CapturedClearableCollectChoresInstructions(),
                CapturedFetchAreaBeginInstructions(),
                instructions));
    }

    [TestMethod]
    public void KleiDirectEligibilityInstructionContract_WhenCapturedInstalledShapesMatch_ResolvesEveryTypedAnchorOnce()
    {
        var anchors = RequireKleiDirectEligibilityInstructionAnchors(
            CapturedClearableCollectChoresInstructions(),
            CapturedFetchAreaBeginInstructions(),
            CapturedFetchAreaCandidateDelegateInstructions());

        Assert.AreEqual(5, anchors.ClearablePickupableLocalIndex);
        Assert.AreEqual(10, anchors.ClearableFetchLocalIndex);
        Assert.AreEqual(14, anchors.CandidateFetchChoreLocalIndex);
        Assert.AreEqual(0, anchors.DelegatePickupableLocalIndex);
        Assert.AreEqual(20, anchors.ClearableEligibilityExtensionIndex);
        Assert.AreEqual(6, anchors.FetchChoreContainmentExtensionIndex);
        Assert.AreEqual(6, anchors.DelegateCanReachCallIndex);
    }

    [TestMethod]
    public void KleiDirectEligibilityInstructionContract_WhenTwoCanReachCallsMatch_ThrowsContractViolation()
    {
        var delegateInstructions =
            CapturedFetchAreaCandidateDelegateInstructions();
        delegateInstructions.AddRange(
            CapturedFetchAreaCandidateDelegateInstructions());

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireKleiDirectEligibilityInstructionAnchors(
                CapturedClearableCollectChoresInstructions(),
                CapturedFetchAreaBeginInstructions(),
                delegateInstructions));
    }

    [TestMethod]
    public void KleiDirectEligibilityInstructionContract_WhenDirectResultBranchIsMissing_ThrowsContractViolation()
    {
        var delegateInstructions =
            CapturedFetchAreaCandidateDelegateInstructions();
        delegateInstructions[7] = BranchInstruction("brfalse.s");

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireKleiDirectEligibilityInstructionAnchors(
                CapturedClearableCollectChoresInstructions(),
                CapturedFetchAreaBeginInstructions(),
                delegateInstructions));
    }

    [TestMethod]
    public void KleiDirectEligibilityAdapterSource_WhenInspected_UsesCentralAllocationFreeChecksAndManualFailClosedContracts()
    {
        var adapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiDirectDeliveryEligibilityPatches.cs");
        Assert.IsTrue(
            File.Exists(adapterPath),
            $"Missing Klei direct delivery eligibility adapter source {adapterPath}.");
        var source = File.ReadAllText(adapterPath);

        StringAssert.Contains(
            source,
            "ResolveFetchManagerIsFetchablePickupTarget");
        StringAssert.Contains(
            source,
            "ResolveClearableManagerCollectChoresTarget");
        StringAssert.Contains(
            source,
            "ResolveFetchAreaChoreStatesInstanceBeginTarget");
        StringAssert.Contains(
            source,
            "ResolveFetchAreaChoreCandidateDelegateTarget");
        StringAssert.Contains(
            source,
            "VerifyKleiDirectDeliveryEligibilityPatchContracts");
        StringAssert.Contains(source, "IsFetchablePickupPostfix");
        StringAssert.Contains(source, "ClearableManagerCollectChoresTranspiler");
        StringAssert.Contains(source, "FetchAreaChoreBeginTranspiler");
        StringAssert.Contains(source, "FetchAreaCandidateDelegateTranspiler");
        StringAssert.Contains(source, "IsPickupAllowedForDestination");
        StringAssert.Contains(
            source,
            "FetchChoreTemperatureConstraintContainment.CanCombine");
        StringAssert.Contains(
            source,
            "TemperatureLimitComponents.TryGetConstraint");
        StringAssert.Contains(source, "constraint.Allows");
        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(source, "constraint.Allows("),
            "The direct adapter must delegate one canonical temperature decision rather than duplicating its bounds.");
        Assert.AreEqual(
            1,
            CountOrdinalOccurrences(source, "primaryElement.Temperature"),
            "The direct adapter must read a candidate's live temperature exactly once in its shared check.");
        StringAssert.Contains(source, "if (!__result)");
        StringAssert.Contains(source, "if (!consumer.CanReach(approachable))");
        StringAssert.Contains(source, "PatchProcessor.GetOriginalInstructions");
        Assert.IsFalse(source.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("operand.ToString", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("<>c__DisplayClass", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GetComponent", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("CaptureSnapshot", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("CurrentFetchTemperatureEligibility", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("HashSet<Tag>[]", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("System.Linq", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("TemperatureLimit.Get", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ValueTuple", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Tuple<", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WorldInventoryInstructionContract_WhenCapturedInstalledShapeMatches_ResolvesEverySemanticAnchorOnce()
    {
        var anchors = RequireWorldInventoryInstructionAnchors(
            CapturedWorldInventoryUpdateInstructions());

        Assert.IsTrue(anchors.InventoryEntryCaptureIndex <
            anchors.ResourceTagStartIndex);
        Assert.IsTrue(anchors.ResourceTagStartIndex <
            anchors.FilteredPickupContributionIndex);
        Assert.IsTrue(anchors.FilteredPickupContributionIndex <
            anchors.ResourceTagCompletionIndex);
        Assert.AreEqual(4, anchors.ResourceTagLocalIndex);
        Assert.AreEqual(5, anchors.AccumulatedAmountLocalIndex);
        Assert.AreEqual(7, anchors.PickupableLocalIndex);
    }

    [TestMethod]
    public void WorldInventoryInstructionContract_WhenResourceTagStartIsMissing_ThrowsContractViolation()
    {
        var instructions = CapturedWorldInventoryUpdateInstructions();
        instructions[2] = CallInstruction("Wrong.get_Key");

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireWorldInventoryInstructionAnchors(instructions));
    }

    [TestMethod]
    public void WorldInventoryInstructionContract_WhenFilteredPickupContributionIsDuplicated_ThrowsContractViolation()
    {
        var instructions = CapturedWorldInventoryUpdateInstructions();
        instructions.AddRange(instructions.GetRange(6, 10));

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireWorldInventoryInstructionAnchors(instructions));
    }

    [TestMethod]
    public void WorldInventoryInstructionContract_WhenPickupGetterIsNotTotalAmount_ThrowsContractViolation()
    {
        var instructions = CapturedWorldInventoryUpdateInstructions();
        instructions[13] = CallInstruction(
            "Pickupable.get_FetchTotalAmount");

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireWorldInventoryInstructionAnchors(instructions));
    }

    [TestMethod]
    public void FetchListStatusInstructionContract_WhenCapturedInstalledShapeMatches_ResolvesEarlyBranchAndAssignmentOnce()
    {
        var anchor = RequireFetchListStatusInstructionAnchor(
            CapturedFetchListStatusRenderInstructions());

        Assert.AreEqual(1, anchor.WorldIdLocalIndex);
        Assert.AreEqual(28, anchor.FetchListLocalIndex);
        Assert.AreEqual(33, anchor.ResourceTagLocalIndex);
        Assert.AreEqual(34, anchor.RemainingAmountLocalIndex);
        Assert.AreEqual(37, anchor.FetchableAmountLocalIndex);
        Assert.AreEqual(38, anchor.MinimumRequiredAmountLocalIndex);
    }

    [TestMethod]
    public void FetchListStatusInstructionContract_WhenFetchableAssignmentIsMissing_ThrowsContractViolation()
    {
        var instructions = CapturedFetchListStatusRenderInstructions();
        instructions[15] = StoreLocalInstruction(99);

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireFetchListStatusInstructionAnchor(instructions));
    }

    [TestMethod]
    public void FetchListStatusInstructionContract_WhenEarlyInsufficientBranchIsReordered_ThrowsContractViolation()
    {
        var instructions = CapturedFetchListStatusRenderInstructions();
        (instructions[20], instructions[21]) =
            (instructions[21], instructions[20]);

        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            RequireFetchListStatusInstructionAnchor(instructions));
    }

    [TestMethod]
    public void InventoryAndStatusAdapterSources_WhenInspected_ExposeManualFailClosedContractsOnly()
    {
        var inventoryAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "KleiWorldInventoryTemperaturePatches.cs");
        var statusAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "TemperatureStatusAvailabilityPatches.cs");
        Assert.IsTrue(
            File.Exists(inventoryAdapterPath),
            $"Missing Klei inventory adapter source {inventoryAdapterPath}.");
        Assert.IsTrue(
            File.Exists(statusAdapterPath),
            $"Missing status adapter source {statusAdapterPath}.");
        var inventorySource = File.ReadAllText(inventoryAdapterPath);
        var statusSource = File.ReadAllText(statusAdapterPath);
        var combinedSource =
            inventorySource + Environment.NewLine + statusSource;

        StringAssert.Contains(
            inventorySource,
            "ResolveWorldInventoryUpdateTarget");
        StringAssert.Contains(
            inventorySource,
            "WorldInventoryUpdateTranspiler");
        StringAssert.Contains(inventorySource, "WorldInventoryUpdatePrefix");
        StringAssert.Contains(inventorySource, "WorldInventoryUpdatePostfix");
        StringAssert.Contains(inventorySource, "WorldInventoryUpdateFinalizer");
        StringAssert.Contains(inventorySource, "BeginResourceTagEnumeration");
        StringAssert.Contains(
            inventorySource,
            "IsTemperatureCollectionActive");
        StringAssert.Contains(inventorySource, "ObserveResourceTagForCoverage");
        StringAssert.Contains(
            inventorySource,
            "RecordFilteredPickupTemperatureAmount");
        StringAssert.Contains(inventorySource, "CompleteResourceTagEnumeration");
        StringAssert.Contains(
            inventorySource,
            "PublishWorldResourceTagCoverage");
        StringAssert.Contains(
            inventorySource,
            "PublishWorldResourceTemperatureSeries");
        StringAssert.Contains(inventorySource, "___firstUpdate");
        StringAssert.Contains(
            statusSource,
            "ResolveFetchListStatusItemUpdaterRender200msTarget");
        StringAssert.Contains(statusSource, "Render200msTranspiler");
        StringAssert.Contains(
            statusSource,
            "ReplaceFetchableAmountWhenInventoryIsComplete");
        StringAssert.Contains(
            statusSource,
            "TemperatureStatusAvailabilityDecision.ShouldTryReplacement");
        StringAssert.Contains(
            statusSource,
            "GetTemperatureConstrainedAmountAvailability");
        Assert.IsTrue(
            CountOrdinalOccurrences(
                combinedSource,
                "HarmonyPatchContractVerifier.RequireSingleMatch") >= 5);
        Assert.IsTrue(
            CountOrdinalOccurrences(
                inventorySource,
                "HarmonyPatchContractVerifier.RequireInstanceMethod") >= 1);
        Assert.IsTrue(
            CountOrdinalOccurrences(
                statusSource,
                "HarmonyPatchContractVerifier.RequireInstanceMethod") >= 1);
        Assert.IsFalse(
            combinedSource.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("CheckTemperatureForStatusItems", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("WorldContainers", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("worldAmounts", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("FastTrack", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GameDestroyInstancesContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(GameDestroyInstancesContractFixture),
            "DestroyInstances",
            DeclaredMemberVisibility.NonPublic,
            typeof(void),
            Array.Empty<Type>());

        AssertExactInstanceMethod(
            method,
            typeof(GameDestroyInstancesContractFixture),
            isPublic: false,
            Array.Empty<Type>());
    }

    [TestMethod]
    public void ClusterManagerRegisterWorldContainerContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(ClusterManagerWorldLifecycleContractFixture),
            "RegisterWorldContainer",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(WorldContainerLifecycleContractFixture)]);

        AssertExactInstanceMethod(
            method,
            typeof(ClusterManagerWorldLifecycleContractFixture),
            isPublic: true,
            [typeof(WorldContainerLifecycleContractFixture)]);
    }

    [TestMethod]
    public void ClusterManagerUnregisterWorldContainerContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(ClusterManagerWorldLifecycleContractFixture),
            "UnregisterWorldContainer",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(WorldContainerLifecycleContractFixture)]);

        AssertExactInstanceMethod(
            method,
            typeof(ClusterManagerWorldLifecycleContractFixture),
            isPublic: true,
            [typeof(WorldContainerLifecycleContractFixture)]);
    }

    [TestMethod]
    public void WorldContainerSetParentIdxContract_WhenInstalledShapeMatches_ReturnsExactMethod()
    {
        var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
            typeof(WorldContainerLifecycleContractFixture),
            "SetParentIdx",
            DeclaredMemberVisibility.Public,
            typeof(void),
            [typeof(int)]);

        AssertExactInstanceMethod(
            method,
            typeof(WorldContainerLifecycleContractFixture),
            isPublic: true,
            [typeof(int)]);
    }

    [TestMethod]
    public void LifecycleTargetContracts_WhenOnlyOverloadsMatch_ThrowContractViolations()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GameDestroyInstancesOverloadOnlyFixture),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>()));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleOverloadOnlyFixture),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleOverloadOnlyFixture),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainerSetParentIdxOverloadOnlyFixture),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(int)]));
    }

    [TestMethod]
    public void LifecycleTargetContracts_WhenReturnTypesChange_ThrowContractViolations()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GameDestroyInstancesChangedReturnFixture),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>()));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleChangedReturnFixture),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleChangedReturnFixture),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainerSetParentIdxChangedReturnFixture),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(int)]));
    }

    [TestMethod]
    public void LifecycleTargetContracts_WhenStaticnessChanges_ThrowContractViolations()
    {
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GameDestroyInstancesStaticFixture),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>()));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleStaticFixture),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManagerWorldLifecycleStaticFixture),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(WorldContainerLifecycleContractFixture)]));
        Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainerSetParentIdxStaticFixture),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                [typeof(int)]));
    }

    [TestMethod]
    public void LifecycleAdapterSources_WhenInspected_DeclareExactResolversWithoutPatchDiscoveryAttributes()
    {
        var shutdownAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "DeliveryTemperatureGameSessionShutdownPatches.cs");
        var worldTopologyAdapterPath = ResolveProductionSourcePath(
            "KleiImplementationAdapters",
            "WorldParentTopologyPatches.cs");
        Assert.IsTrue(
            File.Exists(shutdownAdapterPath),
            $"Missing lifecycle adapter source {shutdownAdapterPath}.");
        Assert.IsTrue(
            File.Exists(worldTopologyAdapterPath),
            $"Missing world-topology adapter source {worldTopologyAdapterPath}.");
        var combinedSource =
            File.ReadAllText(shutdownAdapterPath) +
            Environment.NewLine +
            File.ReadAllText(worldTopologyAdapterPath);

        Assert.AreEqual(
            4,
            CountOrdinalOccurrences(
                combinedSource,
                "HarmonyPatchContractVerifier.RequireInstanceMethod"));
        StringAssert.Contains(
            combinedSource,
            "ResolveGameDestroyInstancesTarget");
        StringAssert.Contains(
            combinedSource,
            "ResolveClusterManagerRegisterWorldContainerTarget");
        StringAssert.Contains(
            combinedSource,
            "ResolveClusterManagerUnregisterWorldContainerTarget");
        StringAssert.Contains(
            combinedSource,
            "ResolveWorldContainerSetParentIdxTarget");
        StringAssert.Contains(combinedSource, "typeof(Game)");
        StringAssert.Contains(combinedSource, "\"DestroyInstances\"");
        StringAssert.Contains(combinedSource, "Array.Empty<Type>()");
        Assert.AreEqual(
            2,
            CountOrdinalOccurrences(
                combinedSource,
                "new[] { typeof(WorldContainer) }"));
        StringAssert.Contains(
            combinedSource,
            "\"RegisterWorldContainer\"");
        StringAssert.Contains(
            combinedSource,
            "\"UnregisterWorldContainer\"");
        StringAssert.Contains(combinedSource, "\"SetParentIdx\"");
        StringAssert.Contains(
            combinedSource,
            "new[] { typeof(int) }");
        Assert.AreEqual(
            4,
            CountOrdinalOccurrences(combinedSource, "typeof(void)"));
        StringAssert.Contains(
            combinedSource,
            "DeclaredMemberVisibility.NonPublic");
        StringAssert.Contains(
            combinedSource,
            "DeclaredMemberVisibility.Public");
        Assert.IsFalse(
            combinedSource.Contains("AccessTools", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("[HarmonyPatch", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("[HarmonyPrefix", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("[HarmonyPostfix", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("[HarmonyFinalizer", StringComparison.Ordinal));
        Assert.IsFalse(
            combinedSource.Contains("OnLoadLevel", StringComparison.Ordinal));
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
                    CallInstruction("HashSet<Pickupable>.GetEnumerator")),
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
            CallInstruction("HashSet<Pickupable>.GetEnumerator"),
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

    private static void AssertExactInstanceMethod(
        MethodInfo method,
        Type expectedDeclaringType,
        bool isPublic,
        IReadOnlyList<Type> expectedParameterTypes) =>
        AssertExactInstanceMethod(
            method,
            expectedDeclaringType,
            isPublic,
            typeof(void),
            expectedParameterTypes);

    private static void AssertExactInstanceMethod(
        MethodInfo method,
        Type expectedDeclaringType,
        bool isPublic,
        Type expectedReturnType,
        IReadOnlyList<Type> expectedParameterTypes)
    {
        Assert.AreSame(expectedDeclaringType, method.DeclaringType);
        Assert.AreEqual(isPublic, method.IsPublic);
        Assert.IsFalse(method.IsStatic);
        Assert.AreSame(expectedReturnType, method.ReturnType);
        Assert.AreSequenceEqual(
            expectedParameterTypes,
            method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
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

    private static ActiveHarmonyPatchDescriptor CreateDescriptor(
        MethodBase targetMethod,
        string patchMethodName,
        string harmonyOwner,
        int priority) =>
        new ActiveHarmonyPatchDescriptor(
            targetMethod,
            RequireFixtureMethod(patchMethodName),
            harmonyOwner,
            priority);

    private static MethodInfo RequireFixtureMethod(string methodName)
    {
        var method = typeof(HarmonyAuthorityFixture).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
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
            NavigatorPickupGroupingContractFixture navigator,
            int minimumPathCost)
        {
            _ = navigator;
            _ = minimumPathCost;
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
        public void Render200ms()
        {
        }
    }

    private sealed class GameDestroyInstancesContractFixture
    {
        private void DestroyInstances()
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
