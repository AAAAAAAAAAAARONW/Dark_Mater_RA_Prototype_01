using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using getReal3D.MultiCluster;

public interface MultiClusterSetupScript
{
    void MultiClusterSetup(bool localPlayer);
}

/// <summary>
/// Script enabling the correct child transform according to the loaded VR device.
/// </summary>
/// <remarks>
/// After the child is enabled, all of its MultiClusterSetupScript components are called.
/// </remarks>
[RequireComponent(typeof(PlayerInputsProxy))]
public class MultiClusterPlayerSetup : NetworkBehaviour
{
    public Dictionary<string, string> m_deviceToObject = new Dictionary<string, string>()
    {
        { "", "[getReal3D]" },
        { "OpenVR", "[SteamVR]" },
    };

    void Start()
    {
        var hmdConnectionHUD = FindObjectOfType<HMDConnectionHUD>();
        Debug.Assert(hmdConnectionHUD != null);

        var deviceName = hmdConnectionHUD.selectedDeviceName;

        {
            var child = transform.Find(deviceName);
            if (child)
            {
                Setup(child);
                return;
            }
        }

        { 
            var child = transform.Find("[" + deviceName + "]");
            if (child)
            {
                Setup(child);
                return;
            }
        }

        try {
            string childName = m_deviceToObject[deviceName];
            Transform child = transform.Find(childName);
            if(child) {
                Setup(child);
            }
            else {
                Debug.LogError("Unable to find the child corresponding to the VR device " +
                    childName);
            }
        }
        catch(KeyNotFoundException) {
            Debug.LogError("Unable to find VR device in list: " + deviceName);
        }
    }

    void Setup(Transform target)
    {
        target.gameObject.SetActive(true);
        if(isLocalPlayer) {
            var playerInputsProxy = GetComponent<PlayerInputsProxy>();
            playerInputsProxy.target = target.GetComponent<PlayerInputs>();
            if(playerInputsProxy.target == null) {
                Debug.LogError("Failed to find PlayerInputs component in target.");
            }
            var wandEventModule = FindObjectOfType<GenericWandEventModule>();
            if(wandEventModule) {
                wandEventModule.playerInputs = playerInputsProxy;
            }
            else {
                Debug.LogError("Failed to find GenericWandEventModule component in scene.");
            }
        }
        foreach(var mcs in target.GetComponentsInChildren<MultiClusterSetupScript>()) {
            mcs.MultiClusterSetup(isLocalPlayer);
        }
    }
}
