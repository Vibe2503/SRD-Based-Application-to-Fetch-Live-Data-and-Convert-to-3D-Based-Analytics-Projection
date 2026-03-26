/*  ARBottomPanel.cs  — DAV VR  AR Edition
 *  Simple, robust AR panel for phone.
 *  Uses screen-space touch — no plane math, no world-space raycasts.
 */

using System.Collections.Generic;
using UnityEngine;

public class ARBottomPanel : MonoBehaviour
{
    // ── Layout ────────────────────────────────────────────────────────
    const float PW = 0.55f;
    const float PH = 0.10f;
    const float DIST = 2.50f;

    // ── State ─────────────────────────────────────────────────────────
    Camera _cam;
    GameObject _root;
    Vector3 _vel = Vector3.zero;
    bool _built = false;
    bool _placementMode = true;
    ARChartPlacer _placer;

    class Btn
    {
        public Renderer Rend;
        public TextMesh Lbl;
        public System.Action Click;
        public Color Nor, Hov;
    }
    List<Btn> _btns = new List<Btn>();

    static readonly string[] Tickers =
        { "AAPL","MSFT","GOOGL","AMZN","META","NVDA","TSLA","NFLX","JPM","V" };

    static readonly BarChartBuilder.MetricType[] Metrics =
    {
        BarChartBuilder.MetricType.Revenue,
        BarChartBuilder.MetricType.NetIncome,
        BarChartBuilder.MetricType.EPS,
        BarChartBuilder.MetricType.LivePrice,
    };
    static readonly string[] MetLabels = { "Rev", "Inc", "EPS", "$" };

    // ── Lifecycle ─────────────────────────────────────────────────────
    void Awake()
    {
        // Stay inactive until ARSessionSetup enables us
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        // Small delay to ensure Camera.main is ready
        StartCoroutine(InitDelayed());
    }

    System.Collections.IEnumerator InitDelayed()
    {
        yield return new WaitForSeconds(0.2f);
        ForceInit();
    }

    public void ForceInit()
    {
        _cam = Camera.main;
        _placer = FindObjectOfType<ARChartPlacer>();

        if (_cam == null)
        {
            Debug.LogWarning("[ARPanel] Camera.main is null — retrying");
            StartCoroutine(RetryInit());
            return;
        }

        if (_root != null) Destroy(_root);
        _btns.Clear();
        Build();
        _built = true;
        Debug.Log("[ARPanel] Built with " + _btns.Count + " buttons. Cam=" + _cam.name);
    }

    System.Collections.IEnumerator RetryInit()
    {
        yield return new WaitForSeconds(0.5f);
        ForceInit();
    }

    void Update()
    {
        if (!_built) return;
        LockToCamera();
        HandleInput();
    }

    // ── Lock panel to bottom of screen ────────────────────────────────
    void LockToCamera()
    {
        if (_root == null || _cam == null) return;
        Vector3 target = _cam.ViewportToWorldPoint(new Vector3(0.5f, 0.06f, DIST));
        _root.transform.position = Vector3.SmoothDamp(
            _root.transform.position, target, ref _vel, 0.04f);
        Vector3 dir = _cam.transform.position - _root.transform.position;
        if (dir.sqrMagnitude > 0.001f)
            _root.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
    }

