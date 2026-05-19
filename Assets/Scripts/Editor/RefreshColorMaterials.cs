#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-shot utility to repaint all ColorData materials to match the current
    /// unhit-vivid / hit-faded convention. Run after changing the faded recipe.
    /// </summary>
    public static class RefreshColorMaterials
    {
        [MenuItem("PixelShoot/Refresh Color Materials (unhit=vivid, hit=faded)")]
        public static void Refresh()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ColorData));
            int updated = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cd = AssetDatabase.LoadAssetAtPath<ColorData>(path);
                if (cd == null) continue;

                Color baseColor = cd.DisplayColor;
                Color faded = FadedVariant(baseColor);

                if (cd.BoxUnhitMaterial != null) SetMaterialColor(cd.BoxUnhitMaterial, baseColor);
                if (cd.BoxHitMaterial != null) SetMaterialColor(cd.BoxHitMaterial, faded);
                if (cd.ShooterMaterial != null) SetMaterialColor(cd.ShooterMaterial, baseColor);
                if (cd.BulletMaterial != null) SetMaterialColor(cd.BulletMaterial, baseColor);
                updated++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Refreshed {updated} ColorData entries: unhit material → vivid base color, hit material → faded.");
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }

        private static Color FadedVariant(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s *= 0.25f;
            v = Mathf.Lerp(v, 0.85f, 0.5f);
            var faded = Color.HSVToRGB(h, s, v);
            faded.a = c.a;
            return faded;
        }
    }
}
#endif
