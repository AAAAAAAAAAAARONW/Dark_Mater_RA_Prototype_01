using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// layerId -> LayerProfile_NEW lookup table.
///
/// Replaces LayerCatalogTest. GetDefaultDefinition() is gone — it had zero callers,
/// and the concept it served ("the layer you return to") does not exist in the
/// one-way gate model. The start layer is identified by LayerProfile_NEW.isStartLayer
/// and resolved once at startup by LayerState_NEW.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Layer Catalog", fileName = "LayerCatalog_New")]
public class LayerCatalog_NEW : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Leave empty to use the profile's own layerId.")]
        public string layerIdOverride;
        public LayerProfile_NEW profile;

        public string ResolvedId =>
            !string.IsNullOrEmpty(layerIdOverride) ? layerIdOverride :
            profile != null ? profile.layerId : null;
    }

    [Tooltip("Order is the journey order. LayerDebugJump_NEW maps number keys to this order.")]
    [SerializeField] Entry[] entries = Array.Empty<Entry>();

    [SerializeField] bool logOnResolveFailure = true;

    Dictionary<string, LayerProfile_NEW> _map;

    /// <summary>Entries in authored order. Used by the debug jump tool.</summary>
    public IReadOnlyList<Entry> Entries => entries;

    void OnEnable() => Rebuild();
    void OnValidate() => Rebuild();

    void Rebuild()
    {
        _map = new Dictionary<string, LayerProfile_NEW>(StringComparer.Ordinal);
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry e = entries[i];
            if (e == null || e.profile == null) continue;

            string id = e.ResolvedId;
            if (string.IsNullOrEmpty(id)) continue;

            if (_map.ContainsKey(id))
                Debug.LogWarning($"[LayerCatalog_NEW] Duplicate layerId '{id}'. The later entry wins.", this);

            _map[id] = e.profile;
        }
    }

    /// <summary>Returns the profile for the id, or null.</summary>
    public LayerProfile_NEW Get(string layerId)
    {
        if (string.IsNullOrEmpty(layerId)) return null;
        if (_map == null) Rebuild();

        if (_map.TryGetValue(layerId, out LayerProfile_NEW p)) return p;

        if (logOnResolveFailure)
            Debug.LogWarning($"[LayerCatalog_NEW] No profile for layerId '{layerId}'.", this);
        return null;
    }

    /// <summary>The single profile flagged isStartLayer, or null if none / several.</summary>
    public LayerProfile_NEW GetStartLayer()
    {
        if (_map == null) Rebuild();

        LayerProfile_NEW found = null;
        foreach (LayerProfile_NEW p in _map.Values)
        {
            if (!p.isStartLayer) continue;
            if (found != null)
            {
                Debug.LogWarning($"[LayerCatalog_NEW] More than one profile sets isStartLayer " +
                                 $"('{found.layerId}' and '{p.layerId}'). Using '{found.layerId}'.", this);
                return found;
            }
            found = p;
        }

        if (found == null)
            Debug.LogWarning("[LayerCatalog_NEW] No profile sets isStartLayer.", this);
        return found;
    }
}
