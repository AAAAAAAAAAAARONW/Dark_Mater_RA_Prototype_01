using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Playtest jump tool with an on-screen readout.
///
/// Replaces PlaytestLayerDebugSwitcher, which read LayerCatalogTest's private fields
/// through reflection, called FindObjectOfType on a ScriptableObject type (which does not
/// find project assets), mapped number keys to catalog order rather than journey order —
/// so key 3 pointed at the start layer, which has no gate and therefore did nothing —
/// and reported everything through Debug.Log, where it scrolled away mid-playtest.
///
/// Here the gates are an explicit ordered list, and the state is drawn on screen.
///
/// One caveat worth knowing while using it: jumping past gates leaves LayerState_NEW's
/// "previous" pointing at wherever you jumped from, and CameraDirector_NEW routes the
/// look-back sequence off that. Jumping straight from the start layer to a mid-journey
/// gate therefore plays the first-gate sequence rather than the normal cover sequence.
/// That is faithful to how the state machine works, not a bug in the tool — but do not
/// judge a transition's timing from a jump.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DEBUG", "#8C8C99")]
public class LayerDebugJump_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] Transform player;
    [SerializeField] LayerState_NEW state;
    [SerializeField] UniverseJourneyTracker tracker;

    [Tooltip("Gates in journey order. Key 1 jumps to the first entry.")]
    [SerializeField] LayerGate_NEW[] gates = Array.Empty<LayerGate_NEW>();

    [Header("Placement")]
    [SerializeField] float verticalOffset = 1f;

    [Tooltip("Offset along the gate's forward axis. Negative places the player just before it.")]
    [SerializeField] float forwardOffset = -2f;

    [Header("Overlay")]
    [Tooltip("F1 toggles DebugView_NEW.Overlay, which every debug visual reads.")]
    [SerializeField] KeyCode toggleOverlayKey = KeyCode.F1;

    [Tooltip("While playing, also drive DebugView_NEW.Gizmos so scene annotations turn " +
             "on and off with this tool instead of being six separate switches.")]
    [SerializeField] bool driveGizmos = true;

    [Tooltip("Screen corner offset in pixels.")]
    [SerializeField] Vector2 overlayMargin = new Vector2(12f, 12f);

    [SerializeField] int fontSize = 13;

    [Header("Options")]
    [Tooltip("Leave off so the tool strips itself out of builds.")]
    [SerializeField] bool enabledInBuild = false;

    string _lastAction = "";
    float _lastActionTime = -999f;

    GUIStyle _panel, _label;
    Texture2D _panelTex;
    readonly StringBuilder _sb = new StringBuilder(512);

    const float ActionFadeSeconds = 2.5f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (tracker == null) tracker = FindObjectOfType<UniverseJourneyTracker>();

        if (player == null)
        {
            PlayerRig_NEW rig = FindObjectOfType<PlayerRig_NEW>();
            if (rig != null) player = rig.transform;
        }

        if (!enabledInBuild && !Application.isEditor)
        {
            enabled = false;
            return;
        }

        if (player == null)
            Debug.LogWarning("[LayerDebugJump_NEW] No player transform. Jumping is disabled.", this);
    }

    void OnDestroy()
    {
        if (_panelTex != null) Destroy(_panelTex);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleOverlayKey))
        {
            DebugView_NEW.Overlay = !DebugView_NEW.Overlay;
            if (driveGizmos) DebugView_NEW.Gizmos = DebugView_NEW.Overlay;
        }

        if (player == null) return;

        int max = Mathf.Min(9, gates.Length);
        for (int i = 0; i < max; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i))
                continue;

            JumpTo(i);
            return;
        }
    }

    // ── Jump ─────────────────────────────────────────────────────────────────

    public void JumpTo(int index)
    {
        if (player == null || index < 0 || index >= gates.Length) return;

        LayerGate_NEW gate = gates[index];
        if (gate == null)
        {
            Note($"slot {index + 1} is empty");
            return;
        }

        Vector3 target = gate.transform.position
                       + Vector3.up * verticalOffset
                       + gate.transform.forward * forwardOffset;

        // CharacterController overrides direct transform writes, so disable it for the move.
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = target;
        if (cc != null) cc.enabled = true;

        Note($"jumped to {gate.LayerId}");
    }

    void Note(string message)
    {
        _lastAction = message;
        _lastActionTime = Time.unscaledTime;
    }

    // ── Overlay ──────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        EnsureStyles();

        string body = BuildBody();
        Vector2 size = _label.CalcSize(new GUIContent(body));

        var rect = new Rect(overlayMargin.x, overlayMargin.y, size.x + 20f, size.y + 16f);

        GUI.Box(rect, GUIContent.none, _panel);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, size.x, size.y), body, _label);
    }

    string BuildBody()
    {
        _sb.Length = 0;

        string current = state != null && state.Current != null ? state.Current.layerId : "—";
        _sb.Append("LAYER DEBUG   ").Append(toggleOverlayKey).Append(" to hide\n");
        _sb.Append("current   ").Append(current).Append('\n');

        if (player != null)
            _sb.Append("player Z  ").Append(player.position.z.ToString("F1")).Append('\n');

        if (tracker != null)
        {
            _sb.Append("phase     ").Append(tracker.CurrentPhase).Append('\n');
            _sb.Append("remaining ")
               .Append(UniverseJourneyTracker.FormatDistance(tracker.RemainingDistanceLy))
               .Append('\n');
        }

        _sb.Append('\n');

        for (int i = 0; i < gates.Length && i < 9; i++)
        {
            LayerGate_NEW g = gates[i];
            string id = g != null ? g.LayerId : "(empty)";
            bool isCurrent = g != null && string.Equals(id, current, StringComparison.Ordinal);

            _sb.Append(isCurrent ? "> " : "  ")
               .Append(i + 1).Append("  ").Append(id).Append('\n');
        }

        float age = Time.unscaledTime - _lastActionTime;
        if (age < ActionFadeSeconds && !string.IsNullOrEmpty(_lastAction))
            _sb.Append('\n').Append("· ").Append(_lastAction);

        return _sb.ToString();
    }

    void EnsureStyles()
    {
        if (_panelTex == null)
        {
            _panelTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _panelTex.SetPixel(0, 0, new Color(0.05f, 0.03f, 0.11f, 0.82f));
            _panelTex.Apply();
        }

        if (_panel == null)
            _panel = new GUIStyle(GUIStyle.none) { normal = { background = _panelTex } };

        if (_label == null)
        {
            _label = new GUIStyle(GUI.skin.label)
            {
                font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize),
                fontSize = fontSize,
                richText = false,
                wordWrap = false,
                normal = { textColor = new Color(0.88f, 0.85f, 0.96f, 1f) }
            };
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (gates == null) return;

        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] == null) continue;
            for (int j = i + 1; j < gates.Length; j++)
                if (gates[j] == gates[i])
                {
                    Debug.LogWarning($"[LayerDebugJump_NEW] Gate '{gates[i].name}' is listed twice " +
                                     $"(slots {i + 1} and {j + 1}).", this);
                    return;
                }
        }
    }
#endif
}
