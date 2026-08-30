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

    private static TranspilerInstructionFixture StaticFieldInstruction(
        string memberIdentity) =>
        new("static-field", memberIdentity, null);

    private static TranspilerInstructionFixture LoadLocalInstruction(
        int? localIndex) =>
        new("load-local", null, localIndex);

    private static TranspilerInstructionFixture StoreLocalInstruction(
        int? localIndex) =>
        new("store-local", null, localIndex);

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
        IReadOnlyList<Type> expectedParameterTypes)
    {
        Assert.AreSame(expectedDeclaringType, method.DeclaringType);
        Assert.AreEqual(isPublic, method.IsPublic);
        Assert.IsFalse(method.IsStatic);
        Assert.AreSame(typeof(void), method.ReturnType);
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
