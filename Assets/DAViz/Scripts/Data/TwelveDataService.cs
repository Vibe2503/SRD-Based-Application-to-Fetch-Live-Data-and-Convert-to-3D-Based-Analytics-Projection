/*  TwelveDataService.cs  — DAV VR
 *  ══════════════════════════════════════════════════════════════════════
 *  Fetches live financial data from Twelve Data (https://twelvedata.com)
 *  Free tier: 800 calls/day, no credit card needed.
 *  Get your free key at: https://twelvedata.com/apikey
 *
 *  Attach to: ChartManager (same GameObject as DAVizConfig)
 *
 *  Provides:
 *    • Real-time quote     → price, change, % change, high, low, open
 *    • Time series         → daily close prices (for candlestick / sparkline)
 *    • Fundamentals        → revenue, net income, EPS (annual, via statistics)
 *
 *  Call flow:
 *    BarChartBuilder  → GetSeriesAsync(ticker, metric, onReady, onError)
 *    PriceTickerStrip → GetQuoteAsync(ticker, onReady, onError)
 *    CandlestickBuilder → GetOHLCAsync(ticker, onReady, onError)
 *
 *    Each call checks DataCache first.
 *    On miss → UnityWebRequest coroutine → parse → cache → callback.
 *    On any error → onError() → caller falls back to MockFinancialData.
 *
 *  IL2CPP safe:
 *    • No Newtonsoft, no reflection, no dynamic, no LINQ
 *    • Hand-rolled JSON field extraction
 *    • No [..n] range syntax, no => void bodies
 *  ══════════════════════════════════════════════════════════════════════ */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class TwelveDataService : MonoBehaviour
{
    public static TwelveDataService Instance { get; private set; }

    // ── Endpoints ─────────────────────────────────────────────────────
    const string BASE       = "https://api.twelvedata.com";
    const string QUOTE_EP   = BASE + "/quote?symbol={0}&apikey={1}";
    const string SERIES_EP  = BASE + "/time_series?symbol={0}&interval=1day&outputsize=5&apikey={1}";
    const string STATS_EP   = BASE + "/statistics?symbol={0}&apikey={1}";

    // ── Twelve Data statistics JSON paths for each metric ─────────────
    // Path format: "section/field" — parsed by ExtractStatField()
    static readonly Dictionary<string, string> MetricPaths =
        new Dictionary<string, string>
        {
            { "Revenue",          "financials/revenue_ttm"           },
            { "NetIncome",        "financials/net_income_ttm"        },
            { "TotalAssets",      "balance_sheet/total_assets_mrq"   },
            { "TotalLiabilities", "balance_sheet/total_liab_mrq"     },
            { "EPS",              "financials/diluted_eps_ttm"        },
        };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Annual financial series for bar charts.
    /// Returns List of FinancialDataPoint (year + value).
    /// </summary>
    public void GetSeriesAsync(string ticker, string metric,
                               Action<List<FinancialDataPoint>> onReady,
                               Action onError)
    {
        if (!CanFetch()) { onError(); return; }

        string cacheKey = DataCache.SeriesKey(ticker, metric);
        var cached = DataCache.Get<List<FinancialDataPoint>>(cacheKey);
        if (cached != null) { onReady(cached); return; }

        StartCoroutine(FetchStats(ticker, metric, onReady, onError));
    }

    /// <summary>
    /// Real-time quote for price ticker strip.
    /// Returns LiveQuote (price, change, % change).
    /// </summary>
    public void GetQuoteAsync(string ticker,
                              Action<LiveQuote> onReady,
                              Action onError)
    {
        if (!CanFetch()) { onError(); return; }

        string cacheKey = DataCache.QuoteKey(ticker);
        var cached = DataCache.Get<LiveQuote>(cacheKey);
        if (cached != null) { onReady(cached); return; }

        StartCoroutine(FetchQuote(ticker, onReady, onError));
    }

    /// <summary>
    /// Daily OHLC time series for candlestick charts.
    /// Returns List of OHLCPoint.
    /// </summary>
    public void GetOHLCAsync(string ticker,
                             Action<List<OHLCPoint>> onReady,
                             Action onError)
    {
        if (!CanFetch()) { onError(); return; }

        string cacheKey = "ohlc:" + ticker;
        var cached = DataCache.Get<List<OHLCPoint>>(cacheKey);
        if (cached != null) { onReady(cached); return; }

        StartCoroutine(FetchOHLC(ticker, onReady, onError));
    }

    // ══════════════════════════════════════════════════════════════════
    //  COROUTINES
    // ══════════════════════════════════════════════════════════════════

    IEnumerator FetchQuote(string ticker,
                           Action<LiveQuote> onReady,
                           Action onError)
    {
        string url = string.Format(QUOTE_EP, ticker,
                                   DAVizConfig.Instance.twelveDataApiKey);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[TwelveData] Quote failed for " + ticker
                                 + ": " + req.error);
                onError();
                yield break;
            }

            LiveQuote q = ParseQuote(req.downloadHandler.text, ticker);
            if (q == null) { onError(); yield break; }

            // Quotes are fresh for 30 seconds
            DataCache.Set(DataCache.QuoteKey(ticker), q, 30f);
            onReady(q);
        }
    }

    IEnumerator FetchStats(string ticker, string metric,
                           Action<List<FinancialDataPoint>> onReady,
                           Action onError)
    {
        string url = string.Format(STATS_EP, ticker,
                                   DAVizConfig.Instance.twelveDataApiKey);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[TwelveData] Stats failed for " + ticker
                                 + ": " + req.error);
                onError();
                yield break;
            }

            List<FinancialDataPoint> series =
                ParseStats(req.downloadHandler.text, metric);

            if (series == null || series.Count == 0)
            {
                Debug.LogWarning("[TwelveData] No stats data for "
                                 + ticker + "/" + metric);
                onError();
                yield break;
            }

            float expiry = DAVizConfig.Instance != null
                ? DAVizConfig.Instance.cacheExpirySeconds : 300f;
            DataCache.Set(DataCache.SeriesKey(ticker, metric), series, expiry);
            onReady(series);
        }
    }

    IEnumerator FetchOHLC(string ticker,
                          Action<List<OHLCPoint>> onReady,
                          Action onError)
    {
        // Request 30 days for candlestick display
        string url = string.Format(
            BASE + "/time_series?symbol={0}&interval=1day&outputsize=30&apikey={1}",
            ticker, DAVizConfig.Instance.twelveDataApiKey);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[TwelveData] OHLC failed for " + ticker
                                 + ": " + req.error);
                onError();
                yield break;
            }

            List<OHLCPoint> points = ParseOHLC(req.downloadHandler.text);
            if (points == null || points.Count == 0) { onError(); yield break; }

            // OHLC cache 5 minutes
            DataCache.Set("ohlc:" + ticker, points, 300f);
            onReady(points);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  JSON PARSERS  (hand-rolled, IL2CPP safe)
    // ══════════════════════════════════════════════════════════════════

    // Twelve Data /quote response:
    // { "symbol":"AAPL","close":"189.42","change":"1.23","percent_change":"0.65",
    //   "high":"191.0","low":"187.5","open":"188.0" }
    LiveQuote ParseQuote(string json, string ticker)
    {
        try
        {
            float close  = ExtractFloat(json, "\"close\":\"");
            float change = ExtractFloat(json, "\"change\":\"");
            float pct    = ExtractFloat(json, "\"percent_change\":\"");
            float high   = ExtractFloat(json, "\"high\":\"");
            float low    = ExtractFloat(json, "\"low\":\"");
            float open   = ExtractFloat(json, "\"open\":\"");

            if (close <= 0f) return null;

            return new LiveQuote
            {
                ticker       = ticker,
                currentPrice = close,
                change       = change,
                changePct    = pct,
                high         = high,
                low          = low,
                open         = open
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TwelveData] Quote parse error: " + ex.Message);
            return null;
        }
    }

    // Twelve Data /statistics — TTM and MRQ fundamentals
    // We synthesise a 1-point "series" from the TTM/MRQ value.
    // For richer annual series, SEC EDGAR is used as secondary source.
    List<FinancialDataPoint> ParseStats(string json, string metric)
    {
        var result = new List<FinancialDataPoint>();
        try
        {
            string path;
            if (!MetricPaths.TryGetValue(metric, out path)) return result;

            bool isEps = (metric == "EPS");
            string fieldName = path.Contains("/")
                ? path.Substring(path.IndexOf("/") + 1) : path;

            float val = ExtractFloat(json, "\"" + fieldName + "\":");
            if (float.IsNaN(val) || val == 0f) return result;

            float displayVal = isEps ? val : val / 1_000_000_000f;

            // Use current year as the data point label
            int year = DateTime.Now.Year;
            result.Add(new FinancialDataPoint
            {
                fiscalYear   = year,
                displayValue = displayVal,
                unit         = isEps ? "USD/share" : "USD-B"
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TwelveData] Stats parse error: " + ex.Message);
        }
        return result;
    }

    // Twelve Data /time_series response:
    // { "values": [ {"datetime":"2024-01-15","open":"...","high":"...","low":"...","close":"..."}, ... ] }
    List<OHLCPoint> ParseOHLC(string json)
    {
        var result = new List<OHLCPoint>();
        try
        {
            string[] dateBlocks = json.Split(new string[] { "\"datetime\":\"" },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < dateBlocks.Length; i++)
            {
                string block = dateBlocks[i];
                // Extract date label (up to closing quote)
                int dateEnd = block.IndexOf("\"");
                string dateLabel = dateEnd > 0
                    ? block.Substring(0, dateEnd) : "?";

                float open  = ExtractFloat(block, "\"open\":\"");
                float high  = ExtractFloat(block, "\"high\":\"");
                float low   = ExtractFloat(block, "\"low\":\"");
                float close = ExtractFloat(block, "\"close\":\"");

                if (close <= 0f) continue;

                result.Add(new OHLCPoint
                {
                    dateLabel = dateLabel,
                    open      = open,
                    high      = high,
                    low       = low,
                    close     = close
                });
            }

            // Reverse so oldest first (API returns newest first)
            result.Reverse();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TwelveData] OHLC parse error: " + ex.Message);
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════
    //  MICRO HELPERS
    // ══════════════════════════════════════════════════════════════════

    // Extract float after a key string — handles both "key":value and "key":"value"
    float ExtractFloat(string json, string key)
    {
        int start = json.IndexOf(key);
        if (start < 0) return float.NaN;
        start += key.Length;
        // Skip optional opening quote
        if (start < json.Length && json[start] == '"') start++;
        // Skip whitespace
        while (start < json.Length &&
               (json[start] == ' ' || json[start] == '\n' || json[start] == '\r'))
            start++;
        int end = start;
        while (end < json.Length &&
               (char.IsDigit(json[end]) || json[end] == '-' ||
                json[end] == '.' || json[end] == 'e' ||
                json[end] == 'E' || json[end] == '+'))
            end++;
        if (end == start) return float.NaN;
        float val;
        if (float.TryParse(json.Substring(start, end - start),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out val))
            return val;
        return float.NaN;
    }

    bool CanFetch()
    {
        if (DAVizConfig.Instance == null)
        {
            Debug.LogWarning("[TwelveData] DAVizConfig not found in scene.");
            return false;
        }
        if (!DAVizConfig.Instance.HasApiKey())
        {
            Debug.LogWarning("[TwelveData] No API key. Add DAVizConfig to scene and set twelveDataApiKey.");
            return false;
        }
        return true;
    }
}

// ── Supporting data types ─────────────────────────────────────────────────

public class LiveQuote
{
    public string ticker;
    public float  currentPrice;
    public float  change;
    public float  changePct;
    public float  high;
    public float  low;
    public float  open;

    public bool IsPositive() { return change >= 0f; }

    public string PriceString()
    {
        return "$" + currentPrice.ToString("F2");
    }

    public string ChangeString()
    {
        string sign = change >= 0f ? "▲+" : "▼";
        return sign + change.ToString("F2")
               + " (" + changePct.ToString("F2") + "%)";
    }
}

public class OHLCPoint
{
    public string dateLabel;
    public float  open;
    public float  high;
    public float  low;
    public float  close;

    public bool IsBullish() { return close >= open; }

    public float Body()  { return Mathf.Abs(close - open); }
    public float Upper() { return high - Mathf.Max(open, close); }
    public float Lower() { return Mathf.Min(open, close) - low; }
}
