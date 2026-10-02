#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// One-click: adds a looping RAINBOW sparkle ParticleSystem child to the Box prefab. Unlike the
    /// key particle it just runs for the box's lifetime (playOnAwake, loop) — no code control. Each
    /// particle picks a random colour along a full rainbow gradient. Re-running updates the child.
    /// </summary>
    public static class BoxParticleCreator
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Box.prefab";
        private const string ChildName  = "BoxParticle";

        [MenuItem("PixelShoot/Box/Add Rainbow Particle to Box Prefab")]
        public static void AddBoxParticle()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError($"[BoxParticle] Couldn't load prefab at {PrefabPath}."); return; }

            try
            {
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

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[BoxParticle] Rainbow particle added/updated on '{PrefabPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Configure(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;                // always on for the box's lifetime
            main.duration = 1f;
            main.startLifetime = 1.0f;
            main.startSpeed = 0.3f;
            main.startSize = 0.1f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;
            main.scalingMode = ParticleSystemScalingMode.Local;

            // Rainbow: each particle picks a random colour along a full-hue gradient.
            var rainbow = new Gradient();
            rainbow.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red,                       0.00f),
                    new GradientColorKey(new Color(1f, 0.5f, 0f),         0.17f), // orange
                    new GradientColorKey(Color.yellow,                    0.33f),
                    new GradientColorKey(Color.green,                     0.50f),
                    new GradientColorKey(Color.cyan,                      0.66f),
                    new GradientColorKey(Color.blue,                      0.83f),
                    new GradientColorKey(new Color(0.6f, 0f, 1f),         1.00f), // violet
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(rainbow)
            {
                mode = ParticleSystemGradientMode.RandomColor
            };

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Fade alpha out over life.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);

            // Shrink over life.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

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
