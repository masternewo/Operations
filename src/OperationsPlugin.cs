using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace Operations.Client
{
    [BepInPlugin("com.operations.client", "Operations Client", "0.5.0")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class OperationsPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static OperationsPlugin Instance;

        private ConfigEntry<float> _pollSeconds;
        private OpsScreen _screen;
        private AlertIcon _alert;
        private TabBinder _tab;
        private Inbox _inbox = new Inbox();
        private float _nextPoll, _nextBind;
        private bool _polling;
        private bool _refreshNow;

        private void Awake()
        {
            Log = Logger; Instance = this;
            _pollSeconds = Config.Bind("General", "PollSeconds", 10f, "How often the server is asked for new orders (menu only).");
            _tab = new TabBinder(OpenScreen);
            Log.LogInfo("Operations client loaded.");
        }

        internal void RequestInboxRefresh() { _refreshNow = true; }

        private static bool InRaid() { return Singleton<GameWorld>.Instantiated; }

        private void EnsureUi()
        {
            if (_screen == null) _screen = OpsScreen.Create(transform);
            if (_alert == null) _alert = AlertIcon.Create(transform, id => { if (_screen != null) _screen.Show(id, true); });
        }

        /// <summary>The look changed on the server: build the screens again with the new palette (the old ones are discarded).</summary>
        private void RebuildUi()
        {
            if (_screen != null) { _screen.Hide(); Destroy(_screen.gameObject); _screen = null; }
            if (_alert != null) { _alert.Clear(); Destroy(_alert.gameObject); _alert = null; }
            EnsureUi();
            _screen.SetInbox(_inbox);
        }

        private void OpenScreen()
        {
            EnsureUi();
            _screen.SetInbox(_inbox);
            _screen.Show(null, false);
            RequestInboxRefresh();
        }

        private void Update()
        {
            Dispatcher.Pump();
            EnsureUi();

            bool raid = InRaid();
            if (raid)
            {
                if (_screen.IsOpen) _screen.Hide();
                _alert.Clear();
                return;
            }

            if (!_tab.Bound && Time.unscaledTime >= _nextBind) { _nextBind = Time.unscaledTime + 1f; _tab.TryBind(); }

            if ((_refreshNow || Time.unscaledTime >= _nextPoll) && !_polling)
            {
                _refreshNow = false;
                _nextPoll = Time.unscaledTime + Mathf.Max(3f, _pollSeconds.Value);
                _polling = true;
                Dispatcher.Run(Api.GetInbox,
                    inbox => { _polling = false; ApplyInbox(inbox); },
                    err => { _polling = false; Log.LogDebug("Inbox poll failed: " + err); });
            }
        }

        private void ApplyInbox(Inbox inbox)
        {
            _inbox = inbox ?? new Inbox();
            if (UiKit.SetTheme(_inbox.Theme)) RebuildUi(); // the admin switched the interface style
            _screen.SetInbox(_inbox);
            _tab.SetCount(_inbox.Enabled ? _inbox.Unread : 0);

            InboxItem pick = null; string title = null, sub = null; bool critical = false;
            foreach (InboxItem i in _inbox.Items)
            {
                if (i.Unread && i.Critical) { pick = i; critical = true; title = "PRIORITY ORDER"; sub = "CLASSIFIED  -  CLICK TO OPEN"; break; }
            }
            if (pick == null)
            {
                foreach (InboxItem i in _inbox.Items)
                {
                    if (i.Squad != null && i.Squad.AllOnline && i.Squad.State == "waiting") { pick = i; title = "SQUAD ASSEMBLED"; sub = "BRIEFING READY  -  CLICK TO OPEN"; break; }
                }
            }
            if (pick != null && _inbox.Enabled && !_screen.IsOpen) _alert.Set(pick.Id, critical, title, sub); else _alert.Clear();
        }
    }
}
