#nullable enable

namespace DeliveryTemperatureLimit
{
    internal enum DeclaredModMatchState
    {
        NotMatched,
        Matched,
        Ambiguous,
        InspectionUnavailable
    }

    internal enum RuntimeAuthorityObservation
    {
        DoesNotOwn,
        OwnsCompatible,
        OwnsIncompatible,
        OwnershipUnavailable
    }

    internal enum IntegrationContractState
    {
        NotEvaluated,
        Compatible,
        Incompatible,
        VerificationUnavailable
    }

    internal enum IntegrationCapabilityDisposition
    {
        NotApplicable,
        Selected,
        Ready,
        Unavailable,
        ActivationBlocking
    }

    internal enum ExternalModIntegrationCategory
    {
        ExclusiveRuntimeAuthority,
        AdditiveInteroperability
    }
}
