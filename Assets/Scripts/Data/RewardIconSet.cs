using UnityEngine;
using PixelShoot.Game;

namespace PixelShoot.Data
{
    /// <summary>
    /// Shared generic reward icons (coin / life / no-ads / bomb / paint), so the same sprites are
    /// reused wherever a reward is shown — the <see cref="PixelShoot.UI.RewardClaimPopup"/> today and
    /// any future UI. One asset via <b>Create ▸ PixelShoot ▸ Reward Icon Set</b>.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardIconSet", menuName = "PixelShoot/Reward Icon Set")]
    public class RewardIconSet : ScriptableObject
    {
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private Sprite lifeIcon;
        [SerializeField] private Sprite noAdsIcon;
        [SerializeField] private Sprite bombIcon;
        [SerializeField] private Sprite paintIcon;

        public Sprite Coin  => coinIcon;
        public Sprite Life  => lifeIcon;
        public Sprite NoAds => noAdsIcon;
        public Sprite Bomb  => bombIcon;
        public Sprite Paint => paintIcon;

        /// <summary>Icon for a start-of-level powerup (Bomb / Paint).</summary>
        public Sprite Powerup(PowerupType t) => t == PowerupType.Bomb ? bombIcon : paintIcon;
    }
}
