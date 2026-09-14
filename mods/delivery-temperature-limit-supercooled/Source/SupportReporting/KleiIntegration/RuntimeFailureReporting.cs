#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>Contains mod failures without invoking player UI from worker callbacks.</summary>
    internal static class RuntimeFailureReporting
    {
        internal static readonly RuntimeFailureState UserInterfaceFailure = new RuntimeFailureState();
        internal static bool IsUserInterfaceEnabled =>
            !UserInterfaceFailure.HasFailed && !DeliveryTemperatureGameSessionHost.RuntimeFailure.HasFailed;

        internal static void DisableUserInterface(string operation, Exception exception)
        {
            if (UserInterfaceFailure.TryRecordFailure())
                DeliveryTemperatureSupportReporter.Record("DTL-UI-DISABLED",
                    SupportDiagnosticSeverity.Error, operation + ": temperature controls disabled until restart.", exception);
        }

        internal static void DisableGameplay(string operation, Exception exception)
        {
            if (DeliveryTemperatureGameSessionHost.TryDisableRuntime())
            {
                DeliveryTemperatureSupportReporter.Record("DTL-GAMEPLAY-DISABLED",
                    SupportDiagnosticSeverity.Error,
                    operation + ": temperature enforcement disabled until restart; saved limits retained.", exception);
            }
        }
    }

    /// <summary>Delivers queued failure warnings on the Unity thread, including while paused.</summary>
    internal sealed class RuntimeFailureNotification : KMonoBehaviour
    {
        private bool presentationFailed;

        private void Update()
        {
            if (presentationFailed ||
                (!DeliveryTemperatureGameSessionHost.RuntimeFailure.HasPendingWarning &&
                 !RuntimeFailureReporting.UserInterfaceFailure.HasPendingWarning)) return;
            try
            {
                if (Game.Instance == null || gameObject == null) return;
                Notifier notifier = gameObject.AddOrGet<Notifier>();
                if (notifier == null) return;
                notifier.InitializeComponent();
                if (DeliveryTemperatureGameSessionHost.RuntimeFailure.TryTakeWarning())
                    Notify(notifier, STRINGS.DELIVERY_TEMPERATURE_LIMIT.GAMEPLAY_FAILURE);
                if (RuntimeFailureReporting.UserInterfaceFailure.TryTakeWarning())
                    Notify(notifier, STRINGS.DELIVERY_TEMPERATURE_LIMIT.USER_INTERFACE_FAILURE);
            }
            catch (Exception exception)
            {
                // Feedback must never become a second failure loop.
                presentationFailed = true;
                DeliveryTemperatureSupportReporter.Record("DTL-WARNING-FAILED",
                    SupportDiagnosticSeverity.Error, "The failure warning could not be displayed.", exception);
            }
        }

        private static void Notify(Notifier notifier, string message)
        {
            notifier.Add(new Notification(message, NotificationType.BadMinor,
                (notifications, data) => message, expires: false));
        }
    }
}
