using UnityEngine;

/// <summary>
/// Marks a hierarchy folder as one stage of the journey: everything you place by hand for that
/// stage — its world (the render layer), the door that ends it, and the triggers inside it —
/// sits under this object. Made by Tools > Journey NEW > Layout > Stages, which also uses it to
/// lock every other stage while you work on one.
///
/// A folder and nothing more: it must stay at the origin with no rotation or scale, so what is
/// under it is placed exactly as if it were at the scene root. Dragged by mistake, it goes
/// straight back (edit mode only; at runtime this does nothing at all).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("STAGE", "#7FB3D5")]
public class StageFolder_NEW : MonoBehaviour
{
    [Tooltip("The layer this stage is (LayerProfile_NEW.layerId).")]
    public string layerId = "";

    [Tooltip("1 = the first stage of the journey.")]
    public int order;

#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying || !transform.hasChanged) return;
        transform.hasChanged = false;

        if (transform.localPosition == Vector3.zero && transform.localRotation == Quaternion.identity &&
            transform.localScale == Vector3.one) return;

        // Moving the folder would move the whole stage — its door with its world — which is
        // never what anybody meant.
        Debug.LogWarning("[StageFolder_NEW] '" + name + "' is a folder and stays at the origin. Move the things " +
                         "inside it instead (Tools > Journey NEW > Layout > Stages locks the rest).", this);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        transform.hasChanged = false;
    }
#endif
}
