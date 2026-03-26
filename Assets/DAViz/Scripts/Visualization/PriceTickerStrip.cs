/*  PriceTickerStrip.cs  — DAV VR
 *  ═══════════════════════════════════════════════════════════════════
 *  Scrolling real-time price strip shown above the FloatingPanel.
 *  Attach to FloatingPanelManager (same GO as FloatingPanel).
 *
 *  Shows:  AAPL $189.42 ▲+1.2%   MSFT $415.30 ▲+0.8%  ...
 *
 *  Fetches from TwelveDataService every 30 seconds.
 *  Falls back to "--" if no API key or fetch fails.
 *  Scrolls smoothly left using TextMesh position offset.
 *  IL2CPP safe — no reflection, no LINQ, no dynamic.
 *  ═══════════════════════════════════════════════════════════════════ */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PriceTickerStrip : MonoBehaviour
{
    [Header("Layout")]
    public float StripWidth = 3.40f;   // match FloatingPanel PW
    public float StripHeight = 0.085f;
    public float YAbovePanel = 0.42f;   // offset above panel centre
    public float DistFromCam = 1.38f;   // slightly closer than panel

    [Header("Scroll")]
    public float ScrollSpeed = 0.18f;   // world units per second

    [Header("Refresh")]
    public float RefreshEvery = 30f;     // seconds between Finnhub calls

    [Header("Colors")]
    public Color BgColor = new Color(0.04f, 0.04f, 0.06f, 0.92f);
    public Color PositiveColor = new Color(0.20f, 1.00f, 0.45f, 1.00f);
    public Color NegativeColor = new Color(1.00f, 0.30f, 0.30f, 1.00f);
    public Color NeutralColor = new Color(0.75f, 0.75f, 0.80f, 1.00f);

    static readonly string[] Tickers = { "AAPL", "MSFT", "GOOGL", "AMZN", "META" };

    // Runtime
    Camera _cam;
    GameObject _root;
    TextMesh _label;
    GameObject _labelGO;
    float _scrollX;
    float _fullWidth;
    float _nextRefresh;
    string _displayText = "  Loading prices...  ";

    Dictionary<string, LiveQuote> _quotes = new Dictionary<string, LiveQuote>();

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        // Disabled — PriceTickerStrip creates the black bar across the top.
        // Re-enable by uncommenting the lines below when you want the ticker.
        // FindCam();
        // Build();
        // _nextRefresh = 0f;
    }

    void Update()
    {
        if (!_root || !_cam) return;

        // Position above FloatingPanel
        LockPosition();

        // Scroll text
        ScrollText();

        // Refresh prices on timer
        if (Time.time >= _nextRefresh)
        {
            _nextRefresh = Time.time + RefreshEvery;
            StartCoroutine(RefreshAllQuotes());
        }
    }

    // ─────────────────────────────────────────────────────────────────
    void LockPosition()
    {
        Vector3 target = _cam.transform.position
                       + _cam.transform.forward * DistFromCam
                       + _cam.transform.up * YAbovePanel;
        _root.transform.position = target;

        Vector3 tc = _cam.transform.position - _root.transform.position;
        if (tc.sqrMagnitude > 0.001f)
            _root.transform.rotation = Quaternion.LookRotation(-tc, _cam.transform.up);
    }

    void ScrollText()
    {
        if (_labelGO == null) return;
        _scrollX -= ScrollSpeed * Time.deltaTime;

        // Calculate text width estimate (rough: chars × charSize × 0.55)
        float textW = _displayText.Length * _label.characterSize * 0.55f;
        if (_scrollX < -textW) _scrollX = StripWidth * 0.5f;

        _labelGO.transform.localPosition = new Vector3(_scrollX, 0f, -0.002f);
    }

    // ─────────────────────────────────────────────────────────────────
    IEnumerator RefreshAllQuotes()
    {
        if (TwelveDataService.Instance == null) yield break;

        int done = 0;
        bool anyFailed = false;

        foreach (string ticker in Tickers)
        {
            string cap = ticker;
            TwelveDataService.Instance.GetQuoteAsync(cap,
                quote =>
                {
                    _quotes[cap] = quote;
                    done++;
                },
                () =>
                {
                    anyFailed = true;
                    done++;
                });

            // Small delay between calls to respect rate limits
            yield return new WaitForSeconds(0.3f);
        }

        // Wait until all 5 calls complete
        float timeout = Time.time + 15f;
        while (done < Tickers.Length && Time.time < timeout)
            yield return null;

        RebuildDisplayText();
    }

    void RebuildDisplayText()
    {
        if (_quotes.Count == 0)
        {
            _displayText = "  No live data — check API key in DAVizConfig  ";
            UpdateLabel();
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (string ticker in Tickers)
        {
            sb.Append("  ");
            sb.Append(ticker);
            sb.Append(" ");

            LiveQuote q;
            if (_quotes.TryGetValue(ticker, out q))
            {
                sb.Append(q.PriceString());
                sb.Append(" ");
                sb.Append(q.ChangeString());
            }
            else
            {
                sb.Append("--");
            }
            sb.Append("   |");
        }
        _displayText = sb.ToString();
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (_label != null)
            _label.text = _displayText;
    }

    // ─────────────────────────────────────────────────────────────────
    void Build()
    {
        if (_root) Destroy(_root);
        _root = new GameObject("PriceStrip_Root");

        // Background
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "StripBG";
        bg.transform.SetParent(_root.transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(StripWidth, StripHeight, 1f);
        Destroy(bg.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = BgColor;
        bg.GetComponent<Renderer>().material = mat;

        // Scrolling text label
        _labelGO = new GameObject("StripLabel");
        _labelGO.transform.SetParent(_root.transform, false);
        _labelGO.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        _labelGO.transform.localRotation = Quaternion.identity;
        _labelGO.transform.localScale = Vector3.one;
        _label = _labelGO.AddComponent<TextMesh>();
        _label.text = _displayText;
        _label.characterSize = 0.018f;
        _label.fontSize = 14;
        _label.color = NeutralColor;
        _label.anchor = TextAnchor.MiddleLeft;
        _label.alignment = TextAlignment.Left;
        _scrollX = StripWidth * 0.5f;
    }

    void FindCam()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindObjectOfType<Camera>();
    }
}