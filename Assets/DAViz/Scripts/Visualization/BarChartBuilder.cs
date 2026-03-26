/*  BarChartBuilder.cs  — DAV VR · Cross-Platform Edition
 *  ══════════════════════════════════════════════════════════════════════
 *  Works on: PC · Mac · Android · iOS · WebGL
 *  Removed:  HoloLens / MRTK / UWP-specific code
 *
 *  Fixes vs previous version:
 *    • [..4] range syntax → .Substring(0, 4)          (IL2CPP/Android safe)
 *    • All => void/property bodies → full { } blocks  (IL2CPP safe)
 *    • Input.mousePosition → DAVizInput.Position       (touch + mouse)
 *    • Input.GetMouseButton* → DAVizInput.Primary*     (touch + mouse)
 *    • Input.GetAxis(ScrollWheel) → DAVizInput.ScrollDelta (pinch + scroll)
 *    • SpawnNewChart positions new chart offset to avoid overlap
 *  ══════════════════════════════════════════════════════════════════════ */

using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class BarChartBuilder : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Chart Settings")]
    public string selectedTicker = "AAPL";
    public MetricType metricToShow = MetricType.Revenue;

    [Header("Cube Size")]
    public float cubeWidth = 1.6f;
    public float cubeHeight = 1.3f;
    public float cubeDepth = 0.55f;
    public float edgeThickness = 0.007f;

    [Header("Position")]
    public float distanceFromCamera = 1.8f;
    public float heightOffset = 0.0f;
    public float sideOffset = 0.0f;  // horizontal offset for multi-chart layout

    [Header("Colors")]
    public Color positiveColor = new Color(0.15f, 1.0f, 0.45f);
    public Color negativeColor = new Color(1.0f, 0.25f, 0.25f);
    public Color edgeColor = new Color(0.35f, 0.85f, 1.0f);

    // ── Metric enum ───────────────────────────────────────────────────────
    public enum MetricType
    {
        Revenue, NetIncome, TotalAssets, TotalLiabilities, EPS,
        LivePrice   // Real-time price — polls every 15 seconds
    }

    // ── Render mode (set by FloatingPanel chart type buttons) ─────────────
    public enum RenderMode { Bar, Line }
    public RenderMode renderMode = RenderMode.Bar;

    // ── Global data source toggle (shared across all charts) ──────────────
    public static bool UseLiveData = true;   // false = always use mock

    // ── Comparison mode: show all tickers side-by-side in one chart ───────
    public static bool ComparisonMode = false;

    // ── Geometry constants — EXPANDED from => for IL2CPP compatibility ────
    private int _seriesCount = 5;

    private float BarSpacing
    {
        get { return (cubeWidth - 0.35f) / Mathf.Max(_seriesCount, 1); }
    }
    private float MaxBarHeight
    {
        get { return cubeHeight - 0.42f; }
    }
    private float BarWidth
    {
        get { return BarSpacing * 0.55f; }
    }
    private float FloorY
    {
        get { return -(cubeHeight / 2f) + 0.09f; }
    }

    // ── State ─────────────────────────────────────────────────────────────
    public GameObject _chartRoot;
    public string _builtTicker;
    public MetricType _builtMetric;

    private Camera _cam;
    private bool _isDragging;
    private Vector3 _dragOffset;
    private float _dragDistance;

    private static BarChartBuilder _lastInteracted;

    // ── Live price polling ($ Price metric) ──────────────────────────────
    private float _livePollTimer = 0f;
    private const float LIVE_POLL_INTERVAL = 15f;
    private bool _isLiveMode = false;
    private List<Renderer> _liveBarRends = new List<Renderer>();
    private List<TextMesh> _liveValLabels = new List<TextMesh>();
    private List<float> _liveTargetH = new List<float>();
    private float _livePriceMax = 1f;

    // ── Fundamentals refresh polling (all other metrics) ─────────────────
    private float _fundPollTimer = 0f;
    private const float FUND_POLL_INTERVAL = 60f;   // 60s — saves API calls
    private bool _isFundLive = false;
    // Stored refs for in-place bar animation (fundamentals)
    private List<Renderer> _fundBarRends = new List<Renderer>();
    private List<TextMesh> _fundValLabels = new List<TextMesh>();
    private List<float> _fundAbsMax = new List<float>();   // per-series max
    // Countdown TextMesh shown in chart corner
    private TextMesh _countdownLbl = null;
    private TextMesh _dataSourceLbl = null;

    // ── Metric → mock data key map ────────────────────────────────────────
    private static readonly Dictionary<MetricType, string> MetricNames =
        new Dictionary<MetricType, string>
        {
            { MetricType.Revenue,          "Revenue"           },
            { MetricType.NetIncome,        "NetIncome"         },
            { MetricType.TotalAssets,      "TotalAssets"       },
            { MetricType.TotalLiabilities, "TotalLiabilities"  },
            { MetricType.EPS,              "EPS"               },
            { MetricType.LivePrice,        "LivePrice"         },
        };

    // =========================================================
    void Awake()
    {
        // Always register as last interacted on creation
        // so FloatingPanel can target it immediately without a click
        if (_lastInteracted == null) _lastInteracted = this;
    }

    void Start()
    {
        FindCam();
        _builtTicker = null;
        _builtMetric = (MetricType)(-1);
        _lastInteracted = this;
        // Delay one frame so camera transform is ready on Android/mobile
        StartCoroutine(BuildNextFrame());
    }

    System.Collections.IEnumerator BuildNextFrame()
    {
        yield return null;   // wait one frame
        FindCam();           // re-find cam after first frame
        Debug.Log("[BCB] BuildChart start — ticker=" + selectedTicker
                + " dist=" + distanceFromCamera + " cam=" + (_cam != null ? _cam.name : "NULL"));
        BuildChart();
    }

    void Update()
    {
        HandleInput();
        TickLivePoll();
    }

    void TickLivePoll()
    {
        // ── $ Price metric polling ────────────────────────────────────────
        if (_isLiveMode && UseLiveData &&
            TwelveDataService.Instance != null &&
            DAVizConfig.Instance != null && DAVizConfig.Instance.HasApiKey())
        {
            _livePollTimer -= Time.deltaTime;
            if (_livePollTimer <= 0f)
            {
                _livePollTimer = LIVE_POLL_INTERVAL;
                string[] tickers = ComparisonMode
                    ? new string[] { "AAPL", "MSFT", "GOOGL", "AMZN", "META", "NVDA", "TSLA", "NFLX", "JPM", "V" }
                    : new string[] { selectedTicker };
                StartCoroutine(PollLivePrices(tickers));
            }
        }

        // ── Fundamentals polling (Revenue / EPS / etc.) ───────────────────
        if (_isFundLive && UseLiveData &&
            TwelveDataService.Instance != null &&
            DAVizConfig.Instance != null && DAVizConfig.Instance.HasApiKey())
        {
            _fundPollTimer -= Time.deltaTime;

            // Update countdown label every second
            UpdateCountdownLabel();

            if (_fundPollTimer <= 0f)
            {
                _fundPollTimer = FUND_POLL_INTERVAL;
                string metricKey = MetricNames[metricToShow];
                // Clear cache so we always get fresh data
                DataCache.Clear(DataCache.SeriesKey(selectedTicker, metricKey));
                TwelveDataService.Instance.GetSeriesAsync(
                    selectedTicker, metricKey,
                    series => AnimateFundBars(series),
                    () => AnimateFundBars(
                        MockFinancialData.GetSeries(selectedTicker, metricKey))
                );
                FlashPulse();
            }
        }
    }

    // ── Countdown display ─────────────────────────────────────────────────
    void UpdateCountdownLabel()
    {
        if (_countdownLbl == null) return;
        int secs = Mathf.CeilToInt(_fundPollTimer);
        _countdownLbl.text = "↻ " + secs + "s";
        // Colour: green when fresh, amber when about to refresh
        float frac = _fundPollTimer / FUND_POLL_INTERVAL;
        _countdownLbl.color = Color.Lerp(
            new Color(1.00f, 0.70f, 0.10f, 0.90f),   // amber — nearly expired
            new Color(0.30f, 1.00f, 0.55f, 0.80f),   // green — just refreshed
            frac);
    }

    // ── Flash pulse: briefly brighten all bars when data arrives ──────────
    void FlashPulse()
    {
        if (_chartRoot == null) return;
        foreach (var rend in _fundBarRends)
        {
            if (rend == null) continue;
            Color orig = rend.material.color;
            rend.transform
                .DOPunchScale(new Vector3(0.08f, 0.15f, 0.08f), 0.35f, 2, 0.5f);
        }
    }

    // ── Animate fundamentals bars in-place with new data ──────────────────
    void AnimateFundBars(List<FinancialDataPoint> series)
    {
        if (series == null || series.Count == 0) return;
        if (_fundBarRends.Count == 0) { _builtTicker = null; BuildChart(); return; }

        // Cap to 5
        if (series.Count > 5) series = series.GetRange(series.Count - 5, 5);

        float absMax = 0f;
        foreach (var dp in series)
            if (Mathf.Abs(dp.displayValue) > absMax) absMax = Mathf.Abs(dp.displayValue);
        if (absMax < 0.0001f) absMax = 1f;

        float floorY = -(cubeHeight / 2f) + 0.09f;
        float innerLeft = -(cubeWidth / 2f) + 0.20f;
        float fz = -(cubeDepth / 2f) - 0.06f;
        bool isEps = (series[0].unit == "USD/shares");

        for (int i = 0; i < Mathf.Min(series.Count, _fundBarRends.Count); i++)
        {
            if (_fundBarRends[i] == null) continue;

            float val = series[i].displayValue;
            bool neg = val < 0f;
            float barH = Mathf.Max((Mathf.Abs(val) / absMax) * MaxBarHeight, 0.005f);
            float xPos = innerLeft + i * BarSpacing + BarSpacing / 2f;
            float yPos = floorY + (neg ? -barH / 2f : barH / 2f);

            // Animate bar height + position together
            _fundBarRends[i].transform
                .DOScaleY(barH, 0.55f)
                .SetEase(DG.Tweening.Ease.OutBack);
            _fundBarRends[i].transform
                .DOLocalMoveY(yPos, 0.55f)
                .SetEase(DG.Tweening.Ease.OutCubic);

            // Colour: positive = chart green, negative = red
            _fundBarRends[i].material.color =
                neg ? new Color(1.0f, 0.25f, 0.25f, 1f)
                    : positiveColor;

            // Update value label position and text
            if (i < _fundValLabels.Count && _fundValLabels[i] != null)
            {
                float stagger = (i % 2 == 0) ? 0f : 0.06f;
                float ly = floorY + (neg ? -barH - 0.10f - stagger : barH + 0.06f + stagger);
                _fundValLabels[i].transform
                    .DOLocalMoveY(ly, 0.55f)
                    .SetEase(DG.Tweening.Ease.OutCubic);

                string valStr = isEps
                    ? "$" + val.ToString("F2")
                    : "$" + Mathf.Abs(val).ToString("F1") + "B";
                _fundValLabels[i].text = valStr;
                _fundValLabels[i].color = neg
                    ? new Color(1.00f, 0.40f, 0.40f, 1f)
                    : new Color(0.30f, 1.00f, 0.55f, 1f);
            }
        }

        // Update data source badge
        if (_dataSourceLbl != null)
        {
            _dataSourceLbl.text = "● LIVE  " + System.DateTime.Now.ToString("HH:mm:ss");
            _dataSourceLbl.color = new Color(0.20f, 1.00f, 0.50f, 1f);
        }
    }

    System.Collections.IEnumerator PollLivePrices(string[] tickers)
    {
        var quotes = new List<LiveQuote>();
        foreach (string tk in tickers)
        {
            bool done = false;
            // Force-expire cache so we get fresh price
            DataCache.Clear(DataCache.QuoteKey(tk));
            TwelveDataService.Instance.GetQuoteAsync(tk,
                q => { quotes.Add(q); done = true; },
                () => { done = true; });
            // Wait up to 5s per request
            float waited = 0f;
            while (!done && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
        if (quotes.Count == 0) yield break;
        AnimateLiveBars(quotes);
    }

    void AnimateLiveBars(List<LiveQuote> quotes)
    {
        if (_liveBarRends.Count == 0) { _builtTicker = null; BuildChart(); return; }

        // Find new max price for normalisation
        float newMax = 0.001f;
        foreach (LiveQuote q in quotes)
            if (q.currentPrice > newMax) newMax = q.currentPrice;
        _livePriceMax = newMax;

        float maxBarH = cubeHeight - 0.42f;
        float floorY = -(cubeHeight / 2f) + 0.09f;

        for (int i = 0; i < Mathf.Min(quotes.Count, _liveBarRends.Count); i++)
        {
            if (_liveBarRends[i] == null) continue;
            float t = quotes[i].currentPrice / _livePriceMax;
            float targetH = Mathf.Max(t * maxBarH, 0.01f);

            // Animate scale Y
            _liveBarRends[i].transform
                .DOScaleY(targetH, 0.60f)
                .SetEase(DG.Tweening.Ease.OutCubic);

            // Reposition bar centre (bottom-anchored)
            Vector3 p = _liveBarRends[i].transform.localPosition;
            _liveBarRends[i].transform
                .DOLocalMoveY(floorY + targetH * 0.5f, 0.60f)
                .SetEase(DG.Tweening.Ease.OutCubic);

            // Update colour — green if up, red if down
            bool up = quotes[i].IsPositive();
            Color col = up ? new Color(0.15f, 1.0f, 0.45f) : new Color(1.0f, 0.25f, 0.25f);
            _liveBarRends[i].material.color = col;

            // Update value label — move it to top of the bar
            if (i < _liveValLabels.Count && _liveValLabels[i] != null)
            {
                _liveValLabels[i].text = "$" + quotes[i].currentPrice.ToString("F2")
                    + "\n" + quotes[i].ChangeString();
                _liveValLabels[i].color = up ? new Color(0.30f, 1.00f, 0.55f, 1f)
                                             : new Color(1.00f, 0.40f, 0.40f, 1f);
                // Slide label up to sit above bar top
                _liveValLabels[i].transform
                    .DOLocalMoveY(floorY + targetH + 0.06f, 0.60f)
                    .SetEase(DG.Tweening.Ease.OutCubic);
            }
        }
    }

    void OnDestroy()
    {
        if (_chartRoot != null) Destroy(_chartRoot);
        if (_lastInteracted == this) _lastInteracted = null;
    }

    // ── Static API (called by FloatingPanel) ──────────────────────────────
    public static BarChartBuilder GetLastInteracted()
    {
        return _lastInteracted;
    }

    // Called by ARBottomPanel / ARChartPlacer to set active chart
    public static void SetLastInteractedStatic(BarChartBuilder b)
    {
        _lastInteracted = b;
    }

    public void MakeLastInteracted()
    {
        _lastInteracted = this;
    }

    public static void SetMetricOnLastInteracted(MetricType metric)
    {
        if (_lastInteracted == null) return;
        _lastInteracted.metricToShow = metric;
        _lastInteracted._builtMetric = (MetricType)(-1);
        _lastInteracted.BuildChart();
    }

    // Spawn: places new chart to the right of the last interacted chart.
    // Uses camera-forward as the depth anchor so charts always face the player.
    // Spacing is in camera-right direction so the row spreads left-to-right
    // relative to the player regardless of which way they are facing.
    public static BarChartBuilder SpawnNewChart(Camera cam)
    {
        BarChartBuilder[] existing = FindObjectsOfType<BarChartBuilder>();
        int count = existing.Length;   // slot index for the NEW chart

        string uid = System.Guid.NewGuid().ToString().Substring(0, 4);
        var go = new GameObject("ChartManager_" + uid);
        var builder = go.AddComponent<BarChartBuilder>();

        // Pick next ticker in sequence after last interacted
        var tickers = MockFinancialData.GetTickers();
        string ticker = tickers[count % tickers.Count];
        if (_lastInteracted != null)
        {
            int idx = tickers.IndexOf(_lastInteracted.selectedTicker);
            ticker = tickers[(idx + 1) % tickers.Count];
        }

        builder.selectedTicker = ticker;
        builder.metricToShow = MetricType.Revenue;
        builder.cubeWidth = 1.6f;
        builder.cubeHeight = 1.3f;
        builder.cubeDepth = 0.55f;
        builder.distanceFromCamera = 2.2f;
        builder.heightOffset = 0.0f;
        builder.sideOffset = 0.0f;

        // ── Placement strategy ────────────────────────────────────────────
        // 1. Base point: directly in front of camera at spawn depth
        // 2. Spread: step right in CAMERA-RIGHT direction per existing chart
        //    so the row always fans out left-to-right as seen by the player
        // 3. Centre the row: offset left by half total row width so new
        //    charts grow outward symmetrically from centre
        const float spawnDist = 2.2f;   // depth in front of camera
        const float slotSpacing = 2.1f;   // gap between chart centres

        if (cam != null)
        {
            // Flat forward — ignore camera pitch so charts stay at eye level
            Vector3 flatForward = cam.transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            // Centre of the whole row
            Vector3 rowCentre = cam.transform.position
                              + flatForward * spawnDist;
            rowCentre.y = cam.transform.position.y; // eye level

            // Total row half-width so we can centre it
            float rowHalfW = (count * slotSpacing) * 0.5f;

            // This chart's slot position
            builder._spawnWorldPos = rowCentre
                - camRight * rowHalfW
                + camRight * (count * slotSpacing);
        }
        else
        {
            builder._spawnWorldPos = new Vector3(count * slotSpacing, 1f, 2f);
        }

        builder._useSpawnWorldPos = true;
        return builder;
    }

    // World-space spawn position set by SpawnNewChart
    public Vector3 _spawnWorldPos;
    public bool _useSpawnWorldPos = false;

    // =========================================================  BUILD
    public void BuildChart()
    {
        if (ComparisonMode)
        {
            BuildComparisonChart();
            return;
        }

        // LivePrice metric — builds a live multi-ticker price chart
        if (metricToShow == MetricType.LivePrice)
        {
            _isLiveMode = true;
            _isFundLive = false;   // disable fundamentals polling
            _livePollTimer = 0f;      // trigger immediate first fetch
            BuildLivePriceChart();
            return;
        }

        _isLiveMode = false;

        if (_builtTicker == selectedTicker && _builtMetric == metricToShow) return;
        _builtTicker = selectedTicker;
        _builtMetric = metricToShow;

        string metricKey = MetricNames[metricToShow];

        bool canLive = UseLiveData
                    && TwelveDataService.Instance != null
                    && DAVizConfig.Instance != null
                    && DAVizConfig.Instance.HasApiKey();

        if (canLive)
        {
            _isFundLive = true;
            _fundPollTimer = FUND_POLL_INTERVAL;   // start countdown immediately
            TwelveDataService.Instance.GetSeriesAsync(
                selectedTicker,
                metricKey,
                series => BuildFromSeries(series),
                () => BuildFromSeries(
                    MockFinancialData.GetSeries(selectedTicker, metricKey))
            );
        }
        else
        {
            _isFundLive = false;
            BuildFromSeries(MockFinancialData.GetSeries(selectedTicker, metricKey));
        }
    }

    // ── Live price chart — multi-ticker, polls every 15s ─────────────────
    private void BuildLivePriceChart()
    {
        string[] tickers = { "AAPL", "MSFT", "GOOGL", "AMZN", "META",
                              "NVDA", "TSLA", "NFLX", "JPM", "V" };

        // Clear old poll lists
        _liveBarRends.Clear();
        _liveValLabels.Clear();
        _liveTargetH.Clear();

        bool canLive = UseLiveData
                    && TwelveDataService.Instance != null
                    && DAVizConfig.Instance != null
                    && DAVizConfig.Instance.HasApiKey();

        if (canLive)
        {
            // Build skeleton chart with loading bars, then fill via coroutine
            BuildLivePriceSkeleton(tickers);
            StartCoroutine(FetchAndFillLivePrices(tickers));
        }
        else
        {
            // Mock: use last closing price from mock time-series
            var mockQuotes = new List<LiveQuote>();
            foreach (string tk in tickers)
            {
                var series = MockFinancialData.GetSeries(tk, "Revenue");
                // Use EPS * 20 as a fake "price" for visual variety in mock mode
                var eps = MockFinancialData.GetSeries(tk, "EPS");
                float fakePrice = eps.Count > 0 ? Mathf.Abs(eps[eps.Count - 1].displayValue) * 20f + 50f : 100f;
                mockQuotes.Add(new LiveQuote
                {
                    ticker = tk,
                    currentPrice = fakePrice,
                    change = UnityEngine.Random.Range(-5f, 5f),
                    changePct = UnityEngine.Random.Range(-2f, 2f),
                });
            }
            BuildLivePriceSkeleton(tickers);
            AnimateLiveBars(mockQuotes);
        }
    }

    private void BuildLivePriceSkeleton(string[] tickers)
    {
        bool had = _chartRoot != null;
        Vector3 pos = had ? _chartRoot.transform.position : Vector3.zero;
        Quaternion rot = had ? _chartRoot.transform.rotation : Quaternion.identity;
        Vector3 scl = had ? _chartRoot.transform.localScale : Vector3.one;
        if (had) Destroy(_chartRoot);

        _chartRoot = new GameObject("Chart_LivePrice");
        _chartRoot.transform.SetParent(null);

        float cmpW = cubeWidth * 2.2f;
        float cmpH = cubeHeight;
        float cmpD = cubeDepth;
        float hw = cmpW * 0.5f;
        float hh = cmpH * 0.5f;
        float hd = cmpD * 0.5f;
        float floorY = -hh + 0.09f;
        float maxBH = cmpH - 0.42f;
        float barW = (cmpW - 0.40f) / tickers.Length;
        float barD = cmpD * 0.42f;

        Color[] palette = {
            new Color(0.20f, 0.65f, 1.00f),
            new Color(0.00f, 0.80f, 0.40f),
            new Color(1.00f, 0.75f, 0.10f),
            new Color(1.00f, 0.40f, 0.10f),
            new Color(0.75f, 0.25f, 1.00f),
            new Color(0.10f, 1.00f, 0.80f),
            new Color(1.00f, 0.20f, 0.35f),
            new Color(0.95f, 0.95f, 0.20f),
            new Color(0.20f, 0.55f, 0.90f),
            new Color(0.60f, 0.90f, 0.30f),
        };

        // Glass box
        Color edgeCol = new Color(0.55f, 0.85f, 1.00f, 0.90f);
        float et = 0.012f;
        AddEdge("EV0", V(-hw, -hh, -hd), V(-hw, hh, -hd), et, edgeCol);
        AddEdge("EV1", V(hw, -hh, -hd), V(hw, hh, -hd), et, edgeCol);
        AddEdge("EV2", V(-hw, -hh, hd), V(-hw, hh, hd), et, edgeCol);
        AddEdge("EV3", V(hw, -hh, hd), V(hw, hh, hd), et, edgeCol);
        AddEdge("EH0", V(-hw, -hh, -hd), V(hw, -hh, -hd), et, edgeCol);
        AddEdge("EH1", V(-hw, -hh, hd), V(hw, -hh, hd), et, edgeCol);
        AddEdge("EH2", V(-hw, -hh, -hd), V(-hw, -hh, hd), et, edgeCol);
        AddEdge("EH3", V(hw, -hh, -hd), V(hw, -hh, hd), et, edgeCol);
        Color glassFace = new Color(0.55f, 0.85f, 1.00f, 0.06f);
        AddFace("Front", V(0, 0, -hd), cmpW, cmpH, glassFace);
        AddFace("Back", V(0, 0, hd), cmpW, cmpH, glassFace);
        AddFace("Left", V(-hw, 0, 0), cmpD, cmpH, glassFace, true);
        AddFace("Right", V(hw, 0, 0), cmpD, cmpH, glassFace, true);
        AddFace("Floor", V(0, -hh, 0), cmpW, cmpD, new Color(0.20f, 0.55f, 1f, 0.15f), false, true);

        // Grid lines
        for (int g = 1; g <= 4; g++)
        {
            float gy = floorY + (maxBH / 4f) * g;
            var gl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gl.name = "Grid_" + g;
            gl.transform.SetParent(_chartRoot.transform, false);
            gl.transform.localPosition = new Vector3(0, gy, -hd + 0.005f);
            gl.transform.localScale = new Vector3(cmpW - 0.02f, 0.003f, 0.003f);
            Destroy(gl.GetComponent<Collider>());
            var gmat = new Material(ShaderHelper.Unlit());
            gmat.color = new Color(0.55f, 0.75f, 1.00f, 0.20f);
            gl.GetComponent<Renderer>().material = gmat;
        }

        // Title + pulse dot
        CreateLabelOn("Title", V(0, hh + 0.06f, 0),
            "Live Stock Prices  (" + System.DateTime.Now.ToString("HH:mm") + ")",
            0.026f, new Color(0.80f, 1.00f, 1.00f, 1f));
        CreateLabelOn("PulseDot", V(hw - 0.12f, hh + 0.06f, 0),
            UseLiveData ? "●" : "○",
            0.030f, UseLiveData ? new Color(0.20f, 1f, 0.50f) : new Color(1f, 0.80f, 0.20f));

        // Skeleton bars (loading height = 20% of max) + labels
        for (int i = 0; i < tickers.Length; i++)
        {
            float bx = -hw + 0.20f + barW * (i + 0.5f);
            float skelH = maxBH * 0.20f;
            float by = floorY + skelH * 0.5f;
            Color col = palette[i % palette.Length];

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "LiveBar_" + tickers[i];
            bar.transform.SetParent(_chartRoot.transform, false);
            bar.transform.localPosition = new Vector3(bx, by, 0f);
            bar.transform.localScale = new Vector3(barW * 0.72f, skelH, barD);
            Destroy(bar.GetComponent<Collider>());
            var bmat = new Material(ShaderHelper.Unlit());
            bmat.color = new Color(col.r, col.g, col.b, 0.45f);  // dim until data arrives
            var rend = bar.GetComponent<Renderer>();
            rend.material = bmat;
            _liveBarRends.Add(rend);

            // Ticker label
            CreateLabelOn("Tk_" + tickers[i],
                V(bx, floorY - 0.055f, -hd - 0.002f),
                tickers[i], 0.018f, col);

            // Value label (loading...)
            var valGo = new GameObject("Val_" + tickers[i]);
            valGo.transform.SetParent(_chartRoot.transform, false);
            valGo.transform.localPosition = new Vector3(bx, floorY + skelH + 0.055f, -hd - 0.002f);
            valGo.transform.localRotation = Quaternion.identity;
            valGo.transform.localScale = Vector3.one;
            var tm = valGo.AddComponent<TextMesh>();
            tm.text = "..."; tm.characterSize = 0.016f; tm.fontSize = 16;
            tm.color = Color.white; tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            var mr = valGo.GetComponent<MeshRenderer>();
            if (mr) mr.sortingOrder = 4;
            _liveValLabels.Add(tm);
        }

        // Poll interval label bottom-right
        CreateLabelOn("PollLbl", V(hw - 0.35f, -hh - 0.06f, 0),
            "↻ every 15s", 0.016f, new Color(0.55f, 0.75f, 0.55f, 0.80f));

        if (had) { _chartRoot.transform.position = pos; _chartRoot.transform.rotation = rot; _chartRoot.transform.localScale = scl; }
        else PlaceInFrontOfCam();
        _lastInteracted = this;
    }

    System.Collections.IEnumerator FetchAndFillLivePrices(string[] tickers)
    {
        var quotes = new List<LiveQuote>();
        foreach (string tk in tickers)
        {
            bool done = false;
            TwelveDataService.Instance.GetQuoteAsync(tk,
                q => { quotes.Add(q); done = true; },
                () => { done = true; });
            float waited = 0f;
            while (!done && waited < 6f) { waited += Time.deltaTime; yield return null; }
        }
        if (quotes.Count > 0) AnimateLiveBars(quotes);

        // Update title timestamp
        if (_chartRoot != null)
        {
            var titleTm = _chartRoot.transform.Find("Title");
            if (titleTm != null)
            {
                var tm = titleTm.GetComponent<TextMesh>();
                if (tm != null)
                    tm.text = "Live Stock Prices  (" + System.DateTime.Now.ToString("HH:mm:ss") + ")";
            }
        }
    }

    // ── Comparison chart: all tickers, most recent year, side by side ─────
    private void BuildComparisonChart()
    {
        string metricKey = MetricNames[metricToShow];
        string[] tickers = { "AAPL", "MSFT", "GOOGL", "AMZN", "META",
                              "NVDA", "TSLA", "NFLX", "JPM", "V" };

        // Gather latest value per ticker from mock (always instant, no async needed)
        var values = new List<CompanyValue>();
        foreach (string tk in tickers)
        {
            var series = MockFinancialData.GetSeries(tk, metricKey);
            if (series == null || series.Count == 0) continue;
            var last = series[series.Count - 1];
            values.Add(new CompanyValue
            {
                ticker = tk,
                value = last.displayValue,
                unit = last.unit
            });
        }

        BuildComparisonFromValues(values, metricKey);
        _lastInteracted = this;
    }

    private struct CompanyValue
    {
        public string ticker;
        public float value;
        public string unit;
    }

    private void BuildComparisonFromValues(List<CompanyValue> values, string metricKey)
    {
        bool had = _chartRoot != null;
        Vector3 pos = had ? _chartRoot.transform.position : Vector3.zero;
        Quaternion rot = had ? _chartRoot.transform.rotation : Quaternion.identity;
        Vector3 scl = had ? _chartRoot.transform.localScale : Vector3.one;
        if (had) Destroy(_chartRoot);

        _chartRoot = new GameObject("Chart_Compare_" + metricKey);
        _chartRoot.transform.SetParent(null);

        // Wider box for comparison
        float cmpW = cubeWidth * 2.2f;
        float cmpH = cubeHeight;
        float cmpD = cubeDepth;

        // Colour palette — one per company
        Color[] palette = {
            new Color(0.20f, 0.65f, 1.00f, 1f),   // AAPL  — blue
            new Color(0.00f, 0.80f, 0.40f, 1f),   // MSFT  — green
            new Color(1.00f, 0.75f, 0.10f, 1f),   // GOOGL — amber
            new Color(1.00f, 0.40f, 0.10f, 1f),   // AMZN  — orange
            new Color(0.75f, 0.25f, 1.00f, 1f),   // META  — purple
            new Color(0.10f, 1.00f, 0.80f, 1f),   // NVDA  — cyan
            new Color(1.00f, 0.20f, 0.35f, 1f),   // TSLA  — red
            new Color(0.95f, 0.95f, 0.20f, 1f),   // NFLX  — yellow
            new Color(0.20f, 0.55f, 0.90f, 1f),   // JPM   — navy
            new Color(0.60f, 0.90f, 0.30f, 1f),   // V     — lime
        };

        // Find max for normalisation (skip negatives for scale)
        float maxVal = 0.001f;
        foreach (var cv in values)
            if (cv.value > maxVal) maxVal = cv.value;

        float hw = cmpW * 0.5f;
        float hh = cmpH * 0.5f;
        float hd = cmpD * 0.5f;
        float floorY = -hh + 0.09f;
        float maxBarH = cmpH - 0.42f;
        float barW = (cmpW - 0.40f) / Mathf.Max(values.Count, 1);
        float barDepth = cmpD * 0.42f;

        // Glass box edges
        Color edgeCol = new Color(0.55f, 0.85f, 1.00f, 0.90f);
        float et = 0.012f;
        // 4 vertical edges
        AddEdge("EV0", V(-hw, -hh, -hd), V(-hw, hh, -hd), et, edgeCol);
        AddEdge("EV1", V(hw, -hh, -hd), V(hw, hh, -hd), et, edgeCol);
        AddEdge("EV2", V(-hw, -hh, hd), V(-hw, hh, hd), et, edgeCol);
        AddEdge("EV3", V(hw, -hh, hd), V(hw, hh, hd), et, edgeCol);
        // 4 bottom edges
        AddEdge("EH0", V(-hw, -hh, -hd), V(hw, -hh, -hd), et, edgeCol);
        AddEdge("EH1", V(-hw, -hh, hd), V(hw, -hh, hd), et, edgeCol);
        AddEdge("EH2", V(-hw, -hh, -hd), V(-hw, -hh, hd), et, edgeCol);
        AddEdge("EH3", V(hw, -hh, -hd), V(hw, -hh, hd), et, edgeCol);

        // Glass faces
        Color glassFace = new Color(0.55f, 0.85f, 1.00f, 0.06f);
        AddFace("Front", V(0, 0, -hd), cmpW, cmpH, glassFace);
        AddFace("Back", V(0, 0, hd), cmpW, cmpH, glassFace);
        AddFace("Left", V(-hw, 0, 0), cmpD, cmpH, glassFace, true);
        AddFace("Right", V(hw, 0, 0), cmpD, cmpH, glassFace, true);
        AddFace("Top", V(0, hh, 0), cmpW, cmpD, glassFace, false, true);
        AddFace("Floor", V(0, -hh, 0), cmpW, cmpD, new Color(0.20f, 0.55f, 1f, 0.15f), false, true);

        // Grid lines on back face
        int gridLines = 4;
        for (int g = 1; g <= gridLines; g++)
        {
            float gy = floorY + (maxBarH / gridLines) * g;
            var gl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gl.name = "Grid_" + g;
            gl.transform.SetParent(_chartRoot.transform, false);
            gl.transform.localPosition = new Vector3(0, gy, -hd + 0.005f);
            gl.transform.localScale = new Vector3(cmpW - 0.02f, 0.003f, 0.003f);
            Destroy(gl.GetComponent<Collider>());
            var gmat = new Material(ShaderHelper.Unlit());
            gmat.color = new Color(0.55f, 0.75f, 1.00f, 0.25f);
            gl.GetComponent<Renderer>().material = gmat;
        }

        // Bars + labels
        for (int i = 0; i < values.Count; i++)
        {
            float t = values[i].value / maxVal;
            float barH = Mathf.Max(t * maxBarH, 0.005f);
            float bx = -hw + 0.20f + barW * (i + 0.5f);
            float by = floorY + barH * 0.5f;
            Color col = palette[i % palette.Length];

            // Negative bar flips downward
            bool neg = values[i].value < 0f;
            if (neg) { t = Mathf.Abs(values[i].value) / maxVal; barH = t * maxBarH * 0.5f; by = floorY - barH * 0.5f; }

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar_" + values[i].ticker;
            bar.transform.SetParent(_chartRoot.transform, false);
            bar.transform.localPosition = new Vector3(bx, by, 0f);
            bar.transform.localScale = new Vector3(barW * 0.72f, 0.001f, barDepth);
            Destroy(bar.GetComponent<Collider>());
            var bmat = new Material(ShaderHelper.Unlit());
            bmat.color = neg ? new Color(1f, 0.3f, 0.3f, 1f) : col;
            bar.GetComponent<Renderer>().material = bmat;

            // DOTween animate if available — same as regular bars
            float targetH = barH;
            int idx = i;
            bar.transform.DOScaleY(targetH, 0.50f)
               .SetDelay(idx * 0.05f)
               .SetEase(DG.Tweening.Ease.OutBack);

            // Ticker label below bar
            CreateLabelOn("Lbl_" + values[i].ticker,
                V(bx, floorY - 0.055f, -hd - 0.002f),
                values[i].ticker, 0.018f, col);

            // Value label above bar
            string valStr = FormatValue(values[i].value, values[i].unit);
            CreateLabelOn("Val_" + values[i].ticker,
                V(bx, floorY + barH + 0.04f, -hd - 0.002f),
                valStr, 0.016f, Color.white);
        }

        // Title
        string metricLabel = metricToShow.ToString();
        CreateLabelOn("Title", V(0, hh + 0.06f, 0),
            "Compare: " + metricLabel + "  (2023)", 0.028f,
            new Color(0.80f, 1.00f, 1.00f, 1f));

        // Data source badge
        string badge = UseLiveData ? "● LIVE" : "● MOCK";
        Color badgeCol = UseLiveData ? new Color(0.20f, 1f, 0.50f, 1f)
                                     : new Color(0.80f, 0.80f, 0.30f, 1f);
        CreateLabelOn("Badge", V(hw - 0.25f, hh - 0.06f, -hd - 0.002f),
            badge, 0.018f, badgeCol);

        if (had) { _chartRoot.transform.position = pos; _chartRoot.transform.rotation = rot; _chartRoot.transform.localScale = scl; }
        else PlaceInFrontOfCam();
        _lastInteracted = this;
    }

    // helper to create edge cylinders (reuse or inline)
    private void AddEdge(string n, Vector3 a, Vector3 b, float r, Color col)
    {
        Vector3 mid = (a + b) * 0.5f;
        float len = Vector3.Distance(a, b);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = n; go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = mid;
        go.transform.localScale = new Vector3(r, len * 0.5f, r);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        Destroy(go.GetComponent<Collider>());
        var mat = new Material(ShaderHelper.Unlit()); mat.color = col;
        go.GetComponent<Renderer>().material = mat;
    }

    private void AddFace(string n, Vector3 lp, float w, float h, Color col,
                         bool rotY = false, bool rotX = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = n; go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = lp;
        go.transform.localScale = new Vector3(w, h, 1f);
        if (rotY) go.transform.localRotation = Quaternion.Euler(0, 90, 0);
        else if (rotX) go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Destroy(go.GetComponent<Collider>());
        var mat = new Material(ShaderHelper.Sprite()); mat.color = col;
        go.GetComponent<Renderer>().material = mat;
    }

    private void CreateLabelOn(string n, Vector3 lp, string txt, float sz, Color col)
    {
        var go = new GameObject(n);
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text = txt; tm.characterSize = sz; tm.fontSize = 16;
        tm.color = col; tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 4;
    }

    private string FormatValue(float v, string unit)
    {
        if (unit == "USD/shares" || unit == "USD/share") return "$" + v.ToString("F2");
        if (Mathf.Abs(v) >= 1000f) return (v / 1000f).ToString("F1") + "T";
        return v.ToString("F1") + "B";
    }

    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    // ── Full geometry rebuild ─────────────────────────────────────────────
    private void BuildFromSeries(List<FinancialDataPoint> series)
    {
        // Save existing transform so chart stays where the user moved it
        bool had = _chartRoot != null;
        Vector3 pos = had ? _chartRoot.transform.position : Vector3.zero;
        Quaternion rot = had ? _chartRoot.transform.rotation : Quaternion.identity;
        Vector3 scl = had ? _chartRoot.transform.localScale : Vector3.one;
        if (had) Destroy(_chartRoot);

        _chartRoot = new GameObject("Chart_" + selectedTicker + "_" + metricToShow);

        if (series == null || series.Count == 0)
        {
            BuildEdges();
            BuildGlassFaces();
            CreateLabel("No data", new Vector3(0, 0, -(cubeDepth / 2f)), 0.026f, Color.red);
            if (had) RestoreTransform(pos, rot, scl); else PlaceInFrontOfCam();
            _lastInteracted = this;
            return;
        }

        // Cap to last 5 data points
        if (series.Count > 5)
            series = series.GetRange(series.Count - 5, 5);
        _seriesCount = series.Count;

        BuildEdges();
        BuildGlassFaces();
        BuildFloor();
        BuildGridLines();
        if (renderMode == RenderMode.Line)
        {
            BuildLineChart(series);
        }
        else
        {
            BuildBars(series);
            BuildTrendLine(series);
        }
        BuildTextLabels(series);
        BuildTitle();

        if (had) RestoreTransform(pos, rot, scl); else PlaceInFrontOfCam();
        _lastInteracted = this;
    }

    // ── Chart geometry ────────────────────────────────────────────────────

    private void BuildEdges()
    {
        float hw = cubeWidth / 2f;
        float hh = cubeHeight / 2f;
        float hd = cubeDepth / 2f;
        float et = edgeThickness;

        // 12 edges of the bounding box
        Vector3[][] edges =
        {
            // Bottom face
            new Vector3[]{new Vector3( 0, -hh, -hd), new Vector3(cubeWidth, et, et)},
            new Vector3[]{new Vector3( 0, -hh,  hd), new Vector3(cubeWidth, et, et)},
            new Vector3[]{new Vector3(-hw, -hh,  0), new Vector3(et, et, cubeDepth)},
            new Vector3[]{new Vector3( hw, -hh,  0), new Vector3(et, et, cubeDepth)},
            // Top face
            new Vector3[]{new Vector3( 0,  hh, -hd), new Vector3(cubeWidth, et, et)},
            new Vector3[]{new Vector3( 0,  hh,  hd), new Vector3(cubeWidth, et, et)},
            new Vector3[]{new Vector3(-hw,  hh,  0), new Vector3(et, et, cubeDepth)},
            new Vector3[]{new Vector3( hw,  hh,  0), new Vector3(et, et, cubeDepth)},
            // Vertical pillars
            new Vector3[]{new Vector3(-hw, 0, -hd), new Vector3(et, cubeHeight, et)},
            new Vector3[]{new Vector3( hw, 0, -hd), new Vector3(et, cubeHeight, et)},
            new Vector3[]{new Vector3(-hw, 0,  hd), new Vector3(et, cubeHeight, et)},
            new Vector3[]{new Vector3( hw, 0,  hd), new Vector3(et, cubeHeight, et)},
        };

        foreach (var e in edges)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Edge";
            cube.transform.SetParent(_chartRoot.transform, false);
            cube.transform.localPosition = e[0];
            cube.transform.localScale = e[1];
            Destroy(cube.GetComponent<Collider>());
            var mat = new Material(ShaderHelper.Unlit());
            mat.color = edgeColor;
            cube.GetComponent<Renderer>().material = mat;
        }
    }

    private void BuildGlassFaces()
    {
        float hw = cubeWidth / 2f;
        float hh = cubeHeight / 2f;
        float hd = cubeDepth / 2f;
        Color glass = new Color(0.20f, 0.55f, 1.00f, 0.08f);

        // Face positions and scales
        float[][] faces =
        {
            new float[]{ 0,    hh,  0,    cubeWidth,  0.001f, cubeDepth  }, // top
            new float[]{ 0,   -hh,  0,    cubeWidth,  0.001f, cubeDepth  }, // bottom
            new float[]{ hw,   0,   0,    0.001f, cubeHeight, cubeDepth  }, // right
            new float[]{-hw,   0,   0,    0.001f, cubeHeight, cubeDepth  }, // left
            new float[]{ 0,    0,   hd,   cubeWidth, cubeHeight, 0.001f  }, // back
            new float[]{ 0,    0,  -hd,   cubeWidth, cubeHeight, 0.001f  }, // front
        };

        foreach (var f in faces)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
            q.name = "GlassFace";
            q.transform.SetParent(_chartRoot.transform, false);
            q.transform.localPosition = new Vector3(f[0], f[1], f[2]);
            q.transform.localScale = new Vector3(f[3], f[4], f[5]);
            Destroy(q.GetComponent<Collider>());
            var mat = new Material(ShaderHelper.Sprite());
            mat.color = glass;
            q.GetComponent<Renderer>().material = mat;
        }
    }

    private void BuildFloor()
    {
        float fy = FloorY;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(_chartRoot.transform, false);
        floor.transform.localPosition = new Vector3(0, fy, 0);
        floor.transform.localScale = new Vector3(cubeWidth - 0.04f, 0.004f, cubeDepth - 0.04f);
        Destroy(floor.GetComponent<Collider>());
        floor.GetComponent<Renderer>().material =
            ShaderHelper.MakeUnlit(new Color(0.25f, 0.65f, 1.0f, 0.35f));
    }

    private void BuildGridLines()
    {
        int gridLines = 4;
        for (int g = 1; g <= gridLines; g++)
        {
            float gy = FloorY + MaxBarHeight * g / gridLines;
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "Grid";
            line.transform.SetParent(_chartRoot.transform, false);
            line.transform.localPosition = new Vector3(0, gy, 0);
            line.transform.localScale = new Vector3(cubeWidth - 0.06f, 0.002f, cubeDepth - 0.06f);
            Destroy(line.GetComponent<Collider>());
            line.GetComponent<Renderer>().material =
                ShaderHelper.MakeUnlit(new Color(0.35f, 0.85f, 1.0f, 0.12f));
        }
    }

    private void BuildBars(List<FinancialDataPoint> series)
    {
        // Clear stored refs — fresh build
        _fundBarRends.Clear();

        float innerLeft = -(cubeWidth / 2f) + 0.20f;
        float absMax = 0f;
        foreach (var dp in series)
            if (Mathf.Abs(dp.displayValue) > absMax)
                absMax = Mathf.Abs(dp.displayValue);
        if (absMax < 0.0001f) absMax = 1f;

        for (int i = 0; i < series.Count; i++)
        {
            float val = series[i].displayValue;
            bool neg = val < 0;
            float barH = (Mathf.Abs(val) / absMax) * MaxBarHeight;
            float xPos = innerLeft + i * BarSpacing + BarSpacing / 2f;
            float yPos = FloorY + (neg ? -barH / 2f : barH / 2f);

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar_" + series[i].fiscalYear;
            bar.transform.SetParent(_chartRoot.transform, false);
            bar.transform.localPosition = new Vector3(xPos, yPos, 0f);
            // Start at zero height for entry animation
            bar.transform.localScale = new Vector3(BarWidth, 0.001f, cubeDepth * 0.48f);
            Destroy(bar.GetComponent<Collider>());
            var rend = bar.GetComponent<Renderer>();
            rend.material = ShaderHelper.MakeUnlit(neg ? negativeColor : positiveColor);
            _fundBarRends.Add(rend);

            // Entry animation — bars grow up from floor
            float targetH = Mathf.Max(barH, 0.005f);
            float targetY = yPos;
            int idx = i;
            bar.transform.DOScaleY(targetH, 0.50f)
               .SetDelay(idx * 0.07f)
               .SetEase(DG.Tweening.Ease.OutBack);
            bar.transform.DOLocalMoveY(targetY, 0.50f)
               .SetDelay(idx * 0.07f)
               .SetEase(DG.Tweening.Ease.OutCubic);
        }
    }

    private void BuildTextLabels(List<FinancialDataPoint> series)
    {
        // Clear stored label refs
        _fundValLabels.Clear();

        float innerLeft = -(cubeWidth / 2f) + 0.20f;
        float fz = -(cubeDepth / 2f) - 0.06f;
        float absMax = 0f;
        foreach (var dp in series)
            if (Mathf.Abs(dp.displayValue) > absMax)
                absMax = Mathf.Abs(dp.displayValue);
        if (absMax < 0.0001f) absMax = 1f;

        for (int i = 0; i < series.Count; i++)
        {
            float val = series[i].displayValue;
            bool neg = val < 0;
            float barH = (Mathf.Abs(val) / absMax) * MaxBarHeight;
            float xPos = innerLeft + i * BarSpacing + BarSpacing / 2f;

            // Year label
            CreateLabel(series[i].fiscalYear.ToString(),
                new Vector3(xPos, -(cubeHeight / 2f) - 0.07f, fz),
                0.016f, Color.white);

            // Value label (staggered to prevent overlap)
            float stagger = (i % 2 == 0) ? 0f : 0.06f;
            float ly = FloorY + (neg ? -barH - 0.10f - stagger : barH + 0.06f + stagger);

            bool isEps = (series[i].unit == "USD/shares");
            string valStr = isEps
                ? "$" + val.ToString("F2")
                : "$" + Mathf.Abs(val).ToString("F1") + "B";

            // Store ref for live updates — moveable by AnimateFundBars
            TextMesh lbl = CreateLabelRef(valStr, new Vector3(xPos, ly, fz),
                        0.014f, neg ? negativeColor : positiveColor);
            _fundValLabels.Add(lbl);
        }
    }

    // ── Line chart (connects data points with cylinders) ─────────────────
    private void BuildLineChart(List<FinancialDataPoint> series)
    {
        if (series == null || series.Count < 2) return;

        float maxVal = float.MinValue;
        float minVal = float.MaxValue;
        foreach (FinancialDataPoint p in series)
        {
            if (p.displayValue > maxVal) maxVal = p.displayValue;
            if (p.displayValue < minVal) minVal = p.displayValue;
        }
        if (maxVal <= minVal) maxVal = minVal + 1f;
        float range = maxVal - minVal;

        float hw = cubeWidth * 0.5f;
        float spacing = (cubeWidth - 0.35f) / Mathf.Max(series.Count - 1, 1);
        float lineW = 0.025f;

        Color lineCol = new Color(0.20f, 0.80f, 1.00f, 1f);
        Color dotCol = new Color(1.00f, 1.00f, 1.00f, 1f);
        Color areaCol = new Color(0.10f, 0.55f, 0.85f, 0.18f);

        // Calculate point positions
        Vector3[] pts = new Vector3[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            float t = (series[i].displayValue - minVal) / range;
            float px = -hw + 0.175f + spacing * i;
            float py = FloorY + t * MaxBarHeight;
            pts[i] = new Vector3(px, py, 0f);
        }

        // Draw line segments between points
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "LineSeg_" + i;
            seg.transform.SetParent(_chartRoot.transform, false);
            seg.transform.localPosition = mid;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            seg.transform.localScale = new Vector3(len, lineW, lineW * 0.8f);
            Destroy(seg.GetComponent<Collider>());
            var mat = new Material(ShaderHelper.Unlit());
            mat.color = lineCol;
            seg.GetComponent<Renderer>().material = mat;

            // Area fill quad below each segment
            float areaH = (a.y + b.y) * 0.5f - FloorY;
            if (areaH > 0.001f)
            {
                Vector3 areaPos = new Vector3(mid.x, FloorY + areaH * 0.5f, 0.01f);
                var area = GameObject.CreatePrimitive(PrimitiveType.Cube);
                area.name = "Area_" + i;
                area.transform.SetParent(_chartRoot.transform, false);
                area.transform.localPosition = areaPos;
                area.transform.localScale = new Vector3(spacing, areaH, 0.001f);
                Destroy(area.GetComponent<Collider>());
                var amat = new Material(ShaderHelper.Sprite());
                amat.color = areaCol;
                area.GetComponent<Renderer>().material = amat;
            }
        }

        // Draw dots at each data point
        for (int i = 0; i < pts.Length; i++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Dot_" + i;
            dot.transform.SetParent(_chartRoot.transform, false);
            dot.transform.localPosition = pts[i];
            dot.transform.localScale = Vector3.one * (lineW * 2.2f);
            Destroy(dot.GetComponent<Collider>());
            var dmat = new Material(ShaderHelper.Unlit());
            dmat.color = dotCol;
            dot.GetComponent<Renderer>().material = dmat;
        }
    }

    private void BuildTrendLine(List<FinancialDataPoint> series)
    {
        if (series.Count < 2) return;

        float absMax = 0f;
        foreach (var dp in series)
            if (Mathf.Abs(dp.displayValue) > absMax)
                absMax = Mathf.Abs(dp.displayValue);
        if (absMax < 0.0001f) absMax = 1f;

        float leftX = -(cubeWidth / 2f) + 0.012f;
        float chartH = MaxBarHeight * 0.55f;
        float stepZ = (cubeDepth - 0.12f) / (series.Count - 1);
        float startZ = -(cubeDepth / 2f) + 0.06f;
        float startY = FloorY + 0.05f;

        var pts = new Vector3[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            float norm = series[i].displayValue / absMax;
            pts[i] = new Vector3(leftX, startY + norm * chartH, startZ + i * stepZ);
        }

        Color trendCol = new Color(1f, 0.85f, 0.2f);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 mid = (pts[i] + pts[i + 1]) * 0.5f;
            float len = Vector3.Distance(pts[i], pts[i + 1]);
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "Trend";
            seg.transform.SetParent(_chartRoot.transform, false);
            seg.transform.localPosition = mid;
            seg.transform.localRotation =
                Quaternion.LookRotation(pts[i + 1] - pts[i]) * Quaternion.Euler(0, 90, 0);
            seg.transform.localScale = new Vector3(0.004f, 0.004f, len);
            Destroy(seg.GetComponent<Collider>());
            seg.GetComponent<Renderer>().material =
                ShaderHelper.MakeUnlit(trendCol);
        }

        for (int i = 0; i < pts.Length; i++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "TrendDot";
            dot.transform.SetParent(_chartRoot.transform, false);
            dot.transform.localPosition = pts[i];
            dot.transform.localScale = Vector3.one * 0.02f;
            Destroy(dot.GetComponent<Collider>());
            dot.GetComponent<Renderer>().material =
                ShaderHelper.MakeUnlit(Color.white);
        }
    }

    private void BuildTitle()
    {
        string title = selectedTicker + " | " + metricToShow.ToString();
        CreateLabel(title,
            new Vector3(0f, cubeHeight * 0.5f + 0.08f, -(cubeDepth * 0.5f)),
            0.022f, new Color(0.35f, 0.85f, 1.0f));

        float fz = -(cubeDepth * 0.5f);

        // ── Countdown refresh label (bottom-right corner) ─────────────────
        _countdownLbl = CreateLabelRef(
            "↻ " + (int)FUND_POLL_INTERVAL + "s",
            new Vector3(cubeWidth * 0.5f - 0.12f, -(cubeHeight * 0.5f) - 0.07f, fz),
            0.013f, new Color(0.30f, 1.00f, 0.55f, 0.80f));

        // ── Live / mock data source badge (bottom-left) ───────────────────
        bool isLive = UseLiveData
                   && TwelveDataService.Instance != null
                   && DAVizConfig.Instance != null
                   && DAVizConfig.Instance.HasApiKey();
        string badge = isLive ? "● LIVE" : "○ MOCK";
        Color badgeCol = isLive
            ? new Color(0.20f, 1.00f, 0.50f, 1f)
            : new Color(1.00f, 0.75f, 0.15f, 1f);
        _dataSourceLbl = CreateLabelRef(badge,
            new Vector3(-(cubeWidth * 0.5f) + 0.12f, -(cubeHeight * 0.5f) - 0.07f, fz),
            0.013f, badgeCol);
    }

    // =========================================================  INPUT
    // All Input.* replaced with DAVizInput.* for cross-platform support
    private void HandleInput()
    {
        if (_chartRoot == null || _cam == null) return;
        if (IsMouseOverPanel()) return;

        Ray ray = DAVizInput.PointerRay(_cam);
        Bounds bounds = GetChartBounds();

        // ── Press: start drag ────────────────────────────────────────────
        if (DAVizInput.PrimaryDown)
        {
            float dist;
            if (bounds.IntersectRay(ray, out dist))
            {
                _isDragging = true;
                _dragDistance = dist;
                _dragOffset = _chartRoot.transform.position - ray.GetPoint(dist);
                _lastInteracted = this;
            }
        }

        // ── Release ───────────────────────────────────────────────────────
        if (DAVizInput.PrimaryUp) _isDragging = false;

        // ── Drag ──────────────────────────────────────────────────────────
        if (_isDragging && DAVizInput.PrimaryHeld)
            _chartRoot.transform.position = ray.GetPoint(_dragDistance) + _dragOffset;

        // ── Scroll / pinch-to-scale ───────────────────────────────────────
        if (!_isDragging)
        {
            float scroll = DAVizInput.ScrollDelta;
            if (Mathf.Abs(scroll) > 0.001f && bounds.IntersectRay(ray))
            {
                _lastInteracted = this;
                float s = Mathf.Clamp(
                    _chartRoot.transform.localScale.x + scroll * 0.5f, 0.3f, 3.0f);
                _chartRoot.transform.localScale = Vector3.one * s;
            }
        }
    }

    private bool IsMouseOverPanel()
    {
        var panel = FindObjectOfType<FloatingPanel>();
        return panel != null && panel.IsMouseOver();
    }

    private Bounds GetChartBounds()
    {
        if (_chartRoot == null) return new Bounds();
        float s = _chartRoot.transform.localScale.x;
        return new Bounds(_chartRoot.transform.position,
            new Vector3((cubeWidth + 0.1f) * s,
                        (cubeHeight + 0.1f) * s,
                        (cubeDepth + 0.1f) * s));
    }

    // =========================================================  HELPERS
    private void CreateLabel(string text, Vector3 lPos, float charSize, Color col)
    {
        var go = new GameObject("Lbl_" + text);
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = lPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.color = col;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 60;
    }

    // Same as CreateLabel but returns the TextMesh so callers can animate it later
    private TextMesh CreateLabelRef(string text, Vector3 lPos, float charSize, Color col)
    {
        var go = new GameObject("Lbl_" + text);
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = lPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.color = col;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 60;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 5;
        return tm;
    }

    private void PlaceInFrontOfCam()
    {
        FindCam();
        if (_cam == null) return;

        if (_useSpawnWorldPos)
        {
            // ADD+ spawn: use pre-calculated world-space slot.
            _chartRoot.transform.position = _spawnWorldPos;
            // Face the chart toward the camera (flat — no tilt)
            Vector3 lookDir = _chartRoot.transform.position - _cam.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                _chartRoot.transform.rotation =
                    Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            _useSpawnWorldPos = false;
            return;
        }

        // Scene-placed chart on Start: place directly in front of camera
        Vector3 flatFwd = _cam.transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        flatFwd.Normalize();

        _chartRoot.transform.position =
            _cam.transform.position
            + flatFwd * distanceFromCamera
            + _cam.transform.right * sideOffset
            + Vector3.up * heightOffset;

        _chartRoot.transform.rotation =
            Quaternion.LookRotation(flatFwd, Vector3.up);
    }

    private void FindCam()
    {
        // Standard cross-platform camera lookup — no MRTK
        _cam = Camera.main;
        if (_cam != null) return;
        _cam = FindObjectOfType<Camera>();
    }

    private void RestoreTransform(Vector3 pos, Quaternion rot, Vector3 scl)
    {
        _chartRoot.transform.position = pos;
        _chartRoot.transform.rotation = rot;
        _chartRoot.transform.localScale = scl;
    }
}