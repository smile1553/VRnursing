using System.Collections.Generic;
using UnityEngine;

// Builds a Humanoid Avatar at runtime for a Mixamo-rigged model (bones named "mixamorig:Hips" etc.).
// Mom (MomStanding Idle.fbx) and Yaya_Kid (Sitting Idle.fbx) are imported as Generic, but
// Listen_Mom.anim / Listen_Kid.anim are Humanoid (muscle) clips. A Humanoid clip does nothing on a
// Generic avatar, so the animation players swap to this avatar only while such a clip plays.
// No asset / import-setting changes are needed.
public static class MixamoHumanoidAvatarBuilder
{
    private static readonly Dictionary<string, string> MixamoToHuman = new Dictionary<string, string>
    {
        { "Hips", "Hips" },
        { "Spine", "Spine" },
        { "Spine1", "Chest" },
        { "Spine2", "UpperChest" },
        { "Neck", "Neck" },
        { "Head", "Head" },

        { "LeftShoulder", "LeftShoulder" },
        { "LeftArm", "LeftUpperArm" },
        { "LeftForeArm", "LeftLowerArm" },
        { "LeftHand", "LeftHand" },
        { "RightShoulder", "RightShoulder" },
        { "RightArm", "RightUpperArm" },
        { "RightForeArm", "RightLowerArm" },
        { "RightHand", "RightHand" },

        { "LeftUpLeg", "LeftUpperLeg" },
        { "LeftLeg", "LeftLowerLeg" },
        { "LeftFoot", "LeftFoot" },
        { "LeftToeBase", "LeftToes" },
        { "RightUpLeg", "RightUpperLeg" },
        { "RightLeg", "RightLowerLeg" },
        { "RightFoot", "RightFoot" },
        { "RightToeBase", "RightToes" },

        { "LeftHandThumb1", "Left Thumb Proximal" },
        { "LeftHandThumb2", "Left Thumb Intermediate" },
        { "LeftHandThumb3", "Left Thumb Distal" },
        { "LeftHandIndex1", "Left Index Proximal" },
        { "LeftHandIndex2", "Left Index Intermediate" },
        { "LeftHandIndex3", "Left Index Distal" },
        { "LeftHandMiddle1", "Left Middle Proximal" },
        { "LeftHandMiddle2", "Left Middle Intermediate" },
        { "LeftHandMiddle3", "Left Middle Distal" },
        { "LeftHandRing1", "Left Ring Proximal" },
        { "LeftHandRing2", "Left Ring Intermediate" },
        { "LeftHandRing3", "Left Ring Distal" },
        { "LeftHandPinky1", "Left Little Proximal" },
        { "LeftHandPinky2", "Left Little Intermediate" },
        { "LeftHandPinky3", "Left Little Distal" },

        { "RightHandThumb1", "Right Thumb Proximal" },
        { "RightHandThumb2", "Right Thumb Intermediate" },
        { "RightHandThumb3", "Right Thumb Distal" },
        { "RightHandIndex1", "Right Index Proximal" },
        { "RightHandIndex2", "Right Index Intermediate" },
        { "RightHandIndex3", "Right Index Distal" },
        { "RightHandMiddle1", "Right Middle Proximal" },
        { "RightHandMiddle2", "Right Middle Intermediate" },
        { "RightHandMiddle3", "Right Middle Distal" },
        { "RightHandRing1", "Right Ring Proximal" },
        { "RightHandRing2", "Right Ring Intermediate" },
        { "RightHandRing3", "Right Ring Distal" },
        { "RightHandPinky1", "Right Little Proximal" },
        { "RightHandPinky2", "Right Little Intermediate" },
        { "RightHandPinky3", "Right Little Distal" },
    };

    // Call in Awake (before the Animator has moved any bone) so the captured pose is the
    // model's rest pose (Mixamo T-pose).
    public static SkeletonBone[] CaptureRestPose(Transform root)
    {
        if (root == null)
            return null;

        List<SkeletonBone> bones = new List<SkeletonBone>();
        HashSet<string> usedNames = new HashSet<string>();

        bones.Add(new SkeletonBone
        {
            name = root.name,
            position = Vector3.zero,
            rotation = Quaternion.identity,
            scale = Vector3.one
        });
        usedNames.Add(root.name);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root || usedNames.Contains(t.name))
                continue;

            usedNames.Add(t.name);
            bones.Add(new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            });
        }

        return bones.ToArray();
    }

    public static Avatar Build(GameObject root, SkeletonBone[] restPose)
    {
        if (root == null || restPose == null || restPose.Length == 0)
            return null;

        List<HumanBone> human = new List<HumanBone>();
        HashSet<string> mappedHumanNames = new HashSet<string>();
        foreach (SkeletonBone bone in restPose)
        {
            string key = StripPrefix(bone.name);
            if (!MixamoToHuman.TryGetValue(key, out string humanName) || mappedHumanNames.Contains(humanName))
                continue;

            HumanLimit limit = new HumanLimit { useDefaultValues = true };
            human.Add(new HumanBone { boneName = bone.name, humanName = humanName, limit = limit });
            mappedHumanNames.Add(humanName);
        }

        if (!mappedHumanNames.Contains("Hips") || !mappedHumanNames.Contains("Head"))
        {
            Debug.LogWarning($"[MixamoHumanoidAvatarBuilder] {root.name}: Mixamo bones (mixamorig:Hips ...) not found, cannot build Humanoid avatar.", root);
            return null;
        }

        HumanDescription description = new HumanDescription
        {
            human = human.ToArray(),
            skeleton = restPose,
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false
        };

        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogWarning($"[MixamoHumanoidAvatarBuilder] {root.name}: built Humanoid avatar is not valid.", root);
            return null;
        }

        avatar.name = root.name + "_RuntimeHumanoid";
        return avatar;
    }

    private static string StripPrefix(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return string.Empty;

        int colon = boneName.LastIndexOf(':');
        return colon >= 0 ? boneName.Substring(colon + 1) : boneName;
    }

    public static Transform FindBone(Transform root, string mixamoName)
    {
        if (root == null)
            return null;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (StripPrefix(t.name) == mixamoName)
                return t;
        }

        return null;
    }
}
