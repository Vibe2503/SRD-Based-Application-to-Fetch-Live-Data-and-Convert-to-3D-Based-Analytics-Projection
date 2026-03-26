/*  ARCameraSetup.cs  — DAV VR
 *
 *  Handles camera background for three modes:
 *    1. Editor / PC       → skybox
 *    2. Android phone     → AR Camera Background (ARCore)
 *    3. Meta Quest 3      → OVR Passthrough layer
 *
 *  Does NOT import AR Foundation or OVR directly — uses
 *  component lookup by type name to avoid assembly conflicts.
 *
 *  REQUIRES on Main Camera:
 *    - Camera component
 *    - AR Camera Manager     (for phone AR)
 *    - AR Camera Background  (for phone AR)
 *    - ARCameraSetup         (this script)
 *
 *  REQUIRES on a scene GameObject (for Quest):
 *    - OVRManager            (on a root GO)
 *    - OVRPassthroughLayer   (on same or child GO)
 */

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ARCameraSetup : MonoBehaviour
{
    Camera _cam;
    Behaviour _arBackground;   // ARCameraBackground — found by type name

    void Awake()
    {
        _cam = GetComponent<Camera>();

        // Find ARCameraBackground without hard AR Foundation reference
        foreach (var b in GetComponents<Behaviour>())
        {
            if (b.GetType().Name == "ARCameraBackground")
            {
                _arBackground = b;
                break;
            }
        }

        // Start disabled — ARSessionSetup calls Enable/Disable
        if (_arBackground != null) _arBackground.enabled = false;

        // Safe dark default
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);
    }

    // ── Called by ARSessionSetup.ActivateAR() ─────────────────────────
    public void EnableCameraBackground()
    {
        string model = SystemInfo.deviceModel.ToLower();
        bool isQuest = model.Contains("quest") || model.Contains("oculus");

        if (isQuest)
            EnableQuestPassthrough();
        else
            EnableARCoreBackground();
    }

    // ── Called by ARSessionSetup.Activate3D() ─────────────────────────
    public void DisableCameraBackground()
    {
        if (_arBackground != null) _arBackground.enabled = false;
        DisableQuestPassthrough();
        _cam.clearFlags = CameraClearFlags.Skybox;
        Debug.Log("[ARCameraSetup] Background disabled — skybox restored");
    }

    // ── ARCore phone camera feed ──────────────────────────────────────
    void EnableARCoreBackground()
    {
        if (_arBackground == null)
        {
            Debug.LogWarning("[ARCameraSetup] ARCameraBackground missing on " +
                             gameObject.name + " — add it in Inspector");
            return;
        }
        _arBackground.enabled = true;
        _cam.clearFlags = CameraClearFlags.Depth;
        Debug.Log("[ARCameraSetup] ARCore camera background ENABLED");
    }

    // ── Meta Quest passthrough ────────────────────────────────────────
    void EnableQuestPassthrough()
    {
        // Find OVRManager and enable passthrough
        var ovrManager = FindObjectOfType<OVRManagerHelper>();
        if (ovrManager != null)
        {
            ovrManager.EnablePassthrough();
        }
        else
        {
            // Try via reflection — avoids hard Oculus SDK dependency
            EnablePassthroughViaReflection();
        }

        // Camera must use Depth clear so passthrough shows behind scene
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.clear;
        Debug.Log("[ARCameraSetup] Quest passthrough ENABLED");
    }

    void DisableQuestPassthrough()
    {
        var ovrManager = FindObjectOfType<OVRManagerHelper>();
        if (ovrManager != null) ovrManager.DisablePassthrough();
    }

    void EnablePassthroughViaReflection()
    {
        // Find OVRPassthroughLayer component anywhere in scene
        var allBehaviours = FindObjectsOfType<Behaviour>();
        foreach (var b in allBehaviours)
        {
            if (b.GetType().Name == "OVRPassthroughLayer")
            {
                b.enabled = true;
                Debug.Log("[ARCameraSetup] OVRPassthroughLayer enabled via reflection");
                return;
            }
        }

        // Find OVRManager and set InsightPassthroughEnabled
        foreach (var b in allBehaviours)
        {
            if (b.GetType().Name == "OVRManager")
            {
                var field = b.GetType().GetField("isInsightPassthroughEnabled");
                if (field != null) field.SetValue(b, true);
                Debug.Log("[ARCameraSetup] OVRManager passthrough enabled via reflection");
                return;
            }
        }

        Debug.LogWarning("[ARCameraSetup] No OVRPassthroughLayer or OVRManager found in scene");
    }
}