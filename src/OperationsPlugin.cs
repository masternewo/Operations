using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using UnityEngine;

namespace Operations.Client
{
    /// <summary>
    /// In-game window for the server-side Operations (missions) module of EconomyGuard.
    /// Press the hotkey (default F10) in the main menu or in raid to open it.
    /// Talks to /client/eft/missionops/state and /missions through SPT's own RequestHandler (same session, same host).
    /// Network calls run on a worker thread; Unity objects are only touched on the main thread.
    /// </summary>
    [BepInPlugin("com.operations.client", "Operations.Client", "0.1.0")]
    public class OperationsPlugin : BaseUnityPlugin
    {
        private const string StateRoute = "/client/eft/missionops/state";
        private const string MissionsRoute = "/client/eft/missionops/missions";

        private ConfigEntry<KeyboardShortcut> _hotkey;
        private ConfigEntry<float> _pollSeconds;
        private ConfigEntry<float> _bannerSeconds;

        private sealed class Mission
        {
            public string Id, Title, Description, Map, Sender, Subject, Body;
            public int Status, MinLevel, SquadMin, SquadMax, Roubles, Xp, Points;
        }

        private sealed class Snapshot
        {
            public bool Enabled;
            public int Points, Completed;
            public string Rank = "";
            public string NextName;
            public int NextRemaining;
            public List<Mission> Missions = new List<Mission>();
        }

        // written by the worker thread, read by the main thread
        private readonly object _lock = new object();
        private Snapshot _pending;
        private string _error;
        private volatile bool _busy;

        private Snapshot _current = new Snapshot();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private bool _firstSnapshot = true;
        private float _nextPoll;
        private string _bannerText;
        private float _bannerUntil;

        private bool _open;
        private Rect _window = new Rect(60f, 60f, 620f, 460f);
        private Vector2 _scroll;
        private string _selectedId;
        private string _lastError;

        private GUIStyle _title, _label, _small, _banner, _body;

