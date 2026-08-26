using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hierarchy tag overrides for scripts you cannot or would rather not annotate.
///
/// The attribute route (HierarchyBadge_NEW) is better when you own the file, because the
/// label cannot drift away from the class. This asset covers everything else: package
/// code, the legacy stack, third-party components, and any case where you want to
/// override an attribute without touching the script.
///
/// Resolution order, highest first:
///
///   1. an entry here                     explicit, wins over everything
///   2. [HierarchyBadge_NEW] on the class  the script speaks for itself
///   3. automatic                          label from the class name, colour from its hash
///
/// Entries are matched on the plain class name, no namespace. `hide` suppresses a type
/// entirely — useful for components that sit on dozens of objects and would otherwise
/// bury the useful tags.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Badge Registry", fileName = "BadgeRegistry_New")]
public class BadgeRegistry_NEW : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Class name without namespace, e.g. WorldSpaceUIPopup.")]
        public string typeName;

        [Tooltip("Text in the tag. Leave empty to fall back to the automatic label.")]
        public string label;

        [Tooltip("Tag colour. Alpha is ignored — the drawer sets it.")]
        public Color color = new Color(0.55f, 0.55f, 0.60f, 1f);

        [Tooltip("Never tag this type. For components that appear on many objects and " +
                 "would drown out everything else.")]
        public bool hide;

        [Tooltip("Draw the tag dimmed. Used to mark a retired script that is on its way out.")]
        public bool dim;
    }

    [Tooltip("Only these are looked up by name; anything absent falls through to the " +
             "attribute, then to the automatic label.")]
    [SerializeField] List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> _map;

    void OnEnable() => Rebuild();
    void OnValidate() => Rebuild();

    void Rebuild()
    {
        _map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        if (entries == null) return;

        foreach (Entry e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.typeName)) continue;

            if (_map.ContainsKey(e.typeName))
                Debug.LogWarning($"[BadgeRegistry_NEW] '{e.typeName}' is listed twice. " +
                                 "The later entry wins.", this);

            _map[e.typeName] = e;
        }
    }

    /// <summary>Entry for a class name, or null.</summary>
    public Entry Lookup(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        if (_map == null) Rebuild();

        return _map.TryGetValue(typeName, out Entry e) ? e : null;
    }
}
