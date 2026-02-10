using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInputRoot : MonoBehaviour
{
    [SerializeField] private VRPlayerRig rig;
    [SerializeField] private VRInputRouter router;
    [SerializeField] private InteractRaycaster rightRaycaster;
    [SerializeField] private RightHandInput rightHandInput;

    private void Awake()
    {
        if (rig == null)
        {
            rig = GetComponentInChildren<VRPlayerRig>();
        }

        if (router == null)
        {
            router = FindObjectOfType<VRInputRouter>();
        }

        if (rightRaycaster == null)
        {
            rightRaycaster = GetComponentInChildren<InteractRaycaster>();
        }

        if (rightHandInput == null)
        {
            rightHandInput = GetComponentInChildren<RightHandInput>();
        }

        if (rig != null && rig.RayOrigin != null && rightRaycaster != null)
        {
            rightRaycaster.SetRayOrigin(rig.RayOrigin);
        }
    }
}
