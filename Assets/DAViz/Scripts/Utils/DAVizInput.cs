/*  DAVizInput.cs
 *  ══════════════════════════════════════════════════════════════════════
 *  Unified input abstraction for DAV VR.
 *  Drop in Assets/DAViz/Scripts/Utils/
 *
 *  Replaces all direct Input.mousePosition / Input.GetMouseButtonDown()
 *  calls in BarChartBuilder and FloatingPanel with a single API that
 *  works identically on:
 *
 *    ✓ PC / Mac / Linux       (mouse + keyboard)
 *    ✓ Android phone/tablet   (touch)
 *    ✓ iOS phone/tablet       (touch)
 *    ✓ WebGL browser          (mouse + touch)
 *    ✓ Standalone builds      (mouse)
 *
 *  USAGE (replace old Input calls with these):
 *
 *    DAVizInput.PrimaryDown        → was Input.GetMouseButtonDown(0)
 *    DAVizInput.PrimaryHeld        → was Input.GetMouseButton(0)
 *    DAVizInput.PrimaryUp          → was Input.GetMouseButtonUp(0)
 *    DAVizInput.Position           → was Input.mousePosition
 *    DAVizInput.ScrollDelta        → was Input.GetAxis("Mouse ScrollWheel")
 *    DAVizInput.PinchDelta         → two-finger pinch scale delta (touch only)
 *    DAVizInput.IsTouchDevice      → runtime bool
 *  ══════════════════════════════════════════════════════════════════════ */

using UnityEngine;

public static class DAVizInput
{
    // ── Cached platform flag (set once on first access) ───────────────────
    static bool? _isTouchDevice;

    public static bool IsTouchDevice
    {
        get
        {
            if (!_isTouchDevice.HasValue)
            {
                // Never treat Editor as touch device — always use mouse in Editor
                if (Application.isEditor)
                {
                    _isTouchDevice = false;
                }
                else
                {
                    _isTouchDevice =
                        Application.platform == RuntimePlatform.Android ||
                        Application.platform == RuntimePlatform.IPhonePlayer;
                }
            }
            return _isTouchDevice.Value;
        }
    }

    // ── Primary pointer position ──────────────────────────────────────────
    // Returns the primary touch position or mouse position in screen pixels.
    public static Vector3 Position
    {
        get
        {
            if (IsTouchDevice && Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
        }
    }

    // ── Primary press / hold / release ────────────────────────────────────
    public static bool PrimaryDown
    {
        get
        {
            if (IsTouchDevice)
                return Input.touchCount > 0 &&
                       Input.GetTouch(0).phase == TouchPhase.Began;
            return Input.GetMouseButtonDown(0);
        }
    }

    public static bool PrimaryHeld
    {
        get
        {
            if (IsTouchDevice)
                return Input.touchCount > 0 &&
                       (Input.GetTouch(0).phase == TouchPhase.Moved ||
                        Input.GetTouch(0).phase == TouchPhase.Stationary);
            return Input.GetMouseButton(0);
        }
    }

    public static bool PrimaryUp
    {
        get
        {
            if (IsTouchDevice)
                return Input.touchCount > 0 &&
                       Input.GetTouch(0).phase == TouchPhase.Ended;
            return Input.GetMouseButtonUp(0);
        }
    }

    // ── Scroll / zoom ─────────────────────────────────────────────────────
    // On desktop: mouse scroll wheel.
    // On touch: two-finger pinch computed as scale delta per frame.
    public static float ScrollDelta
    {
        get
        {
            if (!IsTouchDevice)
                return Input.GetAxis("Mouse ScrollWheel");

            if (Input.touchCount == 2)
                return PinchDelta;

            return 0f;
        }
    }

    // Two-finger pinch: positive = fingers spreading (zoom in),
    //                   negative = fingers closing (zoom out).
    // Returns a value in the same range as mouse scroll (~0.1 per "tick").
    public static float PinchDelta
    {
        get
        {
            if (Input.touchCount < 2) return 0f;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            // Previous frame positions
            Vector2 t0Prev = t0.position - t0.deltaPosition;
            Vector2 t1Prev = t1.position - t1.deltaPosition;

            float prevDist = Vector2.Distance(t0Prev, t1Prev);
            float currDist = Vector2.Distance(t0.position, t1.position);

            if (prevDist < 0.001f) return 0f;

            // Normalise to roughly the same magnitude as scroll wheel
            float delta = (currDist - prevDist) / Screen.dpi;
            return Mathf.Clamp(delta, -0.5f, 0.5f);
        }
    }

    // ── Secondary pointer (right-click / two-finger tap) ──────────────────
    public static bool SecondaryDown
    {
        get
        {
            if (IsTouchDevice)
                return Input.touchCount == 2 &&
                       Input.GetTouch(1).phase == TouchPhase.Began;
            return Input.GetMouseButtonDown(1);
        }
    }

    // ── Ray from current pointer position ─────────────────────────────────
    public static Ray PointerRay(Camera cam)
    {
        return cam.ScreenPointToRay(Position);
    }

    // ── Drag delta (how far pointer moved this frame) ─────────────────────
    public static Vector2 DragDelta
    {
        get
        {
            if (IsTouchDevice && Input.touchCount > 0)
                return Input.GetTouch(0).deltaPosition;
            return new Vector2(
                Input.GetAxis("Mouse X") * Screen.dpi * 0.05f,
                Input.GetAxis("Mouse Y") * Screen.dpi * 0.05f);
        }
    }
}