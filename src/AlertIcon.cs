using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>
    /// Compact HUD tag in the top-right corner for priority orders / an assembled squad:
    /// dark plate, corner brackets, pulsing warning triangle and a two-line caption. Click opens the order.
    /// </summary>
    internal sealed class AlertIcon : MonoBehaviour
    {
        private Canvas _canvas;
        private Image _plate, _frameTop, _frameBottom, _glyph, _stripe;
        private TextMeshProUGUI _title, _sub;
        private Image[] _brackets;
        private string _target;
        private bool _critical;

        public static AlertIcon Create(Transform parent, UnityAction<string> onClick)
        {
            var go = new GameObject("Operations.Alert");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AlertIcon>();
            a.Build(onClick);
            return a;
        }

        private void Build(UnityAction<string> onClick)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 5500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            const float W = 236f, H = 54f;
            _plate = UiKit.Box(transform, "Plate", new Color(0.04f, 0.045f, 0.05f, 0.9f));
            UiKit.Anchor(_plate.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24 - W, -20 - H), new Vector2(-24, -20));
            var btn = _plate.gameObject.AddComponent<Button>(); btn.targetGraphic = _plate; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (!string.IsNullOrEmpty(_target)) onClick(_target); });

            _frameTop = UiKit.Box(_plate.transform, "FrameTop", UiKit.Red); _frameTop.raycastTarget = false;
            UiKit.Anchor(_frameTop.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(0, 0));
            _frameBottom = UiKit.Box(_plate.transform, "FrameBottom", UiKit.Red); _frameBottom.raycastTarget = false;
            UiKit.Anchor(_frameBottom.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1));

            _stripe = UiKit.Box(_plate.transform, "Stripe", UiKit.Red); _stripe.raycastTarget = false;
            UiKit.Anchor(_stripe.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(54, 0));

            _glyph = UiKit.Box(_stripe.transform, "Glyph", new Color(0.04f, 0.045f, 0.05f, 1f)); _glyph.raycastTarget = false;
            _glyph.sprite = UiKit.MakeIcon(1);
            UiKit.Anchor(_glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20, -20), new Vector2(20, 20));

            _title = UiKit.Label(_plate.transform, "PRIORITY ORDER", 16, UiKit.Text, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _title.enableWordWrapping = false; _title.characterSpacing = 3f;
            UiKit.Anchor(_title.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(66, 0), new Vector2(-8, -5));
            _sub = UiKit.Label(_plate.transform, "CLASSIFIED  -  CLICK TO OPEN", 11, UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _sub.enableWordWrapping = false; _sub.characterSpacing = 1.5f;
            UiKit.Anchor(_sub.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(66, 5), new Vector2(-8, 0));

            // HUD corner marks just outside the plate
            Transform marks = UiKit.Rect("Marks", _plate.transform);
            UiKit.Stretch(marks as RectTransform, -4, -4, -4, -4);
            for (int i = 0; i < 4; i++) UiKit.Bracket(marks, i, UiKit.Red);
            _brackets = marks.GetComponentsInChildren<Image>();
            _canvas.enabled = false;
        }

        public void Set(string missionId, bool critical, string title, string sub)
        {
            _target = missionId; _critical = critical;
            Color c = critical ? UiKit.Red : UiKit.Amber;
            _title.text = title; _sub.text = sub; _title.color = c;
            _stripe.color = c; _frameTop.color = c; _frameBottom.color = c;
            foreach (Image i in _brackets) i.color = c;
            _canvas.enabled = true;
        }

        public void Clear() { _target = null; if (_canvas != null) _canvas.enabled = false; }

        private void Update()
        {
            if (_canvas == null || !_canvas.enabled) return;
            float t = Time.unscaledTime;
            float pulse = 0.55f + 0.45f * Mathf.Sin(t * (_critical ? 5f : 2.6f));
            Color c = _critical ? UiKit.Red : UiKit.Amber;
            Color f = c; f.a = 0.35f + 0.65f * pulse;
            _frameTop.color = f; _frameBottom.color = f;
            Color s = c; s.a = 0.75f + 0.25f * pulse; _stripe.color = s;
        }
    }
}
