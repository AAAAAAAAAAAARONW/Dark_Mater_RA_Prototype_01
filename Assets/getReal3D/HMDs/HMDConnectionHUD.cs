using System;
using System.Text.RegularExpressions;
using UnityEngine;
using getReal3D.MultiCluster;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

#if (UNITY_2017_2_OR_NEWER)
using VRSettings = UnityEngine.XR.XRSettings;
#else
using VRSettings = UnityEngine.VR.VRSettings;
#endif

/// <summary>
/// 2D GUI used to connect or disconnect MultiCluster .
/// </summary>
/// <remarks>
/// This script also handles automatic connection via the command line. This script supports
/// connecting with either SteamVR or getReal3D.
/// </remarks>
[RequireComponent(typeof(NetworkManager))]
public class HMDConnectionHUD : MonoBehaviour, INetworkErrorListener {
    private string m_serverAddress = "127.0.0.1";
    private NetworkManager m_networkManager;

    [UnityEngine.HideInInspector]
    public string selectedDeviceName;


    [Tooltip("Delay in seconds before messages are deleted.")]
    public float m_timeout = 20;

    private struct Error {
        public Error(float timeIssued, string text)
        {
            this.timeIssued = timeIssued;
            this.text = text;
        }
        public float timeIssued;
        public string text;
    }

    LinkedList<Error> m_errors = new LinkedList<Error>();

    class Options {
        public bool doHost = false;
        public bool doJoin = false;
        public bool forceAntiAliasing = false;
        public int antiAliasingValue = 0;
        public string hostname = "";

        public void Parse(string[] args)
        {
            for(int i = 0; i < args.Length; i++) {
                if(args[i] == "-host" || args[i] == "--host") {
                    doHost = true;
                }
                else if(i != args.Length - 1 && (args[i] == "-join" || args[i] == "--join")) {
                    doJoin = true;
                    hostname = args[++i];
                }
                else if(i != args.Length - 1 && (args[i] == "-aa" || args[i] == "--aa") &&
                    Regex.IsMatch(args[i + 1], @"^\d+$")) {
                    forceAntiAliasing = true;
                    antiAliasingValue = int.Parse(args[++i]);
                }
            }
        }
    }

    private void Start()
    {
        m_serverAddress = PlayerPrefs.GetString("server_address", m_serverAddress);
        m_networkManager = GetComponent<NetworkManager>();

        Options options = new Options();
        options.Parse(System.Environment.GetCommandLineArgs());

        if(options.forceAntiAliasing) {
            QualitySettings.antiAliasing = options.antiAliasingValue;
        }

        if(options.doHost) {
            StartCoroutine(SetupDeviceAndHostOrJoin(loadedDeviceName(), ""));
        }
        else if(options.doJoin) {
            StartCoroutine(SetupDeviceAndHostOrJoin(loadedDeviceName(), options.hostname));
        }
    }

    private string[] devices()
    {
#       if (UNITY_2019_3_OR_NEWER)
        var displays = new List<UnityEngine.XR.XRDisplaySubsystemDescriptor>();
        SubsystemManager.GetSubsystemDescriptors(displays);
        return displays.Select(d => d.id).Concat(new string[] { "getReal3D" }).ToArray();
#       else
        return VRSettings.supportedDevices.Where(d => d != "None").
                Concat(new string[] { "getReal3D" }).ToArray();
#       endif
    }

    private string loadedDeviceName()
    {
        return UnityEngine.XR.XRSettings.loadedDeviceName;
    }

    public void OnGUI()
    {
        GUILayout.BeginArea(new Rect(0, 0, 300, 500));
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("Network", UnityEngine.GUI.skin.window);

        bool connected = m_networkManager.isNetworkActive;

        if(connected) {
            bool disconnect = GUILayout.Button("Disconnect");
            if(disconnect) {
                m_networkManager.StopClient();
                m_networkManager.StopHost();
            }

            var connections = NetworkServer.connections;
            if(connections != null) {
                GUILayout.Space(20);
                GUILayout.Label("Clients:");
                foreach(var conn in NetworkServer.connections) {
                    GUILayout.Label(conn.address);
                }
            }
        }
        else {

            foreach(var device in devices()) {
                if(GUILayout.Button("Host with " + device)) {
                    StartCoroutine(SetupDeviceAndHostOrJoin(device, ""));
                }
                if(GUILayout.Button("Join with " + device)) {
                    StartCoroutine(SetupDeviceAndHostOrJoin(device, m_serverAddress));
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Address:");
            m_serverAddress = GUILayout.TextField(m_serverAddress);
            GUILayout.EndHorizontal();

        }
        GUILayout.Label("Loaded device: " + loadedDeviceName());
        if(!string.IsNullOrEmpty(m_errorsText)) {
            GUILayout.Label(m_errorsText);
        }
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private IEnumerator SetupDeviceAndHostOrJoin(string device, string serverAddress)
    {
        selectedDeviceName = device;
        if (string.IsNullOrEmpty(serverAddress)) {
            StartHost();
        }
        else {
            StartClient(serverAddress);
        }
        yield break;
    }

    private void StartClient(string serverAddress)
    {
        m_errors.Clear();
        BuildText();
        m_serverAddress = serverAddress;
        PlayerPrefs.SetString("server_address", serverAddress);
        PlayerPrefs.Save();
        m_networkManager.networkAddress = serverAddress;
        m_networkManager.StartClient();
    }

    private void StartHost()
    {
        m_errors.Clear();
        BuildText();
        m_networkManager.StartHost();
    }

    private string m_errorsText;

    void INetworkErrorListener.OnClientError(NetworkConnection conn, int errorCode)
    {
        if(conn != null) {
            AddError("MultiCluster error from server " + conn.address + ". " +
                GetErrorString(errorCode) + ".");
        }
        else {
            AddError("MultiCluster error. " + GetErrorString(errorCode) + ".");
        }
    }

    void INetworkErrorListener.OnServerError(NetworkConnection conn, int errorCode)
    {
        if(conn != null) {
            AddError("MultiCluster error on client " + conn.address + ". " +
                GetErrorString(errorCode) + ".");
        }
        else {
            AddError("MultiCluster error. " + GetErrorString(errorCode) + ".");
        }
    }

    private void AddError(string errorMessage)
    {
        m_errors.AddFirst(new Error(Time.time, errorMessage));
        BuildText();
    }

    private static string GetErrorString(int errorCode)
    {
        switch(errorCode) {
        case (int) NetworkError.Ok: return "No error";
        case (int) NetworkError.ConnectionLost: return "Connection lost";
        case (int) NetworkError.ConnectionFailure: return "Connection failed";
        case (int) NetworkError.LicenseError: return "License error";
        }
        return "Unknown error";
    }

    private void BuildText()
    {
        m_errorsText = string.Empty;
        foreach(Error msg in m_errors) {
            m_errorsText += msg.text;
        }
    }

    public virtual void Update()
    {
        float removalTime = Time.time - m_timeout;
        bool changed = false;
        while(m_errors.Count > 0 && m_errors.Last.Value.timeIssued < removalTime) {
            m_errors.RemoveLast();
            changed = true;
        }
        if(changed) {
            BuildText();
        }
    }
}
