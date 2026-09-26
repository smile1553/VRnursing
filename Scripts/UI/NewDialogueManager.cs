using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;

    [TextArea(3, 5)]
    public string content;
}

public class NewDialogueManager : MonoBehaviour
{
    [Header("UI References")]
    public Image speakerPanel;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI bodyText;
    public Outline panelOutline;

    [Header("Playback")]
    public bool playOnEnable = false;
    public float displayDuration = 3.0f;
    public bool hideWhenFinished = true;

    [Header("Mom Style")]
    public Color momPanelColor = new Color(0.97f, 0.84f, 0.85f, 1f);
    public Color momTextColor = new Color(0.52f, 0.13f, 0.16f, 1f);
    public Color momOutlineColor = new Color(0.97f, 0.84f, 0.85f, 0.6f);

    [Header("Yaya Style")]
    public Color yayaPanelColor = new Color(0.89f, 0.95f, 0.99f, 1f);
    public Color yayaTextColor = new Color(0.12f, 0.23f, 0.54f, 1f);
    public Color yayaOutlineColor = new Color(0.89f, 0.95f, 0.99f, 0.6f);

    [Header("Nurse Style")]
    public Color nursePanelColor = new Color(0.92f, 0.96f, 0.94f, 1f);
    public Color nurseTextColor = new Color(0.12f, 0.32f, 0.24f, 1f);
    public Color nurseOutlineColor = new Color(0.92f, 0.96f, 0.94f, 0.6f);

    [Header("Dialogue Lines")]
    public List<DialogueLine> dialogueLines = new List<DialogueLine>();

    private Coroutine autoPlayRoutine;

    private void Awake()
    {
        TryBindReferences();
    }

    private void OnEnable()
    {
        TryBindReferences();

        if (playOnEnable)
            PlayFromLine(0);
        else
            SetDialoguePanelVisible(false);
    }

    private void Reset()
    {
        TryBindReferences();
    }

    private void OnDisable()
    {
        StopPlayback();
    }

    public void PlayFromLine(int startIndex)
    {
        PlayRange(startIndex, dialogueLines.Count - 1);
    }

    public void PlayRange(int startIndex, int endIndexInclusive)
    {
        StopPlayback();

        if (dialogueLines == null || dialogueLines.Count == 0)
            return;

        startIndex = Mathf.Clamp(startIndex, 0, dialogueLines.Count - 1);
        endIndexInclusive = Mathf.Clamp(endIndexInclusive, startIndex, dialogueLines.Count - 1);

        gameObject.SetActive(true);
        SetDialoguePanelVisible(true);
        autoPlayRoutine = StartCoroutine(PlayRangeRoutine(startIndex, endIndexInclusive));
    }

    public void ShowLine(int index)
    {
        if (dialogueLines == null || index < 0 || index >= dialogueLines.Count)
            return;

        TryBindReferences();

        DialogueLine line = dialogueLines[index];
        ShowText(line.speakerName, line.content);
    }

    public void ShowText(string speakerName, string content)
    {
        TryBindReferences();

        if (speakerText != null)
        {
            TryAddGlyphs(speakerText, speakerName);
            speakerText.text = speakerName;
        }

        if (bodyText != null)
        {
            TryAddGlyphs(bodyText, content);
            bodyText.text = content;
        }

        ApplySpeakerStyle(speakerName);
        SetDialoguePanelVisible(true);
    }

    private void TryBindReferences()
    {
        Transform root = transform;
        Transform dialoguePanel = transform.Find("Dialogue_Panel");
        if (dialoguePanel != null)
            root = dialoguePanel;

        if (speakerPanel == null)
        {
            Transform speakerPanelTransform = root.Find("Speaker_Panel");
            if (speakerPanelTransform != null)
                speakerPanel = speakerPanelTransform.GetComponent<Image>();
        }

        if (speakerText == null)
        {
            Transform speakerTextTransform = root.Find("Speaker_Panel/Speaker_Text");
            if (speakerTextTransform != null)
                speakerText = speakerTextTransform.GetComponent<TextMeshProUGUI>();
        }

        if (bodyText == null)
        {
            Transform bodyTextTransform = root.Find("Body_Text");
            if (bodyTextTransform != null)
                bodyText = bodyTextTransform.GetComponent<TextMeshProUGUI>();
        }

        if (panelOutline == null)
            panelOutline = root.GetComponent<Outline>();
    }

    public void StopPlayback()
    {
        if (autoPlayRoutine == null)
            return;

        StopCoroutine(autoPlayRoutine);
        autoPlayRoutine = null;
    }

    private IEnumerator PlayRangeRoutine(int startIndex, int endIndexInclusive)
    {
        for (int i = startIndex; i <= endIndexInclusive; i++)
        {
            ShowLine(i);
            yield return new WaitForSeconds(Mathf.Max(0.1f, displayDuration));
        }

        autoPlayRoutine = null;

        if (hideWhenFinished)
            SetDialoguePanelVisible(false);
    }

    private void ApplySpeakerStyle(string speakerName)
    {
        if (speakerName == "\u5abd\u5abd")
        {
            ApplyStyle(momPanelColor, momTextColor, momOutlineColor);
            return;
        }

        if (speakerName == "\u82bd\u82bd")
        {
            ApplyStyle(yayaPanelColor, yayaTextColor, yayaOutlineColor);
            return;
        }

        if (speakerName == "\u8b77\u751f")
        {
            ApplyStyle(nursePanelColor, nurseTextColor, nurseOutlineColor);
            return;
        }

        ApplyStyle(Color.white, Color.gray, Color.gray);
    }

    private void TryAddGlyphs(TMP_Text text, string value)
    {
        if (text == null || text.font == null || string.IsNullOrEmpty(value))
            return;

        text.font.TryAddCharacters(value, out string missingCharacters);
        if (!string.IsNullOrEmpty(missingCharacters))
            Debug.LogWarning("[NewDialogueManager] TMP font is missing glyphs: " + missingCharacters, this);
    }

    private void ApplyStyle(Color panelColor, Color textColor, Color outlineColor)
    {
        if (speakerPanel != null)
            speakerPanel.color = panelColor;

        if (speakerText != null)
            speakerText.color = textColor;

        if (panelOutline != null)
            panelOutline.effectColor = outlineColor;
    }

    private void SetDialoguePanelVisible(bool visible)
    {
        Transform panel = transform.Find("Dialogue_Panel");
        if (panel != null)
            panel.gameObject.SetActive(visible);
    }
}
