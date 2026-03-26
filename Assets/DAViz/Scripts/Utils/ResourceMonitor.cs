/*  ResourceMonitor.cs
 *  ══════════════════════════════════════════════════════════════════════
 *  Platform-aware performance guard for DAV VR.
 *  Drop in Assets/DAViz/Scripts/Utils/
 *
 *  Automatically detects device tier at runtime and enforces chart limits
 *  so the app never becomes unresponsive.
 *
 *  How it works:
 *    1. On first call, detects platform + GPU tier (via SystemInfo)
 *    2. Sets a MaxCharts value appropriate for the device
 *    3. FloatingPanel calls IsAtLimit() before spawning — if true, blocks
 *       and shows a timed in-world warning instead
 *    4. At 80% capacity, shows a softer yellow advisory warning
 *
 *  No MonoBehaviour needed — purely static.
 *  ══════════════════════════════════════════════════════════════════════ */

using UnityEngine;

public static class ResourceMonitor
{
    // ── Tier thresholds ───────────────────────────────────────────────────
    // DrawCalls per chart ≈ 42. Targeting 60fps.
    // Low-end Android GPU headroom: ~120 DCs → 2 charts
    // Mid-range: ~200 DCs → 4 charts
    // Flagship / PC: 500+ DCs → 10 charts

    enum DeviceTier { Low, Mid, High, PC }

    static int _maxCharts = -1;
    static DeviceTier _tier = DeviceTier.Mid;
    static bool _initialised;

    // ─────────────────────────────────────────────────────────────────────
    public static int GetMaxCharts()
    {
        if (!_initialised) Initialise();
        return _maxCharts;
    }

    public static bool IsNearLimit()
    {
        int active = Object.FindObjectsOfType<BarChartBuilder>().Length;
        return active >= Mathf.RoundToInt(GetMaxCharts() * 0.80f);
    }

    public static bool IsAtLimit()
    {
        int active = Object.FindObjectsOfType<BarChartBuilder>().Length;
        return active >= GetMaxCharts();
    }

    public static int ActiveCharts()
    {
        return Object.FindObjectsOfType<BarChartBuilder>().Length;
    }

    public static string TierName()
    {
        if (!_initialised) Initialise();
        switch (_tier)
        {
            case DeviceTier.Low: return "Low-end mobile";
            case DeviceTier.Mid: return "Mid-range mobile";
            case DeviceTier.High: return "Flagship mobile";
            default: return "Desktop";
        }
    }

    // ── In-world warning overlay ──────────────────────────────────────────
    // Creates a TextMesh warning that auto-destroys after `duration` seconds.
    public static void ShowWarning(string message, float duration = 3.0f)
    {
        Camera cam = Camera.main != null
            ? Camera.main
            : Object.FindObjectOfType<Camera>();
        if (cam == null) return;

        // Container
        GameObject root = new GameObject("DAViz_Warning");
        root.transform.position =
            cam.transform.position
            + cam.transform.forward * 1.10f
            + cam.transform.up * 0.32f;
        root.transform.rotation = cam.transform.rotation;

        // Background quad
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "WarnBG";
        bg.transform.SetParent(root.transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(0.68f, 0.14f, 1f);
        Object.Destroy(bg.GetComponent<Collider>());
        bg.GetComponent<Renderer>().material =
            new Material(ShaderHelper.Sprite())
            { color = new Color(0.10f, 0.06f, 0.02f, 0.88f) };

        // Warning border
        GameObject border = GameObject.CreatePrimitive(PrimitiveType.Quad);
        border.name = "WarnBorder";
        border.transform.SetParent(root.transform, false);
        border.transform.localPosition = new Vector3(0f, 0f, 0.001f);
        border.transform.localScale = new Vector3(0.70f, 0.155f, 1f);
        Object.Destroy(border.GetComponent<Collider>());
        border.GetComponent<Renderer>().material =
            new Material(ShaderHelper.Sprite())
            { color = new Color(1.00f, 0.58f, 0.05f, 0.80f) };
        border.transform.SetSiblingIndex(0);

        // Text
        GameObject textGO = new GameObject("WarnText");
        textGO.transform.SetParent(root.transform, false);
        textGO.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        textGO.transform.localScale = Vector3.one;
        var tm = textGO.AddComponent<TextMesh>();
        tm.text = message;
        tm.characterSize = 0.013f;
        tm.fontSize = 16;
        tm.color = new Color(1.0f, 0.85f, 0.25f, 1f);
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;

        // Auto-destroy
        Object.Destroy(root, duration);
    }

    // ── Advisory (soft, yellow) ───────────────────────────────────────────
    public static void ShowAdvisory()
    {
        int remaining = GetMaxCharts() - ActiveCharts();
        ShowWarning(
            "⚠  " + remaining + " chart slot" + (remaining == 1 ? "" : "s") +
            " remaining on this device\n(" + TierName() + " · max " + GetMaxCharts() + ")",
            2.5f);
    }

    // ── Hard limit notification ───────────────────────────────────────────
    public static void ShowLimitReached()
    {
        ShowWarning(
            "Chart limit reached  (" + GetMaxCharts() + "/" + GetMaxCharts() + ")\n" +
            "Delete a chart before adding another.",
            3.5f);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  INTERNAL — device tier detection
    // ─────────────────────────────────────────────────────────────────────
    static void Initialise()
    {
        _initialised = true;

        // Desktop / editor always gets the highest tier
        RuntimePlatform plat = Application.platform;
        if (plat == RuntimePlatform.WindowsPlayer ||
            plat == RuntimePlatform.OSXPlayer ||
            plat == RuntimePlatform.LinuxPlayer ||
            plat == RuntimePlatform.WindowsEditor ||
            plat == RuntimePlatform.OSXEditor ||
            plat == RuntimePlatform.LinuxEditor ||
            plat == RuntimePlatform.WebGLPlayer)
        {
            _tier = DeviceTier.PC;
            _maxCharts = 10;
            return;
        }

        // Mobile: use SystemInfo.graphicsMemorySize as GPU tier proxy
        int gpuMB = SystemInfo.graphicsMemorySize;

        if (gpuMB <= 512)
        {
            _tier = DeviceTier.Low;
            _maxCharts = 2;
        }
        else if (gpuMB <= 2048)
        {
            _tier = DeviceTier.Mid;
            _maxCharts = 4;
        }
        else
        {
            _tier = DeviceTier.High;
            _maxCharts = 6;
        }

        // Additional cap based on available system RAM
        int ramMB = SystemInfo.systemMemorySize;
        if (ramMB <= 2048 && _maxCharts > 2)
            _maxCharts = 2;
        else if (ramMB <= 4096 && _maxCharts > 4)
            _maxCharts = 4;

        Debug.Log("[DAViz] Device: " + TierName()
                  + " | GPU " + gpuMB + "MB"
                  + " | RAM " + ramMB + "MB"
                  + " | Max charts: " + _maxCharts);
    }
}