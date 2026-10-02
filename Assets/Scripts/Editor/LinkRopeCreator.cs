#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Obi;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Builds the Obi rope prefab used to connect two linked buses: a rope blueprint, a URP
    /// material, and a LinkRope prefab with an ObiRope + extruded renderer + path smoother and
    /// TWO Static particle attachments (one per end). The runtime code sets each attachment's
    /// target to a bus. Menu: Generator ▸ Create Link Rope Prefab.
    /// </summary>
    public static class LinkRopeCreator
    {
        private const string ObiDir     = "Assets/_Game/Obi";
        private const string BpPath      = ObiDir + "/LinkRopeBlueprint.asset";
        private const string MatPath     = ObiDir + "/M_LinkRope.mat";
        private const string PrefabDir   = "Assets/_Game/Prefabs";
        private const string PrefabPath  = PrefabDir + "/LinkRope.prefab";
        private const string SectionPath = "Assets/Obi/Resources/DefaultRopeSection.asset";

        [MenuItem("Generator/Create Link Rope Prefab")]
        public static void Create()
        {
            EnsureFolder(ObiDir);
            EnsureFolder(PrefabDir);

            // 1) Blueprint — reuse a selected one, an existing asset, or generate a fresh 2-point rope.
            var bp = Selection.activeObject as ObiRopeBlueprint
                     ?? AssetDatabase.LoadAssetAtPath<ObiRopeBlueprint>(BpPath);
            if (bp == null)
            {
                bp = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
                bp.name = "LinkRopeBlueprint";
                bp.resolution = 0.15f;
                bp.thickness = 0.1f;
                bp.pooledParticles = 50;
                AssetDatabase.CreateAsset(bp, BpPath);
                bp.GenerateImmediate();
                foreach (var grp in bp.groups) // persist group sub-assets
                    if (grp != null && !AssetDatabase.Contains(grp))
                        AssetDatabase.AddObjectToAsset(grp, bp);
                EditorUtility.SetDirty(bp);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(bp));
            }
            if (bp.groups == null || bp.groups.Count < 2)
            {
                Debug.LogError("[LinkRopeCreator] Blueprint has <2 particle groups. Open it in the Obi " +
                               "blueprint editor and press Generate, then re-run this menu.");
                return;
            }

            // 2) URP material for the rope.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                // Vertex-colour shader so the rope can show bus A's colour → bus B's colour.
                var shader = Shader.Find("PixelShoot/LinkRopeVertexColor") ?? Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader) { name = "M_LinkRope" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, MatPath);
            }

            // 3) Assemble the rope GameObject.
            var go = new GameObject("LinkRope");
            var rope = go.AddComponent<ObiRope>();
            var soRope = new SerializedObject(rope);
            soRope.FindProperty("m_RopeBlueprint").objectReferenceValue = bp;
            soRope.ApplyModifiedProperties();

            go.AddComponent<ObiPathSmoother>();

            // Extruded renderer: sweeps a section along the rope to build a tube mesh.
            var rend = go.AddComponent<ObiRopeExtrudedRenderer>();
            rend.material = mat;
            rend.section = AssetDatabase.LoadAssetAtPath<ObiRopeSection>(SectionPath);
            rend.thicknessScale = 1f;
            if (rend.section == null)
                Debug.LogWarning("[LinkRopeCreator] DefaultRopeSection not found — assign 'Section' on the ObiRopeExtrudedRenderer manually.");

            AddStaticAttachment(go, rope, bp.groups.First());
            AddStaticAttachment(go, rope, bp.groups.Last());

            // 4) Save as prefab.
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[LinkRopeCreator] Created {PrefabPath} (blueprint {BpPath}, material {MatPath}). " +
                      "Assign it to the LinkRopeController's rope prefab field.");
        }

        private static void AddStaticAttachment(GameObject go, ObiRope rope, ObiParticleGroup group)
        {
            var att = go.AddComponent<ObiParticleAttachment>();
            var so = new SerializedObject(att);
            so.FindProperty("m_Actor").objectReferenceValue = rope;
            so.FindProperty("m_ParticleGroup").objectReferenceValue = group;
            so.FindProperty("m_AttachmentType").enumValueIndex = (int)ObiParticleAttachment.AttachmentType.Static;
            so.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
