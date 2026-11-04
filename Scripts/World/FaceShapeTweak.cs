using System.Collections.Generic;
using UnityEngine;

// Softens the nose of the "civilian_girl" character (lower bridge, less pointed tip) by gently
// reshaping a copy of her head mesh when the game starts. The model file itself is not changed.
// Created automatically; add the component to an object yourself to tune the values.
public class FaceShapeTweak : MonoBehaviour
{
    [Tooltip("Name of the head mesh to reshape.")]
    [SerializeField] private string headMeshName = "head_01";
    [SerializeField] private string leftEyeMeshName = "eye_left";
    [SerializeField] private string rightEyeMeshName = "eye_right";
    [Tooltip("How far the nose tip is pushed back, as a fraction of the distance between the eyes (0 = unchanged, 0.1 is about 6 mm).")]
    [Range(0f, 0.2f)] [SerializeField] private float tipFlatten = 0.10f;
    [Tooltip("How far the nose bridge is lowered (same unit).")]
    [Range(0f, 0.12f)] [SerializeField] private float bridgeFlatten = 0.045f;

    private readonly HashSet<int> done = new HashSet<int>();
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<FaceShapeTweak>(true) != null)
            return;
        new GameObject("FaceShapeTweak (auto)").AddComponent<FaceShapeTweak>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + 1f;

        foreach (SkinnedMeshRenderer head in FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (head == null || head.sharedMesh == null || head.sharedMesh.name != headMeshName)
                continue;
            if (!done.Add(head.GetInstanceID()))
                continue;
            Apply(head);
        }
    }

    private void Apply(SkinnedMeshRenderer head)
    {
        Mesh source = head.sharedMesh;
        if (!source.isReadable)
        {
            Debug.LogWarning("[FaceShapeTweak] Head mesh is not readable. Select the character's FBX > Model tab > tick Read/Write > Apply.", head);
            return;
        }

        // The eyes tell us where the face is and how big it is.
        Transform root = head.transform.parent != null ? head.transform.parent : head.transform;
        Vector3 left = Vector3.zero, right = Vector3.zero;
        bool hasLeft = false, hasRight = false;
        foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.sharedMesh == null)
                continue;
            if (r.sharedMesh.name == leftEyeMeshName) { left = r.sharedMesh.bounds.center; hasLeft = true; }
            else if (r.sharedMesh.name == rightEyeMeshName) { right = r.sharedMesh.bounds.center; hasRight = true; }
        }
        if (!hasLeft || !hasRight)
        {
            Debug.LogWarning("[FaceShapeTweak] Eye meshes not found next to the head.", head);
            return;
        }

        Vector3 mid = (left + right) * 0.5f;
        float eyeGap = Vector3.Distance(left, right);
        Vector3 side = (right - left).normalized;
        Vector3 headCenter = source.bounds.center;

        // Forward / up are two of the mesh axes: the eyes are in front of and above the head centre.
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        Vector3 forward = Vector3.zero, up = Vector3.zero;
        float best = 0f, second = 0f;
        foreach (Vector3 axis in axes)
        {
            if (Mathf.Abs(Vector3.Dot(axis, side)) > 0.5f)
                continue;
            float d = Vector3.Dot(mid - headCenter, axis);
            if (d > best) { second = best; up = forward; best = d; forward = axis; }
            else if (d > second) { second = d; up = axis; }
        }
        if (forward == Vector3.zero || up == Vector3.zero || eyeGap < 1e-6f)
        {
            Debug.LogWarning("[FaceShapeTweak] Could not work out which way the face points.", head);
            return;
        }

        Vector3[] vertices = source.vertices;

        // Nose tip: the most forward point on the centre line, below the eyes.
        int tipIndex = -1;
        float tipForward = float.MinValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i] - mid;
            float lateral = Vector3.Dot(p, side) / eyeGap;
            float height = Vector3.Dot(p, up) / eyeGap;
            if (Mathf.Abs(lateral) > 0.1f || height > 0f || height < -0.9f)
                continue;
            float f = Vector3.Dot(p, forward);
            if (f > tipForward) { tipForward = f; tipIndex = i; }
        }
        if (tipIndex < 0)
            return;

        Vector3 tip = vertices[tipIndex];
        int moved = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i] - tip;
            float u = Vector3.Dot(p, up) / eyeGap;          // 0 at the tip, about 0.6 between the eyes
            float lateral = Mathf.Abs(Vector3.Dot(vertices[i] - mid, side)) / eyeGap;
            float depth = Vector3.Dot(p, forward) / eyeGap;  // 0 at the tip, negative toward the face
            if (u < -0.22f || u > 0.85f || depth < -0.55f)
                continue;

            // Along the nose: full strength at the tip, fading into the bridge and under the nose.
            float along = u < 0f ? Mathf.SmoothStep(0f, 1f, 1f + u / 0.22f) : 1f - Mathf.SmoothStep(0f, 1f, u / 0.85f);
            float amount = Mathf.Lerp(bridgeFlatten, tipFlatten, along) * (u < 0f ? along : 1f) * (u > 0.6f ? 1f - Mathf.SmoothStep(0.6f, 0.85f, u) : 1f);
            // Across the nose: wide at the tip (nostrils follow), narrow at the bridge.
            float width = Mathf.Lerp(0.14f, 0.26f, along);
            float across = Mathf.Exp(-(lateral * lateral) / (width * width));
            // Only the part that sticks out, not the cheeks behind it.
            float outness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.55f, -0.2f, depth));

            float push = amount * across * outness * eyeGap;
            if (push <= 1e-7f)
                continue;
            vertices[i] -= forward * push;
            moved++;
        }

        Mesh copy = Instantiate(source);
        copy.name = source.name;   // keep the name so other scripts still find it
        copy.vertices = vertices;
        copy.RecalculateBounds();
        head.sharedMesh = copy;
        Debug.Log($"[FaceShapeTweak] Softened the nose on '{root.name}' ({moved} points moved).", head);
    }
}
