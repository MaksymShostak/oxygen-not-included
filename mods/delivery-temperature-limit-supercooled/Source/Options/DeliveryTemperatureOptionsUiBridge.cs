#nullable enable

using HarmonyLib;
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Text = STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// PLib 4.25 adapter. The provider hook recognizes only our exact instance and
    /// options type. Screen hooks recognize only currently owned dialog instances.
    /// Nothing changes another mod's options, input, close behaviour, or patch plan.
    /// </summary>
    internal static class DeliveryTemperatureOptionsUiBridge
    {
        private static readonly Dictionary<KScreen, DeliveryTemperatureOptionsDialog> Screens =
            new Dictionary<KScreen, DeliveryTemperatureOptionsDialog>();
        private static readonly HashSet<MethodInfo> PatchedMethods = new HashSet<MethodInfo>();
        private static readonly Harmony UiHarmony =
            new Harmony("MaksymShostak.DeliveryTemperatureLimit.OptionsUI");
        private static POptions? provider;
        private static DeliveryTemperatureOptionsDialog? openDialog;

        internal static void Register(KMod.UserMod2 mod)
        {
            if (provider != null) throw new InvalidOperationException("Options already registered.");
            provider = new POptions();
            MethodInfo target = RequireMethod(typeof(POptions), "Process", typeof(uint), typeof(object));
            Patch(target, nameof(ProcessPrefix));
            provider.RegisterOptions(mod, typeof(DeliveryTemperatureLimitOptions));
        }

        internal static void Attach(KScreen screen, DeliveryTemperatureOptionsDialog controller)
        {
            Type type = screen.GetType();
            // Resolve the complete public shell contract before installing hooks.
            MethodInfo close = RequireCloseMethod(type);
            MethodInfo down = RequireMethod(type, "OnKeyDown", typeof(KButtonEvent));
            MethodInfo up = RequireMethod(type, "OnKeyUp", typeof(KButtonEvent));
            Patch(close, nameof(DeactivatePrefix));
            Patch(down, nameof(KeyDownPrefix));
            Patch(up, nameof(KeyUpPrefix));
            Screens.Add(screen, controller);
        }

        internal static void Detach(KScreen screen) => Screens.Remove(screen);
        internal static void Released(DeliveryTemperatureOptionsDialog dialog)
        {
            if (ReferenceEquals(openDialog, dialog)) openDialog = null;
        }

        private static bool ProcessPrefix(POptions __instance, uint operation, object args)
        {
            if (!ReferenceEquals(provider, __instance) || operation != 0 || args == null)
                return true;
            Type argumentType = args.GetType();
            if (!(argumentType.GetProperty("OptionsType")?.GetValue(args) is Type optionsType) ||
                optionsType != typeof(DeliveryTemperatureLimitOptions))
                return true;
            var onClose = argumentType.GetProperty("OnClose")?.GetValue(args) as System.Action<object>;
            if (openDialog != null)
            {
                // Do not replace a live draft or lose a second caller's callback.
                openDialog.AddCloseCallback(onClose);
                return false;
            }
            try
            {
                _ = DeliveryTemperatureLimitOptions.Instance;
                openDialog = new DeliveryTemperatureOptionsDialog();
                openDialog.AddCloseCallback(onClose);
                openDialog.Show();
            }
            catch (Exception ex)
            {
                openDialog?.AbortOpening();
                openDialog = null;
                DeliveryTemperatureOptionsStore.Log("The PLib options adapter failed.", ex);
                try { KMod.Manager.Dialog(null, Text.DIALOG_TITLE, Text.ERROR_UI_FAILED); }
                catch (Exception displayFailure)
                {
                    DeliveryTemperatureOptionsStore.Log("Options failure feedback was unavailable.", displayFailure);
                }
            }
            return false;
        }

        private static bool DeactivatePrefix(KScreen __instance)
        {
            if (!Screens.TryGetValue(__instance, out var owner)) return true;
            owner.RequestClose();
            return false;
        }

        private static bool KeyDownPrefix(KScreen __instance, KButtonEvent e)
        {
            if (!Screens.TryGetValue(__instance, out var owner)) return true;
            if (!e.Consumed) owner.HandleKeyboard(e.TryConsume(global::Action.Escape));
            e.Consumed = true;
            return false;
        }

        private static bool KeyUpPrefix(KScreen __instance, KButtonEvent e)
        {
            if (!Screens.TryGetValue(__instance, out var owner)) return true;
            e.Consumed = true;
            owner.HandleKeyRelease();
            return false;
        }

        private static MethodInfo RequireCloseMethod(Type type)
        {
            MethodInfo? candidate = null;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "Deactivate" || method.ReturnType != typeof(void) ||
                    method.IsAbstract || method.ContainsGenericParameters) continue;
                bool optional = true;
                foreach (ParameterInfo parameter in method.GetParameters()) optional &= parameter.IsOptional;
                if (!optional) continue;
                if (candidate != null) throw new AmbiguousMatchException("Ambiguous KScreen close contract.");
                candidate = method;
            }
            return candidate ?? throw new MissingMethodException(type.FullName, "Deactivate");
        }

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
        {
            MethodInfo? method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
                null, parameters, null);
            if (method == null || method.ReturnType != typeof(void) || method.IsAbstract ||
                method.ContainsGenericParameters)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static void Patch(MethodInfo target, string prefix)
        {
            if (PatchedMethods.Contains(target)) return;
            var method = typeof(DeliveryTemperatureOptionsUiBridge).GetMethod(prefix,
                BindingFlags.NonPublic | BindingFlags.Static) ?? throw new MissingMethodException(prefix);
            UiHarmony.Patch(target, prefix: new HarmonyMethod(method));
            PatchedMethods.Add(target);
        }
    }

    internal sealed class OptionsDialogLifetime : KMonoBehaviour
    {
        internal System.Action? Disposed { get; set; }
        internal System.Action? PollInput { get; set; }
        private void Update() => PollInput?.Invoke();
        protected override void OnCleanUp()
        {
            System.Action? callback = Disposed;
            Disposed = null;
            PollInput = null;
            try { callback?.Invoke(); }
            finally { base.OnCleanUp(); }
        }
    }
}
