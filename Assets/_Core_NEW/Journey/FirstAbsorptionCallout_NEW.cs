using UnityEngine;

/// <summary>
/// RETIRED — replaced by FilamentAbsorber_NEW. Kept only so a scene that already has it
/// does not show a missing script; it does nothing.
///
/// It waited, after J3, for the light to cross one of the bake's marked absorbers, and
/// fell back to highlighting "the line being born now". In the bake the forest freezes at
/// z 2.46, just before the first cosmic web, so neither ever had a real line to point at.
/// J3b now hangs on a placed yellow filament that cuts its own line (FilamentAbsorber_NEW).
///
/// Wire Journey Voice Over removes this component and the Macro row's call to Arm. Once
/// no scene has it, this file can be deleted.
/// </summary>
[DisallowMultipleComponent]
public class FirstAbsorptionCallout_NEW : MonoBehaviour
{
    /// <summary>Does nothing. Still here because a scene's onFinished may call it.</summary>
    public void Arm() { }

    /// <summary>Does nothing.</summary>
    public void ResetCallout() { }
}
