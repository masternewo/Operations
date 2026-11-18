using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace Operations.Client
{
    [BepInPlugin("com.operations.client", "Operations Client", "0.2.0")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class OperationsPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static OperationsPlugin Instance;

        private ConfigEntry<KeyCode> _hotkey;
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
            _hotkey = Config.Bind("General", "Hotkey", KeyCode.F10, "Opens the Operations screen (fallback to the menu tab).");
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

            if (Input.GetKeyDown(_hotkey.Value)) { if (_screen.IsOpen) _screen.Hide(); else OpenScreen(); }
            if (_screen.IsOpen && Input.GetKeyDown(KeyCode.Escape)) _screen.Hide();

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
            _screen.SetInbox(_inbox);
            _tab.SetCount(_inbox.Enabled ? _inbox.Unread : 0);

            InboxItem pick = null; string text = null; bool critical = false;
            foreach (InboxItem i in _inbox.Items)
            {
                if (i.Unread && i.Critical) { pick = i; critical = true; text = "PRIORITY ORDER"; break; }
            }
            if (pick == null)
            {
                foreach (InboxItem i in _inbox.Items)
                {
                    if (i.Squad != null && i.Squad.AllOnline && i.Squad.State == "waiting") { pick = i; text = "SQUAD READY"; break; }
                }
            }
            if (pick != null && _inbox.Enabled && !_screen.IsOpen) _alert.Set(pick.Id, critical, text); else _alert.Clear();
        }
    }
}
