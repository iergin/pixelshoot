using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Tiny runtime object pool for <see cref="Stickman"/> instances, keyed by prefab.
    /// Spawning a bus passenger every shot would otherwise churn Instantiate/Destroy;
    /// here they're reused. Each instance remembers the prefab it came from so it goes
    /// back into the right bucket on release.
    /// </summary>
    public static class StickmanPool
    {
        private static Transform root;
        private static readonly Dictionary<Stickman, Stack<Stickman>> pools =
            new Dictionary<Stickman, Stack<Stickman>>();

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    // Lives in the active (Game) scene — recreated per level, no DontDestroyOnLoad.
                    var go = new GameObject("StickmanPool");
                    root = go.transform;
                }
                return root;
            }
        }

        /// <summary>Get an active stickman for <paramref name="prefab"/>, parented to <paramref name="parent"/>.</summary>
        public static Stickman Get(Stickman prefab, Transform parent)
        {
            if (prefab == null) return null;

            Stickman inst = null;
            if (pools.TryGetValue(prefab, out var stack))
                while (stack.Count > 0 && inst == null) inst = stack.Pop(); // skip any that were destroyed

            if (inst == null)
            {
                inst = Object.Instantiate(prefab);
                inst.SetSourcePrefab(prefab);
            }

            inst.transform.SetParent(parent, false);
            inst.gameObject.SetActive(true);
            return inst;
        }

        /// <summary>Return a stickman to its pool (or destroy it if it wasn't pooled).</summary>
        public static void Release(Stickman inst)
        {
            if (inst == null) return;
            var prefab = inst.SourcePrefab;
            if (prefab == null) { Object.Destroy(inst.gameObject); return; }

            inst.ResetForPool();
            inst.transform.SetParent(Root, false);
            inst.gameObject.SetActive(false);

            if (!pools.TryGetValue(prefab, out var stack)) { stack = new Stack<Stickman>(); pools[prefab] = stack; }
            stack.Push(inst);
        }
    }
}
