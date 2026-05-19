using UnityEngine;

namespace PixelShoot.Data
{
    [CreateAssetMenu(fileName = "ColorData", menuName = "PixelShoot/Color Data")]
    public class ColorData : ScriptableObject
    {
        [SerializeField] private string colorId;
        [SerializeField] private Color displayColor = Color.white;
        [SerializeField] private Material boxUnhitMaterial;
        [SerializeField] private Material boxHitMaterial;
        [SerializeField] private Material shooterMaterial;
        [SerializeField] private Material bulletMaterial;

        public string ColorId => colorId;
        public Color DisplayColor => displayColor;
        public Material BoxUnhitMaterial => boxUnhitMaterial;
        public Material BoxHitMaterial => boxHitMaterial;
        public Material ShooterMaterial => shooterMaterial;
        public Material BulletMaterial => bulletMaterial;
    }
}
