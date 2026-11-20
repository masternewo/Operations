using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;

namespace Operations.Client
{
    /// <summary>
    /// Accepts / finishes a mission through the game's own quest controller (the same object the Tasks screen uses), so the
    /// client profile and the server stay in sync. Nothing is changed on the server behind the client's back.
    /// Status numbers follow the SPT/EFT quest status enum: 1 available, 2 started, 3 ready to hand in, 4 success, 5+ failed.
    /// </summary>
    internal static class QuestBridge
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        public static void Accept(string questId, Action<string> done) { Run(questId, false, done); }
        public static void Finish(string questId, Action<string> done) { Run(questId, true, done); }

        /// <summary>done(null) = success, otherwise a short human readable reason.</summary>
        private static void Run(string questId, bool finish, Action<string> done)
        {
            try
            {
                AbstractQuestControllerClass ctl = Controller();
                if (ctl == null) { done("The task system is not ready yet. Try again in a moment."); return; }
                QuestClass quest = Find(ctl, questId);
                if (quest == null) { done("This task is not in your client yet. Return to the main menu after a raid (or restart the game) to receive newly published tasks."); return; }
                int status = StatusOf(quest);
                if (finish ? status != 3 : status != 1 && status != 6)
                {
                    done(finish ? "The objectives are not complete yet." : status == 0 ? "This task is locked by the game (requirements not met)." : "This task cannot be accepted right now (status " + status + ").");
                    return;
                }
                Task task = finish ? (Task)ctl.FinishQuest(quest, true) : ctl.AcceptQuest(quest, true);
                task.ContinueWith(t =>
                {
                    string err = null;
                    if (t.IsFaulted) err = t.Exception != null && t.Exception.InnerException != null ? t.Exception.InnerException.Message : "Request failed.";
                    else err = ResultError(t);
                    Dispatcher.Post(() => done(err));
                });
            }
            catch (Exception e)
            {
                OperationsPlugin.Log.LogWarning("Quest action failed: " + e);
                done("Could not complete the action: " + e.Message);
            }
        }

        /// <summary>-2 = the game's quest list does not contain this task (published after login), otherwise its EQuestStatus.</summary>
        public static int Lookup(string questId)
        {
            try
            {
                AbstractQuestControllerClass ctl = Controller();
                if (ctl == null) return -1;
                QuestClass q = Find(ctl, questId);
                return q == null ? -2 : StatusOf(q);
            }
            catch (Exception) { return -1; }
        }

        /// <summary>
        /// Best effort, fully guarded: ask the session for the quest templates again and feed new ones to the quest controller.
        /// Built on reflection because the game assemblies are obfuscated; every step is logged so a failure shows what to adjust.
        /// Returns a short description of how it ended (for the log / status line).
        /// </summary>
        public static void TrySync(string questId, Action<string> done)
        {
            try
            {
                AbstractQuestControllerClass ctl = Controller();
                if (ctl == null) { done("no controller"); return; }
                object app = Singleton<ClientApplication<ISession>>.Instance;
                MethodInfo gs = app == null ? null : app.GetType().GetMethod("GetClientBackEndSession", Any);
                object session = gs != null ? gs.Invoke(app, null) : null;
                if (session == null) { done("no session"); return; }
                MethodInfo req = null;
                foreach (MethodInfo m in session.GetType().GetMethods(Any))
                    if (m.Name.IndexOf("QuestsTemplates", StringComparison.OrdinalIgnoreCase) >= 0 && m.GetParameters().Length == 0) { req = m; break; }
                if (req == null)
                {
                    var names = new System.Text.StringBuilder();
                    foreach (MethodInfo m in session.GetType().GetMethods(Any))
                        if (m.Name.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0) names.Append(m.Name).Append('(').Append(m.GetParameters().Length).Append(") ");
                    OperationsPlugin.Log.LogWarning("TrySync: no parameterless *QuestsTemplates method on " + session.GetType().FullName + ". Quest methods: " + names);
                    done("template request unavailable"); return;
                }
                object res = req.Invoke(session, null);      // only starts the request; never blocks the game thread
                var task = res as Task;
                if (task == null) { Apply(ctl, res, questId, done); return; }
                task.ContinueWith(t =>
                {
                    object payload = null;
                    try { PropertyInfo rp = t.GetType().GetProperty("Result"); payload = t.IsFaulted ? null : (rp != null ? rp.GetValue(t, null) : null); } catch (Exception) { }
                    Dispatcher.Post(() => Apply(ctl, payload, questId, done));   // game objects are touched on the main thread only
                });
            }
            catch (Exception e) { OperationsPlugin.Log.LogWarning("TrySync failed: " + e); done("error"); }
        }

