using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UIEventButton : UISignalButton
{
    [Header("Signal")]
    [SerializeField] private string uiEvent = "UI.Next";
    [SerializeField] private int choiceIndex = 0;

    protected override string SignalName => uiEvent;
    protected override object CreatePayload() => uiEvent == "UI.Choice"
        ? new Dictionary<string, object> { { "choiceIndex", choiceIndex } }
        : null;
    protected override string LogDetails => uiEvent == "UI.Choice" ? $" choiceIndex={choiceIndex}" : string.Empty;
}
