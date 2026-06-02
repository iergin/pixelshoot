using System.ComponentModel;
using UnityEngine;
using PixelShoot.Ads;
using PixelShoot.Game;

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