    // ── Touch input — screen-space nearest button ─────────────────────
    void HandleInput()
    {
        bool tapped = false;
        Vector2 tapPos = Vector2.zero;

        // Primary: Unity touch API
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                tapped = true;
                tapPos = t.position;
                Debug.Log("[ARPanel] Input source: Touch API");
            }
        }

        // Fallback: mouse API — catches touch on devices where OpenXR
        // intercepts Input.touch and routes it as mouse button
        if (!tapped && Input.GetMouseButtonDown(0))
        {
            tapped = true;
            tapPos = Input.mousePosition;
            Debug.Log("[ARPanel] Input source: Mouse API (touch fallback)");
        }

        // Heartbeat every 300 frames so we know Update() is running
        if (Time.frameCount % 300 == 0)
            Debug.Log("[ARPanel] Heartbeat — touchCount=" + Input.touchCount
                      + " built=" + _built + " btns=" + _btns.Count);

        if (!tapped || _cam == null) return;

        // Log every tap — no height cutoff (was rejecting valid taps)
        Debug.Log("[ARPanel] RAW TAP y=" + tapPos.y
                  + " screenH=" + Screen.height
                  + " pct=" + (tapPos.y / Screen.height * 100f).ToString("F1") + "%");

        if (_btns.Count == 0) { ForceInit(); return; }

        // Find closest button in screen space
        Btn best = null;
        float bestD = float.MaxValue;

        foreach (Btn b in _btns)
        {
            if (b.Rend == null) continue;
            Vector3 sp = _cam.WorldToScreenPoint(b.Rend.bounds.center);
            if (sp.z <= 0f) continue;
            float d = Vector2.Distance(tapPos, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; best = b; }
        }

        string bestLabel = (best?.Lbl != null) ? best.Lbl.text : "none";
        Debug.Log("[ARPanel] Best btn='" + bestLabel
                  + "' dist=" + bestD.ToString("F1") + "px threshold=300");

        // 300px threshold — generous for any screen size / DPI
        if (best != null && bestD < 300f)
        {
            Debug.Log("[ARPanel] CLICK -> " + bestLabel);
            best.Click();
            StartCoroutine(Flash(best));
        }
        else
        {
            Debug.Log("[ARPanel] Tap missed — dist=" + bestD.ToString("F1") + " > 300px");
        }
    }

    System.Collections.IEnumerator Flash(Btn b)
    {
        if (b.Rend == null) yield break;
        b.Rend.material.color = b.Hov;
        yield return new WaitForSeconds(0.15f);
        b.Rend.material.color = b.Nor;
    }

    // ── Build panel ───────────────────────────────────────────────────
    void Build()
    {
        _btns.Clear();
        if (_root != null) Destroy(_root);
        _root = new GameObject("ARPanel_Root");

        float fz = -0.002f;
        float btnH = PH * 0.70f;
        float pad = 0.008f;

        // Background
        AddQuad("BG", Vector3.zero, new Vector3(PW, PH, 1f),
                new Color(0.05f, 0.05f, 0.08f, 0.94f), false);
        // Border
        AddQuad("Border", new Vector3(0, 0, 0.001f), new Vector3(PW + 0.006f, PH + 0.006f, 1f),
                new Color(0.4f, 0.4f, 0.5f, 0.6f), false);

        // Tickers (left 55%)
        float tkW = (PW * 0.55f - pad) / Tickers.Length;
        float tkX = -PW * 0.5f + pad;
        for (int i = 0; i < Tickers.Length; i++)
        {
            float cx = tkX + tkW * (i + 0.5f);
            string tk = Tickers[i];
            Color nor = new Color(0.14f, 0.14f, 0.20f, 1f);
            Color hov = new Color(0.10f, 0.45f, 0.85f, 1f);
            AddBtn(Tickers[i], new Vector3(cx, 0, fz), tkW - 0.003f, btnH,
                   nor, hov, () => OnTickerClick(tk));
        }

        // Metrics (next 30%)
        float metW = (PW * 0.30f) / Metrics.Length;
        float metX0 = PW * 0.5f - PW * 0.30f - pad;
        for (int i = 0; i < Metrics.Length; i++)
        {
            float cx = metX0 + metW * (i + 0.5f);
            Color nor = new Color(0.08f, 0.20f, 0.12f, 1f);
            Color hov = new Color(0.10f, 0.55f, 0.25f, 1f);
            var m = Metrics[i];
            string lbl = MetLabels[i];
            AddBtn(lbl, new Vector3(cx, 0, fz), metW - 0.003f, btnH,
                   nor, hov, () => OnMetricClick(m));
        }

        // ADD button (far right)
        float addW = PW * 0.10f;
        float addX = PW * 0.5f - addW * 0.5f - pad;
        AddBtn("+ ADD", new Vector3(addX, 0, fz), addW, btnH,
               new Color(0.05f, 0.35f, 0.65f, 1f),
               new Color(0.08f, 0.50f, 0.90f, 1f),
               TogglePlacement);

        // Initial position
        if (_cam != null)
            _root.transform.position = _cam.ViewportToWorldPoint(
                new Vector3(0.5f, 0.06f, DIST));

        Debug.Log("[ARPanel] Build complete — " + _btns.Count + " buttons");
    }

    // ── Add button ────────────────────────────────────────────────────
    void AddBtn(string label, Vector3 lpos, float w, float h,
                Color nor, Color hov, System.Action click)
    {
        var go = AddQuad("Btn_" + label, lpos, new Vector3(w, h, 1f), nor, false);

        var lblGO = new GameObject("Lbl_" + label);
        lblGO.transform.SetParent(_root.transform, false);
        lblGO.transform.localPosition = lpos + new Vector3(0, 0, -0.001f);
        lblGO.transform.localScale = Vector3.one;
        var tm = lblGO.AddComponent<TextMesh>();
        tm.text = label;
        tm.characterSize = 0.006f;
        tm.fontSize = 14;
        tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        lblGO.GetComponent<MeshRenderer>().sortingOrder = 3;

        _btns.Add(new Btn
        {
            Rend = go.GetComponent<Renderer>(),
            Lbl = tm,
            Click = click,
            Nor = nor,
            Hov = hov,
        });
    }

    GameObject AddQuad(string name, Vector3 lpos, Vector3 scale,
                       Color col, bool hasCollider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = lpos;
        go.transform.localScale = scale;
        if (!hasCollider) Object.Destroy(go.GetComponent<Collider>());
        var mat = new Material(ShaderHelper.Sprite());
        mat.color = col;
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    // ── Callbacks ─────────────────────────────────────────────────────
    void OnTickerClick(string tk)
    {
        Debug.Log("[ARPanel] OnTickerClick: " + tk);
        // Always find from scene — don't rely on static reference
        var b = FindObjectOfType<BarChartBuilder>();
        if (b == null)
        {
            Debug.Log("[ARPanel] No BarChartBuilder found — spawning");
            b = SpawnChart(tk);
        }
        else
        {
            Debug.Log("[ARPanel] Found builder: " + b.gameObject.name + " cam=" + (Camera.main != null ? Camera.main.name : "NULL"));
            BarChartBuilder.SetLastInteractedStatic(b);
            b.selectedTicker = tk;
            b._builtTicker = null;
            b.BuildChart();
        }
        ARHintUI.Show(tk);
    }

    void OnMetricClick(BarChartBuilder.MetricType m)
    {
        Debug.Log("[ARPanel] OnMetricClick: " + m);
        var b = FindObjectOfType<BarChartBuilder>();
        if (b == null) b = SpawnChart("AAPL");
        if (b == null) return;
        BarChartBuilder.SetLastInteractedStatic(b);
        b.metricToShow = m;
        b._builtTicker = null;
        b.BuildChart();
        ARHintUI.Show(m.ToString());
    }

    BarChartBuilder SpawnChart(string ticker)
    {
        // BarChartBuilder.Start() -> BuildNextFrame() will call BuildChart()
        // which calls PlaceInFrontOfCam() using distanceFromCamera
        var go = new GameObject("ARChart_" + ticker);
        var builder = go.AddComponent<BarChartBuilder>();
        builder.selectedTicker = ticker;
        builder.metricToShow = BarChartBuilder.MetricType.Revenue;
        builder.cubeWidth = 0.35f;
        builder.cubeHeight = 0.28f;
        builder.cubeDepth = 0.12f;
        builder.distanceFromCamera = 1.5f;
        builder.heightOffset = 0f;
        // Scale starts at 1 — let BarChartBuilder handle its own animation
        BarChartBuilder.SetLastInteractedStatic(builder);
        Debug.Log("[ARPanel] Spawned chart: " + ticker);
        return builder;
    }

    void TogglePlacement()
    {
        _placementMode = !_placementMode;
        if (_placer != null) _placer.SetPlacementMode(_placementMode);
        ARHintUI.Show(_placementMode ? "Tap floor to place chart" : "Tap chart to select");
        Debug.Log("[ARPanel] PlacementMode: " + _placementMode);
    }
}