#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Obi ships its sample/rope materials on the Built-in "Standard" shader, which renders
    /// magenta under URP. This converts every Standard material under Assets/Obi to URP/Lit,
    /// carrying over the base colour, main texture, metallic and smoothness.
    /// </summary>
    public static class ObiUrpMaterialFixer
    {
        [MenuItem("Generator/Fix Obi Materials (URP)")]
        public static void Fix()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[ObiUrpMaterialFixer] 'Universal Render Pipeline/Lit' not found — is URP installed?");
                return;
            }

            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Obi" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                string sn = mat.shader.name;
                if (sn != "Standard" && sn != "Standard (Specular setup)") continue;

                // Preserve the useful Standard properties.
                Color col     = mat.HasProperty("_Color")      ? mat.GetColor("_Color")       : Color.white;
                Texture tex   = mat.HasProperty("_MainTex")    ? mat.GetTexture("_MainTex")   : null;
                Vector2 scale = mat.HasProperty("_MainTex")    ? mat.GetTextureScale("_MainTex")  : Vector2.one;
                Vector2 off   = mat.HasProperty("_MainTex")    ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                float metallic = mat.HasProperty("_Metallic")   ? mat.GetFloat("_Metallic")   : 0f;
                float smooth   = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

                mat.shader = urpLit;

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_BaseMap") && tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTextureScale("_BaseMap", scale);
                    mat.SetTextureOffset("_BaseMap", off);
                }
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);

                EditorUtility.SetDirty(mat);
                converted++;
                Debug.Log($"[ObiUrpMaterialFixer] Converted → URP/Lit: {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ObiUrpMaterialFixer] Done. Converted {converted} Standard material(s) under Assets/Obi.");
        }
    }
}
#endif
