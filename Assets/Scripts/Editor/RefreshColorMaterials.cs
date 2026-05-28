#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-shot utility to repaint all per-color ColorData materials back to the
    /// vivid display color. (Frontier/unhit boxes now use a single shared material
    /// on GridController, so they are not touched here.)
    /// </summary>
    public static class RefreshColorMaterials
    {
        [MenuItem("PixelShoot/Refresh Color Materials")]
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

                if (cd.BoxHitMaterial != null) SetMaterialColor(cd.BoxHitMaterial, baseColor);
                if (cd.ShooterMaterial != null) SetMaterialColor(cd.ShooterMaterial, baseColor);
                if (cd.BulletMaterial != null) SetMaterialColor(cd.BulletMaterial, baseColor);
                updated++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Refreshed {updated} ColorData entries: per-color materials → vivid base color.");
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }
    }
}
#endif
