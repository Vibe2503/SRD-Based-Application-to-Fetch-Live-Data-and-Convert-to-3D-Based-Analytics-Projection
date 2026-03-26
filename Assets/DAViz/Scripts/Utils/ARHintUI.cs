/*  ARHintUI.cs  — DAV VR  AR Edition
 *  ═══════════════════════════════════════════════════════════════════
 *  Lightweight world-space hint label that floats at the top of the
 *  AR view. No Canvas needed — uses TextMesh parented to camera.
 *
 *  Usage (from any script):
 *    ARHintUI.Show("Move phone to scan");
 *    ARHintUI.Hide();
 *  ═══════════════════════════════════════════════════════════════════ */

using UnityEngine;

public class ARHintUI : MonoBehaviour
{
    static ARHintUI _inst;
    TextMesh _tm;
    GameObject _bg;
    float _hideTimer = 0f;
    const float AUTO_HIDE = 3.5f;   // seconds before hint fades

    void Awake()
    {
        _inst = this;
        BuildUI();
    }

    void Update()
    {
        if (_hideTimer > 0f)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f) Hide();
        }

        // Keep locked to top-centre of camera view
        if (Camera.main != null)
        {
            Vector3 vp = new Vector3(0.50f, 0.88f, 1.10f);
            transform.position = Camera.main.ViewportToWorldPoint(vp);
            Vector3 dir = Camera.main.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        }
    }

    void BuildUI()
    {
        // Dark pill background
        _bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _bg.name = "HintBG";
        _bg.transform.SetParent(transform, false);
        _bg.transform.localPosition = Vector3.zero;
        _bg.transform.localScale = new Vector3(0.38f, 0.055f, 1f);
        Destroy(_bg.GetComponent<Collider>());
        var mat = new Material(ShaderHelper.Sprite());
        mat.color = new Color(0.04f, 0.04f, 0.08f, 0.88f);
        _bg.GetComponent<Renderer>().material = mat;

        // Text
        var go = new GameObject("HintText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, 0, -0.001f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _tm = go.AddComponent<TextMesh>();
        _tm.text = "";
        _tm.characterSize = 0.010f;
        _tm.fontSize = 18;
        _tm.color = new Color(0.85f, 0.95f, 1.00f, 1f);
        _tm.anchor = TextAnchor.MiddleCenter;
        _tm.alignment = TextAlignment.Center;
        go.GetComponent<MeshRenderer>().sortingOrder = 5;

        gameObject.SetActive(false);
    }

    // ── Static API ────────────────────────────────────────────────────
    public static void Show(string msg, float duration = 3.5f)
    {
        if (_inst == null) CreateInstance();
        _inst.gameObject.SetActive(true);
        _inst._tm.text = msg;
        _inst._hideTimer = duration;
    }

    public static void ShowPersistent(string msg)
    {
        if (_inst == null) CreateInstance();
        _inst.gameObject.SetActive(true);
        _inst._tm.text = msg;
        _inst._hideTimer = -1f;   // never auto-hide
    }

    public static void Hide()
    {
        if (_inst != null) _inst.gameObject.SetActive(false);
    }

    static void CreateInstance()
    {
        var go = new GameObject("ARHintUI");
        _inst = go.AddComponent<ARHintUI>();
    }
}