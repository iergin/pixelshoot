using System.ComponentModel;
using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Game;
using PixelShoot.UI;

/// <summary>
/// PixelShoot extensions to SRDebugger's runtime options panel. The class is
/// declared <c>partial</c> in StompyRobot's package; this file just adds
/// PixelShoot-specific categories: Game (force win / lose) and Wallet
/// (live balance + add-coins button).
/// </summary>
public partial class SROptions
{
    // ── Game ────────────────────────────────────────────────────────────
    [Category("PixelShoot — Game")]
    [DisplayName("Force Win")]
    public void DebugForceWin()
    {
        var gc = FindGameController();
        if (gc == null) { LogNoGameController(); return; }
        gc.DebugForceWin();
    }

    [Category("PixelShoot — Game")]
    [DisplayName("Force Lose")]
    public void DebugForceLose()
    {
        var gc = FindGameController();
        if (gc == null) { LogNoGameController(); return; }
        gc.DebugForceFail();
    }

    // ── Powerups (debug grant to test the PlayPopup selector) ───────────
    [Category("PixelShoot — Powerups")]
    [DisplayName("Owned Bomb")]
    public int PowerupBombOwned => PlayerPowerups.Owned(PowerupType.Bomb);

    [Category("PixelShoot — Powerups")]
    [DisplayName("Owned Paint")]
    public int PowerupPaintOwned => PlayerPowerups.Owned(PowerupType.Paint);

    [Category("PixelShoot — Powerups")]
    [DisplayName("Add 5 Bomb Powerups")]
    public void DebugAddBombPowerups() => PlayerPowerups.Add(PowerupType.Bomb, 5);

    [Category("PixelShoot — Powerups")]
    [DisplayName("Add 5 Paint Powerups")]
    public void DebugAddPaintPowerups() => PlayerPowerups.Add(PowerupType.Paint, 5);

    // ── Wallet ──────────────────────────────────────────────────────────
    private int _addCoinAmount = 100;

    [Category("PixelShoot — Wallet"), Sort(0)]
    [DisplayName("Balance")]
    public int CoinBalance => PlayerWallet.Balance;

    [Category("PixelShoot — Wallet"), Sort(1)]
    [DisplayName("Amount to add")]
    [Increment(50)]
    public int AddCoinAmount
    {
        get => _addCoinAmount;
        set
        {
            _addCoinAmount = value;
            OnPropertyChanged(nameof(AddCoinAmount));
        }
    }

    [Category("PixelShoot — Wallet"), Sort(2)]
    [DisplayName("Add coins")]
    public void DebugAddCoins()
    {
        PlayerWallet.Add(_addCoinAmount);
        Debug.Log($"[SRDebug] +{_addCoinAmount} coins. Balance now {PlayerWallet.Balance}.");
        OnPropertyChanged(nameof(CoinBalance));
    }

    [Category("PixelShoot — Wallet"), Sort(3)]
    [DisplayName("Reset to 1000")]
    public void DebugResetWallet()
    {
        PlayerWallet.Reset(1000);
        Debug.Log("[SRDebug] Wallet reset to 1000.");
        OnPropertyChanged(nameof(CoinBalance));
    }

    // ── Lives ───────────────────────────────────────────────────────────
    [Category("PixelShoot — Lives"), Sort(0)]
    [DisplayName("Lives")]
    public int LivesCount => PlayerLives.Lives;

    [Category("PixelShoot — Lives"), Sort(1)]
    [DisplayName("Add life")]
    public void DebugAddLife()
    {
        PlayerLives.AddLives(1);
        Debug.Log($"[SRDebug] +1 life. Lives now {PlayerLives.Lives}.");
        OnPropertyChanged(nameof(LivesCount));
    }

    [Category("PixelShoot — Lives"), Sort(2)]
    [DisplayName("Refill (full)")]
    public void DebugRefillLives()
    {
        PlayerLives.Refill();
        Debug.Log($"[SRDebug] Lives refilled to {PlayerLives.Lives}.");
        OnPropertyChanged(nameof(LivesCount));
    }

    [Category("PixelShoot — Lives"), Sort(3)]
    [DisplayName("Reset to 0")]
    public void DebugZeroLives()
    {
        PlayerLives.SetLives(0);
        Debug.Log("[SRDebug] Lives reset to 0 (regen timer restarted).");
        OnPropertyChanged(nameof(LivesCount));
    }

    // ── Boosters ────────────────────────────────────────────────────────
    [Category("PixelShoot — Boosters")]
    [DisplayName("+5 each booster")]
    public void DebugAddBoosters()
    {
        foreach (var id in new[] { "conveyor_capacity", "booster_2", "booster_3", "booster_4" })
            PlayerBoosters.Add(id, 5);
        Debug.Log("[SRDebug] +5 of every booster.");
    }

    [Category("PixelShoot — Boosters")]
    [DisplayName("Clear all boosters")]
    public void DebugClearBoosters()
    {
        foreach (var id in new[] { "conveyor_capacity", "booster_2", "booster_3", "booster_4" })
            PlayerBoosters.TryConsume(id, PlayerBoosters.Count(id));
        Debug.Log("[SRDebug] Boosters cleared.");
    }

    // ── Tutorials ───────────────────────────────────────────────────────
    [Category("PixelShoot — Tutorials")]
    [DisplayName("Reset special item tutorials")]
    public void DebugResetSpecialItemTutorials()
    {
        SpecialItemTutorialState.ResetAll();
        Debug.Log("[SRDebug] Special item tutorials reset — they'll show again next level.");
    }

    // ── Ads ─────────────────────────────────────────────────────────────
    [Category("PixelShoot — Ads")]
    [DisplayName("Show interstitial")]
    public void DebugShowInterstitial()
    {
        if (AdsManager.Service == null) { Debug.LogWarning("[SRDebug] AdsManager not ready."); return; }
        AdsManager.Service.ShowInterstitial(null);
    }

    [Category("PixelShoot — Ads")]
    [DisplayName("Show rewarded")]
    public void DebugShowRewarded()
    {
        if (AdsManager.Service == null) { Debug.LogWarning("[SRDebug] AdsManager not ready."); return; }
        AdsManager.Service.ShowRewarded(
            onRewarded: () => Debug.Log("[SRDebug] Rewarded callback fired."),
            onClosed: null);
    }

    [Category("PixelShoot — Ads")]
    [DisplayName("Show banner (bottom)")]
    public void DebugShowBannerBottom()
    {
        if (AdsManager.Service == null) { Debug.LogWarning("[SRDebug] AdsManager not ready."); return; }
        AdsManager.Service.ShowBanner(BannerPosition.Bottom);
    }

    [Category("PixelShoot — Ads")]
    [DisplayName("Show banner (top)")]
    public void DebugShowBannerTop()
    {
        if (AdsManager.Service == null) { Debug.LogWarning("[SRDebug] AdsManager not ready."); return; }
        AdsManager.Service.ShowBanner(BannerPosition.Top);
    }

    [Category("PixelShoot — Ads")]
    [DisplayName("Hide banner")]
    public void DebugHideBanner()
    {
        if (AdsManager.Service == null) { Debug.LogWarning("[SRDebug] AdsManager not ready."); return; }
        AdsManager.Service.HideBanner();
    }

    // ── Helpers ─────────────────────────────────────────────────────────
    private static GameController FindGameController()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<GameController>();
#endif
    }

    private static void LogNoGameController()
    {
        Debug.LogWarning("[SRDebug] No GameController in scene — option ignored.");
    }
}
