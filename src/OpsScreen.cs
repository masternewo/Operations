using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>
    /// Full-screen "Operations" interface: order list on the left, classified dossier on the right.
    /// Unread orders stay sealed (CONFIDENTIAL) until the player presses OPEN FILE; squad orders stay gated until the commander
    /// starts the shared briefing (server timestamp, so everybody's countdown ends at the same moment).
    /// </summary>
    internal sealed class OpsScreen : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _listContent, _dossierContent;
        private ScrollRect _dossierScroll;
        private TextMeshProUGUI _status, _subtitle;
        private Inbox _inbox = new Inbox();
        private string _selected;
        private readonly Dictionary<string, Dossier> _opened = new Dictionary<string, Dossier>();
        private readonly Dictionary<string, Texture2D> _images = new Dictionary<string, Texture2D>();
        private string _profile;
        private readonly HashSet<string> _loading = new HashSet<string>(); // images being fetched (no duplicate downloads / leaked textures)
        private bool _busy;

        private void DropImage(string id) { Texture2D t; if (_images.TryGetValue(id, out t)) { if (t != null) Destroy(t); _images.Remove(id); } }
        private void DropAll() { _loading.Clear(); foreach (Texture2D t in _images.Values) if (t != null) Destroy(t); _images.Clear(); _opened.Clear(); }
        private float _nextSquadPoll;
        private TextMeshProUGUI _countdown;
        private float _cdLocalAt; private long _cdServerNow, _cdStartAt; private bool _cdActive; private string _cdMission;

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        public static OpsScreen Create(Transform parent)
        {
            var go = new GameObject("Operations.Screen");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<OpsScreen>();
            s.Build();
            return s;
        }

        // ------------------------------------------------------------------------------------------------ build
        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 6000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            Image dim = UiKit.Box(transform, "Dim", UiKit.Dim); UiKit.Stretch(dim.rectTransform);

            Image win = UiKit.Box(dim.transform, "Window", UiKit.Panel);
            UiKit.Anchor(win.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-720, -400), new Vector2(720, 400));
            UiKit.Cut(win, 26f);
            Image gold = UiKit.Box(win.transform, "TopLine", UiKit.Gold); gold.raycastTarget = false;
            UiKit.Anchor(gold.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(UiKit.Modern ? 26 : 0, -3), new Vector2(0, 0));

            // header
            RectTransform header = UiKit.Rect("Header", win.transform);
            UiKit.Anchor(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -82), new Vector2(-28, -12));
            TextMeshProUGUI title = UiKit.Label(header, "OPERATIONS", 34, UiKit.Gold, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(title.rectTransform, new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(0, 14), new Vector2(0, 0));
            _subtitle = UiKit.Label(header, "SECURE CHANNEL  //  AUTHORIZED PERSONNEL ONLY", 13, UiKit.Muted);
            UiKit.Anchor(_subtitle.rectTransform, new Vector2(0, 0), new Vector2(0.7f, 0), new Vector2(2, 0), new Vector2(0, 18));
            // Small window with a yellow border at the top right: the one way to close the screen (no keyboard shortcut to collide with the game menu).
            Image frame = UiKit.Box(header, "CloseFrame", UiKit.Frame); frame.raycastTarget = false;
            UiKit.Anchor(frame.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-132, -22), new Vector2(0, 22));
            Image inner = UiKit.Box(frame.transform, "CloseInner", UiKit.Panel2);
            UiKit.Stretch(inner.rectTransform, 2, 2, 2, 2);
            var closeBtn = inner.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = inner;
            ColorBlock ccb = closeBtn.colors; ccb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f); ccb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); closeBtn.colors = ccb;
            closeBtn.onClick.AddListener(Hide);
            TextMeshProUGUI closeText = UiKit.Label(inner.transform, "X   CLOSE", 15, UiKit.Frame, FontStyles.Bold, TextAlignmentOptions.Center);
            closeText.enableWordWrapping = false; closeText.characterSpacing = 2f;
            UiKit.Stretch(closeText.rectTransform);
            Image rule = UiKit.Box(win.transform, "Rule", UiKit.Line); rule.raycastTarget = false;
            UiKit.Anchor(rule.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -86), new Vector2(-28, -85));

            // left list
            Image left = UiKit.Box(win.transform, "ListPane", UiKit.Panel2); UiKit.Cut(left, 14f);
            UiKit.Anchor(left.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(28, 52), new Vector2(408, -100));
            TextMeshProUGUI lh = UiKit.Label(left.transform, "ORDERS", 14, UiKit.Muted, FontStyles.Bold);
            UiKit.Anchor(lh.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -30), new Vector2(-14, -8));
            ScrollRect ls; _listContent = UiKit.Scroll(left.transform, out ls, 6f, 8);
            UiKit.Anchor((RectTransform)ls.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, -34));

            // right dossier
            Image right = UiKit.Box(win.transform, "DossierPane", UiKit.Panel2); UiKit.Cut(right, 14f);
            UiKit.Anchor(right.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(424, 52), new Vector2(-28, -100));
            _dossierContent = UiKit.Scroll(right.transform, out _dossierScroll, 10f, 22);
            UiKit.Stretch((RectTransform)_dossierScroll.transform);

            // footer
            _status = UiKit.Label(win.transform, "", 14, UiKit.Muted);
            UiKit.Anchor(_status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(28, 14), new Vector2(-28, 44));

            _canvas.enabled = false;
        }

        // ------------------------------------------------------------------------------------------------ public
        public void SetInbox(Inbox inbox)
        {
            _inbox = inbox ?? new Inbox();
            // another profile logged in (profile switch): nothing cached for the previous one may be shown
            if (!string.IsNullOrEmpty(_inbox.Profile) && _inbox.Profile != _profile)
            {
                _profile = _inbox.Profile; DropAll(); _selected = null;
                if (IsOpen) { RebuildList(); ShowSelected(false); }
                return;
            }
            // a mission edited on the server (text, equipment, image): forget the stale dossier so it is fetched again
            bool staleSelected = false;
            foreach (InboxItem it in _inbox.Items)
            {
                Dossier old;
                if (it.Rev != null && _opened.TryGetValue(it.Id, out old) && old != null && old.Rev != it.Rev)
                {
                    _opened.Remove(it.Id); DropImage(it.Id);
                    if (it.Id == _selected) staleSelected = true;
                }
            }
            if (IsOpen) { RebuildList(); if (staleSelected && !_busy) Refresh(_selected, true); }
        }

        public void Show(string selectId, bool autoOpen)
        {
            _canvas.enabled = true;
            if (!string.IsNullOrEmpty(selectId)) _selected = selectId;
            else if (_selected == null && _inbox.Items.Count > 0) _selected = _inbox.Items[0].Id;
            SetStatus("", false);
            RebuildList();
            ShowSelected(autoOpen);
        }

        public void Hide() { if (_canvas != null) _canvas.enabled = false; _cdActive = false; }

        private void Update()
        {
            if (!IsOpen) return;
            // squad briefing: poll while the file is gated / waiting, and finish the countdown on the server's clock
            if (_selected != null && _opened.TryGetValue(_selected, out Dossier d) && d.Squad != null)
            {
                if (_cdActive && _countdown != null)
                {
                    double remaining = (_cdStartAt - (_cdServerNow + (Time.unscaledTime - _cdLocalAt) * 1000.0)) / 1000.0;
                    if (remaining > 0) _countdown.text = "BRIEFING STARTS IN  " + Mathf.CeilToInt((float)remaining) + " s";
                    else { _countdown.text = "OPENING..."; _cdActive = false; Refresh(_selected); }
                }
                else if (Time.unscaledTime >= _nextSquadPoll && !_busy && (d.Gated || d.Squad.State != "live"))
                {
                    _nextSquadPoll = Time.unscaledTime + 2f; Refresh(_selected, true);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ list
        private void RebuildList()
        {
            Clear(_listContent);
            if (_inbox.Items.Count == 0)
            {
                TextMeshProUGUI none = UiKit.Label(_listContent, "No orders at this time.", 15, UiKit.Muted);
                UiKit.Size(none, 40, 40);
                return;
            }
            foreach (InboxItem it in _inbox.Items)
            {
                InboxItem item = it;
                bool sel = item.Id == _selected;
                Button b = UiKit.Btn(_listContent, "", () => { _selected = item.Id; RebuildList(); ShowSelected(false); }, sel ? new Color(0.2f, 0.21f, 0.2f, 1f) : UiKit.Panel);
                UiKit.Size(b, 74, 74);
                foreach (Transform c in b.transform) c.gameObject.SetActive(false); // drop the default centered label
                Image stripe = UiKit.Box(b.transform, "Stripe", item.Critical ? UiKit.Red : UiKit.Gold); stripe.raycastTarget = false;
                UiKit.Anchor(stripe.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(4, 0));
                TextMeshProUGUI t = UiKit.Label(b.transform, item.Label, 18, item.Unread ? UiKit.Gold : UiKit.Text, item.Unread ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.TopLeft);
                UiKit.Anchor(t.rectTransform, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(16, 0), new Vector2(-10, -8));
                string sub = item.Unread ? (item.Critical ? "PRIORITY: CRITICAL  -  SEALED" : "SEALED  -  OPEN TO READ") : StatusText(item.Status) + (string.IsNullOrEmpty(item.Map) || item.Map == "any" ? "" : "  -  " + item.Map.Replace('_', ' ').ToUpperInvariant());
                if (item.Squad != null && item.Squad.AllOnline && item.Squad.State == "waiting") sub = "SQUAD ASSEMBLED  -  BRIEFING PENDING";
                TextMeshProUGUI s = UiKit.Label(b.transform, sub, 12, item.Critical && item.Unread ? UiKit.Red : UiKit.Muted);
                UiKit.Anchor(s.rectTransform, new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(16, 8), new Vector2(-10, 0));
            }
        }

        private static string StatusText(int s)
        {
            switch (s) { case 2: return "IN PROGRESS"; case 3: return "READY - CLAIM REWARD"; case 4: return "COMPLETED"; case 5: case 6: case 7: return "FAILED"; default: return "NOT ACCEPTED"; }
        }

        // ------------------------------------------------------------------------------------------------ dossier
        private void ShowSelected(bool autoOpen)
        {
            Clear(_dossierContent); _countdown = null; _cdActive = false;
            if (_selected == null)
            {
                Note("Select an order from the list.");
                return;
            }
            InboxItem item = _inbox.Items.Find(i => i.Id == _selected);
            Dossier cached;
            if (_opened.TryGetValue(_selected, out cached)) { BuildDossier(cached); if (!cached.Gated && !_busy) Refresh(_selected, true); return; }
            if (item != null && item.Unread && !autoOpen)
            {
                TextMeshProUGUI c = UiKit.Label(_dossierContent, "CONFIDENTIAL", 46, UiKit.Gold, FontStyles.Bold, TextAlignmentOptions.Center);
                UiKit.Size(c, 90, 90);
                TextMeshProUGUI s = UiKit.Label(_dossierContent, item.Critical ? "PRIORITY ORDER  -  EYES ONLY" : "AUTHORIZED PERSONNEL ONLY", 18, item.Critical ? UiKit.Red : UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.Center);
                UiKit.Size(s, 34, 34);
                string id = _selected;
                Button open = UiKit.Btn(_dossierContent, "OPEN FILE", () => Refresh(id), UiKit.Shade(UiKit.Gold, 0.55f), 20);
                UiKit.Size(open, 56, 56, 320);
                return;
            }
            Refresh(_selected);
        }

        private void Refresh(string id, bool silent = false)
        {
            if (_busy) return;
            _busy = true;
            if (!silent) { Note("Decrypting file..."); }
            Dispatcher.Run(() => Api.Open(id), d =>
            {
                _busy = false;
                if (d == null) { SetStatus("The server returned no data.", true); return; }
                Dossier prev; if (_opened.TryGetValue(id, out prev) && prev != null && prev.Rev != d.Rev) DropImage(id); // content or attachment replaced on the server
                _opened[id] = d;
                MarkRead(id);
                if (id == _selected) { Clear(_dossierContent); BuildDossier(d); }
                if (d.Media.Count > 0 && !d.Gated && !_images.ContainsKey(id) && !_loading.Contains(id)) LoadImage(d);
            }, err => { _busy = false; SetStatus("Cannot open: " + err, true); if (id == _selected && !silent) { Clear(_dossierContent); Note("Cannot open this file: " + err); } });
        }

        private void MarkRead(string id)
        {
            InboxItem it = _inbox.Items.Find(i => i.Id == id);
            if (it != null && it.Unread && _opened[id] != null && !_opened[id].Gated)
            {
                it.Unread = false; if (_opened[id].Title != null) it.Label = _opened[id].Title;
                RebuildList();
                OperationsPlugin.Instance.RequestInboxRefresh();
            }
        }

        /// <summary>Worker thread: disk cache first (by the server's hash), download only when the file is new or was replaced.</summary>
        private static byte[] FetchImage(string missionId, MediaRef m)
        {
            byte[] bytes = ImageCache.TryGet(m.Sha);
            if (bytes != null) return bytes;
            bytes = Api.Media(missionId, m.Index);
            if (bytes != null && bytes.Length > 0) ImageCache.Put(bytes);
            return bytes;
        }

        private void LoadImage(Dossier d)
        {
            MediaRef m = d.Media.Find(x => x.Type == "image");
            if (m == null) return;
            string id = d.Id; string rev = d.Rev;
            _loading.Add(id);
            Dispatcher.Run(() => FetchImage(id, m), bytes =>
            {
                _loading.Remove(id);
                if (bytes == null || bytes.Length == 0) return;
                Dossier now; if (!_opened.TryGetValue(id, out now) || now == null || now.Rev != rev) return; // replaced / profile switched meanwhile
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) { Destroy(tex); return; }
                DropImage(id);
                _images[id] = tex;
                if (id == _selected && _opened.TryGetValue(id, out Dossier cur)) { Clear(_dossierContent); BuildDossier(cur); }
            }, err => { _loading.Remove(id); SetStatus("Attachment unavailable: " + err, true); });
        }

        private void BuildDossier(Dossier d)
        {
            Transform p = _dossierContent;
            TextMeshProUGUI stamp = UiKit.Label(p, d.Level == "critical" ? "TOP SECRET  //  PRIORITY: CRITICAL" : "CONFIDENTIAL", 14, d.Level == "critical" ? UiKit.Red : UiKit.Gold, FontStyles.Bold);
            UiKit.Size(stamp, 20, 20);
            TextMeshProUGUI title = UiKit.Label(p, (d.Title ?? "").ToUpperInvariant(), 40, UiKit.Text, FontStyles.Bold);
            UiKit.Size(title, 50);
            UiKit.Divider(p);

            string meta = "STATUS  " + StatusText(d.Status);
            if (!string.IsNullOrEmpty(d.Map)) meta += "     MAP  " + (d.Map == "any" ? "ANY" : d.Map.Replace('_', ' ').ToUpperInvariant());
            if (!d.Gated) meta += "     MIN LEVEL  " + d.MinLevel + "     SQUAD  " + d.SquadMin + (d.SquadMax != d.SquadMin ? "-" + d.SquadMax : "");
            TextMeshProUGUI mt = UiKit.Label(p, meta, 15, UiKit.Muted); UiKit.Size(mt, 24);

            if (!d.Gated) BuildActions(p, d);
            if (d.Squad != null) BuildSquad(p, d);
            if (d.Gated) { SetStatus("File locked until the squad commander opens the briefing.", false); return; }

            if (!string.IsNullOrEmpty(d.Description) && d.Description != d.Body) { TextMeshProUGUI ds = UiKit.Label(p, d.Description, 17, UiKit.Text); }

            if (d.Objectives.Count > 0)
            {
                UiKit.Section(p, "OBJECTIVES");
                var sb = new StringBuilder();
                for (int i = 0; i < d.Objectives.Count; i++) sb.Append(i + 1).Append(".  ").Append(d.Objectives[i]).Append('\n');
                UiKit.Label(p, sb.ToString().TrimEnd(), 17, UiKit.Text);
            }

            UiKit.Section(p, "REWARD");
            var rw = new List<string>();
            if (d.Roubles > 0) rw.Add(d.Roubles.ToString("N0") + " RUB"); if (d.Xp > 0) rw.Add(d.Xp.ToString("N0") + " XP"); if (d.Points > 0) rw.Add(d.Points + " RANK POINTS");
            foreach (ItemRef r in d.RewardItems) rw.Add(Name(r.Tpl) + " x" + r.Count);
            UiKit.Label(p, rw.Count > 0 ? string.Join("   |   ", rw.ToArray()) : "-", 17, UiKit.Text);

            if (d.Loadout.Count > 0)
            {
                UiKit.Section(p, "ISSUED EQUIPMENT  (delivered when you accept the task)");
                var sb = new StringBuilder();
                foreach (ItemRef r in d.Loadout) sb.Append(Name(r.Tpl)).Append("  x").Append(r.Count).Append('\n');
                UiKit.Label(p, sb.ToString().TrimEnd(), 16, UiKit.Text);
            }

            UiKit.Divider(p);
            UiKit.Section(p, "BRIEFING");
            UiKit.Label(p, "FROM:  " + (d.Sender ?? ""), 15, UiKit.Muted);
            if (!string.IsNullOrEmpty(d.Subject)) UiKit.Label(p, "SUBJECT:  " + d.Subject, 15, UiKit.Muted);
            TextMeshProUGUI body = UiKit.Label(p, d.Body ?? "", 18, UiKit.Text); body.lineSpacing = 8;

            Texture2D tex;
            if (_images.TryGetValue(d.Id, out tex))
            {
                UiKit.Section(p, "ATTACHMENT");
                RectTransform holder = UiKit.Rect("Attachment", p);
                var raw = holder.gameObject.AddComponent<RawImage>(); raw.texture = tex; raw.raycastTarget = false;
                var fitter = holder.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight; fitter.aspectRatio = (float)tex.width / tex.height;
                UiKit.Size(holder, 120, 480, 840);
                var le = holder.GetComponent<LayoutElement>(); le.flexibleWidth = 0;
            }
            SetStatus("", false);
        }

        private void BuildActions(Transform p, Dossier d)
        {
            if (string.IsNullOrEmpty(d.QuestId)) return;
            string id = d.Id, qid = d.QuestId;
            int cs = QuestBridge.Lookup(qid);          // -2: the game does not know this task yet
            int status = cs >= 0 ? cs : d.Status;
            if (cs == -2 && (d.Status <= 1))
            {
                Button sb = UiKit.Btn(p, "SYNC TASK", () => SyncNow(id, qid), UiKit.Shade(UiKit.Gold, 0.55f), 20);
                UiKit.Size(sb, 52, 52, 300);
                UiKit.Label(p, "This task was published while you were in the menu, so your game has not loaded it yet. Press SYNC TASK to try loading it now; if that fails it appears after your next raid or relog.", 13, UiKit.Muted);
                return;
            }
            if (status == 0 || status == 1 || status == 6)
            {
                Button b = UiKit.Btn(p, status == 0 ? "LOCKED" : "ACCEPT TASK", () => Act(id, qid, false), UiKit.Shade(UiKit.Gold, 0.55f), 20);
                b.interactable = status != 0; UiKit.Size(b, 52, 52, 300);
                UiKit.Label(p, status == 0 ? "The game reports this task as locked (its requirements are not met yet)." : d.Loadout.Count > 0 ? "Accepting starts the objectives. The issued equipment is delivered to your messages from the task giver." : "Accepting starts the objectives.", 13, UiKit.Muted);
            }
            else if (status == 2)
            {
                UiKit.Label(p, "TASK ACTIVE  -  complete the objectives in raid, then return here to claim the reward.", 15, UiKit.Green, FontStyles.Bold);
            }
            else if (status == 3)
            {
                Button b = UiKit.Btn(p, "CLAIM REWARD", () => Act(id, qid, true), UiKit.Shade(UiKit.Green, 0.5f), 20);
                UiKit.Size(b, 52, 52, 300);
                UiKit.Label(p, "Objectives complete. Claiming pays the reward (money and items arrive in your messages) and adds your rank points.", 13, UiKit.Muted);
            }
            else if (status == 4) UiKit.Label(p, "TASK COMPLETED  -  reward claimed.", 15, UiKit.Green, FontStyles.Bold);
            else if (status == 5 || status == 7) UiKit.Label(p, "TASK FAILED.", 15, UiKit.Red, FontStyles.Bold);
        }

        private void SyncNow(string id, string qid)
        {
            if (_busy) return; _busy = true;
            SetStatus("Syncing...", false);
            QuestBridge.TrySync(qid, how =>
            {
                _busy = false;
                if (QuestBridge.Lookup(qid) != -2) { SetStatus("Task loaded.", false); Dossier d; if (id == _selected && _opened.TryGetValue(id, out d)) { Clear(_dossierContent); BuildDossier(d); } }
                else SetStatus("Could not load it live (" + how + "). It appears after your next raid or relog.", true);
            });
        }

        private void Act(string id, string questId, bool finish)
        {
            if (_busy) return; _busy = true;
            SetStatus(finish ? "Claiming reward..." : "Accepting task...", false);
            Action<string> done = err =>
            {
                _busy = false;
                if (err != null) { SetStatus(err, true); return; }
                SetStatus(finish ? "Reward claimed." : "Task accepted.", false);
                _opened.Remove(id);
                OperationsPlugin.Instance.RequestInboxRefresh();
                Refresh(id, true);
            };
            if (finish) QuestBridge.Finish(questId, done); else QuestBridge.Accept(questId, done);
        }

        private void BuildSquad(Transform p, Dossier d)
        {
            SquadView s = d.Squad;
            UiKit.Divider(p);
            UiKit.Section(p, "SQUAD  -  " + (s.Name ?? "").ToUpperInvariant());
            var sb = new StringBuilder();
            foreach (SquadMember m in s.Members)
                sb.Append(m.Online ? "[ ONLINE ]  " : "[ OFFLINE ] ").Append(m.Name).Append(m.Commander ? "   (COMMANDER)" : "").Append(m.You ? "   <- you" : "").Append('\n');
            UiKit.Label(p, sb.ToString().TrimEnd(), 16, UiKit.Text);

            if (s.State == "live") { UiKit.Label(p, "Briefing is live. Coordinate with your squad before you deploy.", 15, UiKit.Green); return; }
            if (s.State == "countdown")
            {
                _countdown = UiKit.Label(p, "", 26, UiKit.Amber, FontStyles.Bold); UiKit.Size(_countdown, 40, 40);
                _cdServerNow = s.ServerNow; _cdStartAt = s.StartAt; _cdLocalAt = Time.unscaledTime; _cdActive = true; _cdMission = d.Id;
                _countdown.text = "BRIEFING STARTS SOON...";
                return;
            }
            if (s.IsCommander)
            {
                UiKit.Label(p, s.AllOnline ? "All squad members are online. Brief them over voice chat, then start the shared briefing." : "Waiting for squad members to come online. You may start the briefing at any time.", 15, s.AllOnline ? UiKit.Green : UiKit.Muted);
                string id = d.Id;
                Button go = UiKit.Btn(p, "START BRIEFING FOR THE SQUAD", () => StartBriefing(id), UiKit.Shade(UiKit.Gold, 0.55f), 18);
                UiKit.Size(go, 54, 54);
            }
            else
            {
                UiKit.Label(p, s.Gate ?? "Go to voice / private chat with your squad commander and wait for authorization.", 16, UiKit.Amber);
            }
        }

        private void StartBriefing(string id)
        {
            if (_busy) return; _busy = true; SetStatus("Starting briefing...", false);
            Dispatcher.Run(() => Api.StartBriefing(id), v => { _busy = false; SetStatus("", false); Refresh(id); }, err => { _busy = false; SetStatus("Cannot start the briefing: " + err, true); });
        }

        // ------------------------------------------------------------------------------------------------ helpers
        private static string Name(string tpl)
        {
            if (string.IsNullOrEmpty(tpl)) return "?";
            try { string n = Loc.Get(tpl + " Name"); return string.IsNullOrEmpty(n) || n == tpl + " Name" ? tpl : n; }
            catch (Exception) { return tpl; }
        }

        private void Note(string text)
        {
            TextMeshProUGUI t = UiKit.Label(_dossierContent, text, 18, UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.Center);
            UiKit.Size(t, 120, 120);
        }

        private void SetStatus(string text, bool error) { if (_status == null) return; _status.text = text; _status.color = error ? UiKit.Red : UiKit.Muted; }

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) { Transform c = t.GetChild(i); c.SetParent(null, false); Destroy(c.gameObject); }
        }

        private void OnDestroy() { foreach (Texture2D t in _images.Values) if (t != null) Destroy(t); }
    }
}
