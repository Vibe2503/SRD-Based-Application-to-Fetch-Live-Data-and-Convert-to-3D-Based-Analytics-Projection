/*  MockFinancialData.cs  — DAV VR · Data Layer
 *  ══════════════════════════════════════════════════════════════════════
 *  All financial data is hard-coded. Zero network calls.
 *  Values approximate 2019-2023 annual figures (USD Billions, EPS in USD).
 *
 *  Public API:
 *    MockFinancialData.GetTickers()                    → List<string>
 *    MockFinancialData.GetSeries(ticker, metricKey)    → List<FinancialDataPoint>
 *    MockFinancialData.GetByTicker(ticker)             → List<FinancialEntry>
 *  ══════════════════════════════════════════════════════════════════════ */

using System.Collections.Generic;
using UnityEngine;

// ── Data types ────────────────────────────────────────────────────────────
public struct FinancialDataPoint
{
    public int fiscalYear;
    public float displayValue;   // Billions for most metrics, USD for EPS
    public string unit;           // "USD-B" or "USD/shares"
}

public struct FinancialEntry
{
    public string Company;
    public string Ticker;
    public int Year;
    public float Revenue;
    public float NetIncome;
    public float TotalAssets;
    public float TotalLiabilities;
    public float EPS;
}

// ── Static data store ─────────────────────────────────────────────────────
public static class MockFinancialData
{
    static readonly List<FinancialEntry> _data = new List<FinancialEntry>
    {
        // AAPL ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Apple",  Ticker="AAPL", Year=2019, Revenue=260.2f, NetIncome= 55.3f, TotalAssets=338.5f, TotalLiabilities=248.0f, EPS= 2.97f },
        new FinancialEntry { Company="Apple",  Ticker="AAPL", Year=2020, Revenue=274.5f, NetIncome= 57.4f, TotalAssets=323.9f, TotalLiabilities=258.5f, EPS= 3.28f },
        new FinancialEntry { Company="Apple",  Ticker="AAPL", Year=2021, Revenue=365.8f, NetIncome= 94.7f, TotalAssets=351.0f, TotalLiabilities=287.9f, EPS= 5.61f },
        new FinancialEntry { Company="Apple",  Ticker="AAPL", Year=2022, Revenue=394.3f, NetIncome= 99.8f, TotalAssets=352.8f, TotalLiabilities=302.1f, EPS= 6.11f },
        new FinancialEntry { Company="Apple",  Ticker="AAPL", Year=2023, Revenue=383.3f, NetIncome= 97.0f, TotalAssets=352.6f, TotalLiabilities=290.4f, EPS= 6.13f },
        // MSFT ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Microsoft", Ticker="MSFT", Year=2019, Revenue=125.8f, NetIncome= 39.2f, TotalAssets=286.6f, TotalLiabilities=184.2f, EPS= 5.06f },
        new FinancialEntry { Company="Microsoft", Ticker="MSFT", Year=2020, Revenue=143.0f, NetIncome= 44.3f, TotalAssets=301.3f, TotalLiabilities=183.0f, EPS= 5.76f },
        new FinancialEntry { Company="Microsoft", Ticker="MSFT", Year=2021, Revenue=168.1f, NetIncome= 61.3f, TotalAssets=333.8f, TotalLiabilities=191.8f, EPS= 8.05f },
        new FinancialEntry { Company="Microsoft", Ticker="MSFT", Year=2022, Revenue=198.3f, NetIncome= 72.7f, TotalAssets=364.8f, TotalLiabilities=198.3f, EPS= 9.65f },
        new FinancialEntry { Company="Microsoft", Ticker="MSFT", Year=2023, Revenue=211.9f, NetIncome= 72.4f, TotalAssets=411.9f, TotalLiabilities=205.8f, EPS= 9.72f },
        // GOOGL ─────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Alphabet", Ticker="GOOGL", Year=2019, Revenue=161.9f, NetIncome= 34.3f, TotalAssets=275.9f, TotalLiabilities= 74.5f, EPS= 2.46f },
        new FinancialEntry { Company="Alphabet", Ticker="GOOGL", Year=2020, Revenue=182.5f, NetIncome= 40.3f, TotalAssets=319.6f, TotalLiabilities= 97.1f, EPS= 2.93f },
        new FinancialEntry { Company="Alphabet", Ticker="GOOGL", Year=2021, Revenue=257.6f, NetIncome= 76.0f, TotalAssets=359.3f, TotalLiabilities=107.6f, EPS= 5.61f },
        new FinancialEntry { Company="Alphabet", Ticker="GOOGL", Year=2022, Revenue=282.8f, NetIncome= 59.9f, TotalAssets=359.3f, TotalLiabilities=109.1f, EPS= 4.56f },
        new FinancialEntry { Company="Alphabet", Ticker="GOOGL", Year=2023, Revenue=307.4f, NetIncome= 73.8f, TotalAssets=402.4f, TotalLiabilities=109.1f, EPS= 5.80f },
        // AMZN ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Amazon",  Ticker="AMZN", Year=2019, Revenue=280.5f, NetIncome= 11.6f, TotalAssets=225.2f, TotalLiabilities=163.2f, EPS= 1.16f },
        new FinancialEntry { Company="Amazon",  Ticker="AMZN", Year=2020, Revenue=386.1f, NetIncome= 21.3f, TotalAssets=321.2f, TotalLiabilities=227.8f, EPS= 1.41f },
        new FinancialEntry { Company="Amazon",  Ticker="AMZN", Year=2021, Revenue=469.8f, NetIncome= 33.4f, TotalAssets=420.5f, TotalLiabilities=282.3f, EPS= 1.45f },
        new FinancialEntry { Company="Amazon",  Ticker="AMZN", Year=2022, Revenue=514.0f, NetIncome= -2.7f, TotalAssets=462.7f, TotalLiabilities=316.6f, EPS=-0.27f },
        new FinancialEntry { Company="Amazon",  Ticker="AMZN", Year=2023, Revenue=574.8f, NetIncome= 30.4f, TotalAssets=527.9f, TotalLiabilities=316.6f, EPS= 2.90f },
        // META ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Meta",    Ticker="META", Year=2019, Revenue= 70.7f, NetIncome= 18.5f, TotalAssets=133.4f, TotalLiabilities= 32.3f, EPS= 6.43f },
        new FinancialEntry { Company="Meta",    Ticker="META", Year=2020, Revenue= 86.0f, NetIncome= 29.1f, TotalAssets=159.3f, TotalLiabilities= 31.0f, EPS=10.09f },
        new FinancialEntry { Company="Meta",    Ticker="META", Year=2021, Revenue=118.0f, NetIncome= 39.4f, TotalAssets=165.9f, TotalLiabilities= 47.7f, EPS=13.77f },
        new FinancialEntry { Company="Meta",    Ticker="META", Year=2022, Revenue=116.6f, NetIncome= 23.2f, TotalAssets=185.7f, TotalLiabilities= 67.3f, EPS= 8.59f },
        new FinancialEntry { Company="Meta",    Ticker="META", Year=2023, Revenue=134.9f, NetIncome= 39.1f, TotalAssets=229.6f, TotalLiabilities= 67.3f, EPS=14.87f },
        // NVDA ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="NVIDIA",  Ticker="NVDA", Year=2019, Revenue= 11.7f, NetIncome= 4.1f, TotalAssets= 13.3f, TotalLiabilities=  3.9f, EPS= 1.67f },
        new FinancialEntry { Company="NVIDIA",  Ticker="NVDA", Year=2020, Revenue= 10.9f, NetIncome= 4.3f, TotalAssets= 17.3f, TotalLiabilities=  5.9f, EPS= 1.73f },
        new FinancialEntry { Company="NVIDIA",  Ticker="NVDA", Year=2021, Revenue= 16.7f, NetIncome= 4.3f, TotalAssets= 28.8f, TotalLiabilities= 11.8f, EPS= 1.73f },
        new FinancialEntry { Company="NVIDIA",  Ticker="NVDA", Year=2022, Revenue= 26.9f, NetIncome= 9.8f, TotalAssets= 41.2f, TotalLiabilities= 17.6f, EPS= 3.85f },
        new FinancialEntry { Company="NVIDIA",  Ticker="NVDA", Year=2023, Revenue= 60.9f, NetIncome=29.8f, TotalAssets= 65.7f, TotalLiabilities= 22.8f, EPS=11.93f },
        // TSLA ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Tesla",   Ticker="TSLA", Year=2019, Revenue= 24.6f, NetIncome=-0.9f, TotalAssets= 34.3f, TotalLiabilities= 26.2f, EPS=-0.33f },
        new FinancialEntry { Company="Tesla",   Ticker="TSLA", Year=2020, Revenue= 31.5f, NetIncome= 0.7f, TotalAssets= 52.1f, TotalLiabilities= 28.5f, EPS= 0.25f },
        new FinancialEntry { Company="Tesla",   Ticker="TSLA", Year=2021, Revenue= 53.8f, NetIncome= 5.5f, TotalAssets= 62.1f, TotalLiabilities= 30.5f, EPS= 1.87f },
        new FinancialEntry { Company="Tesla",   Ticker="TSLA", Year=2022, Revenue= 81.5f, NetIncome=12.6f, TotalAssets= 82.3f, TotalLiabilities= 36.4f, EPS= 4.07f },
        new FinancialEntry { Company="Tesla",   Ticker="TSLA", Year=2023, Revenue= 97.7f, NetIncome=15.0f, TotalAssets=106.6f, TotalLiabilities= 43.0f, EPS= 3.53f },
        // NFLX ──────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Netflix", Ticker="NFLX", Year=2019, Revenue= 20.2f, NetIncome= 1.9f, TotalAssets= 33.9f, TotalLiabilities= 26.3f, EPS= 2.68f },
        new FinancialEntry { Company="Netflix", Ticker="NFLX", Year=2020, Revenue= 25.0f, NetIncome= 2.8f, TotalAssets= 39.3f, TotalLiabilities= 30.5f, EPS= 6.08f },
        new FinancialEntry { Company="Netflix", Ticker="NFLX", Year=2021, Revenue= 29.7f, NetIncome= 5.1f, TotalAssets= 44.6f, TotalLiabilities= 34.0f, EPS=11.24f },
        new FinancialEntry { Company="Netflix", Ticker="NFLX", Year=2022, Revenue= 31.6f, NetIncome= 4.5f, TotalAssets= 48.7f, TotalLiabilities= 37.4f, EPS=10.10f },
        new FinancialEntry { Company="Netflix", Ticker="NFLX", Year=2023, Revenue= 33.7f, NetIncome= 5.4f, TotalAssets= 48.7f, TotalLiabilities= 34.5f, EPS=12.03f },
        // JPM ───────────────────────────────────────────────────────────────
        new FinancialEntry { Company="JPMorgan", Ticker="JPM", Year=2019, Revenue=115.6f, NetIncome=36.4f, TotalAssets=2688f,  TotalLiabilities=2440f,  EPS=10.72f },
        new FinancialEntry { Company="JPMorgan", Ticker="JPM", Year=2020, Revenue=119.5f, NetIncome=29.1f, TotalAssets=3386f,  TotalLiabilities=3111f,  EPS= 8.88f },
        new FinancialEntry { Company="JPMorgan", Ticker="JPM", Year=2021, Revenue=121.6f, NetIncome=48.3f, TotalAssets=3743f,  TotalLiabilities=3426f,  EPS=15.36f },
        new FinancialEntry { Company="JPMorgan", Ticker="JPM", Year=2022, Revenue=128.7f, NetIncome=37.7f, TotalAssets=3665f,  TotalLiabilities=3339f,  EPS=12.09f },
        new FinancialEntry { Company="JPMorgan", Ticker="JPM", Year=2023, Revenue=158.1f, NetIncome=49.6f, TotalAssets=3875f,  TotalLiabilities=3528f,  EPS=16.23f },
        // V ─────────────────────────────────────────────────────────────────
        new FinancialEntry { Company="Visa",    Ticker="V",    Year=2019, Revenue= 23.0f, NetIncome=12.1f, TotalAssets= 72.6f, TotalLiabilities= 38.0f, EPS= 5.32f },
        new FinancialEntry { Company="Visa",    Ticker="V",    Year=2020, Revenue= 21.8f, NetIncome=10.9f, TotalAssets= 80.9f, TotalLiabilities= 45.5f, EPS= 5.04f },
        new FinancialEntry { Company="Visa",    Ticker="V",    Year=2021, Revenue= 24.1f, NetIncome=12.3f, TotalAssets= 82.9f, TotalLiabilities= 47.8f, EPS= 5.91f },
        new FinancialEntry { Company="Visa",    Ticker="V",    Year=2022, Revenue= 29.3f, NetIncome=14.9f, TotalAssets= 85.5f, TotalLiabilities= 48.1f, EPS= 7.50f },
        new FinancialEntry { Company="Visa",    Ticker="V",    Year=2023, Revenue= 32.7f, NetIncome=17.3f, TotalAssets= 90.5f, TotalLiabilities= 51.1f, EPS= 8.77f },
    };

