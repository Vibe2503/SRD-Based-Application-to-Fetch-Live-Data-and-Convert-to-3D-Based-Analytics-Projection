/*  DisableXRRays.cs  — DAV VR
 *  Add this to any GameObject in the scene (e.g. ChartManager).
 *  Disables all MRTK / XR raycaster line renderers on Start so the
 *  pink debug rays don't appear in builds.
 */
using UnityEngine;

public class DisableXRRays : MonoBehaviour
{
    void Awake()
    {
        // Kill every LineRenderer in the scene that isn't ours
        LineRenderer[] lines = FindObjectsOfType<LineRenderer>();
        foreach (LineRenderer lr in lines)
        {
            // Only disable ones that look like XR pointer rays
            // (magenta/pink colour or on an XR rig object)
            string goName = lr.gameObject.name.ToLower();
            if (goName.Contains("ray") || goName.Contains("pointer") ||
                goName.Contains("interactor") || goName.Contains("mrtk") ||
                goName.Contains("xr") || goName.Contains("controller"))
            {
                lr.enabled = false;
                lr.gameObject.SetActive(false);
            }
        }

        // Also disable any XR Interaction Manager raycasters
        var xrManagers = FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in xrManagers)
        {
            string typeName = mb.GetType().Name.ToLower();
            if (typeName.Contains("raycaster") || typeName.Contains("interactor") ||
                typeName.Contains("mrtk") || typeName.Contains("pointerhandler"))
            {
                mb.enabled = false;
            }
        }
    }
}
