using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Debug helper for PlaytestBuild:
/// Press number keys (1..9) to teleport the player into the corresponding
/// trigger zone based on LayerCatalogTest entry order.
///
/// This does NOT force layer state directly.
/// It relies on trigger enter so existing responder pipeline stays authoritative.
/// </summary>
public class PlaytestLayerDebugSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerCatalogTest catalog;
    [SerializeField] Transform player;
    [Tooltip("Optional. If empty, auto-finds all LayerTriggerVolumeTest in scene.")]
    [SerializeField] LayerTriggerVolumeTest[] triggerVolumes;

    [Header("Teleport")]
    [SerializeField] float verticalOffset = 1.0f;
    [SerializeField] float forwardOffset = 0.0f;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    readonly List<string> _catalogLayerIds = new List<string>();
    readonly Dictionary<string, LayerTriggerVolumeTest> _triggerByLayerId =
        new Dictionary<string, LayerTriggerVolumeTest>(StringComparer.Ordinal);

    FieldInfo _catalogEntriesField;
    FieldInfo _entryLayerIdField;
    FieldInfo _triggerLayerIdField;

    void Awake()
    {
        if (catalog == null)
            catalog = FindObjectOfType<LayerCatalogTest>();
        if (player == null)
        {
            var controller = FindObjectOfType<DarkMatterPlayerControllerTest>();
            if (controller != null) player = controller.transform;
        }

        CacheReflectionFields();
        RebuildMappings();
    }

    void Update()
    {
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                TrySwitchByIndex(i - 1, i);
                return;
            }
        }
    }

    void CacheReflectionFields()
    {
        _catalogEntriesField = typeof(LayerCatalogTest).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
        _triggerLayerIdField = typeof(LayerTriggerVolumeTest).GetField("layerId", BindingFlags.Instance | BindingFlags.NonPublic);

        var entryType = typeof(LayerCatalogTest).GetNestedType("Entry", BindingFlags.Public | BindingFlags.NonPublic);
        if (entryType != null)
            _entryLayerIdField = entryType.GetField("layerId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    void RebuildMappings()
    {
        _catalogLayerIds.Clear();
        _triggerByLayerId.Clear();

        if (catalog != null && _catalogEntriesField != null && _entryLayerIdField != null)
        {
            var entriesObj = _catalogEntriesField.GetValue(catalog) as Array;
            if (entriesObj != null)
            {
                for (int i = 0; i < entriesObj.Length; i++)
                {
                    object entry = entriesObj.GetValue(i);
                    if (entry == null) continue;

                    string layerId = _entryLayerIdField.GetValue(entry) as string;
                    if (!string.IsNullOrEmpty(layerId))
                        _catalogLayerIds.Add(layerId);
                }
            }
        }

        if (triggerVolumes == null || triggerVolumes.Length == 0)
            triggerVolumes = FindObjectsOfType<LayerTriggerVolumeTest>();

        if (triggerVolumes != null && _triggerLayerIdField != null)
        {
            for (int i = 0; i < triggerVolumes.Length; i++)
            {
                LayerTriggerVolumeTest volume = triggerVolumes[i];
                if (volume == null) continue;
                string layerId = _triggerLayerIdField.GetValue(volume) as string;
                if (string.IsNullOrEmpty(layerId)) continue;
                if (!_triggerByLayerId.ContainsKey(layerId))
                    _triggerByLayerId[layerId] = volume;
            }
        }
    }

    void TrySwitchByIndex(int catalogIndex, int keyNumber)
    {
        if (player == null)
        {
            Debug.LogWarning("[PlaytestLayerDebugSwitcher] Player is not assigned.");
            return;
        }

        if (_catalogLayerIds.Count == 0)
            RebuildMappings();

        if (catalogIndex < 0 || catalogIndex >= _catalogLayerIds.Count)
        {
            if (debugLog)
                Debug.Log($"[PlaytestLayerDebugSwitcher] Key {keyNumber} has no catalog entry.");
            return;
        }

        string layerId = _catalogLayerIds[catalogIndex];
        if (!_triggerByLayerId.TryGetValue(layerId, out var trigger) || trigger == null)
        {
            Debug.LogWarning($"[PlaytestLayerDebugSwitcher] No trigger found for layer '{layerId}' (key {keyNumber}).");
            return;
        }

        Vector3 target = ResolveTeleportPoint(trigger);
        TeleportPlayer(target);

        if (debugLog)
            Debug.Log($"[PlaytestLayerDebugSwitcher] Key {keyNumber} -> layer '{layerId}', trigger '{trigger.name}', teleported to {target}.");
    }

    Vector3 ResolveTeleportPoint(LayerTriggerVolumeTest trigger)
    {
        Transform t = trigger.transform;
        Vector3 target = t.position + t.forward * forwardOffset + Vector3.up * verticalOffset;

        Collider c = trigger.GetComponent<Collider>();
        if (c != null)
        {
            // Use trigger bounds center and offset slightly upward to make sure we are inside.
            target = c.bounds.center + t.forward * forwardOffset + Vector3.up * verticalOffset;
        }
        return target;
    }

    void TeleportPlayer(Vector3 target)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false;
            player.position = target;
            cc.enabled = wasEnabled;
            return;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = target;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        player.position = target;
    }
}
