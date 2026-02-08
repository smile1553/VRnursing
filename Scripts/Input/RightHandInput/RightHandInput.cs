using UnityEngine;

[DisallowMultipleComponent]
public class RightHandInput : MonoBehaviour
{
    [SerializeField] private InteractRaycaster raycaster;
    [SerializeField] private string clickButton = "Fire1";
    [SerializeField] private KeyCode clickKey = KeyCode.Mouse0;
    [SerializeField] private bool useButton = true;
    [SerializeField] private bool useKey = true;

    private void Awake()
    {
        if (raycaster == null)
        {
            raycaster = GetComponentInChildren<InteractRaycaster>();
        }
    }

    private void Update()
    {
        if (raycaster == null)
        {
            return;
        }

        bool clicked = false;

        if (useButton && !string.IsNullOrEmpty(clickButton) && Input.GetButtonDown(clickButton))
        {
            clicked = true;
        }

        if (!clicked && useKey && Input.GetKeyDown(clickKey))
        {
            clicked = true;
        }

        if (clicked)
        {
            raycaster.EmitClick();
        }
    }
}
