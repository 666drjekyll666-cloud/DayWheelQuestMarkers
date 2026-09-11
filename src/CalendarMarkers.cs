using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CalendarQuestsPins
{
    internal enum MarkerStyle
    {
        None = 0,
        Base = 1,
        Stories = 2,
        Violet = 3,
        Souls = 4
    }

    internal sealed class CalendarMarkers
    {
        private const string MarkerName = "CalendarQuestsPins Marker";
        private const float OutwardOffset = 14f;
        private const float SectorDegrees = 60f;

        private sealed class MarkerVisual
        {
            internal GameObject GameObject;
            internal Component Widget;
            internal MarkerStyle Style;
        }

        private readonly Type _hudType;
        private readonly Type _ui2dSpriteType;
        private readonly FieldInfo _sinTypeField;
        private readonly MethodInfo _makePixelPerfect;
        private readonly NativeMarkerSprites _nativeSprites = new NativeMarkerSprites();
        private Component _hud;
        private readonly Component[] _icons = new Component[6];
        private readonly List<MarkerVisual>[] _slotMarkers = new List<MarkerVisual>[6];

        internal CalendarMarkers()
        {
            _hudType = ReflectionUtil.FindType("HUD");
            _ui2dSpriteType = ReflectionUtil.FindType("UI2DSprite");
            var hudSinIconType = ReflectionUtil.FindType("HUDSinIcon");
            if (hudSinIconType != null) _sinTypeField = hudSinIconType.GetField("_sin_type", ReflectionUtil.AnyInstance);
            if (_ui2dSpriteType != null) _makePixelPerfect = ReflectionUtil.FindMethod(_ui2dSpriteType, "MakePixelPerfect", 0, false);
            for (var i = 0; i < _slotMarkers.Length; i++)
                _slotMarkers[i] = new List<MarkerVisual>(4);
        }

        internal bool TryPrewarmNativeSprites()
        {
            return _nativeSprites.TryPrewarm();
        }

        internal bool EnsureAttached()
        {
            // Ordinary menus only deactivate the HUD. Keep the same icon references and marker
            // children across hide/show; rebuild only when the actual HUD or weekday icons die.
            if (_hud != null && _hud && AllIconsAlive()) return true;

            DestroyAll();
            _hud = FindActiveHud();
            if (_hud == null || _ui2dSpriteType == null || _sinTypeField == null) return false;
            if (!_nativeSprites.EnsureLoaded()) return false;

            object iconsValue;
            if (!ReflectionUtil.TryRead(_hud, "sin_icons", out iconsValue) || !(iconsValue is IEnumerable)) return false;
            var i = 0;
            foreach (var icon in (IEnumerable)iconsValue)
            {
                if (i >= 6) break;
                var component = icon as Component;
                if (component == null) { DestroyAll(); return false; }
                _icons[i] = component;
                i++;
            }
            if (i != 6) { DestroyAll(); return false; }
            return true;
        }

        internal void ApplyMarkerSets(List<MarkerStyle>[] stylesBySinType)
        {
            if (stylesBySinType == null) { HideAll(); return; }
            for (var slot = 0; slot < _slotMarkers.Length; slot++)
            {
                var sinType = ReadSinTypeValue(_icons[slot]);
                List<MarkerStyle> styles = null;
                if (sinType > 0 && sinType < stylesBySinType.Length)
                    styles = stylesBySinType[sinType];
                ApplySlot(slot, styles);
            }
        }

        internal void HideAll()
        {
            for (var slot = 0; slot < _slotMarkers.Length; slot++)
            {
                var visuals = _slotMarkers[slot];
                for (var i = 0; i < visuals.Count; i++)
                {
                    var go = visuals[i] == null ? null : visuals[i].GameObject;
                    if (go != null && go.activeSelf) go.SetActive(false);
                }
            }
        }

        internal void DestroyAll()
        {
            for (var slot = 0; slot < _slotMarkers.Length; slot++)
            {
                var visuals = _slotMarkers[slot];
                for (var i = 0; i < visuals.Count; i++)
                {
                    var visual = visuals[i];
                    if (visual != null && visual.GameObject != null)
                        UnityEngine.Object.Destroy(visual.GameObject);
                }
                visuals.Clear();
                _icons[slot] = null;
            }
            _hud = null;
        }

        internal void Dispose()
        {
            DestroyAll();
            _nativeSprites.Dispose();
        }

        private void ApplySlot(int slot, List<MarkerStyle> styles)
        {
            if (slot < 0 || slot >= _slotMarkers.Length) return;
            var icon = _icons[slot];
            if (icon == null) return;

            var visuals = _slotMarkers[slot];
            PruneDead(visuals);
            var desiredCount = styles == null ? 0 : styles.Count;

            while (visuals.Count < desiredCount)
            {
                var visual = CreateMarker(icon, visuals.Count);
                if (visual == null)
                {
                    HideSlot(visuals);
                    return;
                }
                visuals.Add(visual);
            }

            for (var i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                if (visual == null || visual.GameObject == null || visual.Widget == null) continue;

                if (i >= desiredCount)
                {
                    if (visual.GameObject.activeSelf) visual.GameObject.SetActive(false);
                    continue;
                }

                var style = styles[i];
                var sprite = GetSprite(style);
                if (sprite == null)
                {
                    if (visual.GameObject.activeSelf) visual.GameObject.SetActive(false);
                    continue;
                }

                if (visual.Style != style)
                {
                    ApplySprite(visual.Widget, sprite);
                    visual.Style = style;
                }

                LayoutMarker(icon, visual.GameObject.transform, i, desiredCount);
                if (!visual.GameObject.activeSelf) visual.GameObject.SetActive(true);
            }
        }

        private static void HideSlot(List<MarkerVisual> visuals)
        {
            for (var i = 0; i < visuals.Count; i++)
            {
                var go = visuals[i] == null ? null : visuals[i].GameObject;
                if (go != null && go.activeSelf) go.SetActive(false);
            }
        }

        private static void PruneDead(List<MarkerVisual> visuals)
        {
            for (var i = visuals.Count - 1; i >= 0; i--)
            {
                var visual = visuals[i];
                if (visual != null && visual.GameObject != null && visual.Widget != null) continue;
                if (visual != null && visual.GameObject != null)
                    UnityEngine.Object.Destroy(visual.GameObject);
                visuals.RemoveAt(i);
            }
        }

        private void ApplySprite(Component widget, Sprite sprite)
        {
            if (widget == null || sprite == null) return;
            ReflectionUtil.TryWrite(widget, "sprite2D", sprite);
            ReflectionUtil.TryWrite(widget, "width", Mathf.RoundToInt(sprite.rect.width));
            ReflectionUtil.TryWrite(widget, "height", Mathf.RoundToInt(sprite.rect.height));
            if (_makePixelPerfect != null)
            {
                try { _makePixelPerfect.Invoke(widget, null); } catch { }
            }
        }

        private int ReadSinTypeValue(Component icon)
        {
            if (icon == null || _sinTypeField == null) return -1;
            try
            {
                var value = _sinTypeField.GetValue(icon);
                return value == null ? -1 : Convert.ToInt32(value);
            }
            catch { return -1; }
        }

        private Component FindActiveHud()
        {
            if (_hudType == null) return null;
            var objects = Resources.FindObjectsOfTypeAll(_hudType);
            foreach (var obj in objects)
            {
                var component = obj as Component;
                if (component == null || !component.gameObject.activeInHierarchy) continue;
                object icons;
                if (ReflectionUtil.TryRead(component, "sin_icons", out icons) && icons is IEnumerable) return component;
            }
            return null;
        }

        private Sprite GetSprite(MarkerStyle style)
        {
            return _nativeSprites.Get(style);
        }

        private MarkerVisual CreateMarker(Component icon, int ordinal)
        {
            var go = new GameObject(MarkerName + " " + ordinal);
            go.layer = icon.gameObject.layer;
            var transform = go.transform;
            transform.SetParent(icon.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;

            Component spriteComponent;
            try { spriteComponent = go.AddComponent(_ui2dSpriteType); }
            catch { UnityEngine.Object.Destroy(go); return null; }
            if (spriteComponent == null) { UnityEngine.Object.Destroy(go); return null; }

            var baseSprite = GetSprite(MarkerStyle.Base);
            if (baseSprite != null) ApplySprite(spriteComponent, baseSprite);

            var iconDepth = ReadIconDepth(icon);
            ReflectionUtil.TryWrite(spriteComponent, "depth", iconDepth + 2 + ordinal);

            go.SetActive(false);
            return new MarkerVisual
            {
                GameObject = go,
                Widget = spriteComponent,
                Style = MarkerStyle.Base
            };
        }

        private static void LayoutMarker(Component icon, Transform markerTransform, int index, int count)
        {
            if (icon == null || markerTransform == null || count <= 0) return;

            var iconPosition3 = icon.transform.localPosition;
            var iconPosition = new Vector2(iconPosition3.x, iconPosition3.y);
            var iconRadius = iconPosition.magnitude;
            var centerDirection = iconRadius > 0.01f ? iconPosition / iconRadius : Vector2.up;

            // The six weekday symbols define six 60-degree sectors. For N markers, place them at
            // N equally spaced interior division points of that sector: one at center, two at
            // one-third/two-thirds, three at quarter points, etc. All marker centers remain on
            // the same circular arc, preserving exact radial symmetry around the wheel.
            var fraction = (index + 1f) / (count + 1f);
            var angleDegrees = -SectorDegrees * 0.5f + SectorDegrees * fraction;
            var angle = angleDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var direction = new Vector2(
                centerDirection.x * cos - centerDirection.y * sin,
                centerDirection.x * sin + centerDirection.y * cos);

            var targetInIconParent = direction * (iconRadius + OutwardOffset);
            var localOffset = targetInIconParent - iconPosition;
            markerTransform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            markerTransform.localScale = Vector3.one;
        }

        private static int ReadIconDepth(Component icon)
        {
            var max = 0;
            object value;
            foreach (var name in new[] { "spr_back", "spr_active" })
            {
                if (!ReflectionUtil.TryRead(icon, name, out value) || value == null) continue;
                object depth;
                if (ReflectionUtil.TryRead(value, "depth", out depth))
                {
                    try { max = Math.Max(max, Convert.ToInt32(depth)); } catch { }
                }
            }
            return max;
        }

        private bool AllIconsAlive()
        {
            for (var i = 0; i < _icons.Length; i++)
                if (_icons[i] == null) return false;
            return true;
        }
    }
}
