namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Names the mutually exclusive meanings carried by a pickup class key.
    /// </summary>
    internal enum TemperatureEligibilityClassificationKind
    {
        NoTemperatureDistinction,
        OptimizedPartitionInterval,
        ExactTemperatureDecisionBucket,
        MissingPrimaryElement
    }
}
