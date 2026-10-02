using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PixelShoot.Analytics
{
    /// <summary>
    /// Backend-agnostic analytics dispatcher. Game code calls <see cref="Track"/> (or the typed helpers
    /// in <see cref="AnalyticsEvents"/>); every registered <see cref="IAnalyticsSink"/> receives the event.
    ///
    /// <para>Ships with a single <see cref="DebugLogSink"/> so events are visible in the Console right away.
    /// To send them somewhere real (Firebase, GameAnalytics, a custom backend), implement
    /// <see cref="IAnalyticsSink"/> and call <see cref="AddSink"/> once at startup — no game code changes.</para>
    /// </summary>
    public static class AnalyticsManager
    {
        private static readonly List<IAnalyticsSink> sinks = new List<IAnalyticsSink>();
        private static bool bootstrapped;

        /// <summary>Set false to silence all analytics (e.g. for QA builds).</summary>
        public static bool Enabled = true;

        private static void EnsureBootstrapped()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            // Default sink: pretty-prints every event to the Unity Console.
            sinks.Add(new DebugLogSink());
        }

        /// <summary>Register an extra sink (e.g. a Firebase adapter). Ignores duplicates and nulls.</summary>
        public static void AddSink(IAnalyticsSink sink)
        {
            EnsureBootstrapped();
            if (sink == null || sinks.Contains(sink)) return;
            sinks.Add(sink);
        }

        public static void RemoveSink(IAnalyticsSink sink)
        {
            if (sink != null) sinks.Remove(sink);
        }

        /// <summary>Remove the default Console sink (call before AddSink if you don't want console spam in prod).</summary>
        public static void ClearDefaultSink()
        {
            EnsureBootstrapped();
            sinks.RemoveAll(s => s is DebugLogSink);
        }

        /// <summary>Fire an event to every sink. Never throws — a broken sink can't break gameplay.</summary>
        public static void Track(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (!Enabled || string.IsNullOrEmpty(eventName)) return;
            EnsureBootstrapped();
            for (int i = 0; i < sinks.Count; i++)
            {
                try { sinks[i]?.LogEvent(eventName, parameters); }
                catch (System.Exception e) { Debug.LogWarning($"[Analytics] Sink '{sinks[i]?.GetType().Name}' threw on '{eventName}': {e.Message}"); }
            }
        }
    }

    /// <summary>A destination for analytics events. Implement + register via <see cref="AnalyticsManager.AddSink"/>.</summary>
    public interface IAnalyticsSink
    {
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters);
    }

    /// <summary>Default sink — prints each event and its parameters to the Unity Console.</summary>
    public sealed class DebugLogSink : IAnalyticsSink
    {
        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var sb = new StringBuilder();
            sb.Append("[Analytics] ").Append(eventName);
            if (parameters != null && parameters.Count > 0)
            {
                sb.Append("  { ");
                bool first = true;
                foreach (var kv in parameters)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                }
                sb.Append(" }");
            }
            Debug.Log(sb.ToString());
        }
    }
}
