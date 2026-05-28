using UnityEngine;

namespace PixelShoot.Data
{
    [CreateAssetMenu(fileName = "ColorData", menuName = "PixelShoot/Color Data")]
    public class ColorData : ScriptableObject
    {
        [SerializeField] private string colorId;
        [SerializeField] private Color displayColor = Color.white;
        // Frontier (unhit) boxes now use a single shared material set on GridController,
        // so ColorData no longer carries a per-color unhit material.
        [SerializeField] private Material boxHitMaterial;
        [SerializeField] private Material shooterMaterial;
        [SerializeField] private Material bulletMaterial;
        [Tooltip("If set, this entry is a tone variant of another ColorData. Shooters match against the main color, so all tones of the same main are destroyed by the same shooter. Leave empty for main colors.")]
        [SerializeField] private ColorData mainColor;

        public string ColorId => colorId;
        public Color DisplayColor => displayColor;
        public Material BoxHitMaterial => boxHitMaterial;
        public Material ShooterMaterial => shooterMaterial;
        public Material BulletMaterial => bulletMaterial;

        /// <summary>
        /// The color a shooter must match to destroy a box of this color. For a "main"
        /// color this is itself; for a tone, this is the parent main color. Defensive
        /// against accidental cycles — walks the chain at most a few steps.
        /// </summary>
        public ColorData GameplayColor
        {
            get
            {
                var c = this;
                for (int i = 0; i < 4 && c.mainColor != null && c.mainColor != c; i++) c = c.mainColor;
                return c;
            }
        }
    }
}
