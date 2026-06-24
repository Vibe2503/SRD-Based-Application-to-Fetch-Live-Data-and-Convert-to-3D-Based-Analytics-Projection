# SRD-Based Application to Fetch Live Data and Convert to 3D-Based Analytics Projection

**A 3D Live Data Simulation and Projection onto the Real World.**

This project (internally named **DAViz**) fetches live financial market data and projects it as interactive 3D analytics — bar charts, candlesticks, and a live ticker — directly onto the real world using AR, or into a VR/desktop 3D environment. A single Unity codebase detects the platform at runtime and switches between **AR (real-world projection)**, **VR**, and **desktop 3D** modes automatically.

## Features

- **Live Data Fetching** — pulls real-time financial data (quotes, time series, fundamentals) from a live API, with caching to minimize redundant calls
- **3D Analytics Projection** — converts fetched data into 3D visual analytics:
  - **3D Bar Charts** — company metrics (Revenue, Net Income, Total Assets, Total Liabilities, EPS) across fiscal years, rendered as floating 3D bars
  - **3D Candlestick Charts** — 30-day OHLC price action as classic candlesticks (green/red bodies + wicks) in 3D space
  - **Live Price Ticker** — a scrolling strip of real-time quotes and % change for multiple tickers
- **Real-World Projection (AR)** — on phones/tablets, scans the real environment and projects 3D charts onto detected surfaces via tap-to-place
- **3D Simulation (VR / Desktop)** — the same data and charts rendered in an immersive VR scene or desktop 3D space when AR isn't available
- **Floating UI Panels** — draggable, HUD-locked control panels for selecting tickers and metrics, animated with DOTween
- **Live + Offline Data** — falls back to a built-in offline dataset if no API key is set or a live request fails, so the simulation always has data to project
- **Adaptive Performance** — detects device tier at runtime and caps the number of simultaneous 3D charts so lower-end devices stay responsive

## Supported Platforms / Projection Modes

| Platform | Input | Mode |
|---|---|---|
| Android / iOS | Touch + pinch | **Real-world AR projection** (plane detection + tap-to-place) |
| Meta Quest | Controllers / hands | VR simulation with passthrough |
| OpenXR headsets | Controllers | VR simulation |
| PC / Mac / Linux | Mouse + keyboard | Desktop 3D simulation (free-cam) |
| WebGL | Mouse + touch | Desktop 3D simulation |

The app automatically detects AR availability at startup and routes into **AR mode** (live camera feed, charts projected onto the real world) or **3D simulation mode** (skybox + free camera) accordingly.

## Tech Stack

- **Engine:** Unity 2022.3 LTS, Universal Render Pipeline (URP)
- **XR:** AR Foundation, ARCore, OpenXR, XR Interaction Toolkit, Oculus XR Plugin, Microsoft Mixed Reality OpenXR
- **Data API:** [Twelve Data](https://twelvedata.com/apikey) (free tier — 800 calls/day), SEC EDGAR fallback
- **Animation:** DOTween
- **Build target safety:** all runtime data/visualization code is IL2CPP-safe (no LINQ, no reflection, no C# 8 range syntax) for Android/AR builds

## How It Works

The pipeline follows a simple **fetch → cache → convert → project** flow:

1. **Fetch** — `TwelveDataService` requests live quotes, time series, or fundamentals from the data API.
2. **Cache** — `DataCache` stores results with an expiry so repeated charts/tickers don't re-fetch the same data.
3. **Fallback** — if the API call fails or no key is configured, `MockFinancialData` supplies offline data so the simulation never breaks.
4. **Convert to 3D** — `BarChartBuilder` / `CandlestickBuilder` / `PriceTickerStrip` turn the raw data into 3D geometry (bars, candlesticks, scrolling text).
5. **Project** — depending on the detected platform:
   - **AR:** `ARSessionSetup` + `ARChartPlacer` project the 3D charts onto a real-world surface via tap-to-place.
   - **VR / Desktop:** `FloatingPanel` + `FreeCam` simulate the same charts in a 3D virtual scene.

## Project Structure

```
Assets/
├── DAViz/
│   └── Scripts/
│       ├── Data/             # API integration, caching, mock data, config
│       │   ├── DAVizConfig.cs        # API keys & runtime settings
│       │   ├── TwelveDataService.cs  # Live quotes, time series, fundamentals
│       │   ├── DataCache.cs          # Shared in-memory cache w/ expiry
│       │   └── MockFinancialData.cs  # Offline fallback dataset
│       ├── Visualization/    # Chart builders & UI panels
│       │   ├── BarChartBuilder.cs
│       │   ├── CandlestickBuilder.cs
│       │   ├── PriceTickerStrip.cs
│       │   └── FloatingPanel.cs
│       └── Utils/             # AR/VR session handling, input, performance
│           ├── ARSessionSetup.cs     # AR vs 3D mode switch
│           ├── ARChartPlacer.cs      # Tap-to-place in AR
│           ├── ARCameraSetup.cs / ARBottomPanel.cs / ARHintUI.cs
│           ├── OVRManagerHelper.cs   # Meta Quest passthrough
│           ├── DAVizInput.cs         # Unified mouse/touch input
│           ├── FreeCam.cs            # Desktop free camera
│           └── ResourceMonitor.cs    # Device-tier performance guard
├── XR / XRI / Oculus          # XR platform packages
├── Shield Shader FREE         # Third-party shader asset
└── Settings                   # URP render pipeline assets
```

## Getting Started

### Prerequisites
- Unity **2022.3.62f3** (or compatible 2022.3 LTS patch)
- For AR builds: Android SDK/NDK (via Unity Hub) or Xcode for iOS
- For VR builds: Meta Quest device + Quest Link / Air Link, or another OpenXR headset

### Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/Vibe2503/DAV-VR7.git
   ```
2. Open the project in Unity Hub with version **2022.3.62f3**.
3. Open `Assets/Scenes/SampleScene.unity`.
4. (Optional, for live data) Get a free API key from [twelvedata.com/apikey](https://twelvedata.com/apikey) and enter it in the `DAVizConfig` component on the `ChartManager` GameObject in the Inspector.
   - If no key is set, the app automatically uses the built-in mock dataset.
5. Press **Play** to test in desktop 3D mode, or build to Android/iOS for AR, or deploy to a Quest/OpenXR headset for VR.

## Mock Data

If you don't want to set up an API key, the app ships with a realistic offline dataset (2019–2023 figures) so the 3D simulation/projection still has data to render, covering: `AAPL`, `MSFT`, `GOOGL`, `AMZN`, `META`, `NVDA`, `TSLA`, `NFLX`, `JPM`, and `V`.

## License

_Add your license here._
