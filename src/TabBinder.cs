using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>
    /// Turns Fika's "Download Profile" tab into the OPERATIONS tab. That tab is registered with the game's own MenuTaskBar
    /// (EMenuType.NewsHub), so the game keeps enabling/disabling it correctly (greyed out and "not available in raid").
    /// Fika creates it a moment after the menu bar wakes up, so the binder keeps polling instead of cloning another tab
    /// (a clone is unmanaged and stays disabled). Without Fika the stock Watchlist tab is used.
    /// </summary>
    internal sealed class TabBinder
    {
        private const string Tabs = "Preloader UI/Preloader UI/BottomPanel/Content/TaskBar/Tabs/";
        private readonly Action _onClick;
        private GameObject _tab, _badge;
        private TextMeshProUGUI _badgeText;
        private Sprite _sprite;
        private float _waitStart = -1f;
        private int _count;
        private bool _wasBound;

        public TabBinder(Action onClick) { _onClick = onClick; }

        public bool Bound
        {
            get
            {
                if (_tab == null && _wasBound) { _wasBound = false; _badge = null; _waitStart = -1f; } // menu rebuilt (after a raid): bind again
                return _tab != null;
            }
        }

        public bool TryBind()
        {
            if (Bound) return true;
            try
            {
                GameObject src = GameObject.Find(Tabs + "DownloadProfile");
                if (src == null)
                {
                    if (GameObject.Find(Tabs + "FleaMarket") == null) return false; // menu bar not built yet
                    if (_waitStart < 0f) _waitStart = Time.unscaledTime;
                    if (Time.unscaledTime - _waitStart < 20f) return false;          // give Fika time to create its tab
                    src = GameObject.Find(Tabs + "Watchlist");
                    if (src == null) return false;
                }

                EFT.UI.AnimatedToggle toggle = src.GetComponentInChildren<EFT.UI.AnimatedToggle>(true);
                if (toggle == null) return false;

                src.name = "OperationsTab";
                EFT.UI.LocalizedText lt = src.GetComponentInChildren<EFT.UI.LocalizedText>(true);
                if (lt != null) lt.LocalizationKey = "";
                TMP_Text label = src.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "OPERATIONS";

                if (_sprite == null) _sprite = UiKit.MakeIcon(0);
                Image icon = FindIcon(src);
                if (icon != null) { icon.sprite = _sprite; icon.preserveAspect = true; }

                toggle.onValueChanged.RemoveAllListeners(); // drops Fika's "download profile" action
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    toggle.SetIsOnWithoutNotify(false); // behaves like a button, not like a menu page
                    _onClick();
                });

                _badge = BuildBadge(src.transform);
                _tab = src; _wasBound = true;
                ApplyCount();
                OperationsPlugin.Log.LogInfo("Operations tab bound to '" + src.name + "'.");
                return true;
            }
            catch (Exception e)
            {
                OperationsPlugin.Log.LogWarning("Operations tab not bound yet: " + e.Message);
                return false;
            }
        }

        private static Image FindIcon(GameObject tab)
        {
            // Fika's layout: Tab / Button / Icon(Image)
            if (tab.transform.childCount > 0 && tab.transform.GetChild(0).childCount > 0)
            {
                Image direct = tab.transform.GetChild(0).GetChild(0).GetComponent<Image>();
                if (direct != null) return direct;
            }
            Image fallback = null;
            foreach (Image i in tab.GetComponentsInChildren<Image>(true))
            {
                if (i.gameObject.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) return i;
                if (fallback == null && i.sprite != null && i.gameObject != tab) fallback = i;
            }
            return fallback;
        }

        /// <summary>Same look as the game's own small green counters above the other tabs.</summary>
        private GameObject BuildBadge(Transform tab)
        {
            Image bg = UiKit.Box(tab, "OperationsBadge", UiKit.Green); bg.raycastTarget = false;
            UiKit.Anchor(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(34, 16));
            _badgeText = UiKit.Label(bg.transform, "", 13, Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
            _badgeText.enableWordWrapping = false;
            UiKit.Stretch(_badgeText.rectTransform);
            bg.gameObject.SetActive(false);
            return bg.gameObject;
        }

        public void SetCount(int unread) { _count = unread; ApplyCount(); }

        private void ApplyCount()
        {
            if (!Bound || _badge == null) return;
            _badge.SetActive(_count > 0);
            if (_badgeText != null) _badgeText.text = _count > 99 ? "> 99" : _count.ToString();
        }
    }
}
