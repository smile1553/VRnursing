using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ChoiceButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private int choiceIndex;
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
        var payload = new Dictionary<string, object>
        {
            { "choiceIndex", choiceIndex }
        };

        if (logSignals)
        {
            Debug.Log($"[ChoiceButtonUI] Emit UI.Choice choiceIndex={choiceIndex}", this);
        }

        UISignalBus.Emit("UI.Choice", payload);
    }
}
