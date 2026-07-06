using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using PeterHan.PLib.UI;

namespace DeliveryTemperatureLimit
{
    public class TemperatureLimitSideScreen : SideScreenContent
    {
        private TemperatureLimit target;
        private TemperatureLimitWidget widget;

        protected override void OnPrefabInit()
        {
            widget = gameObject.AddOrGet<TemperatureLimitWidget>();
            ContentContainer = gameObject;
            base.OnPrefabInit();
        }

        public override int GetSideScreenSortOrder()
        {
            return -1; // put below other normal sidescreen content
        }

        public override bool IsValidForTarget(GameObject target)
        {
            return TemperatureLimit.Get( target ) != null;
        }

        public override void SetTarget(GameObject new_target)
        {
            if (new_target == null)
            {
                Debug.LogError("Invalid gameObject received");
                return;
            }
            target = TemperatureLimit.Get( new_target );
            if (target == null)
            {
                Debug.LogError("The gameObject received does not contain a TemperatureLimit component");
                return;
            }
            if (widget == null)
            {
                widget = gameObject.AddOrGet<TemperatureLimitWidget>();
            }
            widget.SetTarget(target);
        }

        public override string GetTitle()
        {
            return STRINGS.TEMPERATURELIMIT.SIDESCREEN_TITLE;
        }


        protected override void OnShow(bool show)
        {
            base.OnShow( show );
            if( !show )
                return;
            // See ComplexFabricatorSideScreen_Patch below.
            GameObject complexFabricator = FindComplexFabricatorSideScreen();
            if( complexFabricator == null || !complexFabricator.activeInHierarchy)
                return;
            Canvas.ForceUpdateCanvases(); // for RectTransform to have the proper size
            ComplexFabricatorSideScreen_Patch.ForceMinWidth( complexFabricator,
                GetComponent<RectTransform>().rect.size.x );
        }

        private GameObject FindComplexFabricatorSideScreen()
        {
            GameObject parent = PUIUtils.GetParent( gameObject );
            if( parent == null ) // huh?
                return null;
            Transform transform = parent.transform.Find(nameof(ComplexFabricatorSideScreen));
            if( transform != null )
                return transform.gameObject;
            return null;
        }

        private void Update()
        {
            bool editing = IsAnyFieldFocused();
            if (this.isEditing != editing)
            {
                this.isEditing = editing;
            }
        }

        private bool IsAnyFieldFocused()
        {
            return widget != null && widget.IsAnyFieldFocused();
        }

        protected override void OnDisable()
        {
            this.isEditing = false;
            base.OnDisable();
        }
    }

    [HarmonyPatch(typeof(DetailsScreen))]
    [HarmonyPatch("OnPrefabInit")]
    public static class DetailsScreen_OnPrefabInit_Patch
    {
        public static void Postfix()
        {
            PUIUtils.AddSideScreenContent<TemperatureLimitSideScreen>();
        }
    }

    // The complex fabricator side screen content assumes a specific width
    // that it fits exactly, but if the side screen is made wider as
    // happens when used together with this side screen content then
    // it looks weird, because it's missing parts at the left and right edges.
    // HACK: Force it to have proper width. There's possibly a better way
    // to handle that but this is the best I can do with my (lack of) knowledge
    // of Unity. Basically, find the 2 UI elements that seem to matter,
    // save their original minimal width the first time, and then use the width
    // of the TemperatureLimitSideScreen. If no TemperatureLimit component
    // is present, reset back.
    [HarmonyPatch(typeof(ComplexFabricatorSideScreen))]
    public class ComplexFabricatorSideScreen_Patch
    {
        private static bool initialSetupDone = false;
        private static float originalWidth;

        public static void ForceMinWidth( GameObject complexFabricator, float width )
        {
            Transform contentsTransform = complexFabricator.transform.Find("Contents");
            if(contentsTransform != null)
            {
                Transform transform = contentsTransform.Find("SelectedRecipeTitleBar");
                if(transform != null)
                {
                    LayoutElement element = transform.gameObject.GetComponent<LayoutElement>();
                    if( !initialSetupDone )
                    {
                        originalWidth = element.minWidth;
                        initialSetupDone = true;
                    }
                    if( width != -1 )
                        element.minWidth = Mathf.Max( element.minWidth, width );
                    else
                        element.minWidth = originalWidth; // reset
                }
                transform = contentsTransform.Find("ButtonScrollView");
                if(transform != null)
                {
                    LayoutElement element = transform.gameObject.GetComponent<LayoutElement>();
                    if( width != -1 )
                        element.minWidth = Mathf.Max( element.minWidth, width );
                    else
                        element.minWidth = originalWidth;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(OnShow))]
        public static void OnShow( ComplexFabricatorSideScreen __instance, bool show, ComplexFabricator ___targetFab )
        {
            if( !show || ___targetFab == null )
                return;
            TemperatureLimit limit = TemperatureLimit.Get( ___targetFab.gameObject );
            if( limit == null && initialSetupDone )
                ForceMinWidth( __instance.gameObject, -1 ); // reset to original width
        }
    }
}
