/*  ShaderHelper.cs  — DAV VR
 *  URP-safe material creation.
 *  Creates materials using Sprites/Default which is guaranteed
 *  to exist in every URP Android build (it's in the Always Included list).
 *  Sprites/Default supports color tinting and works identically
 *  to Unlit/Color for our purposes.
 */

using UnityEngine;

public static class ShaderHelper
{
    static Shader _cached;

    // Returns Sprites/Default — always available in URP Android builds
    static Shader Get()
    {
        if (_cached != null) return _cached;
        _cached = Shader.Find("Sprites/Default");
        if (_cached != null)
        {
            Debug.Log("[Shader] Using Sprites/Default");
            return _cached;
        }
        // Absolute last resort
        _cached = Shader.Find("UI/Default");
        if (_cached != null)
        {
            Debug.Log("[Shader] Using UI/Default");
            return _cached;
        }
        Debug.LogError("[Shader] No shader found — materials will be pink");
        return null;
    }

    public static Shader Unlit() => Get();
    public static Shader Sprite() => Get();
    public static Shader UnlitTex() => Get();

    public static Material MakeUnlit(Color color)
    {
        var sh = Get();
        if (sh == null) return new Material(Shader.Find("UI/Default"));
        var mat = new Material(sh);
        mat.color = color;
        return mat;
    }

    public static Material MakeSprite(Color color)
    {
        return MakeUnlit(color);
    }
}