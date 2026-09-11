using System;
using System.Collections.Generic;
using UnityEngine;

namespace CalendarQuestsPins
{
    internal sealed class NativeMarkerSprites
    {
        private readonly Dictionary<MarkerStyle, Sprite> _sprites = new Dictionary<MarkerStyle, Sprite>();
        private bool _prewarmAttempted;
        private bool _runtimeAttempted;

        internal bool TryPrewarm()
        {
            if (HasBase()) return true;
            if (_prewarmAttempted) return false;
            _prewarmAttempted = true;
            ResolveLoadedSprites();
            return HasBase();
        }

        internal bool EnsureLoaded()
        {
            if (HasBase()) return true;
            if (_runtimeAttempted) return false;
            _runtimeAttempted = true;
            ResolveLoadedSprites();
            return HasBase();
        }

        internal Sprite Get(MarkerStyle style)
        {
            Sprite sprite;
            if (_sprites.TryGetValue(style, out sprite) && sprite != null) return sprite;

            // A DLC-specific sprite may not have been resident during loading-screen prewarm.
            // Retry the loaded-sprite lookup once, and only when a real marker asks for a missing style.
            if (style != MarkerStyle.Base && !_runtimeAttempted)
            {
                _runtimeAttempted = true;
                ResolveLoadedSprites();
                if (_sprites.TryGetValue(style, out sprite) && sprite != null) return sprite;
            }

            return _sprites.TryGetValue(MarkerStyle.Base, out sprite) ? sprite : null;
        }

        internal void Dispose()
        {
            // These are game-owned Sprite assets. Never destroy them; only release our references.
            _sprites.Clear();
            _prewarmAttempted = false;
            _runtimeAttempted = false;
        }

        private bool HasBase()
        {
            Sprite sprite;
            return _sprites.TryGetValue(MarkerStyle.Base, out sprite) && sprite != null;
        }

        private void ResolveLoadedSprites()
        {
            Sprite[] sprites;
            try { sprites = Resources.FindObjectsOfTypeAll<Sprite>(); }
            catch { return; }

            for (var i = 0; i < sprites.Length; i++)
            {
                var sprite = sprites[i];
                if (sprite == null) continue;

                if (string.Equals(sprite.name, "icon_quest_mark_small", StringComparison.Ordinal))
                    _sprites[MarkerStyle.Base] = sprite;
                else if (string.Equals(sprite.name, "dlc_quest_mrk", StringComparison.Ordinal))
                    _sprites[MarkerStyle.Stories] = sprite;
                else if (string.Equals(sprite.name, "quest_marker_violet", StringComparison.Ordinal))
                    _sprites[MarkerStyle.Violet] = sprite;
                else if (string.Equals(sprite.name, "Icon_quest_mark_small_blue", StringComparison.Ordinal))
                    _sprites[MarkerStyle.Souls] = sprite;
            }
        }
    }
}