    // ── Public API ────────────────────────────────────────────────────────

    public static List<string> GetTickers()
    {
        var result = new List<string> { "AAPL", "MSFT", "GOOGL", "AMZN", "META", "NVDA", "TSLA", "NFLX", "JPM", "V" };
        return result;
    }

    public static List<FinancialEntry> GetByTicker(string ticker)
    {
        var result = new List<FinancialEntry>();
        foreach (var e in _data)
            if (e.Ticker == ticker)
                result.Add(e);
        return result;
    }

    public static List<FinancialEntry> GetAll()
    {
        return new List<FinancialEntry>(_data);
    }

    /// <summary>
    /// Returns a time series of FinancialDataPoints for the given ticker and metric.
    /// metricKey must be one of:
    ///   "Revenue" | "NetIncome" | "TotalAssets" | "TotalLiabilities" | "EPS"
    /// </summary>
    public static List<FinancialDataPoint> GetSeries(string ticker, string metricKey)
    {
        var entries = GetByTicker(ticker);
        var result = new List<FinancialDataPoint>();

        bool isEps = (metricKey == "EPS");

        foreach (var e in entries)
        {
            float val = 0f;
            switch (metricKey)
            {
                case "Revenue": val = e.Revenue; break;
                case "NetIncome": val = e.NetIncome; break;
                case "TotalAssets": val = e.TotalAssets; break;
                case "TotalLiabilities": val = e.TotalLiabilities; break;
                case "EPS": val = e.EPS; break;
                default:
                    Debug.LogWarning("[MockFinancialData] Unknown metric key: " + metricKey);
                    break;
            }

            result.Add(new FinancialDataPoint
            {
                fiscalYear = e.Year,
                displayValue = val,
                unit = isEps ? "USD/shares" : "USD-B"
            });
        }

        return result;
    }
}