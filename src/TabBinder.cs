using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>
    /// Puts an OPERATIONS tab into the bottom menu bar. If Fika's "Download Profile" tab (a re-purposed Watchlist tab) exists it is
    /// converted; otherwise the Flea Market tab is cloned the same way Fika does it. The tab carries a green unread badge like the other tabs.
    /// </summary>
    internal sealed class TabBinder
    {
        private const string Tabs = "Preloader UI/Preloader UI/BottomPanel/Content/TaskBar/Tabs/";
        private GameObject _tab;
        private TextMeshProUGUI _badgeText;
        private GameObject _badge;
        private Sprite _sprite;
        private readonly Action _onClick;
        private int _lastCount = -1;

        public TabBinder(Action onClick) { _onClick = onClick; }

        public bool Bound { get { return _tab != null; } }

        public bool TryBind()
        {
            if (_tab != null) return true;
            try
            {
                GameObject source = GameObject.Find(Tabs + "DownloadProfile");
                bool cloned = false;
                if (source == null)
                {
                    GameObject flea = GameObject.Find(Tabs + "FleaMarket");
                    if (flea == null) return false;
                    source = UnityEngine.Object.Instantiate(flea);
                    source.transform.SetParent(flea.transform.parent, false);
                    source.transform.SetSiblingIndex(10);
                    cloned = true;
                }
                source.name = "OperationsTab";

                EFT.UI.LocalizedText lt = source.GetComponentInChildren<EFT.UI.LocalizedText>(true);
                if (lt != null) lt.LocalizationKey = "";
                TMP_Text text = source.GetComponentInChildren<TMP_Text>(true);
                if (text != null) text.text = "OPERATIONS";

                if (_sprite == null) _sprite = UiKit.MakeIcon(0);
                Image icon = FindIcon(source);
                if (icon != null) icon.sprite = _sprite;

                EFT.UI.AnimatedToggle toggle = source.GetComponentInChildren<EFT.UI.AnimatedToggle>(true);
                if (toggle == null) { if (cloned) UnityEngine.Object.Destroy(source); return false; }
                toggle.onValueChanged.RemoveAllListeners(); // drops Fika's "download profile" action
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    toggle.SetIsOnWithoutNotify(false); // behave like a button, not a menu page
                    _onClick();
                });

                _badge = BuildBadge(source.transform);
                _tab = source;
                _lastCount = -1;
                OperationsPlugin.Log.LogInfo("Operations tab bound (" + (cloned ? "cloned Flea Market tab" : "converted Fika Download Profile tab") + ").");
                return true;
            }
            catch (Exception e)
            {
                OperationsPlugin.Log.LogWarning("Operations tab not bound: " + e.Message);
                return false;
            }
        }

        private static Image FindIcon(GameObject tab)
        {
            Image fallback = null;
            foreach (Image i in tab.GetComponentsInChildren<Image>(true))
            {
                if (i.gameObject.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) return i;
                if (fallback == null && i.sprite != null && i.gameObject != tab) fallback = i;
            }
            return fallback;
        }

        private GameObject BuildBadge(Transform tab)
        {
            Image bg = UiKit.Box(tab, "OperationsBadge", UiKit.Green); bg.raycastTarget = false;
            UiKit.Anchor(bg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -4), new Vector2(-2, 14));
            _badgeText = UiKit.Label(bg.transform, "", 14, Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Stretch(_badgeText.rectTransform);
            bg.gameObject.SetActive(false);
            return bg.gameObject;
        }

        /// <summary>The tab object disappears when the menu is rebuilt (after a raid): then it has to be bound again.</summary>
        public void Verify() { if (_tab == null) { _tab = null; _badge = null; } }

        public void SetCount(int unread)
        {
            if (_tab == null || _badge == null || unread == _lastCount) return;
            _lastCount = unread;
            _badge.SetActive(unread > 0);
            if (_badgeText != null) _badgeText.text = unread > 99 ? ">99" : unread.ToString();
        }
    }
}
