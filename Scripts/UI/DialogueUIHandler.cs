using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class DialogueUIHandler : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private bool logSignals = false;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (logSignals)
        {
            Debug.Log("[DialogueUIHandler] Emit UI.Next", this);
        }

        UISignalBus.Emit("UI.Next", null);
    }
}
