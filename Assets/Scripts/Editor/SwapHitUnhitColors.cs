#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-shot utility that literally swaps the color value of every ColorData's
    /// BoxUnhitMaterial and BoxHitMaterial. Use it when the two materials already
    /// exist on disk with the wrong assignment (e.g., faded ended up on Unhit
    /// instead of Hit) — this exchanges their colors in place without recomputing
    /// anything from the palette.
    /// </summary>
    public static class SwapHitUnhitColors
    {
        [MenuItem("PixelShoot/Swap Unhit ↔ Hit material colors")]
        public static void Swap()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ColorData));
            int swapped = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cd = AssetDatabase.LoadAssetAtPath<ColorData>(path);
                if (cd == null) continue;
                var unhit = cd.BoxUnhitMaterial;
                var hit = cd.BoxHitMaterial;
                if (unhit == null || hit == null) continue;

                Color unhitColor = GetMatColor(unhit);
                Color hitColor = GetMatColor(hit);

                SetMatColor(unhit, hitColor);
                SetMatColor(hit, unhitColor);
                swapped++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Swapped Unhit ↔ Hit colors on {swapped} ColorData entries.");
        }

        private static Color GetMatColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return m.color;
        }

        private static void SetMatColor(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
        }
    }
}
#endif
