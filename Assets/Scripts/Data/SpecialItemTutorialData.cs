using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Data
{
    /// <summary>
    /// Title / description / icon for each special item's one-time tutorial. One entry per
    /// SpecialItem. Create via Create ▸ PixelShoot ▸ Special Item Tutorials.
    /// </summary>
    [CreateAssetMenu(fileName = "SpecialItemTutorials", menuName = "PixelShoot/Special Item Tutorials")]
    public class SpecialItemTutorialData : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public SpecialItem item;
            public string title;
            [TextArea] public string description;
            public Sprite icon;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>The entry for an item, or null if none is authored.</summary>
        public Entry Get(SpecialItem item)
        {
            foreach (var e in entries)
                if (e != null && e.item == item) return e;
            return null;
        }
    }
}
