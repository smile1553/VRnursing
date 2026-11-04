using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Settings for recording a demo video.
//
// While the game runs, an object named "DEMO_SETTINGS" appears at the top of the Hierarchy
// (under DontDestroyOnLoad). Tick "Hide Skip Buttons" on it and every Skip button becomes
// invisible. The choice is remembered on this computer, so it is still ticked the next time
// you press Play; nothing has to be saved in the scene.
//
// The hidden buttons can still be used from the keyboard: the Skip Key (F8) presses the Skip
// button that would be on screen, so a recording never gets stuck.
public class DemoRecordingMode : MonoBehaviour
{
    const string PrefKey = "DemoRecording.HideSkipButtons.v2";

    [Tooltip("Ticked = every Skip button is invisible (for recording a demo video). Remembered between plays.")]
    public bool hideSkipButtons = true;
    [Tooltip("A button counts as a Skip button when its object name contains this (capital letters do not matter).")]
    public string skipNameContains = "skip";
    [Tooltip("While the buttons are hidden, this key presses the Skip button that would be showing.")]
    public KeyCode skipKey = KeyCode.F8;

    static DemoRecordingMode instance;
    readonly List<Button> buttons = new List<Button>();
    float nextScan;
    bool lastSaved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;
        instance = FindObjectOfType<DemoRecordingMode>();
        if (instance != null)
            return;   // placed in the scene by hand: its own tick box is used
        GameObject go = new GameObject("DEMO_SETTINGS");
        instance = go.AddComponent<DemoRecordingMode>();
        instance.hideSkipButtons = PlayerPrefs.GetInt(PrefKey, 1) == 1;   // hidden unless it was unticked on this device
        instance.lastSaved = instance.hideSkipButtons;
        DontDestroyOnLoad(go);
        Debug.Log($"[DemoRecording] Hide Skip Buttons = {instance.hideSkipButtons} (change it on the 'DEMO_SETTINGS' object in the Hierarchy while playing).");
    }

    void LateUpdate()
    {
        if (hideSkipButtons != lastSaved)
        {
            lastSaved = hideSkipButtons;
            PlayerPrefs.SetInt(PrefKey, hideSkipButtons ? 1 : 0);
            PlayerPrefs.Save();
            nextScan = 0f;
            Debug.Log($"[DemoRecording] Hide Skip Buttons = {hideSkipButtons}");
        }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            buttons.Clear();
            if (!string.IsNullOrEmpty(skipNameContains))
            {
                foreach (Button button in FindObjectsOfType<Button>(true))
                {
                    if (button != null && button.name.IndexOf(skipNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        buttons.Add(button);
                }
            }
        }

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (!hideSkipButtons)
                    continue;
                group = button.gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = hideSkipButtons ? 0f : 1f;
            group.blocksRaycasts = !hideSkipButtons;
            group.interactable = !hideSkipButtons;
        }

        if (hideSkipButtons && SkipKeyPressed())
        {
            foreach (Button button in buttons)
            {
                if (button != null && button.isActiveAndEnabled)
                {
                    button.onClick.Invoke();
                    break;
                }
            }
        }
    }

    bool SkipKeyPressed()
    {
        if (skipKey == KeyCode.None)
            return false;
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null)
            return false;
        foreach (UnityEngine.InputSystem.Controls.KeyControl key in keyboard.allKeys)
        {
            if (key != null && key.wasPressedThisFrame && string.Equals(key.keyCode.ToString(), skipKey.ToString(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(skipKey);
#else
        return false;
#endif
    }
}
