using UnityEngine;

/// <summary>
/// X on the keyboard hides every debug readout on screen, and X again brings them back.
///
/// It flips DebugView_NEW.Overlay, the one switch every readout already asks - the layer
/// readout, the tutorial's beat panels, the voice-over rows, the wall test panel, the Summit
/// overlay, the input diagnostics, the build stats - so one key clears the screen for a
/// recording or a showing without hunting through F-keys. F1 (LayerDebugJump_NEW) flips the
/// same switch, so the two never disagree.
///
/// Installs itself in every scene and survives scene loads; nothing has to be placed. The
/// keyboard only: no pad button is free, and a visitor must never be able to turn the
/// readouts on. On the Vizlab wall only the master's keyboard is read, so it hides the
/// master's readouts, which are the only ones an attendant sees there anyway.
/// </summary>
[DisallowMultipleComponent]
public class DebugToggleKey_NEW : MonoBehaviour
{
    const KeyCode Key = KeyCode.X;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindObjectOfType<DebugToggleKey_NEW>() != null) return;

        var go = new GameObject("Debug Toggle (X)");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugToggleKey_NEW>();
    }

    void Update()
    {
        if (!Input.GetKeyDown(Key)) return;

        DebugView_NEW.Overlay = !DebugView_NEW.Overlay;
        Debug.Log("[DebugToggleKey_NEW] X: debug readouts " + (DebugView_NEW.Overlay ? "shown" : "hidden") + ".");
    }
}
