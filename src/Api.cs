using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace Operations.Client
{
    internal sealed class SquadFlags { public bool AllOnline, IsCommander; public string State = "waiting"; }

    internal sealed class InboxItem
    {
        public string Id, Level, Label, Map, Rev;
        public bool Unread;
        public int Status;
        public SquadFlags Squad;
        public bool Critical { get { return Level == "critical"; } }
    }

    internal sealed class Inbox
    {
        public bool Enabled;
        public string Profile, Theme = "classic";
        public int Unread, Critical;
        public List<InboxItem> Items = new List<InboxItem>();
        public HashSet<string> Keep;   // hashes still needed by the player's missions (null = server did not say)
    }

    internal sealed class SquadMember { public string Name; public bool Online, Commander, You; }

    internal sealed class SquadView
    {
        public string Id, Name, Commander, State = "waiting", Gate, By;
        public MediaRef GateAudio;
        public bool IsCommander, AllOnline;
        public long StartAt, ServerNow;
        public List<SquadMember> Members = new List<SquadMember>();
    }

    internal sealed class MediaRef { public int Index; public string Type, File, Sha; public long Size; }
    internal sealed class ItemRef { public string Tpl; public int Count; }

    internal sealed class Dossier
    {
        public string Id, Level, Title, QuestId, Description, Map, Sender, Subject, Body, Rev;
        public int Status, MinLevel, SquadMin = 1, SquadMax = 1, Roubles, Xp, Points;
        public bool Gated;
        public SquadView Squad;
        public List<MediaRef> Media = new List<MediaRef>();
        public List<string> Objectives = new List<string>();
        public List<ItemRef> Loadout = new List<ItemRef>();
        public List<ItemRef> RewardItems = new List<ItemRef>();
    }

    /// <summary>Blocking calls to the server module (EconomyGuard Operations). Call from a worker thread only.</summary>
    internal static class Api
    {
        private const string Base = "/client/eft/missionops/";

        private static JToken Unwrap(string json, string route)
        {
            if (string.IsNullOrEmpty(json)) throw new Exception("empty reply from " + route);
            JObject root = JObject.Parse(json);
            int err = root.Value<int?>("err") ?? 0;
            if (err != 0) throw new Exception(root.Value<string>("errmsg") ?? "server error");
            return root["data"];
        }

        private static JToken Get(string route) { return Unwrap(RequestHandler.GetJson(Base + route), route); }
        private static JToken Post(string route, JObject body) { return Unwrap(RequestHandler.PostJson(Base + route, body.ToString(Newtonsoft.Json.Formatting.None)), route); }

        public static Inbox GetInbox()
        {
            JToken d = Get("inbox");
            var box = new Inbox();
            if (d == null || d.Type != JTokenType.Object) return box;
            box.Enabled = d.Value<bool?>("enabled") == true;
            box.Profile = d.Value<string>("profile");
            if (d["keep"] is JArray kp) { box.Keep = new HashSet<string>(); foreach (JToken k in kp) box.Keep.Add((string)k); }
            box.Theme = d.Value<string>("theme") == "modern" ? "modern" : "classic";
            box.Unread = d.Value<int?>("unread") ?? 0;
            box.Critical = d.Value<int?>("critical") ?? 0;
            if (d["items"] is JArray items)
            {
                foreach (JToken i in items)
                {
                    var it = new InboxItem
                    {
                        Id = i.Value<string>("id"), Level = i.Value<string>("level") ?? "normal", Label = i.Value<string>("label") ?? "",
                        Unread = i.Value<bool?>("unread") == true, Status = i.Value<int?>("status") ?? 0, Map = i.Value<string>("map"), Rev = i.Value<string>("rev")
                    };
                    JToken sq = i["squad"];
                    if (sq != null && sq.Type == JTokenType.Object)
                        it.Squad = new SquadFlags { AllOnline = sq.Value<bool?>("allOnline") == true, IsCommander = sq.Value<bool?>("isCommander") == true, State = sq.Value<string>("state") ?? "waiting" };
                    if (!string.IsNullOrEmpty(it.Id)) box.Items.Add(it);
                }
            }
            return box;
        }

        public static Dossier Open(string id) { return ParseDossier(Post("open", new JObject { ["id"] = id })); }

        public static SquadView StartBriefing(string id) { return ParseSquad(Post("squad/start-briefing", new JObject { ["id"] = id })); }

        /// <summary>Image attachment bytes (PNG/JPG).</summary>
        public static byte[] MediaFile(string id, object key, long size, string sha)
        {
            var ms = new System.IO.MemoryStream(); long off = 0;
            while (size <= 0 || off < size)
            {
                JToken d = Post("media", new JObject { ["id"] = id, ["index"] = JToken.FromObject(key), ["offset"] = off });
                string b64 = d == null ? null : d.Value<string>("base64");
                if (string.IsNullOrEmpty(b64)) throw new Exception("empty reply");
                if (size <= 0) size = d.Value<long?>("size") ?? 0;
                byte[] part = Convert.FromBase64String(b64); ms.Write(part, 0, part.Length); off += part.Length;
                if (ms.Length > 16L * 1024 * 1024) throw new Exception("file too large");
            }
            byte[] all = ms.ToArray();
            if (ImageCache.IsHash(sha) && ImageCache.HashOf(all) != sha) throw new Exception("checksum mismatch");
            return all;
        }

        private static SquadView ParseSquad(JToken s)
        {
            if (s == null || s.Type != JTokenType.Object) return null;
            var v = new SquadView
            {
                Id = s.Value<string>("id"), Name = s.Value<string>("name"), Commander = s.Value<string>("commander"), Gate = s.Value<string>("gate"),
                IsCommander = s.Value<bool?>("isCommander") == true, AllOnline = s.Value<bool?>("allOnline") == true
            };
            JToken ga = s["gateAudio"]; if (ga != null && ga.Type == JTokenType.Object) v.GateAudio = new MediaRef { Index = -1, Type = ga.Value<string>("kind") ?? "audio", Sha = ga.Value<string>("sha"), Size = ga.Value<long?>("size") ?? 0, File = "gate." + (ga.Value<string>("ext") ?? "mp3") };
            JToken b = s["briefing"];
            if (b != null) { v.State = b.Value<string>("state") ?? "waiting"; v.StartAt = b.Value<long?>("startAt") ?? 0; v.ServerNow = b.Value<long?>("serverNow") ?? 0; v.By = b.Value<string>("by"); }
            if (s["members"] is JArray ms)
                foreach (JToken m in ms)
                    v.Members.Add(new SquadMember { Name = m.Value<string>("name"), Online = m.Value<bool?>("online") == true, Commander = m.Value<bool?>("commander") == true, You = m.Value<bool?>("you") == true });
            return v;
        }

        private static Dossier ParseDossier(JToken d)
        {
            if (d == null || d.Type != JTokenType.Object) return null;
            var o = new Dossier
            {
                Id = d.Value<string>("id"), Level = d.Value<string>("level") ?? "normal", Title = d.Value<string>("title"), QuestId = d.Value<string>("questId"),
                Status = d.Value<int?>("status") ?? 0, Gated = d.Value<bool?>("gated") == true, Squad = ParseSquad(d["squad"]),
                Description = d.Value<string>("description"), Map = d.Value<string>("map"), Rev = d.Value<string>("rev"), MinLevel = d.Value<int?>("minLevel") ?? 1
            };
            JToken sz = d["squadSize"]; if (sz != null && sz.Type == JTokenType.Object) { o.SquadMin = sz.Value<int?>("min") ?? 1; o.SquadMax = sz.Value<int?>("max") ?? 1; }
            JToken b = d["briefing"];
            if (b != null && b.Type == JTokenType.Object)
            {
                o.Sender = b.Value<string>("sender"); o.Subject = b.Value<string>("subject"); o.Body = b.Value<string>("body");
                if (b["media"] is JArray ma) foreach (JToken m in ma) o.Media.Add(new MediaRef { Index = m.Value<int?>("index") ?? 0, Type = m.Value<string>("type"), File = m.Value<string>("file"), Sha = m.Value<string>("sha"), Size = m.Value<long?>("size") ?? 0 });
            }
            if (d["objectives"] is JArray ob) foreach (JToken t in ob) o.Objectives.Add((string)t);
            if (d["loadout"] is JArray lo) foreach (JToken t in lo) o.Loadout.Add(new ItemRef { Tpl = t.Value<string>("tpl"), Count = t.Value<int?>("count") ?? 1 });
            JToken r = d["rewards"];
            if (r != null && r.Type == JTokenType.Object)
            {
                o.Roubles = r.Value<int?>("roubles") ?? 0; o.Xp = r.Value<int?>("xp") ?? 0; o.Points = r.Value<int?>("points") ?? 0;
                if (r["items"] is JArray ri) foreach (JToken t in ri) o.RewardItems.Add(new ItemRef { Tpl = t.Value<string>("tpl"), Count = t.Value<int?>("count") ?? 1 });
            }
            return o;
        }
    }
}
