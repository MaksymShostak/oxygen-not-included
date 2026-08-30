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
