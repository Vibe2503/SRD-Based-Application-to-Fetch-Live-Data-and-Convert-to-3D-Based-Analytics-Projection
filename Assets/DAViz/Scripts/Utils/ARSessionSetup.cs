/*  ARSessionSetup.cs  — DAV VR
 *
 *  Detects whether ARCore is available and routes the app into AR or 3D mode.
 *
 *  CRITICAL: This script must be on a separate "ARSetup" GameObject.
 *            Do NOT put it on "AR Session" — it will disable itself when
 *            Activate3D() runs and turns off the AR Session GO.
 *
 *  AR mode  : real camera feed, ARBottomPanel, ARChartPlacer
 *  3D mode  : FreeCam, FloatingPanel, skybox, AR Session GO disabled
 */

using System.Collections;
using UnityEngine;

public class ARSessionSetup : MonoBehaviour
{
    // Static flag read by other scripts to know which mode is active
    public static bool ARActive = false;

    // -------------------------------------------------------------------------
    // Awake — disable AR-only components so nothing fires before mode is known
    // -------------------------------------------------------------------------
    void Awake()
    {
        // Hide AR panel until mode confirmed
        var arPanel = FindObjectOfType<ARBottomPanel>();
        if (arPanel != null) arPanel.gameObject.SetActive(false);

        // Disable placer until AR confirmed
        var placer = FindObjectOfType<ARChartPlacer>();
        if (placer != null) placer.enabled = false;

        // Kill any leftover MRTK / XR ray LineRenderers
        foreach (LineRenderer lr in FindObjectsOfType<LineRenderer>())
        {
            string n = lr.gameObject.name.ToLower();
            if (n.Contains("ray") || n.Contains("pointer") ||
                n.Contains("interactor") || n.Contains("mrtk") ||
                n.Contains("xr") || n.Contains("controller"))
            {
                lr.enabled = false;
                lr.gameObject.SetActive(false);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Start — auto-create ARHintUI if missing, then detect mode
    // -------------------------------------------------------------------------
    void Start()
    {
        if (FindObjectOfType<ARHintUI>() == null)
            new GameObject("ARHintUI_Auto").AddComponent<ARHintUI>();

        StartCoroutine(Detect());
    }

    // -------------------------------------------------------------------------
    // Detect — one frame delay so all other Start() calls finish first
    // -------------------------------------------------------------------------
    IEnumerator Detect()
    {
        yield return null;   // wait one frame

        bool isAR = !Application.isEditor
                 && Application.platform == RuntimePlatform.Android
                 && CheckARCore();

        Debug.Log("[DAViz] Mode detected: " + (isAR ? "AR" : "3D"));

        if (isAR) ActivateAR();
        else Activate3D();
    }

    // -------------------------------------------------------------------------
    // CheckARCore — queries ARCore availability via JNI
    // -------------------------------------------------------------------------
    bool CheckARCore()
    {
        // Check for Meta Quest device first
        if (IsQuestDevice())
        {
            Debug.Log("[DAViz] Meta Quest detected — using passthrough AR mode");
            return true;
        }

        // ARCore check for standard Android phones
        try
        {
            var arCoreApk = new AndroidJavaClass("com.google.ar.core.ArCoreApk");
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var avail = arCoreApk.CallStatic<AndroidJavaObject>("getInstance")
                                       .Call<AndroidJavaObject>("checkAvailability", activity);
            string s = avail.Call<string>("toString");
            Debug.Log("[DAViz] ARCore availability: " + s);
            return s.StartsWith("SUPPORTED");
        }
        catch (System.Exception ex)
        {
            Debug.Log("[DAViz] ARCore check failed: " + ex.Message);
            return false;
        }
    }

    bool IsQuestDevice()
    {
        // Meta Quest devices report manufacturer as "Oculus" or model contains "Quest"
        try
        {
            string model = SystemInfo.deviceModel.ToLower();
            string name = SystemInfo.deviceName.ToLower();
            bool isQuest = model.Contains("quest") || name.Contains("quest")
                        || model.Contains("oculus") || name.Contains("oculus");
            Debug.Log("[DAViz] Device model=" + SystemInfo.deviceModel + " isQuest=" + isQuest);
            return isQuest;
        }
        catch
        {
            return false;
        }
    }

    // =========================================================================
    // AR MODE
    // =========================================================================
    void ActivateAR()
    {
        ARActive = true;

        // --- Camera: enable real camera feed ---
        // ARCameraSetup is on Main Camera — tell it to show the live feed
        var camSetup = Camera.main != null
                     ? Camera.main.GetComponent<ARCameraSetup>()
                     : null;
        if (camSetup != null)
            camSetup.EnableCameraBackground();
        else
            Debug.LogWarning("[DAViz] ARCameraSetup not found on Main Camera — camera feed may not show.");

        // --- Disable 3D-only components ---
        var freeCam = FindObjectOfType<FreeCam>();
        if (freeCam != null) freeCam.enabled = false;

        var fp = FindObjectOfType<FloatingPanel>();
        if (fp != null) fp.gameObject.SetActive(false);

        // --- Enable AR-only components ---
        var arPanel = FindObjectOfType<ARBottomPanel>();
        if (arPanel != null)
        {
            arPanel.gameObject.SetActive(true);
            arPanel.ForceInit();
        }

        var placer = FindObjectOfType<ARChartPlacer>();
        if (placer != null) placer.enabled = true;

        // Register existing chart so panel buttons work immediately
        StartCoroutine(RegisterExistingChart());

        ARHintUI.Show("Loading...");
        Debug.Log("[DAViz] AR mode active.");
    }

    // Waits for BarChartBuilder.Start() + BuildNextFrame() to complete,
    // then registers it so ARBottomPanel buttons are wired up.
    IEnumerator RegisterExistingChart()
    {
        yield return new WaitForSeconds(0.5f);

        var builder = FindObjectOfType<BarChartBuilder>();
        if (builder != null)
        {
            BarChartBuilder.SetLastInteractedStatic(builder);
            ARHintUI.Show("Tap buttons to explore data");
            Debug.Log("[DAViz] Chart registered: " + builder.selectedTicker);
        }
        else
        {
            Debug.LogWarning("[DAViz] No BarChartBuilder found — check ChartManager GO.");
            ARHintUI.Show("No chart found — check scene setup");
        }
    }

    // =========================================================================
    // 3D MODE
    // =========================================================================
    void Activate3D()
    {
        ARActive = false;

        // --- Camera: restore skybox ---
        var camSetup = Camera.main != null
                     ? Camera.main.GetComponent<ARCameraSetup>()
                     : null;
        if (camSetup != null)
            camSetup.DisableCameraBackground();

        // Disable AR Session GO — prevents black screen and ARCore init in 3D mode
        // NOTE: "this.gameObject" is "ARSetup", NOT "AR Session" — safe to run
        GameObject arSessionGO = GameObject.Find("AR Session");
        if (arSessionGO != null && arSessionGO != this.gameObject)
            arSessionGO.SetActive(false);

        // --- Enable 3D-only components ---
        var freeCam = FindObjectOfType<FreeCam>();
        if (freeCam != null) freeCam.enabled = true;

        var fp = FindObjectOfType<FloatingPanel>();
        if (fp != null) fp.gameObject.SetActive(true);

        // --- Disable AR-only components ---
        var arPanel = FindObjectOfType<ARBottomPanel>();
        if (arPanel != null) arPanel.gameObject.SetActive(false);

        var placer = FindObjectOfType<ARChartPlacer>();
        if (placer != null) placer.enabled = false;

        ARHintUI.Hide();
        Debug.Log("[DAViz] 3D mode active.");
    }
}