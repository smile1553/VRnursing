using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ChoiceButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private int choiceIndex;

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

        UISignalBus.Emit("UI.Choice", payload);
    }
}
