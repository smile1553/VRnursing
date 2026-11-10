using System.Collections.Generic;
using UnityEngine;

// Makes a character's hand really hold a small object (the ear thermometer) like a pistol:
// the hand turns so the fingers point at the target, the fingers curl around the handle and
// the object sits in the palm. Works on any skeleton whose finger bones have "index",
// "middle", "pinky" (or "little") and "thumb" in their names. Call from LateUpdate, after
// the arm has been placed.
public static class HandGrip
{
    private class Rig
    {
        public Transform index, middle, pinky;
        public readonly List<List<Transform>> fingers = new List<List<Transform>>();
        public List<Transform> thumb;
        public bool valid;
    }

    private static readonly Dictionary<Transform, Rig> Rigs = new Dictionary<Transform, Rig>();

    // Some animations do not move the fingers at all. Then what this script wrote last frame
    // would still be there and the curl would be added again and again (fingers spinning).
    // So: if a bone still holds exactly what was written last frame, start again from its
    // remembered pose.
    private static readonly Dictionary<Transform, Quaternion> Written = new Dictionary<Transform, Quaternion>();
    private static readonly Dictionary<Transform, Quaternion> BasePose = new Dictionary<Transform, Quaternion>();

    private static void Begin(Transform bone)
    {
        if (Written.TryGetValue(bone, out Quaternion last) && Mathf.Abs(Quaternion.Dot(bone.localRotation, last)) > 0.999999f)
            bone.localRotation = BasePose[bone];
        else
            BasePose[bone] = bone.localRotation;
    }

    private static void Begin(Transform hand, Rig rig, bool includeHand)
    {
        if (includeHand)
            Begin(hand);
        foreach (List<Transform> chain in rig.fingers)
            foreach (Transform joint in chain)
                Begin(joint);
        if (rig.thumb != null)
            foreach (Transform joint in rig.thumb)
                Begin(joint);
    }

    private static void End(Transform hand, Rig rig, bool includeHand)
    {
        if (includeHand)
            Written[hand] = hand.localRotation;
        foreach (List<Transform> chain in rig.fingers)
            foreach (Transform joint in chain)
                Written[joint] = joint.localRotation;
        if (rig.thumb != null)
            foreach (Transform joint in rig.thumb)
                Written[joint] = joint.localRotation;
    }

    private static Rig GetRig(Transform hand)
    {
        if (Rigs.TryGetValue(hand, out Rig rig))
            return rig;

        rig = new Rig();
        // The first bone of each finger: the highest bones under the hand with a finger name.
        List<Transform> roots = new List<Transform>();
        foreach (Transform t in hand.GetComponentsInChildren<Transform>(true))
        {
            if (t == hand || !IsFingerName(t.name))
                continue;
            bool underFinger = false;
            for (Transform p = t.parent; p != null && p != hand; p = p.parent)
            {
                if (IsFingerName(p.name))
                {
                    underFinger = true;
                    break;
                }
            }
            if (!underFinger)
                roots.Add(t);
        }

        foreach (Transform child in roots)
        {
            string n = child.name.ToLowerInvariant();
            List<Transform> chain = new List<Transform>();
            for (Transform t = child; t != null; t = t.childCount > 0 ? t.GetChild(0) : null)
                chain.Add(t);

            if (n.Contains("thumb")) rig.thumb = chain;
            else if (n.Contains("index")) { rig.index = child; rig.fingers.Add(chain); }
            else if (n.Contains("middle")) { rig.middle = child; rig.fingers.Add(chain); }
            else if (n.Contains("pinky") || n.Contains("little")) { rig.pinky = child; rig.fingers.Add(chain); }
            else if (n.Contains("ring")) rig.fingers.Add(chain);
        }
        rig.valid = rig.index != null && rig.middle != null && rig.pinky != null;
        Rigs[hand] = rig;
        if (rig.valid)
            Debug.Log($"[HandGrip] '{hand.name}': finger bones found ({rig.fingers.Count} fingers, thumb {(rig.thumb != null ? "yes" : "no")}).", hand);
        else
            Debug.LogWarning($"[HandGrip] '{hand.name}': finger bones NOT found (children: {hand.childCount}). The hand only turns toward the target.", hand);
        return rig;
    }

