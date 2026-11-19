using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>Small uGUI toolkit: everything is built from code so there is no asset bundle to ship.</summary>
    internal static class UiKit
    {
        // Palette sampled from the Tarkov menu look (dark steel panels, muted gold accents, red for alerts).
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.78f);
        public static readonly Color Panel = new Color(0.075f, 0.082f, 0.09f, 0.97f);
        public static readonly Color Panel2 = new Color(0.115f, 0.125f, 0.135f, 1f);
        public static readonly Color Line = new Color(0.27f, 0.29f, 0.3f, 1f);
        public static readonly Color Gold = new Color(0.80f, 0.71f, 0.45f, 1f);
        public static readonly Color Text = new Color(0.84f, 0.86f, 0.87f, 1f);
        public static readonly Color Muted = new Color(0.55f, 0.58f, 0.6f, 1f);
        public static readonly Color Red = new Color(0.80f, 0.22f, 0.18f, 1f);
        public static readonly Color Green = new Color(0.58f, 0.82f, 0.18f, 1f);
        public static readonly Color Amber = new Color(0.95f, 0.68f, 0.12f, 1f);

        public static Color Shade(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, 1f); }

        private static TMP_FontAsset _font;

        /// <summary>Prefer the font the game menu already uses so the screen matches the rest of the UI.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                TMP_Text menuText = null;
                GameObject tab = GameObject.Find("Preloader UI/Preloader UI/BottomPanel/Content/TaskBar/Tabs/FleaMarket");
                if (tab != null) menuText = tab.GetComponentInChildren<TMP_Text>(true);
                if (menuText != null && menuText.font != null) _font = menuText.font;
                if (_font == null)
                {
                    TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                    _font = all.FirstOrDefault(f => f != null && f.name.ToLowerInvariant().Contains("bender")) ?? all.FirstOrDefault();
                }
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float l = 0, float t = 0, float rt = 0, float b = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
        }

        public static void Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
        }

        public static Image Box(Transform parent, string name, Color color)
        {
            RectTransform r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = true;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, int size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            RectTransform r = Rect("Label", parent);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text ?? ""; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
            t.enableWordWrapping = true; t.raycastTarget = false; t.richText = false;
            return t;
        }

        public static Button Btn(Transform parent, string text, UnityAction onClick, Color bg, int size = 16)
        {
            Image img = Box(parent, "Button", bg);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); cb.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
            TextMeshProUGUI t = Label(img.transform, text, size, Text, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 8, 2, 8, 2);
            return b;
        }

        /// <summary>A vertical scroll area; returns the content container (children are stacked automatically).</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect scroll, float spacing = 8f, int padding = 12)
        {
            RectTransform view = Rect("Scroll", parent);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            view.gameObject.AddComponent<RectMask2D>();
            var bg = view.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f); // receives wheel events
            RectTransform content = Rect("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 28f;
            scroll = sr;
            return content;
        }

        public static LayoutElement Size(Component c, float? minH = null, float? prefH = null, float? prefW = null)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (minH.HasValue) le.minHeight = minH.Value;
            if (prefH.HasValue) le.preferredHeight = prefH.Value;
            if (prefW.HasValue) le.preferredWidth = prefW.Value;
            return le;
        }

        public static Image Divider(Transform parent)
        {
            Image i = Box(parent, "Divider", Line); i.raycastTarget = false; Size(i, 1, 1);
            return i;
        }

        public static TextMeshProUGUI Section(Transform parent, string title)
        {
            TextMeshProUGUI t = Label(parent, title, 15, Gold, FontStyles.Bold);
            return t;
        }

        /// <summary>Procedural, anti-aliased icons (no external assets): 0 = dossier, 1 = warning triangle with exclamation mark, 2 = target reticle.</summary>
        public static Sprite MakeIcon(int kind, int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            const int ss = 3; // 3x3 supersampling = smooth edges
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float dx = ((x + (sx + 0.5f) / ss) / size) * 2f - 1f, dy = ((y + (sy + 0.5f) / ss) / size) * 2f - 1f;
                            if (Inside(kind, dx, dy)) hit++;
                        }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hit / (ss * ss)));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool Inside(int kind, float dx, float dy)
        {
            if (kind == 1)
            {
                // rounded warning triangle (outline) with a solid exclamation mark
                float top = 0.88f, bot = -0.72f, half = 0.92f;
                if (dy > top || dy < bot) return false;
                float w = half * (top - dy) / (top - bot);               // half width at this height
                bool outer = Mathf.Abs(dx) <= w;
                float k = 0.17f;                                           // outline thickness
                float ti = top - k * 2.1f, bi = bot + k, wi = (half - k * 1.55f) * (ti - dy) / (ti - bi);
                bool inner = dy <= ti && dy >= bi && Mathf.Abs(dx) <= wi;
                bool bar = Mathf.Abs(dx) < 0.085f && dy > -0.22f && dy < 0.38f;
                bool dot = dx * dx + (dy + 0.4f) * (dy + 0.4f) < 0.011f * 1.9f;
                return (outer && !inner) || (inner && (bar || dot));
            }
            if (kind == 2)
            {
                // reticle: ring + four ticks + center dot
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = r > 0.55f && r < 0.66f;
                bool ticks = (Mathf.Abs(dx) < 0.05f && Mathf.Abs(dy) > 0.42f && Mathf.Abs(dy) < 0.92f) || (Mathf.Abs(dy) < 0.05f && Mathf.Abs(dx) > 0.42f && Mathf.Abs(dx) < 0.92f);
                return ring || ticks || r < 0.1f;
            }
            // document with folded corner and text lines
            bool body = dx > -0.6f && dx < 0.6f && dy > -0.84f && dy < 0.84f && !(dx > 0.26f && dy > 0.5f && (dx - 0.26f) + (dy - 0.5f) > 0.34f);
            bool line = Mathf.Abs(dx) < 0.36f && ((dy > 0.1f && dy < 0.2f) || (dy > -0.15f && dy < -0.05f) || (dy > -0.4f && dy < -0.3f));
            return body && !line;
        }

        /// <summary>Thin L-shaped corner marks (HUD style). corner: 0 = top-left, 1 = top-right, 2 = bottom-left, 3 = bottom-right.</summary>
        public static void Bracket(Transform parent, int corner, Color color, float len = 9f, float thick = 2f)
        {
            bool right = corner == 1 || corner == 3, bottom = corner >= 2;
            Vector2 a = new Vector2(right ? 1 : 0, bottom ? 0 : 1);
            Image h = Box(parent, "BracketH", color); h.raycastTarget = false;
            Anchor(h.rectTransform, a, a, new Vector2(right ? -len : 0, bottom ? 0 : -thick), new Vector2(right ? 0 : len, bottom ? thick : 0));
            Image v = Box(parent, "BracketV", color); v.raycastTarget = false;
            Anchor(v.rectTransform, a, a, new Vector2(right ? -thick : 0, bottom ? 0 : -len), new Vector2(right ? 0 : thick, bottom ? len : 0));
        }
    }
}
