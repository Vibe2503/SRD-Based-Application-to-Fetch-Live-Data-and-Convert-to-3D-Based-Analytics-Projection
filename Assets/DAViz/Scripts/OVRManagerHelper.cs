/*  OVRManagerHelper.cs  — DAV VR
 *
 *  Wrapper around Meta Quest passthrough.
 *  Add this component to the same GameObject as OVRManager in your scene.
 *
 *  Requires Oculus XR Plugin / Meta XR SDK installed.
 *  If OVRManager is not present this script logs a warning and does nothing.
 *
 *  Scene setup for Quest passthrough:
 *    1. Add OVRCameraRig to scene (replaces XR Origin)
 *    2. On OVRCameraRig: Add OVRManager component
 *    3. On OVRCameraRig: Add OVRPassthroughLayer component
 *       - set Placement = Underlay
 *    4. Add OVRManagerHelper to same GO
 *    5. On each eye camera: set Clear Flags = Solid Color, BG = (0,0,0,0)
 */

using UnityEngine;

public class OVRManagerHelper : MonoBehaviour
{
    [Header("Quest Passthrough")]
    public bool enablePassthroughOnStart = false;

    void Start()
    {
        if (enablePassthroughOnStart)
            EnablePassthrough();
    }

    public void EnablePassthrough()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var ovrManager = GetComponent<OVRManager>();
        if (ovrManager != null)
        {
            ovrManager.isInsightPassthroughEnabled = true;
            Debug.Log("[OVRHelper] InsightPassthrough enabled on OVRManager");
        }
        else
        {
            Debug.LogWarning("[OVRHelper] OVRManager not found on " + gameObject.name);
        }

        var layer = GetComponent<OVRPassthroughLayer>();
        if (layer == null) layer = GetComponentInChildren<OVRPassthroughLayer>();
        if (layer != null)
        {
            layer.enabled = true;
            Debug.Log("[OVRHelper] OVRPassthroughLayer enabled");
        }
        else
        {
            Debug.LogWarning("[OVRHelper] OVRPassthroughLayer not found — add it in Inspector");
        }
#else
        Debug.Log("[OVRHelper] Passthrough skipped in Editor");
#endif
    }

    public void DisablePassthrough()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var layer = GetComponent<OVRPassthroughLayer>();
        if (layer == null) layer = GetComponentInChildren<OVRPassthroughLayer>();
        if (layer != null) layer.enabled = false;
#endif
    }
}
