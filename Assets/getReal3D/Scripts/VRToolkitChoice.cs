using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if (UNITY_2017_2_OR_NEWER)
using VRSettings = UnityEngine.XR.XRSettings;
#else
using VRSettings = UnityEngine.VR.VRSettings;
#endif

/// <summary>
/// This script does the setup for either getReal3D or a different VR Toolkit.
/// A VR Toolkit is a child of the GenericPlayer that has a PlayerInputs in it.
/// A specific toolkit can be forced. If no VR device is forced, then a 2D UI is displayed to the
/// user. It is also possible to automatically select the toolkit if the toolkit XR device name
/// corresponds to the currently loaded XR device name.
/// Once the toolkit is chosen, the PlayerInputsProxy in this Game Object is set using any
/// PlayerInputs in the activated child.
/// </summary>
[RequireComponent(typeof(PlayerInputsProxy))]
public class VRToolkitChoice : MonoBehaviour
{
    [System.Serializable]
    public class VRToolkit {
        [Tooltip("Game object to enable when starting the VR Toolkit.")]
        public GameObject target;
        [Tooltip("Corresponding Unity XR device name.")]
        public string deviceName;
    };

    /// The different toolkits
    public List<VRToolkit> toolkits = new List<VRToolkit>();

    /// The index of a toolkit is to forcibly load.
    public int forcedToolkitIndex = -1;

    /// If true then the toolkit with the XR device name corresponding to the loaded XR device
    /// (VRSettings.loadedDeviceName) will be chose.
    public bool autoSelectFromLoadedDeviceName = false;

    private bool m_done = false;
    private string m_error = null;
    private const string gr3dChildName = "[getReal3D]";
    private const string gr3dDeviceName = "getReal3D";
    private const string MissingWandEventModuleMessage = "Failed to find GenericWandEventModule " +
        "component in scene. Please add the getReal3D/UI/EventSystem prefab in the scene.";

    void Reset()
    {
        toolkits = new List<VRToolkit>();
        toolkits.Add(new VRToolkit());
        toolkits.Last().deviceName = gr3dDeviceName;
        toolkits.Last().target = transform.Find(gr3dChildName).gameObject;
        forcedToolkitIndex = -1;
        autoSelectFromLoadedDeviceName = false;
    }

    void Start()
    {
        if (toolkits.Count == 1) {
            StartCoroutine(SetupToolkit(toolkits[0]));
        }
        else if (forcedToolkitIndex >= 0 && forcedToolkitIndex < toolkits.Count) {
            StartCoroutine(SetupToolkit(toolkits[forcedToolkitIndex]));
        }
        else if (isWithinGetReal3D) {
            StartCoroutine(SetupToolkit(FindGetReal3DToolkit()));
        }
        else if (autoSelectFromLoadedDeviceName) {
            getReal3D.Plugin.debug("Loading VR toolkit from device " + VRSettings.loadedDeviceName);
            var toolkit = FindToolkitFromDeviceName(VRSettings.loadedDeviceName);
            if (toolkit == null) {
                getReal3D.Plugin.error("Failed to find VR toolkit from device " + VRSettings.loadedDeviceName);
            }
            else {
                StartCoroutine(SetupToolkit(toolkit));
            }
        }
        else if (GetSupportedDevices().Count() == 1) {
            StartCoroutine(SetupToolkit(FindToolkitFromDeviceName(GetSupportedDevices().First())));
        }
    }

    private VRToolkit FindGetReal3DToolkit()
    {
        return toolkits.Find(x => x.target == transform.Find(gr3dChildName).gameObject);
    }

    private VRToolkit FindToolkitFromDeviceName(string deviceName)
    {
        return toolkits.Find(x => 0 == string.Compare(x.deviceName, deviceName, true));
    }

    IEnumerator SetupToolkit(VRToolkit toolkit)
    {
        Setup(toolkit);
        m_done = true;
        yield break;
    }

    public void OnGUI()
    {
        if(m_done) {
            return;
        }

        int w = 300;
        int h = 200;
        int x = Screen.width / 2 - w/2;
        int y = Screen.height / 2 - h/2;

        Rect rect = new Rect(x, y, w, h);
        GUILayout.BeginArea(rect);

        GUILayout.BeginVertical("VR Device", GUI.skin.window);

        foreach(var toolkit in toolkits) {
            if(GUILayout.Button("Start with " + toolkit.deviceName)) {
                StartCoroutine(SetupToolkit(toolkit));
            }
        }

        if(!string.IsNullOrEmpty(m_error)) {
            GUILayout.Label(m_error);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    IEnumerable<string> GetSupportedDevices()
    {
        return VRSettings.supportedDevices.
            Where(d => d != "None").
            Where(d => d != "stereo").
            Concat(new string[] { gr3dDeviceName });
    }

    void Setup(VRToolkit toolkit)
    {
        if (toolkit == null) {
            throw new System.Exception("Toolkit is null.");
        }
        toolkit.target.SetActive(true);
        var playerInputsProxy = GetComponent<PlayerInputsProxy>();
        playerInputsProxy.target = toolkit.target.GetComponent<PlayerInputs>();
        if(playerInputsProxy.target == null) {
            Debug.LogError("Failed to find PlayerInputs component in target.");
        }
        var wandEventModule = FindObjectOfType<GenericWandEventModule>();
        if(wandEventModule) {
            wandEventModule.playerInputs = playerInputsProxy;
        }
        else {
            Debug.LogError(MissingWandEventModuleMessage);
        }
        var createScreens = toolkit.target.GetComponent<CreateScreens>();
        if (createScreens) {
            createScreens.enabled = true;
        }
    }

    bool isWithinGetReal3D {
        get {
            return System.Environment.GetEnvironmentVariable("GETREAL_CONFIG") != null;
        }
    }
}
