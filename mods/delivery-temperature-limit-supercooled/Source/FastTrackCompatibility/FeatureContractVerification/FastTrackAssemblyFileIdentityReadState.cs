#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Describes whether the loaded FastTrack assembly could be tied to one
    /// readable physical file. Only <see cref="Success"/> can authorize an active
    /// third-party replacement.
    /// </summary>
    internal enum FastTrackAssemblyFileIdentityReadState
    {
        NotRead,
        Success,
        DynamicAssembly,
        LocationUnavailable,
        AssemblyFileMissing,
        ReadFailed
    }
}
