using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Fixed amounts one powerup grants at level start. Read by
    /// <see cref="PixelShoot.UI.StreakGiftController"/> when applying a selected powerup.
    /// Create via <b>Assets ▸ Create ▸ PixelShoot ▸ Powerups Config</b>.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerupsConfig", menuName = "PixelShoot/Powerups Config")]
    public class PowerupsConfig : ScriptableObject
    {
        [Tooltip("Bombs placed at level start when one Bomb powerup is used.")]
        [Min(0)] public int bombsPerPowerup = 3;
        [Tooltip("Free painted pixels at level start when one Paint powerup is used.")]
        [Min(0)] public int paintsPerPowerup = 5;

        public int AmountFor(PowerupType t) => t == PowerupType.Bomb ? bombsPerPowerup : paintsPerPowerup;
    }
}
