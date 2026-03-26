/*  FloatingPanel.cs  — DAV VR · Cross-Platform Edition
 *  ══════════════════════════════════════════════════════════════════════
 *  Works identically on:
 *    ✓ PC / Mac / Linux (mouse + keyboard)
 *    ✓ Android phone / tablet (touch + pinch)
 *    ✓ iOS phone / tablet (touch + pinch)
 *    ✓ WebGL browser (mouse + touch)
 *    ✗ HoloLens — removed
 *
 *  Changes from previous version:
 *    • ALL Input.mousePosition / GetMouseButton calls → DAVizInput
 *    • SpawnClick() now checks ResourceMonitor before spawning
 *    • ResourceMonitor shows in-world warning overlay at 80% + 100% capacity
 *    • FindCam() no longer looks for "Camera Offset" (MRTK-specific)
 *    • No MRTK, no OpenXR, no UWP-only code
 *
 *  Dependencies (must also be in Assets/DAViz/Scripts/Utils/):
 *    DAVizInput.cs
 *    ResourceMonitor.cs
 *  ══════════════════════════════════════════════════════════════════════ */

using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class FloatingPanel : MonoBehaviour
{
    // ── Panel size ────────────────────────────────────────────────────────
    [Header("Panel Dimensions")]
    public float PW = 2.20f;
    public float PH = 0.55f;
    public float PD = 0.008f;

    // ── HUD lock ──────────────────────────────────────────────────────────
    [Header("HUD Lock")]
    public bool LockToView = true;
    public float LockDist = 2.8f;
    public float LockVertOffset = -0.42f;
    public float LockHorizOffset = 0.0f;
    public float LockSmoothTime = 0.08f;

    [Header("Spawn Position (when LockToView = false)")]
    public float StartDist = 2.8f;
    public float StartSide = 0.0f;
    public float StartHeight = -0.42f;

    // ── Text sizes ────────────────────────────────────────────────────────
    [Header("Text Sizes")]
    public float TickerTextSize = 0.030f;
    public float MetricTextSize = 0.022f;
    public float BtnTextSize = 0.026f;

    // ── Colours ───────────────────────────────────────────────────────────
    [Header("Panel Colours")]
    public Color PanelBgColor = new Color(0.06f, 0.06f, 0.08f, 0.82f);
    public Color BorderColor = new Color(0.55f, 0.55f, 0.60f, 0.60f);
    public Color CardFaceColor = new Color(0.96f, 0.96f, 0.98f, 1.00f);
    public Color CardHovColor = new Color(0.80f, 0.93f, 1.00f, 1.00f);
    public Color CardSelColor = new Color(0.55f, 0.85f, 0.55f, 1.00f);
    public Color CardTextNor = new Color(0.08f, 0.08f, 0.10f, 1.00f);
    public Color CardTextSel = new Color(0.04f, 0.38f, 0.10f, 1.00f);
    public Color MetricBgColor = new Color(0.10f, 0.10f, 0.13f, 0.90f);
    public Color MetricBgSel = new Color(0.04f, 0.22f, 0.10f, 1.00f);
    public Color MetricDivColor = new Color(0.55f, 0.55f, 0.60f, 0.55f);
    public Color MetricTextNor = new Color(0.82f, 0.82f, 0.85f, 1.00f);
    public Color MetricTextSel = new Color(0.30f, 1.00f, 0.55f, 1.00f);
    public Color AddBtnColor = new Color(0.12f, 0.72f, 0.12f, 1.00f);
    public Color DelBtnColor = new Color(0.85f, 0.10f, 0.10f, 1.00f);
    public Color BtnTextColor = Color.white;
    public Color IconBgColor = new Color(0.15f, 0.15f, 0.18f, 1.00f);
    public Color IconBorderColor = new Color(0.55f, 0.55f, 0.60f, 0.70f);

    // Colour used to tint ADD button when at/near resource limit
    Color _addBtnWarnColor = new Color(0.90f, 0.55f, 0.05f, 1.00f);
    Color _addBtnBlockColor = new Color(0.45f, 0.45f, 0.45f, 1.00f);

    // ── Textures ──────────────────────────────────────────────────────────
    [Header("Textures (optional)")]
    public Texture2D PanelBgTexture;
    public Texture2D CardTexture;
    public Texture2D AddBtnTexture;
    public Texture2D DelBtnTexture;

    // ── Runtime ───────────────────────────────────────────────────────────
    Camera _cam;
    GameObject _root;
    List<HitBtn> _btns = new List<HitBtn>();
    HitBtn _hov;
    HitBtn _addBtn;   // reference kept so we can tint it
    HitBtn _liveBtn;  // Live/Mock toggle
    HitBtn _cmpBtn;   // Compare mode toggle
    bool _drag;
    Vector3 _dOff;
    float _dDist;
    bool _over;
    Vector3 _hudVel;

    // ── Panel open / close ────────────────────────────────────────────────
    bool _panelOpen = true;
    GameObject _openBtnRoot = null;   // the small floating toggle button
    Renderer _openBtnRend = null;
    TextMesh _openBtnLbl = null;
    Vector3 _openBtnVel = Vector3.zero;

    // Toggle button visual constants
    const float TB_W = 0.18f;
    const float TB_H = 0.18f;
    // Position: bottom-centre of view, same depth as panel
    const float TB_DIST = 2.80f;
    const float TB_VERT_OFFSET = -0.38f;
    const float TB_HORIZ_OFFSET = 0.0f;

    static readonly string[] Tickers =
        { "AAPL", "MSFT", "GOOGL", "AMZN", "META", "NVDA", "TSLA", "NFLX", "JPM", "V" };

    static readonly BarChartBuilder.MetricType[] Metrics =
    {
        BarChartBuilder.MetricType.Revenue,
        BarChartBuilder.MetricType.NetIncome,
        BarChartBuilder.MetricType.TotalAssets,
        BarChartBuilder.MetricType.TotalLiabilities,
        BarChartBuilder.MetricType.EPS,
        BarChartBuilder.MetricType.LivePrice,
    };

    static readonly string[] MetLabels =
        { "Revenue", "Net Inc.", "Assets", "Liability", "EPS", "$ Price" };

    class HitBtn
    {
        public Renderer Rend;
        public TextMesh Lbl;
        public System.Action Click;
        public Color Nor, Hov, Sel;
        public bool IsSel;
        public string TK;
        public BarChartBuilder.MetricType? MK;
        public string ChartTypeKey;   // "Bar" / "Candle" / "Line"
    }

    // ── Chart type state ─────────────────────────────────────────────────
    static string _activeChartType = "Bar";   // persists across panel rebuilds

    static readonly string[] ChartTypeKeys = { "Bar", "Candle", "Line" };
    static readonly string[] ChartTypeLabels = { "Bar", "Candle", "Line" };
    static readonly Color ChartTypeBg = new Color(0.10f, 0.10f, 0.14f, 0.95f);
    static readonly Color ChartTypeSel = new Color(0.05f, 0.30f, 0.55f, 1.00f);
    static readonly Color ChartTypeHov = new Color(0.18f, 0.28f, 0.45f, 1.00f);
    static readonly Color ChartTypeText = new Color(0.80f, 0.85f, 1.00f, 1.00f);
    static readonly Color ChartTypeTextSel = new Color(1.00f, 1.00f, 1.00f, 1.00f);

    // =========================================================
    void Start()
    {
        FindCam();
        Build();
        BuildOpenBtn();
        // Panel starts open so open button must start hidden
        if (_openBtnRoot != null) _openBtnRoot.SetActive(false);
    }

    void Update()
    {
        if (LockToView) LockHUD();
        LockOpenBtn();
        DoInput();
        DoOpenBtnInput();
        DoRefresh();
        RefreshAddBtnTint();
    }

    public bool IsPointerOver() { return _over; }

    // Legacy name kept so BarChartBuilder.IsMouseOverPanel() still compiles
    public bool IsMouseOver() { return _over; }

    // ── HUD lock ──────────────────────────────────────────────────────────
    void LockHUD()
    {
        if (!_root || !_cam) return;
        Vector3 target = _cam.transform.position
                       + _cam.transform.forward * LockDist
                       + _cam.transform.up * LockVertOffset
                       + _cam.transform.right * LockHorizOffset;

        if (LockSmoothTime <= 0f)
            _root.transform.position = target;
        else
            _root.transform.position = Vector3.SmoothDamp(
                _root.transform.position, target, ref _hudVel,
                LockSmoothTime, float.MaxValue, Time.deltaTime);

        Vector3 tc = _cam.transform.position - _root.transform.position;
        if (tc.sqrMagnitude > 0.001f)
            _root.transform.rotation = Quaternion.LookRotation(-tc, Vector3.up);
    }

    // ── Open button HUD lock — viewport-based so it's ALWAYS at screen left ──
    void LockOpenBtn()
    {
        if (_openBtnRoot == null || !_cam) return;
        if (!_openBtnRoot.activeSelf) return;  // skip position update when hidden

        // Viewport (0,0)=bottom-left (1,1)=top-right, z=distance from cam
        // We want left-centre: x=0.04 (just inside left edge), y=0.50 (middle height)
        Vector3 vp = new Vector3(0.50f, 0.50f, LockDist);
        Vector3 target = _cam.ViewportToWorldPoint(vp);

        _openBtnRoot.transform.position = Vector3.SmoothDamp(
            _openBtnRoot.transform.position, target,
            ref _openBtnVel, 0.05f, float.MaxValue, Time.deltaTime);

        Vector3 tc = _cam.transform.position - _openBtnRoot.transform.position;
        if (tc.sqrMagnitude > 0.001f)
            _openBtnRoot.transform.rotation = Quaternion.LookRotation(-tc, Vector3.up);
    }

    // ── Toggle button input ───────────────────────────────────────────────
    void DoOpenBtnInput()
    {
        if (_openBtnRoot == null || !_cam) return;
        if (!_openBtnRoot.activeSelf) return;

        if (DAVizInput.IsTouchDevice)
        {
            // Touch: screen-space distance to ☰ button
            if (Input.touchCount == 0) return;
            Touch t = Input.GetTouch(0);
            if (t.phase != TouchPhase.Began) return;

            if (_openBtnRend == null) return;
            Vector3 sp = _cam.WorldToScreenPoint(_openBtnRend.bounds.center);
            if (sp.z <= 0f) return;

            float d = Vector2.Distance(t.position, new Vector2(sp.x, sp.y));
            Debug.Log("[FP] OpenBtn touch dist=" + d.ToString("F1") + "px");
            if (d < 200f) TogglePanel();
        }
        else
        {
            // Mouse: plane raycast (original)
            Ray r = DAVizInput.PointerRay(_cam);
            Vector3 tn = _openBtnRoot.transform.forward;
            Vector3 tc2 = _openBtnRoot.transform.position;
            float den = Vector3.Dot(tn, r.direction);
            if (Mathf.Abs(den) < 0.0001f) return;

            float t2 = Vector3.Dot(tc2 - r.origin, tn) / den;
            if (t2 <= 0f) return;

            Vector3 lp = _openBtnRoot.transform.InverseTransformPoint(r.GetPoint(t2));
            bool over = Mathf.Abs(lp.x) <= TB_W * 0.5f + 0.02f &&
                        Mathf.Abs(lp.y) <= TB_H * 0.5f + 0.02f;

            if (_openBtnRend)
            {
                Color base2 = _panelOpen
                    ? new Color(0.05f, 0.35f, 0.65f, 0.95f)
                    : new Color(0.06f, 0.06f, 0.10f, 0.95f);
                _openBtnRend.material.color = over
                    ? Color.Lerp(base2, Color.white, 0.22f) : base2;
            }

            if (DAVizInput.PrimaryDown && over)
                TogglePanel();
        }
    }

    // ── Build the small floating toggle button ────────────────────────────
    void BuildOpenBtn()
    {
        if (_openBtnRoot != null) Destroy(_openBtnRoot);

        _openBtnRoot = new GameObject("FP_OpenBtn");

        // Background quad
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "OB_BG";
        bg.transform.SetParent(_openBtnRoot.transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(TB_W, TB_H, 1f);
        bg.transform.localRotation = Quaternion.identity;
        Destroy(bg.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.06f, 0.06f, 0.10f, 0.95f);
        _openBtnRend = bg.GetComponent<Renderer>();
        _openBtnRend.material = mat;
        _openBtnRend.sortingOrder = 1;

        // Border
        var brd = GameObject.CreatePrimitive(PrimitiveType.Quad);
        brd.name = "OB_Border";
        brd.transform.SetParent(_openBtnRoot.transform, false);
        brd.transform.localPosition = new Vector3(0, 0, 0.001f);
        brd.transform.localScale = new Vector3(TB_W + 0.010f, TB_H + 0.010f, 1f);
        brd.transform.localRotation = Quaternion.identity;
        Destroy(brd.GetComponent<Collider>());
        var bmat = new Material(Shader.Find("Sprites/Default"));
        bmat.color = new Color(0.55f, 0.55f, 0.60f, 0.70f);
        brd.GetComponent<Renderer>().material = bmat;
        brd.GetComponent<Renderer>().sortingOrder = -1;

        // Gloss shine
        var shine = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shine.name = "OB_Shine";
        shine.transform.SetParent(_openBtnRoot.transform, false);
        shine.transform.localPosition = new Vector3(0, TB_H * 0.22f, -0.001f);
        shine.transform.localScale = new Vector3(TB_W - 0.010f, TB_H * 0.28f, 1f);
        shine.transform.localRotation = Quaternion.identity;
        Destroy(shine.GetComponent<Collider>());
        var smat = new Material(Shader.Find("Sprites/Default"));
        smat.color = new Color(1f, 1f, 1f, 0.10f);
        shine.GetComponent<Renderer>().material = smat;
        shine.GetComponent<Renderer>().sortingOrder = 2;

        // Icon label  ☰ = panel closed,  ✕ = panel open
        var lblGo = new GameObject("OB_Lbl");
        lblGo.transform.SetParent(_openBtnRoot.transform, false);
        lblGo.transform.localPosition = new Vector3(0, 0, -0.002f);
        lblGo.transform.localRotation = Quaternion.identity;
        lblGo.transform.localScale = Vector3.one;
        _openBtnLbl = lblGo.AddComponent<TextMesh>();
        _openBtnLbl.text = "☰";   // always open icon
        _openBtnLbl.characterSize = 0.030f;
        _openBtnLbl.fontSize = 16;
        _openBtnLbl.color = new Color(0.85f, 0.95f, 1.00f, 1f);
        _openBtnLbl.anchor = TextAnchor.MiddleCenter;
        _openBtnLbl.alignment = TextAlignment.Center;
        _openBtnLbl.fontStyle = FontStyle.Bold;
        var mr = lblGo.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 3;

        // Place immediately using viewport coords so it's always at screen left
        if (_cam)
        {
            Vector3 vp = new Vector3(0.50f, 0.50f, LockDist);
            _openBtnRoot.transform.position = _cam.ViewportToWorldPoint(vp);
            Vector3 tc3 = _cam.transform.position - _openBtnRoot.transform.position;
            if (tc3.sqrMagnitude > 0.001f)
                _openBtnRoot.transform.rotation =
                    Quaternion.LookRotation(-tc3, Vector3.up);
        }
    }

    // ── Open / close panel with scale animation ───────────────────────────
    void TogglePanel()
    {
        _panelOpen = !_panelOpen;

        // Open button always shows ☰ (it only appears when panel is closed)
        if (_openBtnLbl != null)
            _openBtnLbl.text = "☰";

        // Show open button only when panel is CLOSED — small delay so
        // the same tap that closed the panel doesn't immediately reopen it
        if (_openBtnRoot != null)
        {
            if (!_panelOpen)
                StartCoroutine(ShowOpenBtnDelayed());
            else
                _openBtnRoot.SetActive(false);
        }

        if (_panelOpen)
        {
            // Show panel — scale from zero with DOTween bounce
            if (_root != null)
            {
                _root.SetActive(true);
                _root.transform.localScale = Vector3.zero;
                _root.transform
                    .DOScale(Vector3.one, 0.28f)
                    .SetEase(DG.Tweening.Ease.OutBack);
            }
            // Show dropdown tab too
            if (_ddRoot != null) _ddRoot.SetActive(true);
        }
        else
        {
            // Hide panel — scale to zero then deactivate
            if (_root != null)
            {
                var capturedRoot = _root;
                capturedRoot.transform
                    .DOScale(Vector3.zero, 0.18f)
                    .SetEase(DG.Tweening.Ease.InBack)
                    .SetAutoKill(true)
                    .OnComplete(() =>
                    {
                        if (capturedRoot != null)
                            capturedRoot.SetActive(false);
                    });
            }
            if (_ddRoot != null) _ddRoot.SetActive(false);
            _over = false;
        }
    }

    // ── Delay showing ☰ so close-tap doesn't immediately reopen ─────────
    System.Collections.IEnumerator ShowOpenBtnDelayed()
    {
        yield return new WaitForSeconds(0.25f);
        if (_openBtnRoot != null && !_panelOpen)
            _openBtnRoot.SetActive(true);
    }

    // ── Camera finder — cross-platform, no MRTK ──────────────────────────
    void FindCam()
    {
        // 1. Tagged MainCamera (standard for all platforms)
        _cam = Camera.main;
        if (_cam) return;

        // 2. Any Camera in the scene (editor / WebGL fallback)
        _cam = FindObjectOfType<Camera>();
    }

    // =========================================================  INPUT
    // Touch devices: screen-space nearest-button (no ray needed).
    // Mouse devices: plane raycast as before.
    void DoInput()
    {
        if (!_root || !_cam) return;
        if (!_panelOpen) return;

        if (DAVizInput.IsTouchDevice)
            DoInputTouch();
        else
            DoInputMouse();
    }

    // ── Touch path — screen-space nearest button ──────────────────────────
    // Works regardless of camera angle or panel world position.
    void DoInputTouch()
    {
        if (Input.touchCount == 0) return;
        Touch t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;

        Vector2 tapPos = t.position;

        Debug.Log("[FP] Touch tap y=" + tapPos.y
                  + " screenH=" + Screen.height
                  + " pct=" + (tapPos.y / Screen.height * 100f).ToString("F1") + "%");

        // Find closest button in screen space
        HitBtn best = null;
        float bestD = float.MaxValue;

        foreach (HitBtn b in _btns)
        {
            if (b.Rend == null) continue;
            Vector3 sp = _cam.WorldToScreenPoint(b.Rend.bounds.center);
            if (sp.z <= 0f) continue;
            float d = Vector2.Distance(tapPos, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; best = b; }
        }

        // Also check the open/close button
        HitBtn openCandidate = null;
        if (_openBtnRend != null)
        {
            Vector3 sp = _cam.WorldToScreenPoint(_openBtnRend.bounds.center);
            if (sp.z > 0f)
            {
                float d = Vector2.Distance(tapPos, new Vector2(sp.x, sp.y));
                if (d < bestD) { bestD = d; openCandidate = null; /* handled below */ }
            }
        }

        string bestLabel = (best?.Lbl != null) ? best.Lbl.text : "none";
        Debug.Log("[FP] Best btn='" + bestLabel + "' dist=" + bestD.ToString("F1") + "px");

        // 300px threshold — generous for any phone screen size / DPI
        if (best != null && bestD < 300f)
        {
            Debug.Log("[FP] CLICK -> " + bestLabel);
            best.Click();
            StartCoroutine(Flash(best));
        }
        else
        {
            Debug.Log("[FP] Tap missed — dist=" + bestD.ToString("F1") + " > 300px");
        }
    }

    // ── Mouse path — plane raycast (original, unchanged) ─────────────────
    void DoInputMouse()
    {
        Ray r = DAVizInput.PointerRay(_cam);

        Vector3 panelNormal = _root.transform.forward;
        Vector3 panelCenter = _root.transform.position;
        float denom = Vector3.Dot(panelNormal, r.direction);
        bool hitPlane = false;
        Vector2 localHit = Vector2.zero;

        if (Mathf.Abs(denom) > 0.0001f)
        {
            float t = Vector3.Dot(panelCenter - r.origin, panelNormal) / denom;
            if (t > 0f)
            {
                Vector3 worldHit = r.GetPoint(t);
                Vector3 localPos = _root.transform.InverseTransformPoint(worldHit);
                float maxX = PW * 0.5f + DD_W + 0.05f;
                float maxY = PH * 0.5f + 0.06f;
                if (localPos.x >= -(PW * 0.5f + 0.06f) && localPos.x <= maxX &&
                    Mathf.Abs(localPos.y) <= maxY)
                {
                    hitPlane = true;
                    localHit = new Vector2(localPos.x, localPos.y);
                }
            }
        }
        _over = hitPlane;

        if (DAVizInput.PrimaryDown && hitPlane)
        {
            HitBtn hit = PickByLocalPos(localHit);
            if (hit != null) { hit.Click(); StartCoroutine(Flash(hit)); }
            else if (!LockToView)
            {
                _drag = true;
                float d = Vector3.Dot(panelCenter - r.origin, panelNormal) / denom;
                _dOff = _root.transform.position - r.GetPoint(d);
            }
        }
        if (DAVizInput.PrimaryUp) _drag = false;
        if (_drag && !LockToView && DAVizInput.PrimaryHeld)
        {
            float d2 = Vector3.Dot(panelNormal, r.direction);
            if (Mathf.Abs(d2) > 0.0001f)
            {
                float t2 = Vector3.Dot(panelCenter - r.origin, panelNormal) / d2;
                _root.transform.position = r.GetPoint(t2) + _dOff;
            }
        }
        if (hitPlane)
        {
            HitBtn hv = PickByLocalPos(localHit);
            if (hv != _hov)
            {
                if (_hov != null && !_hov.IsSel) _hov.Rend.material.color = _hov.Nor;
                if (hv != null && !hv.IsSel) hv.Rend.material.color = hv.Hov;
                _hov = hv;
            }
        }
        else if (_hov != null)
        {
            if (!_hov.IsSel) _hov.Rend.material.color = _hov.Nor;
            _hov = null;
        }
    }

    HitBtn PickByLocalPos(Vector2 localHit)
    {
        HitBtn best = null;
        float bd = float.MaxValue;
        foreach (HitBtn btn in _btns)
        {
            if (!btn.Rend) continue;
            Vector3 btnLocal = _root.transform.InverseTransformPoint(
                                   btn.Rend.transform.position);
            Vector3 btnScale = btn.Rend.transform.lossyScale;
            float hw = btnScale.x * 0.5f + 0.008f;
            float hh = btnScale.y * 0.5f + 0.008f;
            if (Mathf.Abs(localHit.x - btnLocal.x) <= hw &&
                Mathf.Abs(localHit.y - btnLocal.y) <= hh)
            {
                float dist = Vector2.Distance(localHit,
                                 new Vector2(btnLocal.x, btnLocal.y));
                if (dist < bd) { bd = dist; best = btn; }
            }
        }
        return best;
    }
    System.Collections.IEnumerator Flash(HitBtn b)
    {
        if (b == null || !b.Rend) yield break;
        b.Rend.material.color = Color.white * 0.6f + b.Nor * 0.4f;
        yield return new WaitForSeconds(0.10f);
        if (b.Rend) b.Rend.material.color = b.IsSel ? b.Sel : b.Nor;
    }

    // ── Sync button highlight state to active chart ───────────────────────
    void DoRefresh()
    {
        BarChartBuilder a = BarChartBuilder.GetLastInteracted();
        if (!a) return;
        foreach (HitBtn b in _btns)
        {
            if (!b.Rend) continue;
            if (b.TK != null)
            {
                bool s = (a.selectedTicker == b.TK);
                if (b.IsSel == s) continue;
                b.IsSel = s;
                b.Rend.material.color = s ? b.Sel : b.Nor;
                if (b.Lbl) b.Lbl.color = s ? CardTextSel : CardTextNor;
            }
            if (b.MK.HasValue)
            {
                bool s = (a.metricToShow == b.MK.Value);
                if (b.IsSel == s) continue;
                b.IsSel = s;
                b.Rend.material.color = s ? b.Sel : b.Nor;
                if (b.Lbl) b.Lbl.color = s ? MetricTextSel : MetricTextNor;
            }
            if (b.ChartTypeKey != null && b.ChartTypeKey != "__header__")
            {
                bool s = (b.ChartTypeKey == _activeChartType);
                if (b.IsSel == s) continue;
                b.IsSel = s;
                b.Nor = s ? ChartTypeSel : ChartTypeBg;
                b.Rend.material.color = b.Nor;
                if (b.Lbl) b.Lbl.color = s ? ChartTypeTextSel : ChartTypeText;
            }
        }
    }

    // ── Tint ADD+ button based on resource headroom ───────────────────────
    void RefreshAddBtnTint()
    {
        if (_addBtn == null || !_addBtn.Rend) return;

        Color target;
        if (ResourceMonitor.IsAtLimit())
            target = _addBtnBlockColor;
        else if (ResourceMonitor.IsNearLimit())
            target = _addBtnWarnColor;
        else
            target = AddBtnColor;

        if (_addBtn.Rend.material.color != target)
        {
            _addBtn.Rend.material.color = target;
            _addBtn.Nor = target;
            _addBtn.Hov = Color.Lerp(target, Color.white, 0.22f);
        }
    }

    // =========================================================  BUILD
    void Build()
    {
        if (_root) Destroy(_root);
        _btns.Clear();
        _addBtn = null;

        _root = new GameObject("FP_Root");
        _root.transform.position = Vector3.zero;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;

        float fz = -(PD * 0.5f) - 0.003f;

        // Glass background + border
        RoundedQuad("BG", V(0, 0, 0), PW, PH, PanelBgColor, PanelBgTexture, 0);
        RoundedQuad("Border", V(0, 0, 0.001f), PW + 0.012f, PH + 0.012f, BorderColor, null, -1);

        // Layout — 3 rows: topBar / ticker / metric
        float pad = 0.060f;
        float innerW = PW - pad * 2f;
        float topBarH = 0.150f;
        float tickerH = 0.220f;
        float metricH = 0.110f;
        float btmPad = 0.036f;
        float interGap = (PH - topBarH - tickerH - metricH - btmPad * 2f) / 2f;

        float topY = PH * 0.5f - topBarH * 0.5f - btmPad;
        float tickerY = topY - topBarH * 0.5f - interGap - tickerH * 0.5f;
        float metricY = -PH * 0.5f + metricH * 0.5f + btmPad;

        // ✕ Close button — top-left of panel, closes the panel
        float iconSz = 0.100f;
        float iconX = -PW * 0.5f + pad + iconSz * 0.5f;
        CloseBtn(V(iconX, topY, fz), iconSz);

        // ADD + / DEL - buttons
        float abW = 0.380f;
        float abH = topBarH * 0.80f;
        float delX = PW * 0.5f - pad - abW * 0.5f;
        float addX = delX - abW - 0.024f;

        _addBtn = BigBtn("ADD +", V(addX, topY, fz), abW, abH,
                          AddBtnColor, AddBtnTexture, SpawnClick);
        BigBtn("DEL -", V(delX, topY, fz), abW, abH,
               DelBtnColor, DelBtnTexture, DeleteClick);

        // Live / Mock toggle  — sits to the left of ADD+
        float toggleW = 0.320f;
        float toggleX = addX - abW - 0.020f;
        Color liveBg = BarChartBuilder.UseLiveData
                      ? new Color(0.05f, 0.50f, 0.25f, 1f)
                      : new Color(0.40f, 0.30f, 0.05f, 1f);
        string liveLabel = BarChartBuilder.UseLiveData ? "● LIVE" : "○ MOCK";
        _liveBtn = BigBtn(liveLabel, V(toggleX, topY, fz), toggleW, abH, liveBg, null, ToggleLiveMock);

        // Compare button — to the left of Live/Mock
        float cmpW2 = 0.300f;
        float cmpX = toggleX - toggleW - 0.018f;
        Color cmpBg = BarChartBuilder.ComparisonMode
                    ? new Color(0.10f, 0.35f, 0.75f, 1f)
                    : new Color(0.14f, 0.14f, 0.18f, 1f);
        string cmpLabel = BarChartBuilder.ComparisonMode ? "CMP ●" : "CMP";
        _cmpBtn = BigBtn(cmpLabel, V(cmpX, topY, fz), cmpW2, abH, cmpBg, null, ToggleCompare);

        // Ticker rows — 2 rows of 5
        float colW = innerW / 5f;
        float cardW = colW - 0.022f;
        float row1Y = tickerY + tickerH * 0.26f;
        float row2Y = tickerY - tickerH * 0.26f;
        float rowCardH = tickerH * 0.46f;
        for (int i = 0; i < 5; i++)
        {
            float cx = -PW * 0.5f + pad + colW * (i + 0.5f);
            TickerCard(Tickers[i], V(cx, row1Y, fz), cardW, rowCardH);
            TickerCard(Tickers[i + 5], V(cx, row2Y, fz), cardW, rowCardH);
        }

        // Metric pill
        float pillW = innerW * 0.82f;
        MetricPill(V(0f, metricY, fz), pillW, metricH);

        // Side dropdown tab (right side of panel)
        BuildSideDropdown();

        Place();
    }

    // ── Icon square ───────────────────────────────────────────────────────
    void IconSquare(Vector3 c, float sz)
    {
        float fz = -(PD * 0.5f) - 0.003f;
        RoundedQuad("IconBG", c, sz, sz, IconBgColor, null, 0);
        RoundedQuad("IconBorder", V(c.x, c.y, c.z + 0.001f), sz + 0.008f, sz + 0.008f, IconBorderColor, null, -1);

        Color wf = new Color(0.85f, 0.85f, 0.90f, 1f);
        float fw = sz * 0.56f;
        float fh = sz * 0.08f;
        for (int i = 0; i < 3; i++)
        {
            float scale = 1.0f - i * 0.28f;
            float arcY = c.y - sz * 0.06f + i * sz * 0.14f;
            RoundedQuad("Arc" + i, V(c.x, arcY, fz - 0.001f),
                        fw * scale, fh, wf, null, 1);
        }
        RoundedQuad("Dot", V(c.x, c.y - sz * 0.18f, fz - 0.002f),
                    sz * 0.10f, sz * 0.10f, wf, null, 1);
    }

    // ── ✕ Close button (top-left of panel) ──────────────────────────────────
    void CloseBtn(Vector3 c, float sz)
    {
        float fz = -(PD * 0.5f) - 0.003f;

        // Dark glass background matching panel
        Color closeBg = new Color(0.10f, 0.10f, 0.14f, 0.97f);
        Color closeBrd = new Color(0.55f, 0.55f, 0.60f, 0.70f);
        Color closeHov = new Color(0.70f, 0.12f, 0.12f, 1.00f);  // red on hover
        Color closeTxt = new Color(0.85f, 0.85f, 0.90f, 1.00f);

        var go = RoundedQuadBtn("CloseBtn", c, sz, sz, closeBg, null);
        RoundedQuad("CloseBorder", V(c.x, c.y, c.z + 0.001f),
                    sz + 0.008f, sz + 0.008f, closeBrd, null, -1);
        // Gloss shine
        RoundedQuad("CloseShine", V(c.x, c.y + sz * 0.22f, fz - 0.001f),
                    sz - 0.008f, sz * 0.28f, new Color(1f, 1f, 1f, 0.10f), null, 2);

        var lbl = MakeText("✕", V(c.x, c.y, fz - 0.002f), 0.028f, closeTxt, true);

        _btns.Add(new HitBtn
        {
            Rend = go.GetComponent<Renderer>(),
            Lbl = lbl,
            Click = TogglePanel,
            Nor = closeBg,
            Hov = closeHov,
            Sel = closeBg
        });
    }

    // ── Big action button — returns the HitBtn so caller can store it ─────
    HitBtn BigBtn(string label, Vector3 c, float w, float h,
                  Color bg, Texture2D tex, System.Action click)
    {
        float fz = -(PD * 0.5f) - 0.003f;
        var go = RoundedQuadBtn("Btn_" + label, c, w, h, bg, tex);
        RoundedQuad("BtnShine", V(c.x, c.y + h * 0.25f, fz - 0.001f),
                    w - 0.010f, h * 0.30f, new Color(1f, 1f, 1f, 0.12f), null, 1);
        MakeText(label, V(c.x, c.y, fz - 0.002f), BtnTextSize, BtnTextColor, true);

        var hb = new HitBtn
        {
            Rend = go.GetComponent<Renderer>(),
            Lbl = null,
            Click = click,
            Nor = bg,
            Hov = Color.Lerp(bg, Color.white, 0.22f),
            Sel = bg
        };
        _btns.Add(hb);
        return hb;
    }

    // ── Ticker card ───────────────────────────────────────────────────────
    void TickerCard(string ticker, Vector3 c, float w, float h)
    {
        float fz = -(PD * 0.5f) - 0.003f;
        var go = RoundedQuadBtn("Card_" + ticker, c, w, h, CardFaceColor, CardTexture);
        RoundedQuad("CardShadow",
                    V(c.x, c.y - h * 0.5f + 0.008f, fz - 0.001f),
                    w - 0.006f, 0.012f, new Color(0f, 0f, 0f, 0.18f), null, 1);

        var lbl = MakeText(ticker, V(c.x, c.y, fz - 0.002f),
                           TickerTextSize, CardTextNor, true);
        string cap = ticker;
        _btns.Add(new HitBtn
        {
            Rend = go.GetComponent<Renderer>(),
            Lbl = lbl,
            Click = delegate { TickerClick(cap); },
            Nor = CardFaceColor,
            Hov = CardHovColor,
            Sel = CardSelColor,
            TK = ticker
        });
    }

    // ── Metric pill ───────────────────────────────────────────────────────
    void MetricPill(Vector3 c, float w, float h)
    {
        float fz = -(PD * 0.5f) - 0.003f;
        RoundedQuad("PillBG", c, w, h, MetricBgColor, null, 0);
        RoundedQuad("PillBorder", V(c.x, c.y, c.z + 0.001f), w + 0.008f, h + 0.008f, MetricDivColor, null, -1);

        float slotW = w / Metrics.Length;
        for (int i = 0; i < Metrics.Length; i++)
        {
            float mx = c.x - w * 0.5f + slotW * (i + 0.5f);
            var go = RoundedQuadBtn("Met_" + i, V(mx, c.y, fz - 0.001f),
                                       slotW - 0.006f, h - 0.008f, MetricBgColor, null);
            var lbl = MakeText(MetLabels[i], V(mx, c.y, fz - 0.003f),
                                 MetricTextSize, MetricTextNor, false);
            if (i < Metrics.Length - 1)
                RoundedQuad("Div" + i, V(mx + slotW * 0.5f, c.y, fz - 0.002f),
                            0.0018f, h - 0.016f, MetricDivColor, null, 1);

            BarChartBuilder.MetricType cap = Metrics[i];
            _btns.Add(new HitBtn
            {
                Rend = go.GetComponent<Renderer>(),
                Lbl = lbl,
                Click = delegate { BarChartBuilder.SetMetricOnLastInteracted(cap); },
                Nor = MetricBgColor,
                Hov = new Color(0.18f, 0.18f, 0.22f, 1f),
                Sel = MetricBgSel,
                MK = Metrics[i]
            });
        }
    }

    // ── Side dropdown — vertical tab attached to right edge of panel ────────
    //
    //  Visual layout (right side, always visible):
    //
    //   Panel right edge
    //        │  ┌──────────┐
    //        │  │  ▼ Bar   │  ← header tab (shows active type + arrow)
    //        │  └──────────┘
    //        │  ┌──────────┐
    //        │  │   Bar    │  ← option 0  (only shown when _ddOpen)
    //        │  ├──────────┤
    //        │  │  Candle  │  ← option 1
    //        │  ├──────────┤
    //        │  │   Line   │  ← option 2
    //        │  └──────────┘
    //
    //  Clicking the header toggles open/close.
    //  Clicking an option selects it and closes the dropdown.
    // ─────────────────────────────────────────────────────────────────────

    // Dropdown state
    bool _ddOpen = false;
    GameObject _ddRoot = null;   // parent for option quads — rebuilt on toggle
    HitBtn _ddHeader = null;   // header button reference for label update
    TextMesh _ddHeaderLbl = null;

    const float DD_W = 0.28f;   // dropdown width
    const float DD_H = 0.095f;  // height of each row
    const float DD_GAP = 0.0f;    // gap between rows

    void BuildSideDropdown()
    {
        float fz = -(PD * 0.5f) - 0.005f;
        float rightX = PW * 0.5f + DD_W * 0.5f + 0.008f;   // just right of panel
        float headerY = PH * 0.5f - DD_H * 0.5f - 0.02f;    // top-right corner

        // ── Header tab ───────────────────────────────────────────────────
        // Match main panel glass finish
        Color hdrBg = new Color(0.06f, 0.06f, 0.08f, 0.92f);   // same as PanelBgColor
        Color hdrBrd = new Color(0.55f, 0.55f, 0.60f, 0.70f);   // same as BorderColor

        // Thin connector strip between panel and dropdown
        RoundedQuad("DDConnector",
            V(PW * 0.5f + 0.004f, headerY, fz + 0.001f),
            0.012f, DD_H * 0.85f, hdrBrd, null, 0);

        // Glass background matching main panel
        var hdrGo = RoundedQuadBtn("DDHeader",
            V(rightX, headerY, fz), DD_W, DD_H, hdrBg, null);
        // Outer border
        RoundedQuad("DDHdrBorder",
            V(rightX, headerY, fz + 0.001f),
            DD_W + 0.012f, DD_H + 0.012f, hdrBrd, null, -1);
        // Gloss shine strip at top of header (matches BigBtn)
        RoundedQuad("DDHdrShine",
            V(rightX, headerY + DD_H * 0.25f, fz - 0.001f),
            DD_W - 0.010f, DD_H * 0.28f,
            new Color(1f, 1f, 1f, 0.10f), null, 2);
        // Subtle accent line on left edge
        RoundedQuad("DDHdrAccent",
            V(rightX - DD_W * 0.5f + 0.003f, headerY, fz - 0.001f),
            0.005f, DD_H - 0.012f,
            new Color(0.40f, 0.75f, 1.00f, 0.70f), null, 2);

        // Cyan label colour matching metric selected state
        Color hdrTextCol = new Color(0.30f, 1.00f, 0.85f, 1.00f);
        string hdrText = ArrowFor(_ddOpen) + "  " + ActiveLabel();
        _ddHeaderLbl = MakeText(hdrText,
            V(rightX, headerY, fz - 0.002f), 0.020f, hdrTextCol, true);

        _ddHeader = new HitBtn
        {
            Rend = hdrGo.GetComponent<Renderer>(),
            Lbl = _ddHeaderLbl,
            Click = ToggleDropdown,
            Nor = hdrBg,
            Hov = new Color(0.12f, 0.18f, 0.22f, 0.97f),
            Sel = hdrBg,
            ChartTypeKey = "__header__"
        };
        _btns.Add(_ddHeader);

        // ── Option rows (only if open) ───────────────────────────────────
        if (_ddOpen) BuildDropdownOptions(rightX, headerY, fz);
    }

    void BuildDropdownOptions(float rightX, float headerY, float fz)
    {
        // Destroy previous option objects if any
        if (_ddRoot != null) UnityEngine.Object.Destroy(_ddRoot);
        _ddRoot = new GameObject("DD_Options");
        _ddRoot.transform.SetParent(_root.transform, false);

        // Glass option colours — match main panel
        Color optBg = new Color(0.06f, 0.06f, 0.08f, 0.95f);   // panel bg
        Color optBrd = new Color(0.55f, 0.55f, 0.60f, 0.60f);   // panel border
        Color optSelBg = new Color(0.04f, 0.22f, 0.10f, 1.00f);   // MetricBgSel green
        Color divClr = new Color(0.55f, 0.55f, 0.60f, 0.35f);   // subtle divider

        // Outer glass border covering all options
        float totalH = ChartTypeKeys.Length * DD_H;
        float listCY = headerY - DD_H - totalH * 0.5f;
        MakeQuadOn(_ddRoot, "DDOptBorder",
            V(rightX, listCY, fz + 0.001f),
            DD_W + 0.012f, totalH + 0.012f, optBrd, -1);
        // Dark glass fill behind all options
        MakeQuadOn(_ddRoot, "DDOptBg",
            V(rightX, listCY, fz),
            DD_W, totalH, optBg, 0);

        for (int i = 0; i < ChartTypeKeys.Length; i++)
        {
            float optY = headerY - DD_H * (i + 1) - DD_GAP * i;
            bool isSel = (ChartTypeKeys[i] == _activeChartType);
            Color bg = isSel ? optSelBg : new Color(0f, 0f, 0f, 0f); // transparent over DDOptBg

            var optGo = MakeQuadOn(_ddRoot, "DDOpt_" + ChartTypeKeys[i],
                V(rightX, optY, fz + 0.001f), DD_W - 0.004f, DD_H - 0.006f, bg, 1);

            // Accent left edge when selected
            if (isSel)
                MakeQuadOn(_ddRoot, "DDSelAccent_" + i,
                    V(rightX - DD_W * 0.5f + 0.004f, optY, fz - 0.001f),
                    0.006f, DD_H - 0.016f,
                    new Color(0.30f, 1.00f, 0.55f, 0.90f), 2);

            // Gloss shine on each row
            MakeQuadOn(_ddRoot, "DDShine_" + i,
                V(rightX, optY + DD_H * 0.25f, fz - 0.002f),
                DD_W - 0.010f, DD_H * 0.22f,
                new Color(1f, 1f, 1f, isSel ? 0.08f : 0.04f), 2);

            // Divider line below each option except last
            if (i < ChartTypeKeys.Length - 1)
                MakeQuadOn(_ddRoot, "DDDiv_" + i,
                    V(rightX, optY - DD_H * 0.5f, fz - 0.001f),
                    DD_W - 0.016f, 0.0018f, divClr, 2);

            Color tc = isSel ? new Color(0.30f, 1.00f, 0.55f, 1f) : ChartTypeText;
            var lbl = MakeTextOn(_ddRoot, ChartTypeLabels[i],
                V(rightX, optY, fz - 0.003f), 0.021f, tc, isSel);

            string keyCapture = ChartTypeKeys[i];
            var hb = new HitBtn
            {
                Rend = optGo.GetComponent<Renderer>(),
                Lbl = lbl,
                Click = delegate { SelectChartType(keyCapture); },
                Nor = bg,
                Hov = Color.Lerp(bg, Color.white, 0.20f),
                Sel = optSelBg,
                IsSel = isSel,
                ChartTypeKey = ChartTypeKeys[i]
            };
            _btns.Add(hb);
        }
    }

    // Toggle open / close
    void ToggleDropdown()
    {
        _ddOpen = !_ddOpen;
        if (_ddHeaderLbl != null)
            _ddHeaderLbl.text = ArrowFor(_ddOpen) + " " + ActiveLabel();

        if (_ddOpen)
        {
            // Rebuild options on the existing _root
            float fz = -(PD * 0.5f) - 0.005f;
            float rightX = PW * 0.5f + DD_W * 0.5f + 0.008f;
            float headerY = PH * 0.5f - DD_H * 0.5f - 0.02f;
            BuildDropdownOptions(rightX, headerY, fz);
        }
        else
        {
            if (_ddRoot != null) { UnityEngine.Object.Destroy(_ddRoot); _ddRoot = null; }
            // Remove option HitBtns from list
            _btns.RemoveAll(b => b.ChartTypeKey != null
                             && b.ChartTypeKey != "__header__");
        }
    }

    // Select an option — closes dropdown, applies chart type
    void SelectChartType(string key)
    {
        _ddOpen = false;
        _activeChartType = key;

        // Update header label
        if (_ddHeaderLbl != null)
            _ddHeaderLbl.text = ArrowFor(false) + " " + ActiveLabel();

        // Destroy option objects
        if (_ddRoot != null) { UnityEngine.Object.Destroy(_ddRoot); _ddRoot = null; }
        _btns.RemoveAll(b => b.ChartTypeKey != null
                         && b.ChartTypeKey != "__header__");

        // Apply to active chart
        ChartTypeClick(key);
    }

    string ActiveLabel()
    {
        for (int i = 0; i < ChartTypeKeys.Length; i++)
            if (ChartTypeKeys[i] == _activeChartType)
                return ChartTypeLabels[i];
        return "Bar";
    }

    string ArrowFor(bool open) { return open ? "▲" : "▼"; }

    // Helper: create a quad parented to a specific GO instead of _root
    GameObject MakeQuadOn(GameObject parent, string name,
                           Vector3 lp, float w, float h, Color col, int sort)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = lp;
        go.transform.localScale = new Vector3(w, h, 1f);
        go.transform.localRotation = Quaternion.identity;
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = col;
        var rend = go.GetComponent<Renderer>();
        rend.material = mat;
        rend.sortingOrder = sort;
        return go;
    }

    // Helper: create text parented to a specific GO instead of _root
    TextMesh MakeTextOn(GameObject parent, string txt,
                         Vector3 lp, float cs, Color col, bool bold)
    {
        var go = new GameObject("T_" + txt);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text = txt;
        tm.characterSize = cs;
        tm.fontSize = 16;
        tm.color = col;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 3;
        return tm;
    }

    // ── Callbacks ─────────────────────────────────────────────────────────

    // SpawnClick: checks resource limit before spawning
    void SpawnClick()
    {
        if (ResourceMonitor.IsAtLimit())
        {
            ResourceMonitor.ShowLimitReached();
            return;
        }
        if (ResourceMonitor.IsNearLimit())
            ResourceMonitor.ShowAdvisory();

        Camera cam = Camera.main ? Camera.main : FindObjectOfType<Camera>();
        BarChartBuilder.SpawnNewChart(cam);
    }

    void DeleteClick()
    {
        BarChartBuilder[] all = FindObjectsOfType<BarChartBuilder>();
        if (all.Length <= 1) return;
        BarChartBuilder target = BarChartBuilder.GetLastInteracted();
        if (target == null) return;
        BarChartBuilder next = null;
        foreach (BarChartBuilder b in all)
            if (b != target) { next = b; break; }
        if (target._chartRoot != null) Destroy(target._chartRoot);
        Destroy(target.gameObject);
        if (next != null) next.MakeLastInteracted();
    }

    void TickerClick(string tk)
    {
        BarChartBuilder t = BarChartBuilder.GetLastInteracted();
        if (t == null) t = FindObjectOfType<BarChartBuilder>();
        if (t == null) return;
        t.selectedTicker = tk;
        t._builtTicker = null;
        t.BuildChart();
    }

    void ToggleLiveMock()
    {
        BarChartBuilder.UseLiveData = !BarChartBuilder.UseLiveData;

        // Update button appearance
        if (_liveBtn != null && _liveBtn.Rend)
        {
            bool live = BarChartBuilder.UseLiveData;
            Color liveBg = live ? new Color(0.05f, 0.50f, 0.25f, 1f)
                                : new Color(0.40f, 0.30f, 0.05f, 1f);
            _liveBtn.Nor = liveBg;
            _liveBtn.Hov = Color.Lerp(liveBg, Color.white, 0.22f);
            _liveBtn.Rend.material.color = liveBg;
            if (_liveBtn.Lbl) _liveBtn.Lbl.text = live ? "● LIVE" : "○ MOCK";
        }

        // Rebuild all charts with new source
        BarChartBuilder[] all = FindObjectsOfType<BarChartBuilder>();
        foreach (BarChartBuilder b in all)
        {
            b._builtTicker = null;
            b.BuildChart();
        }
    }

    void ToggleCompare()
    {
        BarChartBuilder.ComparisonMode = !BarChartBuilder.ComparisonMode;

        // Update button appearance
        if (_cmpBtn != null && _cmpBtn.Rend)
        {
            bool cmp = BarChartBuilder.ComparisonMode;
            Color cmpBg = cmp ? new Color(0.10f, 0.35f, 0.75f, 1f)
                              : new Color(0.14f, 0.14f, 0.18f, 1f);
            _cmpBtn.Nor = cmpBg;
            _cmpBtn.Hov = Color.Lerp(cmpBg, Color.white, 0.22f);
            _cmpBtn.Rend.material.color = cmpBg;
            if (_cmpBtn.Lbl) _cmpBtn.Lbl.text = cmp ? "CMP ●" : "CMP";
        }

        // Rebuild the active chart to enter/exit comparison mode
        BarChartBuilder active = BarChartBuilder.GetLastInteracted();
        if (active == null) active = FindObjectOfType<BarChartBuilder>();
        if (active != null)
        {
            active._builtTicker = null;
            active.BuildChart();
        }
    }

    // ChartTypeClick: called by SelectChartType — _activeChartType already set
    void ChartTypeClick(string key)
    {
        // Fallback: if nothing was interacted with yet, grab the first one in scene
        BarChartBuilder chart = BarChartBuilder.GetLastInteracted();
        if (chart == null) chart = FindObjectOfType<BarChartBuilder>();
        if (chart == null) return;

        if (key == "Bar")
        {
            // Remove any CandlestickBuilder that was added
            CandlestickBuilder cb = chart.gameObject.GetComponent<CandlestickBuilder>();
            if (cb != null)
            {
                if (cb.chartRoot != null) Destroy(cb.chartRoot);
                Destroy(cb);
            }
            chart.renderMode = BarChartBuilder.RenderMode.Bar;
            chart._builtTicker = null;
            chart.BuildChart();
        }
        else if (key == "Line")
        {
            // Remove any CandlestickBuilder
            CandlestickBuilder cb = chart.gameObject.GetComponent<CandlestickBuilder>();
            if (cb != null)
            {
                if (cb.chartRoot != null) Destroy(cb.chartRoot);
                Destroy(cb);
            }
            chart.renderMode = BarChartBuilder.RenderMode.Line;
            chart._builtTicker = null;
            chart.BuildChart();
        }
        else if (key == "Candle")
        {
            // Destroy existing bar chart visuals
            if (chart._chartRoot != null)
            {
                Destroy(chart._chartRoot);
                chart._chartRoot = null;
            }
            chart._builtTicker = "";   // prevent BarChartBuilder from rebuilding

            // Add or reuse CandlestickBuilder on the same GO
            CandlestickBuilder cb = chart.gameObject.GetComponent<CandlestickBuilder>();
            if (cb == null) cb = chart.gameObject.AddComponent<CandlestickBuilder>();
            cb.selectedTicker = chart.selectedTicker;
            cb.BuildChart();
        }
    }

    // =========================================================  TEXT
    TextMesh MakeText(string txt, Vector3 lp, float cs, Color col, bool bold)
    {
        var go = new GameObject("T_" + txt);
        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text = txt;
        tm.characterSize = cs;
        tm.fontSize = 16;
        tm.color = col;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 3;
        return tm;
    }

    TextMesh MakeText(string txt, Vector3 lp, float cs, Color col)
    {
        return MakeText(txt, lp, cs, col, false);
    }

    // =========================================================  QUAD HELPERS
    GameObject RoundedQuad(string n, Vector3 lp, float w, float h,
                            Color col, Texture2D tex, int sortingBias)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = n;
        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = lp;
        go.transform.localScale = new Vector3(w, h, 1f);
        go.transform.localRotation = Quaternion.identity;
        Destroy(go.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = col;
        if (tex != null) mat.mainTexture = tex;
        var rend = go.GetComponent<Renderer>();
        rend.material = mat;
        rend.sortingOrder = sortingBias;
        return go;
    }

    GameObject RoundedQuadBtn(string n, Vector3 lp, float w, float h,
                               Color col, Texture2D tex)
    {
        return RoundedQuad(n, lp, w, h, col, tex, 1);
    }

    void Place()
    {
        if (!_cam) FindCam();
        if (!_cam) return;
        Vector3 p = _cam.transform.position
                  + _cam.transform.forward * StartDist
                  + _cam.transform.right * StartSide
                  + Vector3.up * StartHeight;
        _root.transform.position = p;
        _root.transform.rotation =
            Quaternion.LookRotation(p - _cam.transform.position, Vector3.up);
    }

    static Vector3 V(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }
}