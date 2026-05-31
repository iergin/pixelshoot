using System.ComponentModel;
using UnityEngine;
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
