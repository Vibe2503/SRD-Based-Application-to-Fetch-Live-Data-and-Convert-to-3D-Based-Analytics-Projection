/*  FreeCam.cs  — DAV VR  v4  (Mobile-first)
 *  ═══════════════════════════════════════════════════════════════════
 *  MOBILE (touch):
 *    Single finger drag       → look around
 *    Single finger on panel   → panel interaction (blocked here)
 *    Pinch two fingers        → move forward / back (zoom)
 *
 *  PC (mouse / keyboard):
 *    Right-click + drag       → look around
 *    W / A / S / D            → move
 *    Q / E                    → down / up
 *    Shift                    → faster
 *    Scroll wheel             → move forward / back
 *    Double-click chart       → fly to chart
 *  ═══════════════════════════════════════════════════════════════════ */

using System.Collections;
using UnityEngine;

public class FreeCam : MonoBehaviour
{
    [Header("Look")]
    public float mouseSensitivity = 2.5f;
    public float touchSensitivity = 0.12f;   // degrees per pixel on mobile

    [Header("Move")]
    public float moveSpeed = 3.0f;
    public float fastMultiplier = 3.0f;
    public float scrollSpeed = 5.0f;
    public float pinchZoomSpeed = 0.04f;   // world units per pixel pinch delta

    [Header("Fly-To (double-tap / double-click)")]
    public float flyDuration = 0.55f;
    public float flyStopDistance = 2.2f;

    // ── Runtime ───────────────────────────────────────────────────────
    float _yaw;
    float _pitch;
    Vector3 _velocity = Vector3.zero;
    Vector3 _smoothRef = Vector3.zero;

    // Touch state
    bool _lookDragging = false;
    Vector2 _lastLookPos = Vector2.zero;
    int _lookFingerId = -1;

    // Pinch state
    float _lastPinchDist = 0f;

    // Double-tap
    float _lastTapTime = 0f;
    const float DOUBLE_TAP = 0.32f;

    // Flying
    bool _flying = false;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        _yaw = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        if (_flying) return;

        if (DAVizInput.IsTouchDevice)
            UpdateTouch();
        else
            UpdateMouse();
    }

    // ── TOUCH ─────────────────────────────────────────────────────────
    void UpdateTouch()
    {
        int tc = Input.touchCount;

        // ── Two fingers: pinch to zoom forward/back ───────────────────
        if (tc == 2)
        {
            // Cancel any single-finger look drag
            _lookDragging = false;
            _lookFingerId = -1;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);

            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                _lastPinchDist = dist;
            }
            else
            {
                float delta = dist - _lastPinchDist;
                _lastPinchDist = dist;
                // Move forward/back along camera forward
                transform.position += transform.forward * delta * pinchZoomSpeed;
            }
            return;
        }

        // ── Single finger: look around ────────────────────────────────
        if (tc == 1)
        {
            Touch t = Input.GetTouch(0);

            // Check if touch is over the panel — if so don't look
            if (IsOverPanel(t.position)) return;

            if (t.phase == TouchPhase.Began)
            {
                _lookDragging = true;
                _lookFingerId = t.fingerId;
                _lastLookPos = t.position;

                // Double-tap detection
                float now = Time.time;
                if (now - _lastTapTime < DOUBLE_TAP)
                    HandleDoubleTap(t.position);
                _lastTapTime = now;
            }
            else if (t.phase == TouchPhase.Moved && _lookDragging && t.fingerId == _lookFingerId)
            {
                Vector2 delta = t.position - _lastLookPos;
                _lastLookPos = t.position;

                _yaw += delta.x * touchSensitivity;
                _pitch -= delta.y * touchSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                _lookDragging = false;
                _lookFingerId = -1;
            }
        }
        else
        {
            _lookDragging = false;
            _lookFingerId = -1;
        }
    }

    // ── MOUSE (PC) ────────────────────────────────────────────────────
    void UpdateMouse()
    {
        // Right-click drag = look
        if (Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        if (IsOverPanel(Input.mousePosition)) return;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= fastMultiplier;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float up = Input.GetKey(KeyCode.E) ? 1f : Input.GetKey(KeyCode.Q) ? -1f : 0f;
        float sc = Input.GetAxis("Mouse ScrollWheel");

        Vector3 wishDir = transform.right * h
                        + transform.forward * v
                        + transform.up * up
                        + transform.forward * sc * scrollSpeed;

        Vector3 targetVel = wishDir * speed;
        _velocity = Vector3.SmoothDamp(_velocity, targetVel,
                                       ref _smoothRef, 0.12f,
                                       float.MaxValue, Time.deltaTime);
        transform.position += _velocity * Time.deltaTime;

        // Double-click fly-to
        if (Input.GetMouseButtonDown(0))
        {
            float now = Time.time;
            if (now - _lastTapTime < DOUBLE_TAP)
                HandleDoubleTap(Input.mousePosition);
            _lastTapTime = now;
        }
    }

    // ── Double tap / click — fly to nearest chart ─────────────────────
    void HandleDoubleTap(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit rh;
        if (!Physics.Raycast(ray, out rh, 30f)) return;

        BarChartBuilder chart = rh.collider.GetComponentInParent<BarChartBuilder>();
        if (chart == null || chart._chartRoot == null) return;

        Vector3 targetPos = chart._chartRoot.transform.position
                          - chart._chartRoot.transform.forward * flyStopDistance
                          + Vector3.up * 0.2f;
        StartCoroutine(FlyTo(targetPos, chart._chartRoot.transform.position));
    }

    // ── Smooth fly-to coroutine ───────────────────────────────────────
    IEnumerator FlyTo(Vector3 dest, Vector3 lookAt)
    {
        _flying = true;
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyDuration);
            transform.position = Vector3.Lerp(startPos, dest, t);
            Vector3 dir = (lookAt - transform.position);
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            yield return null;
        }

        transform.position = dest;
        // Sync yaw/pitch after fly so subsequent look drag starts correctly
        _yaw = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
        _flying = false;
    }

    // ── Panel overlap check ───────────────────────────────────────────
    bool IsOverPanel(Vector2 screenPos)
    {
        var panel = FindObjectOfType<FloatingPanel>();
        return panel != null && panel.IsPointerOver();
    }
}