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

        public override bool IsValidForTarget(GameObject target)
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return false;
            try { return TemperatureLimit.Get(target) != null; }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(IsValidForTarget), exception);
                return false;
            }
        }

        public override void SetTarget(GameObject newTarget)
        {
            try
            {
                InitializeComponent();
                if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
                TemperatureLimit? temperatureLimit = TemperatureLimit.Get(newTarget);
                if (temperatureLimit == null)
                {
                    if (widget != null) widget.SetTarget(null);
                    if (newTarget != null && !DeliveryTemperatureGameSessionHost.RuntimeFailure.HasFailed)
                    {
                        DeliveryTemperatureSupportReporter.Record(
                            "DTL-SIDE-SCREEN-REGISTRATION-FAILED",
                            SupportDiagnosticSeverity.Error,
                            "Delivery Temperature Limit received an unregistered side-screen target.");
                        RuntimeFailureReporting.DisableUserInterface(nameof(SetTarget),
                            new InvalidOperationException("The selected temperature-limit component is not registered."));
                    }
                    return;
                }

                if (ContentContainer == null)
                    throw new InvalidOperationException("Temperature editor content was not initialized.");
                if (widget == null) widget = ContentContainer.AddOrGet<TemperatureLimitWidget>();
                if (widget == null)
                    throw new InvalidOperationException("Temperature editor component could not be created.");
                widget.InitializeComponent();
                widget.SetTarget(temperatureLimit);
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(SetTarget), exception);
            }
        }

        public override string GetTitle() =>
            STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TITLE;

        protected override void OnPrefabInit()
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
            {
                // GetTitle supplies only the tab's top title. This section owns a
                // native header even when other side screens appear above it.
                DetailsScreen details = DetailsScreen.Instance;
                if (details == null) throw new InvalidOperationException("Details screen is unavailable.");
                GameObject? tabBody = details.GetTabOfType(
                    DetailsScreen.SidescreenTabTypes.Config)?.bodyInstance;
                if (tabBody == null) throw new InvalidOperationException("Configuration tab is unavailable.");
                HierarchyReferences references = tabBody.GetComponent<HierarchyReferences>();
                if (references == null) throw new InvalidOperationException("Configuration tab references are unavailable.");
                var title = references.GetReference("Title");
                if (title == null) throw new InvalidOperationException("Configuration title is unavailable.");
                GameObject headerTemplate = title.gameObject;
                GameObject header = Util.KInstantiateUI(headerTemplate, gameObject);
                header.name = "DeliveryTemperatureLimitHeader";
                LocText headerLabel = header.GetComponentInChildren<LocText>(true);
                headerLabel.key = "STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TITLE";
                headerLabel.SetText(GetTitle());

                // The tab title normally sits outside its layout. Our copy is a
                // regular row, retaining the native title's height and styling.
                LayoutElement headerLayout = header.AddOrGet<LayoutElement>();
                headerLayout.ignoreLayout = false;
                headerLayout.minHeight = headerLayout.preferredHeight =
                    headerTemplate.GetComponent<RectTransform>().rect.height;
                headerLayout.flexibleWidth = 1f;
                header.transform.SetAsFirstSibling();
                header.SetActive(true);
                CheckShouldShowTopTitle = () => false;

                ContentContainer = PUIElements.CreateUI(gameObject, "TemperatureLimitContent");
                ContentContainer.AddComponent<BoxLayoutGroup>();
                widget = ContentContainer.AddOrGet<TemperatureLimitWidget>();
                base.OnPrefabInit();
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnPrefabInit), exception);
            }
        }

        protected override void OnShow(bool show)
        {
            try
            {
                base.OnShow(show);
                if (!show || !RuntimeFailureReporting.IsUserInterfaceEnabled)
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
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnShow), exception);
            }
        }

        private GameObject? FindComplexFabricatorSideScreen()
        {
            GameObject? parent = PUIUtils.GetParent(gameObject);
            Transform? transform = parent?.transform.Find(
                nameof(ComplexFabricatorSideScreen));
            return transform?.gameObject;
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

        internal static void DetailsScreenPrefabInitializationPostfix()
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try { PUIUtils.AddSideScreenContent<TemperatureLimitSideScreen>(); }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(DetailsScreenPrefabInitializationPostfix), exception);
            }
        }
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
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
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
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(ComplexFabricatorSideScreenShowPostfix), exception);
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
