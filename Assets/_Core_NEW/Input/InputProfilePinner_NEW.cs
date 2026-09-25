using UnityEngine;

/// <summary>
/// Pins the pad profile for this scene, in Awake, before anything reads input.
///
/// WHY PINNING IS THE DEFAULT AT THE OBSERVATORIES. InputScheme_NEW can guess the pad
/// from its reported name, and that is fine at a desk where a wrong guess is visible in
/// two seconds and fixable in two more. On site it is the opposite: the pad is one known
/// Logitech, nobody present can open Unity, and a wrong guess does not look like a wrong
/// guess - it looks like a visitor who cannot work the controls (see PadProfile_NEW for
/// what a scrambled mapping actually feels like in the hand).
///
/// So the exhibition build states the answer instead of inferring it. Drop this on the
/// systems root of both scenes, set profileId to Vizlab, and detection never runs.
///
/// LEAVE profileId EMPTY to opt back into detection - that is the desk setting, and it
/// is why this is a component with a field rather than a line of code somewhere.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PAD", "#E09E38")]
public class InputProfilePinner_NEW : MonoBehaviour
{
    [Tooltip("Which pad profile to pin: Vizlab, PlayStation or Xbox.\n\n" +
             "Vizlab is the Carnegie Observatories Logitech and is what an exhibition " +
             "build should ship with.\n\n" +
             "EMPTY means do not pin - fall back to detecting from the connected pad's " +
             "name, which is the right setting at a desk and the wrong one on site.")]
    [SerializeField] string profileId = "Vizlab";

    [Tooltip("Log the profile in force at startup. Cheap, once, and it is the first " +
             "thing to check when a control does not respond on site.")]
    [SerializeField] bool logOnAwake = true;

    void Awake()
    {
        if (string.IsNullOrEmpty(profileId))
        {
            // Not pinning is a decision, not an omission, so say which one was reached.
            if (logOnAwake)
            {
                Debug.Log("[InputProfilePinner_NEW] profileId is empty - detecting. " +
                          "Detected '" + InputScheme_NEW.Pad.displayName + "'.", this);
            }

            return;
        }

        InputScheme_NEW.Pin(profileId);

        if (logOnAwake)
        {
            Debug.Log("[InputProfilePinner_NEW] Pinned pad profile '" +
                      InputScheme_NEW.Pad.displayName + "'. Detection will not run.", this);
        }
    }

    /// <summary>
    /// Re-pin at runtime, for the diagnostic page and for a UnityEvent on a debug button.
    /// Pinning again is always safe; it simply replaces the answer.
    /// </summary>
    public void Repin(string id)
    {
        profileId = id;
        InputScheme_NEW.Pin(id);
    }
}
