using UnityEditor;
using UnityEngine;

/// <summary>
/// Hides the fields a beat is currently ignoring.
///
/// Every field on TutorialBeat_NEW is drawn by default, including the ones its own
/// settings make inert, and that has already cost somebody an afternoon: hintText is
/// editable while hintMode is Keep, so you can type a new prompt into A2, press Play,
/// and watch nothing happen with no error and nothing in the Console. An Inspector that
/// offers a field it will not read is lying about what it does.
///
/// Two of those:
///
///   hintText            read only when hintMode is Show.
///   duration            read only in Duration mode. PlayerAction beats advance on the
///                       gate and never on the clock, which is the invariant
///                       TutorialBeat_NEW exists to hold.
///   inputGraceSeconds   the mirror image: PlayerAction only.
///
/// Applied to derived classes as well, so Beat_LookAt_NEW and the rest get it without
/// each needing an editor of their own.
/// </summary>
[CustomEditor(typeof(TutorialBeat_NEW), true)]
public class TutorialBeatEditor_NEW : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty hintMode = serializedObject.FindProperty("hintMode");
        SerializedProperty advanceMode = serializedObject.FindProperty("advanceMode");

        bool showsHint = hintMode != null &&
                         hintMode.enumValueIndex == (int)TutorialBeat_NEW.HintMode.Show;

        bool runsOnClock = advanceMode != null &&
                           advanceMode.enumValueIndex == (int)TutorialBeat_NEW.AdvanceMode.Duration;

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property);

                continue;
            }

            if (property.propertyPath == "hintText" && !showsHint) continue;
            if (property.propertyPath == "duration" && !runsOnClock) continue;
            if (property.propertyPath == "inputGraceSeconds" && runsOnClock) continue;

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