    private static bool IsFingerName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("thumb") || n.Contains("index") || n.Contains("middle") || n.Contains("ring") || n.Contains("pinky") || n.Contains("little");
    }

    // Only curls the fingers (a relaxed, half-closed hand), without turning the hand.
    public static void Curl(Transform hand, bool rightHand, float amount)
    {
        if (hand == null || amount <= 0f)
            return;
        Rig rig = GetRig(hand);
        if (!rig.valid)
            return;
        Begin(hand, rig, false);
        Vector3 fingers = (rig.middle.position - hand.position).normalized;
        Vector3 indexSide = (rig.index.position - rig.pinky.position).normalized;
        Vector3 palmNormal = Vector3.Cross(fingers, indexSide).normalized * (rightHand ? 1f : -1f);
        CurlFingers(rig, fingers, palmNormal, amount, 0.6f);
        End(hand, rig, false);
    }

    // Pointing: the index finger stays as the animation has it (it is nearly straight in the
    // sitting pose), the other fingers fold softly into the palm and the thumb rests on them.
    // Starts from the animated pose every frame, so nothing adds up over time.
    public static void Point(Transform hand, bool rightHand, float amount)
    {
        if (hand == null || amount <= 0f)
            return;
        Rig rig = GetRig(hand);
        if (!rig.valid)
            return;
        Begin(hand, rig, false);
        amount = Mathf.Clamp01(amount);
        Vector3 fingers = (rig.middle.position - hand.position).normalized;
        Vector3 indexSide = (rig.index.position - rig.pinky.position).normalized;
        Vector3 palmNormal = Vector3.Cross(fingers, indexSide).normalized * (rightHand ? 1f : -1f);
        float[] bend = { 40f, 55f, 35f };
        foreach (List<Transform> chain in rig.fingers)
        {
            if (chain.Count > 0 && chain[0] == rig.index)
            {
                // Index finger: straightened (the sitting pose has it bent, which looked like a
                // claw / pinch), each joint in line with the one before it.
                Vector3 straight = (rig.index.position - hand.position).normalized;
                for (int i = 0; i < chain.Count; i++)
                {
                    Transform joint = chain[i];
                    if (joint.childCount == 0)
                        break;
                    Vector3 along = joint.GetChild(0).position - joint.position;
                    if (along.sqrMagnitude > 1e-10f && straight.sqrMagnitude > 1e-10f)
                        joint.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(along, straight), amount) * joint.rotation;
                    straight = (joint.GetChild(0).position - joint.position).normalized;
                }
                continue;
            }
            if (chain.Count == 0)
                continue;
            for (int i = 0; i < chain.Count; i++)
            {
                Transform joint = chain[i];
                Vector3 along = joint.childCount > 0 ? joint.GetChild(0).position - joint.position : fingers;
                Vector3 axis = Vector3.Cross(along.normalized, palmNormal);
                if (axis.sqrMagnitude < 1e-6f)
                    continue;
                joint.rotation = Quaternion.AngleAxis(bend[Mathf.Min(i, bend.Length - 1)] * amount, axis.normalized) * joint.rotation;
            }
        }
        if (rig.thumb != null)
        {
            for (int i = 0; i < rig.thumb.Count; i++)
            {
                Transform joint = rig.thumb[i];
                Vector3 along = joint.childCount > 0 ? joint.GetChild(0).position - joint.position : fingers;
                Vector3 axis = Vector3.Cross(along.normalized, palmNormal);
                if (axis.sqrMagnitude > 1e-6f)
                    joint.rotation = Quaternion.AngleAxis(15f * amount, axis.normalized) * joint.rotation;
            }
        }
        End(hand, rig, false);
    }

    private static void CurlFingers(Rig rig, Vector3 fingers, Vector3 palmNormal, float amount, float thumbAmount)
    {
        float[] bend = { 50f, 75f, 50f, 30f };
        foreach (List<Transform> chain in rig.fingers)
        {
            for (int i = 0; i < chain.Count; i++)
            {
                Transform joint = chain[i];
                Vector3 along = joint.childCount > 0 ? joint.GetChild(0).position - joint.position : fingers;
                Vector3 axis = Vector3.Cross(along.normalized, palmNormal);
                if (axis.sqrMagnitude < 1e-6f)
                    continue;
                joint.rotation = Quaternion.AngleAxis(bend[Mathf.Min(i, bend.Length - 1)] * amount, axis.normalized) * joint.rotation;
            }
        }
        if (rig.thumb != null)
        {
            for (int i = 0; i < rig.thumb.Count; i++)
            {
                Transform joint = rig.thumb[i];
                Vector3 along = joint.childCount > 0 ? joint.GetChild(0).position - joint.position : fingers;
                Vector3 axis = Vector3.Cross(along.normalized, palmNormal);
                if (axis.sqrMagnitude > 1e-6f)
                    joint.rotation = Quaternion.AngleAxis(18f * amount * thumbAmount, axis.normalized) * joint.rotation;
            }
        }
    }

    // Returns false (and only places the object at the hand) if the finger bones were not found.
    public static bool Apply(Transform hand, bool rightHand, Transform prop, Vector3 aimAt, float weight, float curl = 1f, float maxWristBend = 50f)
    {
        if (hand == null)
            return false;

        Rig rig = GetRig(hand);
        if (rig.valid)
            Begin(hand, rig, false);

        Vector3 aim = aimAt - hand.position;
        if (aim.sqrMagnitude < 1e-8f)
            aim = hand.forward;
        aim.Normalize();

        // A wrist only bends so far: keep the hand within ~50 degrees of the forearm, so it
        // never ends up twisted back on itself.
        Vector3 forearm = hand.parent != null ? hand.position - hand.parent.position : aim;
        if (forearm.sqrMagnitude > 1e-8f)
        {
            forearm.Normalize();
            aim = Vector3.RotateTowards(forearm, aim, maxWristBend * Mathf.Deg2Rad, 0f).normalized;
        }
        Vector3 up = Mathf.Abs(Vector3.Dot(aim, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;

        if (!rig.valid)
        {
            // No finger bones: at least point the hand at the target and put the object in it.
            hand.rotation = Quaternion.Slerp(hand.rotation, Quaternion.FromToRotation(forearm, aim) * hand.rotation, Mathf.Clamp01(weight));
            if (prop != null)
                prop.SetPositionAndRotation(hand.position + aim * 0.05f, Quaternion.LookRotation(aim, up));
            return false;
        }

        weight = Mathf.Clamp01(weight);

        // 1) Turn the hand: fingers toward the target, index finger on top (thumb up).
        Vector3 fingers = (rig.middle.position - hand.position).normalized;
        Vector3 indexSide = (rig.index.position - rig.pinky.position).normalized;
        Quaternion turn = Quaternion.LookRotation(aim, up) * Quaternion.Inverse(Quaternion.LookRotation(fingers, indexSide));
        hand.rotation = Quaternion.Slerp(hand.rotation, turn * hand.rotation, weight);

        // 2) Where the palm is now.
        Vector3 toKnuckles = rig.middle.position - hand.position;
        float size = Mathf.Max(0.01f, toKnuckles.magnitude);
        fingers = toKnuckles / size;
        indexSide = (rig.index.position - rig.pinky.position).normalized;
        Vector3 palmNormal = Vector3.Cross(fingers, indexSide).normalized * (rightHand ? 1f : -1f);

        // 3) Curl the fingers (and a little the thumb) around the handle.
        CurlFingers(rig, fingers, palmNormal, curl * weight, 1f);

        // 4) The object lies in the palm, its tip pointing at the target.
        if (prop != null)
        {
            Vector3 palm = hand.position + fingers * (size * 0.62f) + palmNormal * (size * 0.30f);
            // The object itself always points exactly at the target, even when the wrist cannot.
            // (its probe sits a little above the grip, so the handle is held that much lower)
            Vector3 exact = aimAt - up * EarThermometerProp.TipRise - palm;
            prop.SetPositionAndRotation(palm, Quaternion.LookRotation(exact.sqrMagnitude > 1e-8f ? exact.normalized : aim, up));
        }
        End(hand, rig, false);
        return true;
    }
}
