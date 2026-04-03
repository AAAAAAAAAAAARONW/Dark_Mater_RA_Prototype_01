using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary> 
/// This defines the catalog .asset which maps layer id's to layer profiles
/// Create a new LayerDefinition with asset menu and add to catalog .asset
/// </summary>

// Should only be one catalog .asset as source of truth, be sure to add new layer definitions to it.
// Just change the number of total entries, then add the profile with id and .asset 

[CreateAssetMenu(menuName = "Layer Profiles/Layer Catalog")]
public class LayerCatalog : ScriptableObject
{
    // defines structure of mapping
    [Serializable]
    public class Entry
    {
        public string layerId;
        public LayerDefinition definition;
    }

    // array that stores mappings
    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    [SerializeField] bool logOnResolveFailure = true;
    Dictionary<string, LayerDefinition> _map;

    // safety functions
    void OnEnable()
    {
        Rebuild();
    }
    void OnValidate()
    {
        Rebuild();
    }
    void Rebuild()
    {
        _map = new Dictionary<string, LayerDefinition>(StringComparer.Ordinal);
        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.layerId)) continue;
            if (e.definition == null) continue;
            if (_map.ContainsKey(e.layerId))
            {
                Debug.LogWarning($"[LayerCatalog] Duplicate layerId '{e.layerId}'. Keeping last.");
            }
            _map[e.layerId] = e.definition;
        }
    }

    // fetches definition given id requested
    public LayerDefinition GetDefinition(string layerId)
    {
        if (string.IsNullOrEmpty(layerId)) return null;
        if (_map == null) Rebuild();
        if (_map.TryGetValue(layerId, out var def))
            return def;
        if (logOnResolveFailure)
            Debug.LogWarning($"[LayerCatalog] No LayerDefinition found for layerId '{layerId}'.");
        return null;
    }
}