        private static void Apply(AbstractQuestControllerClass ctl, object res, string questId, Action<string> done)
        {
            try
            {
                if (res == null) { done("empty reply"); return; }
                foreach (FieldInfo f in ctl.GetType().GetFields(Any))
                {
                    MethodInfo add = f.FieldType.GetMethod("AddTemplates", Any);
                    if (add == null) continue;
                    object store = f.GetValue(ctl);
                    if (store == null) continue;
                    object arg = res;
                    PropertyInfo inner = res.GetType().GetProperty("Result");
                    Type want = add.GetParameters()[0].ParameterType;
                    if (!want.IsInstanceOfType(arg) && inner != null) arg = inner.GetValue(res, null);
                    if (arg == null || !want.IsInstanceOfType(arg)) { OperationsPlugin.Log.LogInfo("TrySync: payload type " + (arg == null ? "null" : arg.GetType().FullName) + " does not fit AddTemplates"); done("payload mismatch"); return; }
                    add.Invoke(store, new[] { arg });
                    MethodInfo nq = ctl.GetType().GetMethod("NewQuests", Any);
                    if (nq != null) nq.Invoke(ctl, new object[] { null });
                    done(Lookup(questId) != -2 ? "ok" : "task still unknown");
                    return;
                }
                done("no template store");
            }
            catch (Exception e) { OperationsPlugin.Log.LogWarning("TrySync apply failed: " + e); done("error"); }
        }

        private static AbstractQuestControllerClass Controller()
        {
            object app = Singleton<ClientApplication<ISession>>.Instance;
            if (app == null) return null;
            // TarkovApplication keeps the running main-menu controller; that one owns the quest controller the Tasks screen uses.
            foreach (FieldInfo f in app.GetType().GetFields(Any))
            {
                if (f.FieldType != typeof(MainMenuControllerClass)) continue;
                object menu = f.GetValue(app);
                if (menu == null) continue;
                foreach (FieldInfo q in menu.GetType().GetFields(Any))
                {
                    if (!typeof(AbstractQuestControllerClass).IsAssignableFrom(q.FieldType)) continue;
                    var c = q.GetValue(menu) as AbstractQuestControllerClass;
                    if (c != null) return c;
                }
            }
            return null;
        }

        private static QuestClass Find(AbstractQuestControllerClass ctl, string questId)
        {
            var list = ctl.Quests as IEnumerable;
            if (list == null) return null;
            foreach (object o in list)
            {
                var q = o as QuestClass;
                if (q != null && q.Template != null && q.Template.Id == questId) return q;
            }
            return null;
        }

        public static int StatusOf(QuestClass quest)
        {
            foreach (PropertyInfo p in quest.GetType().GetProperties(Any))
            {
                if (p.PropertyType.IsEnum && p.PropertyType.Name == "EQuestStatus" && p.GetIndexParameters().Length == 0)
                    return Convert.ToInt32(p.GetValue(quest, null));
            }
            return -1;
        }

        /// <summary>Reads Failed/Error from the game's result structs without depending on their obfuscated type names.</summary>
        private static string ResultError(Task t)
        {
            try
            {
                PropertyInfo rp = t.GetType().GetProperty("Result");
                object r = rp != null ? rp.GetValue(t, null) : null;
                if (r == null) return null;
                PropertyInfo failed = r.GetType().GetProperty("Failed");
                if (failed != null && failed.PropertyType == typeof(bool) && (bool)failed.GetValue(r, null))
                {
                    PropertyInfo err = r.GetType().GetProperty("Error");
                    object msg = err != null ? err.GetValue(r, null) : null;
                    return msg != null ? msg.ToString() : "The server refused the request.";
                }
            }
            catch (Exception) { /* unreadable result: treat as success, the server state is re-read afterwards */ }
            return null;
        }
    }
}
