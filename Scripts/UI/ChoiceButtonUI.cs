using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChoiceButtonUI : UISignalButton
{
    [SerializeField] private int choiceIndex;

    protected override string SignalName => "UI.Choice";
    protected override object CreatePayload()
    {
        return new Dictionary<string, object> { { "choiceIndex", choiceIndex } };
    }

    protected override string LogDetails => $" choiceIndex={choiceIndex}";
}
