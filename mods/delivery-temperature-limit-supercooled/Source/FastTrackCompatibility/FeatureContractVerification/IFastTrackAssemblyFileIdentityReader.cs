#nullable enable

using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Isolates the one physical-file read from reflection-only feature
    /// inspection. Tests can supply an explicit identity without weakening the
    /// production requirement that every active feature has a readable file.
    /// </summary>
    internal interface IFastTrackAssemblyFileIdentityReader
    {
        FastTrackAssemblyFileIdentity Read(Assembly fastTrackAssembly);
    }
}
