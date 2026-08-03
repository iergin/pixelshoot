using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PixelShoot.Audio;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>Where a reward icon flies to: the coin HUD, the Play button, or the life HUD.</summary>
    public enum RewardFlyKind { Coin, PlayButton, Life }

    /// <summary>
    /// Menu-only locator + choreographer for the reward-claim fly animation. Lives on the MainMenu; its
    /// <see cref="Instance"/> is how <see cref="RewardFlow"/> and <see cref="RewardClaimPopup"/> decide
    /// "menu (fly) vs in-game (no fly)" — the Game scene simply has no RewardFlyTargets, so Instance is
    /// null there and nothing flies.
    ///
    /// <para>Holds the HUD anchors (coin icon, Play button, life widget), a top <see cref="flyLayer"/>
    /// canvas for the flying sprites, and the <see cref="CoinLabel"/> / <see cref="LivesHud"/> it
    /// freezes during a claim so their numbers only update when the flying coins/life land.</para>
    /// </summary>
    public class RewardFlyTargets : MonoBehaviour
    {
        public static RewardFlyTargets Instance { get; private set; }

        [Header("HUD widgets to freeze + update")]
        [Tooltip("The menu coin counter. Frozen during a claim, then counts up when the coins land.")]
        [SerializeField] private CoinLabel coinLabel;
        [Tooltip("The menu life widget. Frozen during a claim, then refreshed when the life lands.")]
        [SerializeField] private LivesHud livesHud;

        [Header("Menu navigation")]
        [Tooltip("Optional bottom NavigationBar. Before the claim popup shows, we switch it to Home so " +
                 "the reward flies to the Home page's HUD / Play button (the shop may be a different page).")]
        [SerializeField] private NavigationBar navBar;
        [Tooltip("Home tab index on the NavigationBar.")]
        [SerializeField] private int homeTabIndex = 1;

        [Header("Fly destinations (RectTransforms)")]
        [Tooltip("Coins fly here (the coin icon in the HUD).")]
        [SerializeField] private RectTransform coinTarget;
        [Tooltip("Boosters / powerups / No-Ads fly here (the Play button). No count-up text.")]
        [SerializeField] private RectTransform playButtonTarget;
        [Tooltip("Lives fly here (the life widget).")]
        [SerializeField] private RectTransform lifeTarget;

        [Header("Fly layer")]
        [Tooltip("Full-screen RectTransform on a HIGH-sorting-order canvas that hosts the flying sprites " +
                 "so they draw above the popup and the HUD.")]
        [SerializeField] private RectTransform flyLayer;
        [Tooltip("Camera for the flyLayer's canvas. Leave EMPTY for a Screen Space - Overlay canvas.")]
        [SerializeField] private Camera uiCamera;
        [Tooltip("Prefab for ONE flying icon (styled: glow / trail / shadow / spin via RewardFlyItem). " +
                 "If empty, a plain Image is built at runtime and sized by Fly Icon Size.")]
        [SerializeField] private RewardFlyItem flyItemPrefab;
        [Tooltip("Pixel size of a runtime-built flying icon (used only when Fly Item Prefab is empty).")]
        [SerializeField] private Vector2 flyIconSize = new Vector2(90f, 90f);

        [Header("Coins")]
        [Tooltip("How many coin sprites burst out and fly to the coin HUD.")]
        [SerializeField, Min(1)] private int coinBurstCount = 10;
        [Tooltip("Seconds each coin takes to reach the coin HUD.")]
        [SerializeField, Min(0.05f)] private float coinFlyDuration = 0.55f;
        [Tooltip("Stagger between consecutive coins leaving.")]
        [SerializeField, Min(0f)] private float coinStagger = 0.05f;
        [Tooltip("Random scatter radius (px) coins pop to before homing in on the target.")]
        [SerializeField, Min(0f)] private float coinScatter = 120f;
        [Tooltip("Seconds the coin counter takes to count up once the coins land.")]
        [SerializeField, Min(0f)] private float coinCountUpDuration = 0.4f;
        [Tooltip("Uniform localScale a coin reaches as it lands on the coin HUD (1 = the flyer's native size).")]
        [SerializeField, Min(0f)] private float coinEndScale = 1f;

        [Header("Items (booster / powerup / no-ads → Play button)")]
        [SerializeField, Min(0.05f)] private float itemFlyDuration = 0.5f;
        [SerializeField, Min(0f)] private float itemStagger = 0.08f;
        [Tooltip("Uniform localScale an item reaches as it lands on the Play button (1 = the flyer's native size).")]
        [SerializeField, Min(0f)] private float itemEndScale = 1f;

        [Header("Life")]
        [SerializeField, Min(0.05f)] private float lifeFlyDuration = 0.55f;
        [Tooltip("Uniform localScale the life icon reaches as it lands on the life widget (1 = the flyer's native size).")]
        [SerializeField, Min(0f)] private float lifeEndScale = 1f;

        [Header("Feedback (optional)")]
        [Tooltip("Spawned at a fly icon's landing spot (coin sparkle / pop). Optional.")]
        [SerializeField] private GameObject landVfxPrefab;
        [Tooltip("Scale punch applied to the target when an icon lands.")]
        [SerializeField] private float landPunch = 0.25f;
        [Tooltip("Played (via AudioManager) when a coin lands.")]
        [SerializeField] private AudioClip coinLandSfx;
        [Tooltip("Played (via AudioManager) when a booster / powerup / no-ads / life lands.")]
        [SerializeField] private AudioClip itemLandSfx;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [Tooltip("Minimum seconds between land SFX (so a coin burst doesn't machine-gun).")]
        [SerializeField, Min(0f)] private float sfxThrottle = 0.05f;

        private bool holding;
        private float lastSfxTime;
        private readonly Dictionary<RectTransform, Vector3> baseScales = new Dictionary<RectTransform, Vector3>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogWarning($"[RewardFlyTargets] Duplicate on '{name}'."); }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── HUD freeze / release ─────────────────────────────────────────────
        /// <summary>Switch the bottom nav to the Home tab (as if the player tapped Home), so the reward
        /// flies to Home's HUD / Play button. Called right before the claim popup opens.</summary>
        public void FocusHome()
        {
            if (navBar != null) navBar.Select(homeTabIndex);
        }

        /// <summary>Freeze the coin + life HUD at their current values (called just before the grant).</summary>
        public void BeginHold()
        {
            holding = true;
            coinLabel?.BeginClaimHold();
            livesHud?.BeginHold();
        }

        /// <summary>Release the HUD to the real (already-granted) values instantly — the safety net for
        /// a popup dismissed without the fly ever running.</summary>
        public void EndHoldImmediate()
        {
            if (!holding) return;
            holding = false;
            coinLabel?.EndClaimImmediate();
            livesHud?.ReleaseHold();
        }

        // ── The fly choreography ─────────────────────────────────────────────
        /// <summary>Fly the given reward icons from their popup positions to the HUD, in the order
        /// coins → Play-button items → life, updating the frozen HUD numbers as they land. Calls
        /// <paramref name="onComplete"/> when everything has settled.</summary>
        public void Fly(List<FlyRequest> requests, Action onComplete)
        {
            if (flyLayer == null || requests == null || requests.Count == 0)
            {
                EndHoldImmediate();      // nothing to animate → just reveal the granted values
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(FlyRoutine(requests, onComplete));
        }

        private IEnumerator FlyRoutine(List<FlyRequest> requests, Action onComplete)
        {
            // 1) SPAWN EVERYTHING AT ONCE — the instant the popup closes, all reward icons appear at
            //    their popup-row positions (coins as a stacked burst). They just sit there until flown.
            var coinIcons = new List<RectTransform>();
            var itemIcons = new List<RectTransform>();
            var lifeIcons = new List<RectTransform>();

            foreach (var r in requests)
            {
                Vector2 start = LocalOfWorld(r.startWorld);
                switch (r.kind)
                {
                    case RewardFlyKind.Coin:
                        for (int i = 0; i < coinBurstCount; i++) coinIcons.Add(SpawnIcon(r.sprite, start));
                        break;
                    case RewardFlyKind.PlayButton:
                        itemIcons.Add(SpawnIcon(r.sprite, start));
                        break;
                    case RewardFlyKind.Life:
                        lifeIcons.Add(SpawnIcon(r.sprite, start));
                        break;
                }
            }

            bool hasCoins = coinIcons.Count > 0;
            bool hasLife = lifeIcons.Count > 0;

            // 2) FLY THEM OUT, one group at a time: coins → Play-button items → life.
            if (hasCoins) yield return StartCoroutine(FlyCoins(coinIcons));

            foreach (var icon in itemIcons)
            {
                StartCoroutine(FlyOne(icon, playButtonTarget, itemFlyDuration, RewardFlyKind.PlayButton, itemEndScale));
                yield return WaitUnscaled(itemStagger);
            }

            foreach (var icon in lifeIcons)
                yield return StartCoroutine(FlyOne(icon, lifeTarget, lifeFlyDuration, RewardFlyKind.Life, lifeEndScale));

            // Release anything that had no reward of its kind (so nothing stays frozen).
            if (!hasCoins) coinLabel?.EndClaimImmediate();
            if (!hasLife)  livesHud?.ReleaseHold();
            holding = false;
            onComplete?.Invoke();
        }

        // Fly a batch of already-spawned coin icons to the coin HUD (scatter → home in), then count up.
        private IEnumerator FlyCoins(List<RectTransform> icons)
        {
            Vector2 dest = LocalOf(coinTarget);
            int total = icons.Count;
            int landed = 0;

            foreach (var icon in icons)
            {
                Vector2 start = icon.anchoredPosition;
                Vector2 scatter = start + UnityEngine.Random.insideUnitCircle * coinScatter;

                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Append(icon.DOAnchorPos(scatter, coinFlyDuration * 0.35f).SetEase(Ease.OutQuad));
                seq.Append(icon.DOAnchorPos(dest, coinFlyDuration * 0.65f).SetEase(Ease.InBack));
                seq.Insert(0f, icon.DOScale(icon.localScale * coinEndScale, coinFlyDuration).SetEase(Ease.InQuad)); // reach the target scale as it lands
                seq.OnComplete(() =>
                {
                    OnLand(coinTarget, coinLandSfx);
                    icon.GetComponent<RewardFlyItem>()?.OnLanded();
                    Destroy(icon.gameObject);
                    landed++;
                    if (landed >= total)
                        coinLabel?.ReleaseClaimTo(PlayerWallet.Balance, coinCountUpDuration);
                });

                if (coinStagger > 0f) yield return WaitUnscaled(coinStagger);
            }

            // Wait out the last coin's flight + the count-up so callers sequence cleanly.
            yield return WaitUnscaled(coinFlyDuration + coinCountUpDuration);
        }

        // Fly a single already-spawned icon to its target.
        private IEnumerator FlyOne(RectTransform icon, RectTransform to, float dur, RewardFlyKind kind, float endScale)
        {
            if (icon == null) yield break;
            if (to == null) { Destroy(icon.gameObject); yield break; }

            bool done = false;
            icon.DOScale(icon.localScale * endScale, dur).SetEase(Ease.InQuad).SetUpdate(true); // reach the target scale as it lands
            icon.DOAnchorPos(LocalOf(to), dur).SetEase(Ease.InBack).SetUpdate(true)
                .OnComplete(() =>
                {
                    OnLand(to, kind == RewardFlyKind.Coin ? coinLandSfx : itemLandSfx);
                    if (kind == RewardFlyKind.Life) livesHud?.ReleaseHold();
                    icon.GetComponent<RewardFlyItem>()?.OnLanded();
                    Destroy(icon.gameObject);
                    done = true;
                });
            while (!done) yield return null;
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private RectTransform SpawnIcon(Sprite sprite, Vector2 localPos)
        {
            RectTransform rt;
            if (flyItemPrefab != null)
            {
                var item = Instantiate(flyItemPrefab, flyLayer);
                item.SetSprite(sprite);
                rt = (RectTransform)item.transform;
            }
            else
            {
                var go = new GameObject("FlyIcon", typeof(RectTransform), typeof(Image));
                rt = (RectTransform)go.transform;
                rt.SetParent(flyLayer, false);
                rt.sizeDelta = flyIconSize;
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
            }
            rt.anchoredPosition = localPos;
            return rt;
        }

        // Convert a target RectTransform's centre into the flyLayer's local space (robust across
        // Overlay / Camera canvases via a screen-point round-trip).
        private Vector2 LocalOf(RectTransform rt) => rt == null ? Vector2.zero : LocalOfWorld(rt.position);

        // Same, from a raw world position — used for the fly ORIGINS, captured synchronously when the
        // player presses Continue (the popup rows are destroyed by the time later phases run).
        private Vector2 LocalOfWorld(Vector3 world)
        {
            if (flyLayer == null) return Vector2.zero;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(flyLayer, screen, uiCamera, out var local);
            return local;
        }

        private void OnLand(RectTransform target, AudioClip clip)
        {
            if (target != null)
            {
                Punch(target);
                if (landVfxPrefab != null) Instantiate(landVfxPrefab, target.position, Quaternion.identity, flyLayer);
            }
            PlaySfx(clip);
        }

        // A punch that ALWAYS ends back at the target's original scale — overlapping punches (a coin
        // burst hits the same target 10×) otherwise stack and leave it stuck large. We remember the
        // original scale once (before any punch), then reset + restore around each punch.
        private void Punch(RectTransform target)
        {
            if (target == null || landPunch <= 0f) return;
            if (!baseScales.TryGetValue(target, out var baseScale))
            {
                baseScale = target.localScale; // captured before the first punch → the true original
                baseScales[target] = baseScale;
            }
            target.DOKill();                 // drop any in-flight punch on this target
            target.localScale = baseScale;   // start clean
            target.DOPunchScale(Vector3.one * landPunch, 0.25f, 6, 0.6f)
                  .SetUpdate(true)
                  .OnComplete(() => target.localScale = baseScale); // guarantee the exact original
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || AudioManager.Instance == null) return;
            float now = Time.unscaledTime;
            if (now - lastSfxTime < sfxThrottle) return; // stops a coin burst from machine-gunning
            lastSfxTime = now;
            AudioManager.Instance.PlaySfx(clip, sfxVolume); // manager gates on SfxEnabled + pools sources
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds <= 0f) yield break;
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>One icon to fly, produced by the popup from a reward row. <see cref="startWorld"/> is
        /// the row icon's world position, captured synchronously on Continue (the row is destroyed by
        /// the time the later fly phases run, so a live RectTransform reference wouldn't survive).</summary>
        public struct FlyRequest
        {
            public RewardFlyKind kind;
            public Sprite sprite;
            public Vector3 startWorld;
        }
    }
}
