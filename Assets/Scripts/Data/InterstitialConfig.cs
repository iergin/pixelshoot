using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Designer-tunable knob for interstitial ad pacing.
    ///
    /// <para><b>StartLevel</b>: 1-based level at which interstitials may begin showing.
    /// e.g. set to 3 → no ads on Levels 1 / 2.</para>
    ///
    /// <para><b>LevelsBetweenAds</b>: how many level-end events (win OR lose count
    /// equally) need to pass before the next interstitial. e.g. 3 → every third
    /// level result triggers an ad.</para>
    ///
    /// <para>Watching a rewarded ad resets the counter, so engaged players get a cooldown.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "InterstitialConfig", menuName = "PixelShoot/Interstitial Config")]
    public class InterstitialConfig : ScriptableObject
    {
        [Tooltip("First DisplayLevel that may show an interstitial. Below this, no ads.")]
        [SerializeField, Min(1)] private int startLevel = 3;

        [Tooltip("Levels (win OR lose) between consecutive interstitials. 1 = every level; 3 = every third level.")]
        [SerializeField, Min(1)] private int levelsBetweenAds = 3;

        [Tooltip("Minimum seconds that must pass AFTER an interstitial CLOSES before another may show. " +
                 "The timer starts when the ad closes (not when it opens), so time spent watching the ad " +
                 "doesn't count. 0 = no time cooldown (level cadence only).")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 30f;

        public int StartLevel => startLevel;
        public int LevelsBetweenAds => levelsBetweenAds;
        public float CooldownSeconds => cooldownSeconds;
    }
}
