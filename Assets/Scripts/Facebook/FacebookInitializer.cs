using UnityEngine;
#if PIXELSHOOT_FACEBOOK
using Facebook.Unity;
#endif

namespace PixelShoot.FacebookIntegration
{
    /// <summary>
    /// Initialises the Facebook SDK once per app lifetime, ActivateApp's on resume,
    /// and exposes a few helpers for common app events (level completed, purchase,
    /// etc.). The whole class compiles into an empty stub when the FB SDK isn't
    /// imported and PIXELSHOOT_FACEBOOK isn't defined, so the rest of the game
    /// can call <see cref="LogLevelCompleted"/> etc. unconditionally.
    ///
    /// <para>Auto-bootstraps via <see cref="RuntimeInitializeOnLoadMethod"/> so you
    /// don't have to drop the component on any scene object.</para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class FacebookInitializer : MonoBehaviour
    {
        private static FacebookInitializer instance;
        public static bool IsInitialized { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("[FacebookInitializer]");
            instance = go.AddComponent<FacebookInitializer>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        private void Init()
        {
#if PIXELSHOOT_FACEBOOK
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                IsInitialized = true;
                Debug.Log("[Facebook] Already initialised; called ActivateApp.");
                return;
            }
            FB.Init(OnFbInitComplete, OnFbHideUnity);
#else
            Debug.Log("[Facebook] SDK not installed (PIXELSHOOT_FACEBOOK not defined). All FB calls are no-ops.");
            IsInitialized = false;
#endif
        }

#if PIXELSHOOT_FACEBOOK
        private void OnFbInitComplete()
        {
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                IsInitialized = true;
                Debug.Log("[Facebook] FB.Init complete + ActivateApp.");
            }
            else
            {
                Debug.LogWarning("[Facebook] FB.Init failed.");
                IsInitialized = false;
            }
        }

        private void OnFbHideUnity(bool isUnityShown)
        {
            // Pause game audio etc. while the FB dialog is open.
            Time.timeScale = isUnityShown ? 1f : 0f;
        }

        private void OnApplicationPause(bool paused)
        {
            // ActivateApp should be called when the app comes to the foreground so
            // session tracking stays accurate.
            if (!paused && FB.IsInitialized) FB.ActivateApp();
        }
#endif

        // ── Public helpers — safe to call even when SDK isn't installed ──
        public static void LogLevelCompleted(int displayLevel)
        {
#if PIXELSHOOT_FACEBOOK
            if (!IsInitialized) return;
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { AppEventParameterName.Level, displayLevel.ToString() }
            };
            FB.LogAppEvent(AppEventName.CompletedTutorial, parameters: parameters);
            FB.LogAppEvent("level_completed", parameters: parameters);
#endif
        }

        public static void LogPurchase(decimal amount, string currency, string productId)
        {
#if PIXELSHOOT_FACEBOOK
            if (!IsInitialized) return;
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { AppEventParameterName.ContentID, productId }
            };
            FB.LogPurchase((float)amount, currency, parameters);
#endif
        }

        public static void LogCustomEvent(string eventName,
            System.Collections.Generic.Dictionary<string, object> parameters = null)
        {
#if PIXELSHOOT_FACEBOOK
            if (!IsInitialized) return;
            FB.LogAppEvent(eventName, parameters: parameters);
#endif
        }
    }
}
