using UnityEngine;

// Small two-bone IK for Mixamo rigs (works on Generic avatars too).
// Call Solve() from LateUpdate, after the Animator has written the pose.
public static class ArmReachIK
{
    public struct Arm
    {
        public Transform upper;   // mixamorig:RightArm
        public Transform lower;   // mixamorig:RightForeArm
        public Transform hand;    // mixamorig:RightHand
        public bool IsValid => upper != null && lower != null && hand != null;
    }

    public static Arm FindArm(Transform root, bool right)
    {
        string side = right ? "Right" : "Left";
        return new Arm
        {
            upper = MixamoHumanoidAvatarBuilder.FindBone(root, side + "Arm"),
            lower = MixamoHumanoidAvatarBuilder.FindBone(root, side + "ForeArm"),
            hand = MixamoHumanoidAvatarBuilder.FindBone(root, side + "Hand"),
        };
    }

    // Picks the arm whose shoulder is closest to the target.
    public static Arm FindClosestArm(Transform root, Vector3 target)
    {
        Arm right = FindArm(root, true);
        Arm left = FindArm(root, false);
        if (!right.IsValid) return left;
        if (!left.IsValid) return right;
        return (right.upper.position - target).sqrMagnitude <= (left.upper.position - target).sqrMagnitude ? right : left;
    }

    // Same, with the elbow pushed toward `elbowDirection` (e.g. down and out to the side).
    public static void SolveWithElbow(Arm arm, Vector3 target, Vector3 elbowDirection, float weight)
    {
        if (!arm.IsValid || weight <= 0.0001f)
            return;

        weight = Mathf.Clamp01(weight);
        Vector3 a = arm.upper.position;
        float la = Vector3.Distance(a, arm.lower.position);
        float lb = Vector3.Distance(arm.lower.position, arm.hand.position);
        if (la < 1e-5f || lb < 1e-5f)
            return;

        Vector3 toTarget = target - a;
        float d = Mathf.Clamp(toTarget.magnitude, 0.01f, (la + lb) * 0.98f);
        Vector3 dir = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(elbowDirection, dir);
        if (bend.sqrMagnitude < 1e-6f)
            bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        bend.Normalize();

        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        Vector3 elbow = a + dir * (cosA * la) + bend * (Mathf.Sqrt(1f - cosA * cosA) * la);

        Quaternion upper = Quaternion.FromToRotation(arm.lower.position - a, elbow - a) * arm.upper.rotation;
        arm.upper.rotation = Quaternion.Slerp(arm.upper.rotation, upper, weight);
        Vector3 wanted = a + dir * d;
        Quaternion lower = Quaternion.FromToRotation(arm.hand.position - arm.lower.position, wanted - arm.lower.position) * arm.lower.rotation;
        arm.lower.rotation = Quaternion.Slerp(arm.lower.rotation, lower, weight);
    }

    public static void Solve(Arm arm, Vector3 target, float weight)
    {
        if (!arm.IsValid || weight <= 0.0001f)
            return;

        weight = Mathf.Clamp01(weight);
        Vector3 a = arm.upper.position;
        Vector3 b = arm.lower.position;
        Vector3 c = arm.hand.position;
        float la = Vector3.Distance(a, b);
        float lb = Vector3.Distance(b, c);
        if (la < 1e-5f || lb < 1e-5f)
            return;

        Vector3 toTarget = target - a;
        float d = Mathf.Clamp(toTarget.magnitude, 0.01f, (la + lb) * 0.999f);
        Vector3 dir = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : (c - a).normalized;

        // Keep the elbow bending the way it currently bends, nudged downward (natural).
        Vector3 pole = (b + Vector3.down * la * 0.5f) - a;
        Vector3 bendDir = pole - Vector3.Dot(pole, dir) * dir;
        if (bendDir.sqrMagnitude < 1e-8f)
            bendDir = Vector3.ProjectOnPlane(Vector3.down, dir);
        bendDir.Normalize();

        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        float sinA = Mathf.Sqrt(1f - cosA * cosA);
        Vector3 elbow = a + dir * (cosA * la) + bendDir * (sinA * la);

        Quaternion upperTarget = Quaternion.FromToRotation(b - a, elbow - a) * arm.upper.rotation;
        arm.upper.rotation = Quaternion.Slerp(arm.upper.rotation, upperTarget, weight);

        b = arm.lower.position;
        c = arm.hand.position;
        Vector3 wantedHand = a + dir * d;
        Quaternion lowerTarget = Quaternion.FromToRotation(c - b, wantedHand - b) * arm.lower.rotation;
        arm.lower.rotation = Quaternion.Slerp(arm.lower.rotation, lowerTarget, weight);
    }
}
