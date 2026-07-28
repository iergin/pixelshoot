#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PixelShoot.Game;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click: adds a looping sparkle ParticleSystem child to the KeyVisual prefab and wires it to
    /// <c>KeyVisual.idleParticle</c>. The particle loops while the key idles and stops emitting the
    /// moment the key flies to its lock (handled in KeyVisual). Re-running updates the existing child.
    /// </summary>
    public static class KeyParticleCreator
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/KeyVisual.prefab";
        private const string ChildName  = "KeyParticle";

        [MenuItem("PixelShoot/Key/Add Key Particle to KeyVisual Prefab")]
        public static void AddKeyParticle()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError($"[KeyParticle] Couldn't load prefab at {PrefabPath}."); return; }

            try
            {
                // Find or create the child.
                var existing = root.transform.Find(ChildName);
                GameObject go = existing != null ? existing.gameObject : new GameObject(ChildName);
                if (existing == null)
                {
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = Vector3.zero;
                }

                var ps = go.GetComponent<ParticleSystem>();
                if (ps == null) ps = go.AddComponent<ParticleSystem>();
                Configure(ps);

                // Wire it onto KeyVisual.idleParticle.
                var key = root.GetComponent<KeyVisual>();
                if (key != null)
                {
                    var so = new SerializedObject(key);
                    var prop = so.FindProperty("idleParticle");
                    if (prop != null) { prop.objectReferenceValue = ps; so.ApplyModifiedProperties(); }
                }
                else Debug.LogWarning("[KeyParticle] KeyVisual component not found on the prefab root — assign idleParticle by hand.");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[KeyParticle] Sparkle particle added/updated on '{PrefabPath}' and wired to KeyVisual.idleParticle.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Configure(ParticleSystem ps)
        {
            // Stopped at author time; KeyVisual.Init() plays it at runtime.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;               // KeyVisual controls Play/Stop
            main.duration = 1f;
            main.startLifetime = 1.0f;
            main.startSpeed = 0.35f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.85f, 0.25f, 1f); // warm gold
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 30;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            // Fade alpha out over life.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Shrink over life.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            // Renderer material — use the built-in particle material ASSET (persists in the prefab).
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (renderer.sharedMaterial == null)
                {
                    var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
                    if (mat != null) renderer.sharedMaterial = mat;
                }
            }
        }
    }
}
#endif
