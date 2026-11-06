using TUnit.Core;

namespace MaksymShostak.Testing;

internal static class TUnitProjectDefaults
{
    [Before(HookType.TestDiscovery)]
    public static void Configure(BeforeTestDiscoveryContext context)
    {
        context.Settings.Parallelism.MaximumParallelTests = 4;
    }
}
