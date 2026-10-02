#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PixelShoot.EditorTools
{
    /// <summary>
    /// Builds a ready-to-use exhaust-smoke ParticleSystem and (if a GameObject is selected)
    /// parents it under it. Uses M_ExhaustSmoke (PixelShoot/ParticleTextured + SmokeTexture).
    /// Tweak freely afterwards — this just gives a good starting point.
    /// </summary>
    public static class ExhaustParticleCreator
    {
        private const string MaterialPath = "Assets/_Game/Materials/M_ExhaustSmoke.mat";

        [MenuItem("Generator/Create Exhaust Particle")]
        public static void CreateExhaust()
        {
            var go = new GameObject("ExhaustParticle");
            var ps = go.AddComponent<ParticleSystem>();

            // ── Main ──
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // random spin
            main.startColor    = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.6f, 0.64f, 1f));
            main.gravityModifier = -0.03f;                       // smoke drifts up a touch
            main.simulationSpace = ParticleSystemSimulationSpace.World; // trails behind a moving bus
            main.maxParticles = 80;

            // ── Emission ──
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 16f;

            // ── Shape: a small cone out of the pipe (point the object where it should blow) ──
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.03f;

            // ── Size over lifetime: puffs grow as they dissipate ──
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 2.4f)));

            // ── Colour over lifetime: fade in fast, fade out, darken slightly ──
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.65f, 0.65f, 0.7f), 0f),
                        new GradientColorKey(new Color(0.4f, 0.4f, 0.45f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.9f, 0.15f),
                        new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // ── Renderer ──
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.OldestInFront;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) renderer.sharedMaterial = mat;
            else Debug.LogWarning($"[ExhaustParticleCreator] Material not found at {MaterialPath} — assign M_ExhaustSmoke manually.");

            // Parent under the current selection (e.g. the bus's exhaust pipe) if any.
            var parent = Selection.activeGameObject;
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = Vector3.zero;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Exhaust Particle");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[ExhaustParticleCreator] Exhaust particle created. Rotate it so the cone points out of the pipe.");
        }
    }
}
#endif
