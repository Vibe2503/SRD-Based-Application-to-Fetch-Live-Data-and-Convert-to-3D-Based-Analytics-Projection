/*  DAVizConfig.cs  — DAV VR
 *  ══════════════════════════════════════════════════════════════════════
 *  Central config for all external API keys and runtime settings.
 *  Add this component to ChartManager. Set keys once in the Inspector.
 *
 *  APIs used:
 *    Twelve Data  → https://twelvedata.com/apikey  (free, 800 calls/day)
 *    SEC EDGAR    → no key needed (automatic fallback)
 *  ══════════════════════════════════════════════════════════════════════ */

using UnityEngine;

public class DAVizConfig : MonoBehaviour
{
    public static DAVizConfig Instance { get; private set; }

    [Header("Twelve Data API  —  https://twelvedata.com/apikey")]
    [Tooltip("Free tier: 800 calls/day. Sign up takes 2 minutes.")]
    public string twelveDataApiKey = "";

    [Header("Cache Settings")]
    [Tooltip("Seconds before fundamentals cache expires")]
    public float cacheExpirySeconds = 300f;
    [Tooltip("Seconds before live quote cache expires")]
    public float quoteCacheSeconds  = 30f;

    [Header("Fallback")]
    [Tooltip("Use MockFinancialData if any API call fails")]
    public bool useMockOnError   = true;
    [Tooltip("Use SEC EDGAR as secondary source for annual fundamentals")]
    public bool useEdgarFallback = true;

    [Header("Rate Limiting")]
    [Tooltip("Min seconds between successive API calls")]
    public float minCallInterval = 0.5f;

    int _callsToday = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool HasApiKey()
    {
        return !string.IsNullOrEmpty(twelveDataApiKey) &&
               twelveDataApiKey.Length > 8;
    }

    public void TrackCall()        { _callsToday++; }
    public bool IsNearDailyLimit() { return _callsToday >= 720; }
    public int  CallsToday()       { return _callsToday; }
}
