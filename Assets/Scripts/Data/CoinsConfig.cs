using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// All coin-related tunables live in one ScriptableObject so the designer can
    /// rebalance them without touching code. Wallet is initialised from
    /// InitialBalance on first run, level wins pay LevelWinReward, and Play-On
    /// (revive) deducts ReviveCost.
    /// </summary>
    [CreateAssetMenu(fileName = "CoinsConfig", menuName = "PixelShoot/Coins Config")]
    public class CoinsConfig : ScriptableObject
    {
        [SerializeField, Min(0)] private int initialBalance = 1000;
        [SerializeField, Min(0)] private int levelWinReward = 20;
        [SerializeField, Min(0)] private int reviveCost = 900;

        public int InitialBalance => initialBalance;
        public int LevelWinReward => levelWinReward;
        public int ReviveCost => reviveCost;
    }
}
