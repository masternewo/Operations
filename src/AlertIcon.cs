using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>
    /// Floating attention icon in the top-right corner for critical orders / an assembled squad.
    /// Clicking it opens the Operations screen directly on the order.
    /// </summary>
    internal sealed class AlertIcon : MonoBehaviour
    {
        private Canvas _canvas;
        private Image _icon;
        private TextMeshProUGUI _label;
        private Button _button;
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

            Image root = UiKit.Box(transform, "Anchor", new Color(0, 0, 0, 0));
            UiKit.Anchor(root.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-250, -118), new Vector2(-14, -14));
            root.raycastTarget = false;

            _icon = UiKit.Box(root.transform, "Icon", UiKit.Red);
            _icon.sprite = UiKit.MakeIcon(1);
            UiKit.Anchor(_icon.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-72, -72), new Vector2(0, 0));
            _button = _icon.gameObject.AddComponent<Button>(); _button.targetGraphic = _icon; _button.transition = Selectable.Transition.None;
            _button.onClick.AddListener(() => { if (!string.IsNullOrEmpty(_target)) onClick(_target); });

            _label = UiKit.Label(root.transform, "", 14, UiKit.Text, FontStyles.Bold, TextAlignmentOptions.TopRight);
            UiKit.Anchor(_label.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 36));
            _canvas.enabled = false;
        }

        public void Set(string missionId, bool critical, string text)
        {
            _target = missionId; _critical = critical;
            _icon.color = critical ? UiKit.Red : UiKit.Amber;
            _label.text = text; _label.color = critical ? UiKit.Red : UiKit.Amber;
            _canvas.enabled = true;
        }

        public void Clear() { _target = null; if (_canvas != null) _canvas.enabled = false; }

        private void Update()
        {
            if (_canvas == null || !_canvas.enabled) return;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * (_critical ? 5.5f : 3f));
            Color c = _critical ? UiKit.Red : UiKit.Amber; c.a = pulse; _icon.color = c;
        }
    }
}