        private void Awake()
        {
            _hotkey = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.F10), "Opens/closes the Operations window.");
            _pollSeconds = Config.Bind("General", "PollSeconds", 15f, new ConfigDescription("How often the server is asked for missions.", new AcceptableValueRange<float>(5f, 120f)));
            _bannerSeconds = Config.Bind("General", "BannerSeconds", 8f, new ConfigDescription("How long the new-mission banner stays on screen.", new AcceptableValueRange<float>(2f, 30f)));
            Logger.LogInfo("Operations.Client loaded. Hotkey: " + _hotkey.Value);
        }

        private void Update()
        {
            if (_hotkey.Value.IsDown()) _open = !_open;

            Snapshot fresh = null; string err = null;
            lock (_lock) { fresh = _pending; _pending = null; err = _error; _error = null; }
            if (err != null) _lastError = err;
            if (fresh != null) { _lastError = null; Apply(fresh); }

            if (Time.unscaledTime >= _nextPoll && !_busy)
            {
                _nextPoll = Time.unscaledTime + _pollSeconds.Value;
                _busy = true;
                Task.Run(Fetch);
            }
        }

        // ---------------------------------------------------------------- network (worker thread)
        private void Fetch()
        {
            try
            {
                var snap = new Snapshot();
                JToken state = Data(RequestHandler.GetJson(StateRoute));
                snap.Enabled = state != null && state.Value<bool?>("enabled") == true;
                if (snap.Enabled)
                {
                    snap.Points = state.Value<int?>("points") ?? 0;
                    snap.Completed = state.Value<int?>("completed") ?? 0;
                    snap.Rank = state.Value<string>("rank") ?? "";
                    JToken next = state["next"];
                    if (next != null && next.Type == JTokenType.Object) { snap.NextName = next.Value<string>("name"); snap.NextRemaining = next.Value<int?>("remaining") ?? 0; }

                    if (Data(RequestHandler.GetJson(MissionsRoute)) is JArray list)
                    {
                        foreach (JToken m in list)
                        {
                            JToken b = m["briefing"], r = m["rewards"], s = m["squad"];
                            snap.Missions.Add(new Mission
                            {
                                Id = m.Value<string>("id"), Title = m.Value<string>("title"), Description = m.Value<string>("description"),
                                Map = m.Value<string>("map"), Status = m.Value<int?>("status") ?? 0, MinLevel = m.Value<int?>("minLevel") ?? 1,
                                SquadMin = s?.Value<int?>("min") ?? 1, SquadMax = s?.Value<int?>("max") ?? 1,
                                Sender = b?.Value<string>("sender"), Subject = b?.Value<string>("subject"), Body = b?.Value<string>("body"),
                                Roubles = r?.Value<int?>("roubles") ?? 0, Xp = r?.Value<int?>("xp") ?? 0, Points = r?.Value<int?>("points") ?? 0
                            });
                        }
                    }
                }
                lock (_lock) { _pending = snap; }
            }
            catch (Exception e)
            {
                lock (_lock) { _error = e.Message; }
            }
            finally { _busy = false; }
        }

        /// <summary>The server wraps every reply as {"err":0,"errmsg":null,"data":...}.</summary>
        private static JToken Data(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            JToken root = JToken.Parse(json);
            return root is JObject o && o.ContainsKey("data") ? o["data"] : root;
        }

        // ---------------------------------------------------------------- main thread
        private void Apply(Snapshot s)
        {
            if (!_firstSnapshot)
            {
                foreach (Mission m in s.Missions)
                {
                    if (_seen.Contains(m.Id) || m.Status >= 2) continue;
                    _bannerText = "NEW OPERATION: " + m.Title;
                    _bannerUntil = Time.unscaledTime + _bannerSeconds.Value;
                    break;
                }
            }
            foreach (Mission m in s.Missions) _seen.Add(m.Id);
            _firstSnapshot = false;
            _current = s;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = false };
            _banner = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_bannerText != null && Time.unscaledTime < _bannerUntil)
            {
                float w = Mathf.Min(720f, Screen.width - 40f);
                GUI.Box(new Rect((Screen.width - w) / 2f, 18f, w, 44f), _bannerText + "   [" + _hotkey.Value + "]", _banner);
            }
            if (_open) _window = GUILayout.Window(731_001, _window, DrawWindow, "OPERATIONS");
        }

        private static string StatusText(int status)
        {
            switch (status) { case 2: return "IN PROGRESS"; case 4: return "COMPLETED"; case 5: return "FAILED"; default: return "AVAILABLE"; }
        }

        private void DrawWindow(int id)
        {
            Snapshot s = _current;
            if (!s.Enabled)
            {
                GUILayout.Label(_lastError != null ? "Cannot reach the Operations service: " + _lastError : "Operations are not enabled on this server.", _label);
            }
            else
            {
                GUILayout.Label("Rank: " + s.Rank + "   |   Points: " + s.Points + "   |   Missions done: " + s.Completed
                    + (s.NextName != null ? "   |   " + s.NextRemaining + " to " + s.NextName : ""), _title);
                _scroll = GUILayout.BeginScrollView(_scroll);
                if (s.Missions.Count == 0) GUILayout.Label("No active operations right now.", _label);
                foreach (Mission m in s.Missions) DrawMission(m);
                GUILayout.EndScrollView();
                GUILayout.Label("Accept missions from the trader's quest list. Rewards are paid by the game; points and rank by the server.", _small);
            }
            if (GUILayout.Button("Close")) _open = false;
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void DrawMission(Mission m)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(m.Title + "  —  " + StatusText(m.Status), _title);
            if (GUILayout.Button(_selectedId == m.Id ? "Hide letter" : "Read briefing", GUILayout.Width(120f))) _selectedId = _selectedId == m.Id ? null : m.Id;
            GUILayout.EndHorizontal();
            GUILayout.Label("Map: " + m.Map + "   Level " + m.MinLevel + "+   Squad " + m.SquadMin + "-" + m.SquadMax
                + "   Reward: " + m.Roubles + " RUB, " + m.Xp + " XP, " + m.Points + " pts", _small);
            if (_selectedId == m.Id)
            {
                GUILayout.Space(4f);
                GUILayout.Label("FROM: " + m.Sender, _small);
                GUILayout.Label("SUBJECT: " + m.Subject, _label);
                GUILayout.Label(m.Body, _body);
            }
            else GUILayout.Label(m.Description, _small);
            GUILayout.EndVertical();
        }
    }
}
