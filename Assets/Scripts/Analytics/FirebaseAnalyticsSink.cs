using System.Collections.Generic;
using UnityEngine;
#if PIXELSHOOT_FIREBASE
using Firebase.Analytics;
#endif

namespace PixelShoot.Analytics
{
    /// <summary>
    /// Sends analytics events to Firebase Analytics. Registered with <see cref="AnalyticsManager"/> by
    /// <see cref="FirebaseInitializer"/> once the Firebase SDK is ready. Converts each event's
    /// <c>object</c> parameters to strongly-typed Firebase <c>Parameter</c>s (int/bool → long,
    /// float → double, everything else → string).
    ///
    /// <para>The whole Firebase-touching body is guarded by <c>PIXELSHOOT_FIREBASE</c>, so this file
    /// compiles into a harmless stub until the Firebase Unity SDK is imported and the define is added.
    /// See the class header of <see cref="FirebaseInitializer"/> for setup steps.</para>
    /// </summary>
    public sealed class FirebaseAnalyticsSink : IAnalyticsSink
    {
        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
#if PIXELSHOOT_FIREBASE
            if (string.IsNullOrEmpty(eventName)) return;
            if (parameters == null || parameters.Count == 0)
            {
                FirebaseAnalytics.LogEvent(eventName);
                return;
            }

            var fbParams = new Parameter[parameters.Count];
            int i = 0;
            foreach (var kv in parameters)
                fbParams[i++] = ToParameter(kv.Key, kv.Value);

            FirebaseAnalytics.LogEvent(eventName, fbParams);
#endif
        }

#if PIXELSHOOT_FIREBASE
        private static Parameter ToParameter(string name, object value)
        {
            switch (value)
            {
                case null:   return new Parameter(name, "");
                case int i:  return new Parameter(name, (long)i);
                case long l: return new Parameter(name, l);
                case bool b: return new Parameter(name, b ? 1L : 0L);
                case float f:  return new Parameter(name, (double)f);
                case double d: return new Parameter(name, d);
                case string s: return new Parameter(name, s);
                default:       return new Parameter(name, value.ToString());
            }
        }
#endif
    }
}
