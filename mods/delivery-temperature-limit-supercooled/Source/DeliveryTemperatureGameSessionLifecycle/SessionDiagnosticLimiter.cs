#nullable enable

using System;
using System.Collections.Concurrent;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Permits one diagnostic emission per exact semantic key for one game session.
    /// ConcurrentDictionary provides an atomic first-writer decision so multiple
    /// worker observations cannot turn one stale condition into a large-colony log
    /// storm.
    /// </summary>
    internal sealed class SessionDiagnosticLimiter
    {
        private readonly ConcurrentDictionary<string, byte>
            emittedDiagnosticKeys =
                new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        internal bool ShouldEmit(string diagnosticKey)
        {
            if (diagnosticKey is null)
            {
                throw new ArgumentNullException(nameof(diagnosticKey));
            }

            if (string.IsNullOrWhiteSpace(diagnosticKey))
            {
                throw new ArgumentException(
                    "A session diagnostic key must contain a semantic identity.",
                    nameof(diagnosticKey));
            }

            return emittedDiagnosticKeys.TryAdd(diagnosticKey, 0);
        }
    }
}
