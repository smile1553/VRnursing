using System.Collections.Generic;
using UnityEngine;

// The ear thermometer that is handed around (the thermometers placed in the room are static
// scenery and cannot move).
//
// It uses the model at  Assets/Resources/EarThermometer_Model.prefab  - put any other model
// there (same file name) to change how it looks; it is fitted automatically: the long part is
// taken as the handle and the part that sticks out sideways as the probe.
//
// The object returned is held like a pistol: its origin is where the hand grips the handle,
// its forward (blue) axis is the direction the probe points, its up axis runs along the
// handle toward the head. The probe tip is TipDistance metres forward and TipRise metres up.
public static class EarThermometerProp
{
    private const string ModelResource = "EarThermometer_Model";
    private const float RealLength = 0.15f;

    // Distance (m) from the grip to the probe tip of the last thermometer created.
    public static float TipDistance { get; private set; } = 0.06f;
    // Height (m) of the probe above the grip.
    public static float TipRise { get; private set; }

    public static Transform Create(Vector3 position, Quaternion rotation, float size = 1f)
    {
        size = Mathf.Max(0.1f, size);
        Transform t = CreateFromModel(size);
        if (t == null)
            t = CreateFromShapes(size);
        t.SetPositionAndRotation(position, rotation);
        return t;
    }

    private static Transform CreateFromModel(float size)
    {
        GameObject prefab = Resources.Load<GameObject>(ModelResource);
        if (prefab == null)
            return null;

        GameObject root = new GameObject("EarThermometer_Prop");
        GameObject model = Object.Instantiate(prefab, root.transform);
        Transform m = model.transform;
        m.localPosition = Vector3.zero;
        m.localRotation = Quaternion.identity;
        m.localScale = Vector3.one;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Object.Destroy(c);
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            Object.Destroy(body);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // All vertices, in the model's own space.
        List<Vector3> points = new List<Vector3>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            if (!mesh.isReadable)
            {
                Debug.LogWarning($"[EarThermometerProp] Mesh '{mesh.name}' is not readable (tick Read/Write in its import settings): using the simple thermometer instead.");
                root.SetActive(false);
                Object.Destroy(root);
                return null;
            }
            Matrix4x4 toModel = m.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (Vector3 v in mesh.vertices)
                points.Add(toModel.MultiplyPoint3x4(v));
        }
        if (points.Count < 8)
        {
            root.SetActive(false);
            Object.Destroy(root);
            return null;
        }

        // Handle direction = the direction in which the model is longest.
        Vector3 mean = Vector3.zero;
        foreach (Vector3 p in points)
            mean += p;
        mean /= points.Count;
        float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
        foreach (Vector3 p in points)
        {
            Vector3 d = p - mean;
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
            yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }
        Vector3 axis = new Vector3(1f, 0.9f, 0.8f).normalized;
        for (int i = 0; i < 40; i++)
        {
            axis = new Vector3(
                xx * axis.x + xy * axis.y + xz * axis.z,
                xy * axis.x + yy * axis.y + yz * axis.z,
                xz * axis.x + yz * axis.y + zz * axis.z).normalized;
        }

        float tMin = float.MaxValue, tMax = float.MinValue, tTip = 0f, farthest = -1f;
        Vector3 tip = mean;
        foreach (Vector3 p in points)
        {
            float t = Vector3.Dot(p - mean, axis);
            tMin = Mathf.Min(tMin, t);
            tMax = Mathf.Max(tMax, t);
            float away = ((p - mean) - axis * t).sqrMagnitude;
            if (away > farthest)
            {
                farthest = away;
                tip = p;
                tTip = t;
            }
        }
        float length = Mathf.Max(1e-5f, tMax - tMin);
        bool headAtMax = tTip >= (tMin + tMax) * 0.5f;
        Vector3 headUp = headAtMax ? axis : -axis;
        // Gripped on the handle, a little below the head.
        float tGrip = headAtMax ? Mathf.Lerp(tMax, tMin, 0.45f) : Mathf.Lerp(tMin, tMax, 0.45f);
        Vector3 grip = mean + axis * tGrip;

        // The probe sticks out sideways from the head: that direction is "forward".
        Vector3 probe = (tip - mean) - axis * tTip;
        if (probe.sqrMagnitude < 1e-10f)
            probe = Vector3.Cross(headUp, Vector3.right);
        Quaternion frame = Quaternion.LookRotation(probe.normalized, headUp);
        float scale = RealLength * size / length;

        m.localRotation = Quaternion.Inverse(frame);
        m.localScale = Vector3.one * scale;
        m.localPosition = -(m.localRotation * (grip * scale));
        TipDistance = probe.magnitude * scale;
        TipRise = Mathf.Abs(tTip - tGrip) * scale;
        return root.transform;
    }

    // Fallback: a simple thermometer built from basic shapes.
    private static Transform CreateFromShapes(float size)
    {
        GameObject root = new GameObject("EarThermometer_Prop");
        GameObject shapes = new GameObject("Shapes");
        Transform t = shapes.transform;
        t.SetParent(root.transform, false);

        Color body = new Color(0.97f, 0.97f, 0.96f);
        Color accent = new Color(0.30f, 0.62f, 0.86f);
        Color probe = new Color(0.86f, 0.88f, 0.90f);

        Add(t, PrimitiveType.Capsule, new Vector3(0f, -0.040f, -0.010f), new Vector3(-12f, 0f, 0f), new Vector3(0.024f, 0.042f, 0.027f), body);
        Add(t, PrimitiveType.Capsule, new Vector3(0f, -0.058f, -0.0138f), new Vector3(-12f, 0f, 0f), new Vector3(0.0252f, 0.020f, 0.0283f), accent);
        Add(t, PrimitiveType.Sphere, new Vector3(0f, 0.010f, 0.004f), Vector3.zero, new Vector3(0.030f, 0.032f, 0.044f), body);
        Add(t, PrimitiveType.Cylinder, new Vector3(0f, 0.011f, 0.027f), new Vector3(90f, 0f, 0f), new Vector3(0.018f, 0.005f, 0.018f), probe);
        Add(t, PrimitiveType.Cylinder, new Vector3(0f, 0.011f, 0.036f), new Vector3(90f, 0f, 0f), new Vector3(0.013f, 0.006f, 0.013f), probe);
        Add(t, PrimitiveType.Cylinder, new Vector3(0f, 0.011f, 0.046f), new Vector3(90f, 0f, 0f), new Vector3(0.008f, 0.005f, 0.008f), probe);
        Add(t, PrimitiveType.Sphere, new Vector3(0f, 0.011f, 0.051f), Vector3.zero, new Vector3(0.008f, 0.008f, 0.008f), probe);
        Add(t, PrimitiveType.Cube, new Vector3(0f, 0.012f, -0.0165f), new Vector3(-10f, 0f, 0f), new Vector3(0.016f, 0.012f, 0.003f), new Color(0.10f, 0.30f, 0.28f));
        Add(t, PrimitiveType.Sphere, new Vector3(0f, 0.0255f, 0.002f), Vector3.zero, new Vector3(0.010f, 0.005f, 0.010f), accent);

        t.localPosition = new Vector3(0f, -0.011f, 0f) * size;   // probe on the forward axis
        t.localScale = Vector3.one * size;
        TipDistance = 0.055f * size;
        TipRise = 0f;
        return root.transform;
    }

    private static Renderer Add(Transform parent, PrimitiveType type, Vector3 position, Vector3 euler, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.material.color = color;
        if (renderer.material.HasProperty("_Glossiness"))
            renderer.material.SetFloat("_Glossiness", 0.45f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }
}
