using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Designer-editable streak tuning: how many streak steps there are and what each step grants.
    /// Read by <see cref="PlayerStreak"/>. Index = streak step (0 = no streak, 1 = 1st win, …), so the
    /// reward arrays must have exactly <c>maxRewardStreak + 1</c> entries.
    ///
    /// <para>Create one via <b>Assets ▸ Create ▸ PixelShoot ▸ Streak Config</b>. Either drop it in a
    /// <b>Resources</b> folder named <c>StreakConfig</c> (auto-loaded), or assign it on AppBootstrap.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StreakConfig", menuName = "PixelShoot/Streak Config")]
    public class StreakConfig : ScriptableObject
    {
        [Tooltip("Number of streak steps = fill-bar length. Rewards + bar cap here (streak keeps counting).")]
        [Min(1)] public int maxRewardStreak = 5;

        [Tooltip("Bombs dropped at start of level, per streak step. Index 0 = no streak. Needs maxRewardStreak + 1 entries.")]
        public int[] bombsByStreak = { 0, 3, 6, 9, 12, 15 };

        [Tooltip("Free painted pixels, per streak step. Index 0 = no streak. Needs maxRewardStreak + 1 entries.")]
        public int[] paintsByStreak = { 0, 5, 10, 15, 20, 25 };

        public int MaxRewardStreak => Mathf.Max(1, maxRewardStreak);
        public int Bombs(int step)  => Read(bombsByStreak, step);
        public int Paints(int step) => Read(paintsByStreak, step);

        private static int Read(int[] arr, int step)
        {
            if (arr == null || arr.Length == 0) return 0;
            return arr[Mathf.Clamp(step, 0, arr.Length - 1)];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxRewardStreak < 1) maxRewardStreak = 1;
            int need = maxRewardStreak + 1;
            if (bombsByStreak != null && bombsByStreak.Length != need)
                Debug.LogWarning($"[StreakConfig] bombsByStreak has {bombsByStreak.Length} entries but needs {need} (maxRewardStreak + 1).", this);
            if (paintsByStreak != null && paintsByStreak.Length != need)
                Debug.LogWarning($"[StreakConfig] paintsByStreak has {paintsByStreak.Length} entries but needs {need} (maxRewardStreak + 1).", this);
        }
#endif
    }
}
