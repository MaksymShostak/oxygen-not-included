#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>Explicit keyboard order, including Klei controls that are not Selectables.</summary>
    internal sealed class OptionsFocusNavigation
    {
        private sealed class Item
        {
            internal GameObject Target = null!;
            internal System.Action Activate = null!;
            internal Func<bool> Enabled = null!;
            internal TMP_InputField? Field;
            internal Outline? Ring;
        }
        private readonly List<Item> items = new List<Item>();
        private int current = -1;

        internal void Add(GameObject target, System.Action activate, Func<bool> enabled)
        {
            var item = new Item { Target = target, Activate = activate, Enabled = enabled,
                Field = target.GetComponent<TMP_InputField>() };
            Graphic? graphic = target.GetComponent<Graphic>() ?? target.GetComponentInChildren<Graphic>();
            if (graphic != null)
            {
                item.Ring = graphic.gameObject.AddComponent<Outline>();
                item.Ring.effectColor = Color.white;
                item.Ring.effectDistance = new Vector2(2, -2);
                item.Ring.enabled = false;
            }
            int index = items.Count;
            items.Add(item);
            var trigger = target.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => { ClearRing(); current = index; });
            trigger.triggers.Add(entry);
        }

        internal void Move(bool reverse)
        {
            int focusedField = items.FindIndex(item => item.Field != null && item.Field.isFocused);
            if (focusedField >= 0) current = focusedField;
            if (reverse && current < 0) current = 0;
            for (int offset = 1; offset <= items.Count; offset++)
            {
                int index = (current + (reverse ? -offset : offset) + items.Count * 2) % items.Count;
                if (Available(items[index])) { Select(index); return; }
            }
        }

        internal void Focus(GameObject? target)
        {
            int index = items.FindIndex(item => item.Target == target);
            if (index >= 0 && Available(items[index])) Select(index);
        }

        internal bool IsTextFocused => items.Exists(item => item.Field != null && item.Field.isFocused);

        internal void Activate()
        {
            Item? input = items.Find(item => item.Field != null && item.Field.isFocused);
            if (input?.Field != null) { input.Field.DeactivateInputField(); return; }
            if (current >= 0 && Available(items[current])) items[current].Activate();
        }

        private static bool Available(Item item) =>
            item.Target != null && item.Target.activeInHierarchy &&
            item.Target.transform.lossyScale.sqrMagnitude > 0.01f && item.Enabled();

        private void Select(int index)
        {
            ClearRing();
            current = index;
            Item item = items[index];
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(item.Target);
            if (item.Ring != null) item.Ring.enabled = true;
            if (item.Field != null) item.Field.ActivateInputField();
            Canvas.ForceUpdateCanvases();
            ScrollRect? scroll = item.Target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.content == null) return;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                scroll.viewport, item.Target.transform);
            float delta = bounds.max.y > scroll.viewport.rect.yMax ?
                scroll.viewport.rect.yMax - bounds.max.y :
                bounds.min.y < scroll.viewport.rect.yMin ?
                scroll.viewport.rect.yMin - bounds.min.y : 0;
            scroll.StopMovement();
            scroll.content.anchoredPosition += Vector2.up * delta;
        }

        private void ClearRing()
        {
            if (current >= 0 && current < items.Count && items[current].Ring != null)
                items[current].Ring!.enabled = false;
        }
    }
}
