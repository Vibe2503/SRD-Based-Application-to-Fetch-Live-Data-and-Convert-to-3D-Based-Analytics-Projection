/*  CandlestickBuilder.cs  — DAV VR
 *  ══════════════════════════════════════════════════════════════════════
 *  Builds a 3D candlestick chart from 30-day OHLC data.
 *  Fetches from TwelveDataService. Falls back to generated mock OHLC
 *  derived from MockFinancialData if API unavailable.
 *
 *  Attach to a new empty GameObject in your scene (e.g. "CandlestickChart")
 *  or spawn via SpawnCandlestick(cam) static method from FloatingPanel.
 *
 *  Candle anatomy (3D):
 *    Body   = box from open to close (green=bullish, red=bearish)
 *    Wick   = thin cylinder from low to high
 *    Label  = date on X axis, price on Y axis
 *
 *  IL2CPP safe. No DOTween dependency (animates via coroutine).
 *  ══════════════════════════════════════════════════════════════════════ */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CandlestickBuilder : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────
    [Header("Ticker")]
    public string selectedTicker = "AAPL";

    [Header("Dimensions")]
    public float CandleWidth   = 0.06f;
    public float CandleSpacing = 0.10f;
    public float MaxHeight     = 1.20f;
    public float WickWidth     = 0.006f;

    [Header("Colors")]
    public Color BullishColor  = new Color(0.15f, 0.85f, 0.30f, 1f);  // green
    public Color BearishColor  = new Color(0.90f, 0.20f, 0.20f, 1f);  // red
    public Color WickColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color LabelColor    = new Color(0.85f, 0.85f, 0.90f, 1f);
    public Color BgColor       = new Color(0.05f, 0.05f, 0.07f, 0.85f);

    [Header("Position")]
    public float DistanceFromCamera = 1.8f;
    public float HeightOffset       = 0.0f;
    public float SideOffset         = 0.0f;

    // ── Runtime ───────────────────────────────────────────────────────
    GameObject _chartRoot;
    string     _builtTicker;
    Camera     _cam;

    static CandlestickBuilder _lastInteracted;

    // ── Animation ─────────────────────────────────────────────────────
    bool _animating = false;

    void Start()
    {
        _cam = Camera.main ? Camera.main : FindObjectOfType<Camera>();
        if (_lastInteracted == null) _lastInteracted = this;
        Place();
        BuildChart();
    }

    void OnMouseDown()
    {
        _lastInteracted = this;
    }

    public static CandlestickBuilder GetLastInteracted()
    {
        return _lastInteracted;
    }

    // ── Spawn static helper ───────────────────────────────────────────
    public static void SpawnCandlestick(Camera cam, string ticker = "AAPL")
    {
        var go = new GameObject("Candlestick_" + ticker);
        var cb = go.AddComponent<CandlestickBuilder>();
        cb.selectedTicker = ticker;

        CandlestickBuilder[] existing = FindObjectsOfType<CandlestickBuilder>();
        cb.SideOffset = existing.Length * 2.2f;
    }

    // ── Build chart ───────────────────────────────────────────────────
    public void BuildChart()
    {
        if (_builtTicker == selectedTicker) return;
        _builtTicker = selectedTicker;

        if (TwelveDataService.Instance != null &&
            DAVizConfig.Instance != null &&
            DAVizConfig.Instance.HasApiKey())
        {
            TwelveDataService.Instance.GetOHLCAsync(
                selectedTicker,
                points => BuildFromPoints(points),
                ()     => BuildFromPoints(GenerateMockOHLC())
            );
        }
        else
        {
            BuildFromPoints(GenerateMockOHLC());
        }
    }

    void BuildFromPoints(List<OHLCPoint> points)
    {
        if (_chartRoot != null) Destroy(_chartRoot);
        _chartRoot = new GameObject("CS_Root_" + selectedTicker);
        _chartRoot.transform.position = transform.position;

        if (points == null || points.Count == 0) return;

        // Find price range for scaling
        float minPrice = float.MaxValue;
        float maxPrice = float.MinValue;
        foreach (OHLCPoint p in points)
        {
            if (p.low  < minPrice) minPrice = p.low;
            if (p.high > maxPrice) maxPrice = p.high;
        }
        float range = maxPrice - minPrice;
        if (range < 0.001f) range = 1f;

        float totalW = points.Count * (CandleWidth + CandleSpacing);

        // Background panel
        MakeBackground(totalW);

        // Y-axis labels
        MakeYAxisLabels(minPrice, maxPrice);

        for (int i = 0; i < points.Count; i++)
        {
            OHLCPoint p = points[i];
            float cx = -totalW * 0.5f + i * (CandleWidth + CandleSpacing)
                       + CandleWidth * 0.5f;

            float openY  = (p.open  - minPrice) / range * MaxHeight;
            float closeY = (p.close - minPrice) / range * MaxHeight;
            float highY  = (p.high  - minPrice) / range * MaxHeight;
            float lowY   = (p.low   - minPrice) / range * MaxHeight;

            bool bullish = p.IsBullish();
            Color bodyCol = bullish ? BullishColor : BearishColor;

            float bodyBot  = Mathf.Min(openY, closeY);
            float bodyTop  = Mathf.Max(openY, closeY);
            float bodyH    = Mathf.Max(bodyTop - bodyBot, 0.005f);
            float bodyCenY = bodyBot + bodyH * 0.5f;

            // Body
            MakeBox("Body_" + i, cx, bodyCenY, bodyH, bodyCol, i);

            // Wick
            float wickH   = highY - lowY;
            float wickCenY = lowY + wickH * 0.5f;
            MakeWick("Wick_" + i, cx, wickCenY, wickH);

            // Date label (every 5th candle to avoid overlap)
            if (i % 5 == 0)
                MakeLabel(p.dateLabel, cx, -0.08f);
        }

        // Title
        MakeTitleLabel(selectedTicker + " | 30-Day Price");
    }

    // ── Geometry helpers ──────────────────────────────────────────────
    void MakeBox(string name, float cx, float cy, float h,
                 Color col, int index)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = new Vector3(cx, cy, 0f);
        go.transform.localScale    = new Vector3(CandleWidth, 0.001f, CandleWidth * 0.7f);
        Destroy(go.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = col;
        go.GetComponent<Renderer>().material = mat;

        // Animate body growing to full height
        StartCoroutine(GrowBox(go.transform, h, index * 0.04f));
    }

    IEnumerator GrowBox(Transform t, float targetH, float delay)
    {
        yield return new WaitForSeconds(delay);
        float elapsed = 0f;
        float dur     = 0.40f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            // Overshoot for springy feel
            float overshoot = 1f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
            Vector3 s = t.localScale;
            s.y = Mathf.Lerp(0.001f, targetH, progress) * overshoot;
            t.localScale = s;
            yield return null;
        }
        Vector3 final = t.localScale;
        final.y = targetH;
        t.localScale = final;
    }

    void MakeWick(string name, float cx, float cy, float h)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = new Vector3(cx, cy, 0f);
        go.transform.localScale    = new Vector3(WickWidth, h * 0.5f, WickWidth);
        Destroy(go.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = WickColor;
        go.GetComponent<Renderer>().material = mat;
    }

    void MakeBackground(float w)
    {
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "CS_BG";
        bg.transform.SetParent(_chartRoot.transform, false);
        bg.transform.localPosition = new Vector3(0f, MaxHeight * 0.5f, 0.01f);
        bg.transform.localScale    = new Vector3(w + 0.2f, MaxHeight + 0.3f, 1f);
        Destroy(bg.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = BgColor;
        bg.GetComponent<Renderer>().material = mat;
    }

    void MakeYAxisLabels(float minP, float maxP)
    {
        for (int i = 0; i <= 4; i++)
        {
            float t     = i / 4f;
            float price = Mathf.Lerp(minP, maxP, t);
            float y     = t * MaxHeight;
            MakeLabel("$" + price.ToString("F0"), -0.5f, y);
        }
    }

    void MakeLabel(string text, float x, float y)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = new Vector3(x, y, -0.02f);
        go.transform.localScale    = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text          = text;
        tm.characterSize = 0.018f;
        tm.fontSize      = 12;
        tm.color         = LabelColor;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
    }

    void MakeTitleLabel(string title)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(_chartRoot.transform, false);
        go.transform.localPosition = new Vector3(0f, MaxHeight + 0.10f, -0.02f);
        go.transform.localScale    = Vector3.one;
        var tm = go.AddComponent<TextMesh>();
        tm.text          = title;
        tm.characterSize = 0.022f;
        tm.fontSize      = 14;
        tm.fontStyle     = FontStyle.Bold;
        tm.color         = Color.white;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
    }

    // ── Placement ─────────────────────────────────────────────────────
    void Place()
    {
        if (!_cam) return;
        transform.position = _cam.transform.position
                           + _cam.transform.forward * DistanceFromCamera
                           + _cam.transform.right   * SideOffset
                           + Vector3.up             * HeightOffset;
        transform.LookAt(_cam.transform.position);
        transform.Rotate(0f, 180f, 0f);
    }

    // ── Mock OHLC generator (fallback when no API) ────────────────────
    List<OHLCPoint> GenerateMockOHLC()
    {
        var result = new List<OHLCPoint>();
        float price = 180f;
        System.Random rng = new System.Random(selectedTicker.GetHashCode());

        for (int i = 0; i < 30; i++)
        {
            float change = (float)(rng.NextDouble() - 0.48) * 4f;
            float open   = price;
            float close  = price + change;
            float high   = Mathf.Max(open, close) + (float)rng.NextDouble() * 2f;
            float low    = Mathf.Min(open, close) - (float)rng.NextDouble() * 2f;
            price        = close;

            result.Add(new OHLCPoint
            {
                dateLabel = "D" + (i + 1),
                open      = open,
                high      = high,
                low       = low,
                close     = close
            });
        }
        return result;
    }

    // ── Public chartRoot accessor (used by FreeCam raycast) ───────────
    public GameObject chartRoot { get { return _chartRoot; } }
}
