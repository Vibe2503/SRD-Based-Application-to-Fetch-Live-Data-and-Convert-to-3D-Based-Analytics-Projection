/*  ARSessionWrapper.cs  — DAV VR
 *  Attach this to the AR Session GameObject.
 *  ARSessionSetup uses it to find and disable the AR Session
 *  when running in 3D mode (Editor / non-AR device).
 */
using UnityEngine;

public class ARSessionWrapper : MonoBehaviour
{
    // Intentionally empty — acts as a tag component so
    // ARSessionSetup can find and disable the AR Session GO
    // in 3D mode without needing AR Foundation types.
}