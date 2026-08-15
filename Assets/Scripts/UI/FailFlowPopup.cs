using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PixelShoot.Ads;
using PixelShoot.Data;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// The chained fail / quit acknowledgement popup. ONE popup with a separate child GameObject per
    /// step — you build each step's icon / message / art yourself in the inspector, and this script
    /// just activates the current step and hides the others as the player taps X to advance. The green
    /// button (shared, always visible) offers to keep playing throughout.
    ///
    /// <para><b>Steps</b> (X advances, green keeps playing):</para>
    /// <list type="number">
    /// <item><b>Continue</b> (Fail mode only) — "move the buses aside and keep going"; green = PlayOn
    /// (pay <see cref="GameController.ReviveCost"/>; can't afford → Shop opens on top).</item>
    /// <item><b>Streak warning</b> (only if streak &gt; 0) — "you'll lose your N streak".</item>
    /// <item><b>Life warning</b> — "you'll lose a life".</item>
    /// </list>
    /// Past the last step the streak is reset (declined to continue) and the <see cref="PlayPopup"/>
    /// opens in its in-game retry mode. In <b>Quit</b> mode the Continue step is skipped and the green
    /// button simply resumes play.
    /// </summary>
    public class FailFlowPopup : BasePopup
    {
        public enum Mode { Fail, Quit }
        private enum Step { Continue, StreakWarn, LifeWarn }

        [Header("Step objects (build each one's icon / text yourself)")]
        [Tooltip("Shown for step A — Continue (Fail mode only).")]
        [SerializeField] private GameObject continueStep;
        [Tooltip("Shown for step B — Streak warning (only when streak > 0).")]
        [SerializeField] private GameObject streakStep;
        [Tooltip("Shown for step C — Life warning.")]
        [SerializeField] private GameObject lifeStep;
        [Tooltip("Optional label INSIDE the streak step that shows the streak number. {0} = streak.")]
        [SerializeField] private TMP_Text streakCountLabel;
        [SerializeField] private string streakFormat = "{0}";
        [Header("Streak fill bar (inside the streak step)")]
        [Tooltip("Filled (Horizontal) Image. Fills to current streak, then drains to 0 to show the loss.")]
        [SerializeField] private Image streakFillBar;
        [Tooltip("Seconds to hold the full bar before it drains to 0.")]
        [SerializeField, Min(0f)] private float streakDrainDelay = 0.6f;
        [Tooltip("Seconds the drain-to-0 takes.")]
        [SerializeField, Min(0f)] private float streakDrainDuration = 0.5f;
        [SerializeField] private Ease streakDrainEase = Ease.InQuad;

        [Header("Shared controls")]
        [Tooltip("The X button — ADVANCES the chain (does NOT close). Do NOT also add it to Close Buttons.")]
        [SerializeField] private Button advanceButton;
        [Tooltip("The SINGLE shared green button (not inside any step). Its label objects + cost live on it.")]
        [SerializeField] private Button greenButton;
        [Tooltip("Label object shown in FAIL mode (e.g. the 'Play On' graphic). Hidden in Quit mode.")]
        [SerializeField] private GameObject continueLabelObject;
        [Tooltip("Label object shown in QUIT mode (e.g. the 'Keep Playing' graphic). Hidden in Fail mode.")]
        [SerializeField] private GameObject resumeLabelObject;
        [Tooltip("Coin-cost readout child of the green button (shown in Fail mode only). {0} = cost.")]
        [SerializeField] private GameObject costRoot;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private string costFormat = "x{0}";
        [Tooltip("Current coin balance readout (shown in Fail/Play On mode). Updates live when coins " +
                 "change (e.g. after buying from the shop). {0} = balance.")]
        [SerializeField] private TMP_Text balanceLabel;
        [SerializeField] private string balanceFormat = "{0}";

        [Header("Level reward")]
        [Tooltip("Coins config (SO asset) — used to read this level's win reward.")]
        [SerializeField] private CoinsConfig coinsConfig;
        [Tooltip("Shows how many coins completing the level grants. {0} = reward.")]
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private string rewardFormat = "x{0}";

        private Mode mode = Mode.Fail;
        private readonly List<Step> steps = new List<Step>();
        private int index;
        private Tween streakTween;

        /// <summary>Set BEFORE the popup opens (via the Create configure callback).</summary>
        public void SetMode(Mode m) => mode = m;

        protected override void OnInit()
        {
            if (advanceButton != null) { advanceButton.onClick.RemoveAllListeners(); advanceButton.onClick.AddListener(Advance); }
            if (greenButton   != null) { greenButton.onClick.RemoveAllListeners();   greenButton.onClick.AddListener(OnGreen); }

            // Build the step list for this mode (Continue only on Fail; Streak only if there's one).
            steps.Clear();
            if (mode == Mode.Fail) steps.Add(Step.Continue);
            if (PlayerStreak.Current > 0) steps.Add(Step.StreakWarn);
            steps.Add(Step.LifeWarn);

            // Green button content: Fail = "Play On" label + coin cost, Quit = "Keep Playing" label.
            bool isFail = mode == Mode.Fail;
            if (continueLabelObject != null) continueLabelObject.SetActive(isFail);
            if (resumeLabelObject != null)   resumeLabelObject.SetActive(!isFail);
            if (costRoot != null) costRoot.SetActive(isFail);
            if (isFail && costLabel != null)
            {
                int cost = GameController.Instance != null ? GameController.Instance.ReviveCost : 0;
                costLabel.text = string.Format(costFormat, cost);
            }

            // Current coin balance (Play On mode) — live so buying coins in the shop updates it.
            if (balanceLabel != null)
            {
                balanceLabel.transform.parent.gameObject.SetActive(isFail);
                if (isFail)
                {
                    RefreshBalance(PlayerWallet.Balance);
                    PlayerWallet.OnBalanceChanged += RefreshBalance;
                }
            }

            // Reward this level grants on completion (motivation to continue / retry).
            if (rewardLabel != null && coinsConfig != null)
                rewardLabel.text = string.Format(rewardFormat, coinsConfig.LevelWinReward);

            index = 0;
            ShowStep();
        }

        private void ShowStep()
        {
            streakTween?.Kill();
            // Hide all, then activate the current step's object.
            if (continueStep != null) continueStep.SetActive(false);
            if (streakStep != null)   streakStep.SetActive(false);
            if (lifeStep != null)     lifeStep.SetActive(false);
            if (index < 0 || index >= steps.Count) return;

            switch (steps[index])
            {
                case Step.Continue:
                    if (continueStep != null) continueStep.SetActive(true);
                    break;
                case Step.StreakWarn:
                    if (streakCountLabel != null) streakCountLabel.text = string.Format(streakFormat, PlayerStreak.Current);
                    if (streakStep != null) streakStep.SetActive(true);
                    PlayStreakDrain();
                    break;
                case Step.LifeWarn:
                    if (lifeStep != null) lifeStep.SetActive(true);
                    break;
            }
        }

        // Fill to the current streak, hold, then drain to 0 to show the streak being lost.
        private void PlayStreakDrain()
        {
            if (streakFillBar == null) return;
            streakTween?.Kill();
            int max = PlayerStreak.MaxRewardStreak;
            streakFillBar.fillAmount = max > 0 ? Mathf.Clamp01((float)PlayerStreak.Current / max) : 0f;
            streakTween = DOTween.To(() => streakFillBar.fillAmount, x => streakFillBar.fillAmount = x, 0f, streakDrainDuration)
                .SetEase(streakDrainEase)
                .SetDelay(streakDrainDelay)
                .SetUpdate(true); // unscaled — the game is frozen while the popup is up
        }

        // X → next step; past the last step → abandon (reset streak) and go to the TryAgain popup.
        private void Advance()
        {
            index++;
            if (index < steps.Count) ShowStep();
            else Finish();
        }

        // Green → Fail: PlayOn (pay coins, keep playing); can't afford → Shop stacked on top.
        //         Quit: just resume (cancel the quit).
        private void OnGreen()
        {
            if (mode == Mode.Quit) { Close(); return; } // resume play

            var gc = GameController.Instance;
            if (gc != null && gc.PlayOn()) { Close(); return; } // revived → back to play

            // Couldn't afford the revive → open the shop ON TOP (nested), so they can buy and come back.
            PixelShoot.Analytics.AnalyticsEvents.TrackShopOpen("revive_insufficient_coins");
            CreatePopup<ShopPopup>();
        }

        private void Finish()
        {
            // The attempt truly ends here (player declined to continue / confirmed quit) — fire the
            // fail event now, not at the Fail() moment, so a revived-then-won attempt isn't mis-counted.
            LevelAnalytics.FireFail(mode == Mode.Fail ? "fail" : "quit");

            PlayerStreak.Reset(); // declined to continue → the streak is lost (as warned)

            // Interstitial trigger point: only now, after ALL the X taps (not on fail, which would
            // cover the popups). Fail counts as a level result; quitting doesn't tick the counter.
            if (mode == Mode.Fail) InterstitialController.Instance?.NotifyLevelEnded();

            // Reuse the PlayPopup (unbound = in-game retry mode: Play restarts the level, X → menu).
            if (PopupService.Instance != null) PopupService.Instance.Create<PlayPopup>();
            Close();
        }

        private void RefreshBalance(int balance)
        {
            if (balanceLabel != null) balanceLabel.text = string.Format(balanceFormat, balance);
        }

        protected override void OnDestroy()
        {
            PlayerWallet.OnBalanceChanged -= RefreshBalance;
            streakTween?.Kill();
            base.OnDestroy();
        }
    }
}
