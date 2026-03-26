/*  ARChartPlacer.cs  — DAV VR  AR Edition
 *  Places charts via tap. Disabled in 3D/Editor mode.
 *  Uses physics plane raycast — no AR Foundation types needed.
 */

using DG.Tweening;
using UnityEngine;

public class ARChartPlacer : MonoBehaviour
{
    [Header("Chart Settings")]
    public float chartScale = 0.5f;
    public float chartHeightOffset = 0.02f;

    bool _placementMode = true;
    int _placedCount = 0;
    float _tapCooldown = 0f;   // prevent double-taps

    // Reticle
    GameObject _reticle;
    bool _validPlane = false;
    Vector3 _planeHitPos = Vector3.zero;

    static readonly Color ColReady = new Color(0.20f, 1.00f, 0.55f, 0.85f);
    static readonly Color ColScanning = new Color(1.00f, 0.75f, 0.15f, 0.60f);

    GameObject _fallbackPlane;

    void OnEnable()
    {
        BuildReticle();
        BuildFallbackPlane();
    }

    void OnDisable()
    {
        if (_reticle != null) _reticle.SetActive(false);
    }

    void Update()
    {
        if (!ARSessionSetup.ARActive) return;

        _tapCooldown -= Time.deltaTime;

        UpdateReticle();
        HandleInput();
    }

    void UpdateReticle()
    {
        if (_reticle == null || Camera.main == null) return;

        Ray ray = new Ray(
            Camera.main.transform.position,
            Camera.main.transform.forward);

        RaycastHit rh;
        if (Physics.Raycast(ray, out rh, 15f)
            && rh.collider != null
            && rh.collider.name == "ARFallbackPlane")
        {
            _validPlane = true;
            _planeHitPos = rh.point;

            _reticle.SetActive(true);
            _reticle.transform.position = _planeHitPos + Vector3.up * 0.002f;
            _reticle.transform.rotation = Quaternion.Euler(0,
                Camera.main.transform.eulerAngles.y, 0);
            SetReticleColor(ColReady);
        }
        else
        {
            _validPlane = false;
            if (_reticle != null) _reticle.SetActive(false);
        }
    }

    void HandleInput()
    {
        if (_tapCooldown > 0f) return;

        // Touch input for phone
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase != TouchPhase.Began) return;

            // Bottom 40% is panel area — ignore
            if (t.position.y < Screen.height * 0.40f) return;

            _tapCooldown = 0.5f;  // 500ms cooldown between placements

            if (_placementMode && _validPlane)
                PlaceChart(_planeHitPos);
            else
                TrySelectChart(t.position);
        }

        // Mouse input for Editor testing
        if (Input.GetMouseButtonDown(0))
        {
            if (Input.mousePosition.y < Screen.height * 0.40f) return;
            _tapCooldown = 0.5f;
            if (_placementMode && _validPlane)
                PlaceChart(_planeHitPos);
            else
                TrySelectChart(Input.mousePosition);
        }
    }

    void PlaceChart(Vector3 pos)
    {
        SpawnChartAt(pos + Vector3.up * chartHeightOffset);
        ARHintUI.Show("Chart placed!  Tap again to add another");
    }

    void SpawnChartAt(Vector3 pos)
    {
        var go = new GameObject("ARChart_" + _placedCount);
        var builder = go.AddComponent<BarChartBuilder>();

        var tickers = MockFinancialData.GetTickers();
        builder.selectedTicker = tickers[_placedCount % tickers.Count];
        builder.metricToShow = BarChartBuilder.MetricType.Revenue;
        builder.cubeWidth = 0.35f;
        builder.cubeHeight = 0.28f;
        builder.cubeDepth = 0.12f;
        builder.distanceFromCamera = 0f;

        go.transform.position = pos;
        go.transform.localScale = Vector3.zero;

        // Face toward camera
        if (Camera.main != null)
        {
            Vector3 dir = Camera.main.transform.position - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                go.transform.rotation =
                    Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }

        go.transform.DOScale(chartScale, 0.35f)
            .SetEase(DG.Tweening.Ease.OutBack);

        _placedCount++;
    }

    void TrySelectChart(Vector2 screenPos)
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit rh;
        if (Physics.Raycast(ray, out rh, 20f))
        {
            var builder = rh.collider.GetComponentInParent<BarChartBuilder>();
            if (builder != null)
            {
                BarChartBuilder.SetLastInteractedStatic(builder);
                ARHintUI.Show(builder.selectedTicker + " selected");
            }
        }
    }

    public void SetPlacementMode(bool placing)
    {
        _placementMode = placing;
        if (_reticle != null) _reticle.SetActive(placing && _validPlane);
    }

    void BuildFallbackPlane()
    {
        if (_fallbackPlane != null) return;
        _fallbackPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _fallbackPlane.name = "ARFallbackPlane";
        _fallbackPlane.transform.position = new Vector3(0, -0.3f, 0);
        _fallbackPlane.transform.localScale = new Vector3(20f, 1f, 20f);
        var r = _fallbackPlane.GetComponent<Renderer>();
        if (r != null) r.enabled = false;
    }

    void BuildReticle()
    {
        if (_reticle != null) return;
        _reticle = new GameObject("AR_Reticle");

        for (int i = 0; i < 32; i++)
        {
            float a0 = i * Mathf.PI * 2f / 32f;
            float a1 = (i + 1) * Mathf.PI * 2f / 32f;
            float r = 0.12f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.transform.SetParent(_reticle.transform, false);
            seg.transform.localPosition = (p0 + p1) * 0.5f;
            seg.transform.localScale = new Vector3(
                Vector3.Distance(p0, p1), 0.003f, 0.008f);
            seg.transform.localRotation =
                Quaternion.LookRotation((p1 - p0).normalized, Vector3.up)
                * Quaternion.Euler(0, 90, 0);
            Destroy(seg.GetComponent<Collider>());
            seg.GetComponent<Renderer>().material =
                ShaderHelper.MakeUnlit(ColReady);
        }

        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.transform.SetParent(_reticle.transform, false);
        dot.transform.localPosition = Vector3.up * 0.002f;
        dot.transform.localScale = Vector3.one * 0.025f;
        Destroy(dot.GetComponent<Collider>());
        dot.GetComponent<Renderer>().material =
            ShaderHelper.MakeUnlit(new Color(1f, 1f, 1f, 0.9f));

        _reticle.SetActive(false);
    }

    void SetReticleColor(Color col)
    {
        if (_reticle == null) return;
        foreach (var rend in _reticle.GetComponentsInChildren<Renderer>())
            rend.material.color = col;
    }
}