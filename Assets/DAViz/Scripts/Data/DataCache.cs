/*  DataCache.cs  — DAV VR
 *  ═══════════════════════════════════════════════════════════════════
 *  In-memory cache with expiry timestamps.
 *  Shared across all BarChartBuilders so 5 charts showing AAPL
 *  only make 1 API call instead of 5.
 *
 *  Static — no MonoBehaviour needed.
 *  IL2CPP safe.
 *  ═══════════════════════════════════════════════════════════════════ */

using System.Collections.Generic;
using UnityEngine;

public static class DataCache
{
    class CacheEntry
    {
        public object  Data;
        public float   StoredAt;   // Time.realtimeSinceStartup
        public float   ExpirySeconds;

        public bool IsExpired()
        {
            return (Time.realtimeSinceStartup - StoredAt) > ExpirySeconds;
        }
    }

    static readonly Dictionary<string, CacheEntry> _store =
        new Dictionary<string, CacheEntry>();

    // ── Store ─────────────────────────────────────────────────────────
    public static void Set(string key, object data, float expirySeconds = 300f)
    {
        _store[key] = new CacheEntry
        {
            Data          = data,
            StoredAt      = Time.realtimeSinceStartup,
            ExpirySeconds = expirySeconds
        };
    }

    // ── Retrieve ──────────────────────────────────────────────────────
    // Returns null if missing or expired.
    public static T Get<T>(string key) where T : class
    {
        CacheEntry entry;
        if (!_store.TryGetValue(key, out entry)) return null;
        if (entry.IsExpired())
        {
            _store.Remove(key);
            return null;
        }
        return entry.Data as T;
    }

    // ── Check ─────────────────────────────────────────────────────────
    public static bool Has(string key)
    {
        CacheEntry entry;
        if (!_store.TryGetValue(key, out entry)) return false;
        if (entry.IsExpired()) { _store.Remove(key); return false; }
        return true;
    }

    // ── Invalidate ────────────────────────────────────────────────────
    public static void Clear(string key)
    {
        if (_store.ContainsKey(key)) _store.Remove(key);
    }

    public static void ClearAll()
    {
        _store.Clear();
        Debug.Log("[DataCache] All entries cleared.");
    }

    // ── Cache key helpers ─────────────────────────────────────────────
    public static string QuoteKey(string ticker)
    {
        return "quote:" + ticker;
    }

    public static string FinancialsKey(string ticker, string metric)
    {
        return "fin:" + ticker + ":" + metric;
    }

    public static string SeriesKey(string ticker, string metric)
    {
        return "series:" + ticker + ":" + metric;
    }
}
