using UnityEngine;
#if PIXELSHOOT_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif

namespace PixelShoot.Analytics
{
    /// <summary>
    /// Initialises Firebase once per app lifetime, then registers a <see cref="FirebaseAnalyticsSink"/>
    /// with <see cref="AnalyticsManager"/> so every game event (level_start / level_complete /
    /// level_fail, and anything else routed through AnalyticsManager) is forwarded to Firebase Analytics.
    ///
    /// <para>Auto-bootstraps via <see cref="RuntimeInitializeOnLoadMethod"/> — no scene wiring needed.
    /// It spawns a persistent (DontDestroyOnLoad) object that runs Firebase's dependency check and,
    /// on success, adds the sink. Compiles into a no-op stub until the Firebase Unity SDK is imported
    /// and <c>PIXELSHOOT_FIREBASE</c> is defined.</para>
    ///
    /// <para><b>Setup</b> (once the SDK is imported):</para>
    /// <list type="number">
    /// <item>Import the Firebase Unity SDK's <c>FirebaseAnalytics.unitypackage</c> (pulls in the
    /// External Dependency Manager + Firebase.Analytics).</item>
    /// <item>Drop <c>google-services.json</c> (Android) / <c>GoogleService-Info.plist</c> (iOS) into
    /// <c>Assets/</c> — the Firebase editor plugin wires them into the build.</item>
    /// <item>Add <c>PIXELSHOOT_FIREBASE</c> to Scripting Define Symbols (Player Settings → Android and
    /// iOS), exactly like <c>PIXELSHOOT_FACEBOOK</c>.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class FirebaseInitializer : MonoBehaviour
    {
        private static FirebaseInitializer instance;
        private static bool sinkRegistered; // app-lifetime guard: never add the Firebase sink twice

        /// <summary>True once Firebase is ready and the analytics sink is registered.</summary>
        public static bool IsAvailable { get; private set; }

        [Tooltip("Remove the default Console (Debug.Log) analytics sink once Firebase is ready, so events " +
                 "don't also spam the log in a shipping build. Leave OFF while testing.")]
        [SerializeField] private bool silenceConsoleSink = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("[FirebaseInitializer]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FirebaseInitializer>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            Init();
        }

        private void Init()
        {
#if PIXELSHOOT_FIREBASE
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    // Enable collection explicitly (in case it was disabled by config).
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    IsAvailable = true;
                    if (!sinkRegistered) // register the sink exactly once per app run
                    {
                        sinkRegistered = true;
                        if (silenceConsoleSink) AnalyticsManager.ClearDefaultSink();
                        AnalyticsManager.AddSink(new FirebaseAnalyticsSink());
                        Debug.Log("[Firebase] Analytics ready — sink registered.");
                    }
                    else Debug.Log("[Firebase] Analytics re-checked — sink already registered.");
                }
                else
                {
                    IsAvailable = false;
                    Debug.LogWarning($"[Firebase] Could not resolve dependencies: {status}. Analytics disabled.");
                }
            });
#else
            IsAvailable = false;
            Debug.Log("[Firebase] SDK not installed (PIXELSHOOT_FIREBASE not defined). Events stay on the Console sink only.");
#endif
        }
    }
}
