using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIEventButton : MonoBehaviour
{
    [Header("Signal")]
    [SerializeField] private string uiEvent = "UI.Next";
    [SerializeField] private int choiceIndex = 0;
    [SerializeField] private bool logSignals = false;

    [Header("Refs")]
    [SerializeField] private Button button;

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
        if (string.IsNullOrEmpty(uiEvent))
        {
            return;
        }

        if (uiEvent == "UI.Choice")
        {
            var payload = new Dictionary<string, object>
            {
                { "choiceIndex", choiceIndex }
            };

            if (logSignals)
            {
                Debug.Log($"[UIEventButton] Emit {uiEvent} choiceIndex={choiceIndex}", this);
            }

            UISignalBus.Emit(uiEvent, payload);
            return;
        }

        if (logSignals)
        {
            Debug.Log($"[UIEventButton] Emit {uiEvent}", this);
        }

        UISignalBus.Emit(uiEvent, null);
    }
}
