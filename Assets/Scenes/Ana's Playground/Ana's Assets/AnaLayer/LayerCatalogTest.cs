using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cleaned-up version of LayerCatalog.
/// Maps layerIds to LayerDefinitionTest profiles.
/// Changes from original:
///   - Uses LayerDefinitionTest instead of LayerDefinition
///   - Added GetDefaultDefinition() helper so listeners can find
///     the baseline layer without hardcoding an id
/// </summary>
[CreateAssetMenu(menuName = "Layer Profiles/Layer Catalog Test")]
public class LayerCatalogTest : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string layerId;
        public LayerDefinitionTest definition;
    }

    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    [SerializeField] bool logOnResolveFailure = true;

    Dictionary<string, LayerDefinitionTest> _map;

    void OnEnable()  => Rebuild();
    void OnValidate() => Rebuild();

    void Rebuild()
    {
        _map = new Dictionary<string, LayerDefinitionTest>(StringComparer.Ordinal);
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.layerId) || e.definition == null) continue;
            if (_map.ContainsKey(e.layerId))
                Debug.LogWarning($"[LayerCatalogTest] Duplicate layerId '{e.layerId}'. Keeping last.");
            _map[e.layerId] = e.definition;
        }
    }

    /// <summary>Returns the LayerDefinitionTest for the given id, or null if not found.</summary>
    public LayerDefinitionTest GetDefinition(string layerId)
    {
        if (string.IsNullOrEmpty(layerId)) return null;
        if (_map == null) Rebuild();
        if (_map.TryGetValue(layerId, out var def)) return def;
        if (logOnResolveFailure)
            Debug.LogWarning($"[LayerCatalogTest] No definition found for layerId '{layerId}'.");
        return null;
    }

    /// <summary>
    /// Returns the first entry marked isDefaultLayer = true.
    /// Used by listeners to identify the baseline/fallback layer.
    /// </summary>
    public LayerDefinitionTest GetDefaultDefinition()
    {
        if (_map == null) Rebuild();
        foreach (var def in _map.Values)
            if (def.isDefaultLayer) return def;
        Debug.LogWarning("[LayerCatalogTest] No LayerDefinitionTest with isDefaultLayer = true found.");
        return null;
    }
}
