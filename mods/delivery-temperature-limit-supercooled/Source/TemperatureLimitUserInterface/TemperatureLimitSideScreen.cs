#nullable enable

using PeterHan.PLib.UI;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Displays the delivery-temperature editor for registered destination
    /// components without performing a Unity GetComponent traversal per query.
    /// </summary>
    internal sealed class TemperatureLimitSideScreen : SideScreenContent
    {
        private TemperatureLimitWidget? widget;

        public override int GetSideScreenSortOrder() => -1;

        public override bool IsValidForTarget(GameObject target) =>
            TemperatureLimit.Get(target) != null;

        public override void SetTarget(GameObject newTarget)
        {
            TemperatureLimit? temperatureLimit = TemperatureLimit.Get(newTarget);
            if (temperatureLimit == null)
            {
                DeliveryTemperatureSupportReporter.Record(
                    "DTL-SIDE-SCREEN-REGISTRATION-FAILED",
                    SupportDiagnosticSeverity.Error,
                    "Delivery Temperature Limit received an unregistered side-screen target.");
                return;
            }

            widget ??= gameObject.AddOrGet<TemperatureLimitWidget>();
            widget.SetTarget(temperatureLimit);
        }

        public override string GetTitle() =>
            STRINGS.TEMPERATURELIMIT.SIDESCREEN_TITLE;

        protected override void OnPrefabInit()
        {
            widget = gameObject.AddOrGet<TemperatureLimitWidget>();
            ContentContainer = gameObject;
            base.OnPrefabInit();
        }

        protected override void OnShow(bool show)
        {
            base.OnShow(show);
            if (!show)
            {
                return;
            }

            GameObject? complexFabricatorSideScreen =
                FindComplexFabricatorSideScreen();
            if (complexFabricatorSideScreen == null ||
                !complexFabricatorSideScreen.activeInHierarchy)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            ComplexFabricatorTemperatureLimitLayoutPatches
                .ApplyMinimumWidth(
                    complexFabricatorSideScreen,
                    GetComponent<RectTransform>().rect.size.x);
        }

        protected override void OnDisable()
        {
            isEditing = false;
            base.OnDisable();
        }

        private GameObject? FindComplexFabricatorSideScreen()
        {
            GameObject? parent = PUIUtils.GetParent(gameObject);
            Transform? transform = parent?.transform.Find(
                nameof(ComplexFabricatorSideScreen));
            return transform?.gameObject;
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            bool isAnyTemperatureFieldFocused =
                widget != null && widget.IsAnyFieldFocused();
            if (isEditing != isAnyTemperatureFieldFocused)
            {
                isEditing = isAnyTemperatureFieldFocused;
            }

            if (!e.Consumed && isEditing)
            {
                e.Consumed = true;
            }
        }

        public override void OnKeyUp(KButtonEvent e)
        {
            bool isAnyTemperatureFieldFocused =
                widget != null && widget.IsAnyFieldFocused();
            if (isEditing != isAnyTemperatureFieldFocused)
            {
                isEditing = isAnyTemperatureFieldFocused;
            }

            if (!e.Consumed && isEditing)
            {
                e.Consumed = true;
            }
        }
    }

    /// <summary>
    /// Registers the side-screen type through one explicitly installed ONI hook.
    /// </summary>
    internal static class TemperatureLimitSideScreenRegistrationPatches
    {
        internal static MethodInfo ResolveDetailsScreenPrefabInitializationTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(DetailsScreen),
                "OnPrefabInit",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());

        internal static void DetailsScreenPrefabInitializationPostfix() =>
            PUIUtils.AddSideScreenContent<TemperatureLimitSideScreen>();
    }

    /// <summary>
    /// Preserves the complex-fabricator side screen's original minimum width when
    /// the temperature editor temporarily widens their shared container.
    /// </summary>
    internal static class ComplexFabricatorTemperatureLimitLayoutPatches
    {
        private static bool hasCapturedOriginalWidth;
        private static float originalMinimumWidth;

        internal static MethodInfo ResolveComplexFabricatorSideScreenShowTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ComplexFabricatorSideScreen),
                "OnShow",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                new[] { typeof(bool) });

        internal static void ComplexFabricatorSideScreenShowPostfix(
            ComplexFabricatorSideScreen __instance,
            bool show,
            ComplexFabricator ___targetFab)
        {
            if (!show || ___targetFab == null)
            {
                return;
            }

            if (TemperatureLimit.Get(___targetFab.gameObject) == null &&
                hasCapturedOriginalWidth)
            {
                ApplyMinimumWidth(__instance.gameObject, width: null);
            }
        }

        internal static void ApplyMinimumWidth(
            GameObject complexFabricatorSideScreen,
            float? width)
        {
            Transform? contents =
                complexFabricatorSideScreen.transform.Find("Contents");
            if (contents == null)
            {
                return;
            }

            ApplyMinimumWidth(
                contents.Find("SelectedRecipeTitleBar"),
                width,
                capturesOriginalWidth: true);
            ApplyMinimumWidth(
                contents.Find("ButtonScrollView"),
                width,
                capturesOriginalWidth: false);
        }

        private static void ApplyMinimumWidth(
            Transform? targetTransform,
            float? width,
            bool capturesOriginalWidth)
        {
            LayoutElement? layoutElement =
                targetTransform?.gameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                return;
            }

            if (capturesOriginalWidth && !hasCapturedOriginalWidth)
            {
                originalMinimumWidth = layoutElement.minWidth;
                hasCapturedOriginalWidth = true;
            }

            if (width.HasValue)
            {
                layoutElement.minWidth = Mathf.Max(
                    layoutElement.minWidth,
                    width.Value);
            }
            else if (hasCapturedOriginalWidth)
            {
                layoutElement.minWidth = originalMinimumWidth;
            }
        }
    }
}
