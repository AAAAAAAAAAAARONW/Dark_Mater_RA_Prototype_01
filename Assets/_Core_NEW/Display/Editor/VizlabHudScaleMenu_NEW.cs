using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes every HUD canvas in the open scenes keep the size it has on the wall, on any
/// screen — so Native on a desk monitor shows the HUD as Fit does.
///
/// WHAT WAS WRONG. A Canvas Scaler set to Scale With Screen Size scales by the screen's
/// width, its height or a mix of the two, against a reference resolution. The gameplay
/// scenes' LAF HUD and MilestoneUI canvases are at Unity's default — 800 x 600, matched on
/// width — and the journey tracker's canvas expands from a 16:9 1920 x 1080. On the wall,
/// and in the wall test panel's Fit (which renders the wall's shape), all of them come out
/// right. On a screen wider than the wall, which is any desk monitor in Native, a canvas
/// matched on width keeps its width and so grows against the height: on 16:9 the LAF bar,
/// a seventh of the picture's height in Fit, takes a fifth of it in Native.
///
/// WHAT THIS DOES. Gives each canvas a reference resolution in the wall's own shape
/// (9600 : 7560) that scales exactly as its current one does on the wall, and sets it to
/// Expand. On the wall and in Fit nothing changes, to the pixel. On any other screen the
/// canvas fits the largest wall-shaped rectangle in it — by height on a wider screen, by
/// width on a narrower one — so the HUD is the same share of the picture it is on the
/// wall, and what the screen has beyond the wall's shape is room at the sides (or above and
/// below), not a bigger HUD. Elements anchored to an edge still go to the screen's edge.
///
/// Matching on height, as Set Canvas to Full Surface and the tutorial's HUD do, already gets
/// the wider screens right; Expand is the same there and also keeps the HUD inside a
/// narrower one, such as a Game View docked tall.
///
/// Root canvases only (a nested canvas takes its scale from its root), screen-space ones
/// only, and only those set to Scale With Screen Size: a Constant Pixel Size canvas such as
/// the Earth video's is left alone. One undoable step; save the scene to keep it. Run it
/// again any time — a canvas already in the wall's shape is reported and not touched.
/// </summary>
static class VizlabHudScaleMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Scale HUD Canvases to the Wall's Shape (open scenes)";

    /// <summary>How close to the wall's shape an Expand reference must be to count as done.</summary>
    const float Tolerance = 0.002f;

    [MenuItem(MenuPath)]
    static void ScaleOpenScenes()
    {
        Undo.SetCurrentGroupName("Scale HUD canvases to the wall's shape");
        int group = Undo.GetCurrentGroup();

        int changed = 0, already = 0;
        var report = new StringBuilder();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                if (!IsHudCanvas(scaler)) continue;

                Vector2 wall = WallShapedReference(scaler);
                if (IsAlreadyWallShaped(scaler, wall))
                {
                    already++;
                    continue;
                }

                string before = Describe(scaler);

                Undo.RecordObject(scaler, "Scale HUD canvases to the wall's shape");
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                scaler.referenceResolution = wall;
                EditorUtility.SetDirty(scaler);

                changed++;
                report.Append("\n  ").Append(scene.name).Append(" / ").Append(PathOf(scaler.transform))
                      .Append(":  ").Append(before).Append("  ->  ").Append(Describe(scaler));
            }
        }

        Undo.CollapseUndoOperations(group);

        if (changed == 0)
        {
            Debug.Log("Journey NEW: every HUD canvas in the open scenes already scales with the wall's shape" +
                      (already > 0 ? " (" + already + " checked)." : "; there are none to check."));
            return;
        }

        Debug.Log("Journey NEW: " + changed + " HUD canvas" + (changed == 1 ? "" : "es") + " now scale with the " +
                  "wall's shape — the same on the wall and in Fit, and the same share of the picture in Native." +
                  report + "\nSave the scene to keep it.");
    }

    [MenuItem(MenuPath, true)]
    static bool ScaleOpenScenesValidate() => !Application.isPlaying;

    /// <summary>
    /// The reference resolution, in the wall's shape, under which Expand scales this canvas on
    /// a wall-shaped screen exactly as its current settings do there.
    ///
    /// On a wall-shaped screen of height 1 (width a, the wall's aspect), Unity's scale factor
    /// is (a / refW)^(1-m) * (1 / refH)^m for Match Width Or Height — a blend in log space —
    /// min(a / refW, 1 / refH) for Expand and max of the same for Shrink. A wall-shaped
    /// reference of height h scales by 1 / h there under any mode, so h = 1 / that factor.
    /// </summary>
    public static Vector2 WallShapedReference(CanvasScaler scaler)
    {
        float a = VizlabDisplay_NEW.CanvasWidthPx / (float)VizlabDisplay_NEW.CanvasHeightPx;
        Vector2 r = scaler.referenceResolution;
        float w = Mathf.Max(r.x, 1e-3f), h = Mathf.Max(r.y, 1e-3f);

        float scale;
        switch (scaler.screenMatchMode)
        {
            case CanvasScaler.ScreenMatchMode.Expand:
                scale = Mathf.Min(a / w, 1f / h);
                break;
            case CanvasScaler.ScreenMatchMode.Shrink:
                scale = Mathf.Max(a / w, 1f / h);
                break;
            default:
                float m = Mathf.Clamp01(scaler.matchWidthOrHeight);
                scale = Mathf.Pow(a / w, 1f - m) * Mathf.Pow(1f / h, m);
                break;
        }

        float height = 1f / scale;
        return new Vector2(height * a, height);
    }

    static bool IsAlreadyWallShaped(CanvasScaler scaler, Vector2 wall)
    {
        if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand) return false;
        Vector2 r = scaler.referenceResolution;
        return Mathf.Abs(r.x - wall.x) <= wall.x * Tolerance && Mathf.Abs(r.y - wall.y) <= wall.y * Tolerance;
    }

    /// <summary>A screen-space root canvas set to Scale With Screen Size: the ones a screen's shape changes.</summary>
    static bool IsHudCanvas(CanvasScaler scaler)
    {
        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return false;

        Canvas canvas = scaler.GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return false;

        // Root by hierarchy rather than Canvas.isRootCanvas, which is not to be trusted on an
        // inactive object — and MilestoneUI starts inactive.
        for (Transform t = scaler.transform.parent; t != null; t = t.parent)
            if (t.GetComponent<Canvas>() != null) return false;

        return true;
    }

    static string Describe(CanvasScaler scaler)
    {
        Vector2 r = scaler.referenceResolution;
        string match = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
            ? (scaler.matchWidthOrHeight <= 0f ? "match width"
               : scaler.matchWidthOrHeight >= 1f ? "match height"
               : "match " + scaler.matchWidthOrHeight.ToString("0.##"))
            : scaler.screenMatchMode.ToString();
        return r.x.ToString("0.#") + " x " + r.y.ToString("0.#") + ", " + match;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
