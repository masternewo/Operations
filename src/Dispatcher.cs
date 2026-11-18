using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Operations.Client
{
    /// <summary>Runs blocking server calls on a worker thread and delivers the result on Unity's main thread.</summary>
    internal static class Dispatcher
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        public static void Run<T>(Func<T> work, Action<T> done, Action<string> fail)
        {
            Task.Run(() =>
            {
                try { T r = work(); Queue.Enqueue(() => done(r)); }
                catch (Exception e) { string m = e.Message; Queue.Enqueue(() => { if (fail != null) fail(m); }); }
            });
        }

        /// <summary>Call from MonoBehaviour.Update.</summary>
        public static void Pump()
        {
            int budget = 32; Action a;
            while (budget-- > 0 && Queue.TryDequeue(out a))
            {
                try { a(); } catch (Exception e) { OperationsPlugin.Log.LogError("UI callback failed: " + e); }
            }
        }
    }
}
