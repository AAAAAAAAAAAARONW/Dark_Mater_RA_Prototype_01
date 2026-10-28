using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if (UNITY_2017_2_OR_NEWER)
using VRSettings = UnityEngine.XR.XRSettings;
#else
using VRSettings = UnityEngine.VR.VRSettings;
#endif

namespace getReal3D {

    [CustomEditor(typeof(VRToolkitChoice))]
    public class VRToolkitChoiceEditor : UnityEditor.Editor {

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox("List of VR toolkit that can be used.", MessageType.Info);

            var toolkitChoice = target as VRToolkitChoice;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add toolkit", GUILayout.Width(80))) {
                toolkitChoice.toolkits.Add(new VRToolkitChoice.VRToolkit());
            }
            GUILayout.EndHorizontal();

            int toDelete = -1;
            for (int i=0; i< toolkitChoice.toolkits.Count; ++i) {
                var toolkit = toolkitChoice.toolkits[i];
                EditorGUI.BeginChangeCheck();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target:");
                toolkit.target = EditorGUILayout.ObjectField(toolkit.target, typeof(GameObject),
                    true) as GameObject;
                GUILayout.Label("XR Device:");
                toolkit.deviceName = EditorGUILayout.TextField(toolkit.deviceName);
                GUILayout.Label("Force ");
                bool wasDefault = i == toolkitChoice.forcedToolkitIndex;
                bool isDefault = EditorGUILayout.Toggle(wasDefault);
                if (wasDefault && !isDefault) {
                    toolkitChoice.forcedToolkitIndex = -1;
                }
                else if (!wasDefault && isDefault) {
                    toolkitChoice.forcedToolkitIndex = i;
                }

                if (EditorGUI.EndChangeCheck()) {
                    EditorUtility.SetDirty(toolkitChoice);
                }

                if (GUILayout.Button("X", GUILayout.Width(20))) {
                    toDelete = i;
                }
                GUILayout.EndHorizontal();
            }

            if(toDelete >= 0) {
                toolkitChoice.toolkits.RemoveAt(toDelete);
                EditorUtility.SetDirty(toolkitChoice);
                if (toolkitChoice.forcedToolkitIndex == toDelete) {
                    toolkitChoice.forcedToolkitIndex = -1;
                }
                else if (toolkitChoice.forcedToolkitIndex > toDelete) {
                    --toolkitChoice.forcedToolkitIndex;
                }
            }

            if (toolkitChoice.toolkits.Count > 1) {
                EditorGUI.BeginChangeCheck();
                toolkitChoice.autoSelectFromLoadedDeviceName = EditorGUILayout.Toggle(
                    "Select toolkit from loaded XR device name.",
                    toolkitChoice.autoSelectFromLoadedDeviceName);
                if (EditorGUI.EndChangeCheck()) {
                    EditorUtility.SetDirty(toolkitChoice);
                }
                if (!string.IsNullOrEmpty(VRSettings.loadedDeviceName)) {
                    EditorGUILayout.HelpBox("Loaded device: " + VRSettings.loadedDeviceName,
                        MessageType.Info);
                }

            }

            serializedObject.ApplyModifiedProperties();
        }
    }

}
