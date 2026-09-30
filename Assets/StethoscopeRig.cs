using UnityEngine;

public class StethoscopeRig : MonoBehaviour
{
    [Header("References")]
    public Animator kidAnimator;
    public Transform headGroup;
    public Transform handGroup;
    public Transform tubeStart;
    public Transform tubeEnd;
    public Renderer staticTube;

    [Header("Tube")]
    public float tubeRadius = 0.0042f;
    public float sag = 0.25f;
    public int segments = 24;
    public Material tubeMaterial;

    [Header("Captured Offsets")]
    public bool captured;
    public Vector3 headPosOffset;
    public Quaternion headRotOffset = Quaternion.identity;
    public Vector3 handPosOffset;
    public Quaternion handRotOffset = Quaternion.identity;

    private Transform headBone;
    private Transform handBone;
    private LineRenderer line;

    private void Awake()
    {
        FindBones();
        if (!captured)
            CaptureOffsets();

        if (staticTube != null)
            staticTube.enabled = false;

        SetupLine();
    }

    private void LateUpdate()
    {
        Follow(headGroup, headBone, headPosOffset, headRotOffset);
        Follow(handGroup, handBone, handPosOffset, handRotOffset);
        UpdateTube();
    }

    [ContextMenu("Capture Offsets")]
    public void CaptureOffsets()
    {
        FindBones();
        if (headBone == null || handBone == null)
        {
            Debug.LogWarning("[StethoscopeRig] Missing humanoid head or right-hand bone. Assign a Humanoid kid Animator.", this);
            return;
        }

        if (headGroup != null)
        {
            Quaternion inv = Quaternion.Inverse(headBone.rotation);
            headPosOffset = inv * (headGroup.position - headBone.position);
            headRotOffset = inv * headGroup.rotation;
        }

        if (handGroup != null)
        {
            Quaternion inv = Quaternion.Inverse(handBone.rotation);
            handPosOffset = inv * (handGroup.position - handBone.position);
            handRotOffset = inv * handGroup.rotation;
        }

        captured = true;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[StethoscopeRig] Captured stethoscope offsets.", this);
    }

    private void FindBones()
    {
        if (kidAnimator == null)
            return;

        if (kidAnimator.avatar == null || !kidAnimator.avatar.isHuman)
        {
            headBone = null;
            handBone = null;
            return;
        }

        headBone = kidAnimator.GetBoneTransform(HumanBodyBones.Head);
        handBone = kidAnimator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private static void Follow(Transform part, Transform bone, Vector3 posOffset, Quaternion rotOffset)
    {
        if (part == null || bone == null)
            return;

        part.SetPositionAndRotation(bone.position + bone.rotation * posOffset, bone.rotation * rotOffset);
    }

    private void SetupLine()
    {
        line = GetComponent<LineRenderer>();
        if (line == null)
            line = gameObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = Mathf.Max(2, segments + 1);
        line.startWidth = tubeRadius * 2f;
        line.endWidth = tubeRadius * 2f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;

        Material material = tubeMaterial;
        if (material == null && staticTube != null)
            material = staticTube.sharedMaterial;

        if (material != null)
            line.sharedMaterial = material;
    }

    private void UpdateTube()
    {
        if (line == null || tubeStart == null || tubeEnd == null)
            return;

        int count = Mathf.Max(2, segments + 1);
        if (line.positionCount != count)
            line.positionCount = count;

        Vector3 p0 = tubeStart.position;
        Vector3 p3 = tubeEnd.position;
        float dist = Vector3.Distance(p0, p3);
        Vector3 droop = Vector3.down * dist * sag;
        Vector3 p1 = p0 + droop;
        Vector3 p2 = p3 + droop;

        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : (float)i / (count - 1);
            float u = 1f - t;
            Vector3 p = u * u * u * p0
                + 3f * u * u * t * p1
                + 3f * u * t * t * p2
                + t * t * t * p3;
            line.SetPosition(i, p);
        }
    }
}
