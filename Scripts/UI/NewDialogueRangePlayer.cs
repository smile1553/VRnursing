using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class NewDialogueRangePlayer : MonoBehaviour
{
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private GameObject dialogueRoot;

    [Header("Timing")]
    [SerializeField] private float defaultLineDuration = 2.7f;
    [SerializeField] private bool hideWhenFinished = true;

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onLineDisplayed;
    [SerializeField] private UnityEvent onRangeFinished;

    private Coroutine playRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    public void PlayFromLine(int startIndex)
    {
        ResolveReferences();
        PlayRange(startIndex, -1);
    }

    public void PlaySingleLine(int lineIndex)
    {
        ResolveReferences();
        StopPlayback();
        ShowLine(lineIndex);
    }

    public void PlayRange(int startIndex, int endIndexInclusive)
    {
        ResolveReferences();
        StopPlayback();

        if (dialogueManager == null || dialogueManager.dialogueLines == null || dialogueManager.dialogueLines.Count == 0)
            return;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);

        if (dialogueManager != null)
            dialogueManager.StopAllCoroutines();

        if (endIndexInclusive < 0 || endIndexInclusive >= dialogueManager.dialogueLines.Count)
            endIndexInclusive = dialogueManager.dialogueLines.Count - 1;

        startIndex = Mathf.Clamp(startIndex, 0, dialogueManager.dialogueLines.Count - 1);
        endIndexInclusive = Mathf.Clamp(endIndexInclusive, startIndex, dialogueManager.dialogueLines.Count - 1);

        playRoutine = StartCoroutine(PlayRangeRoutine(startIndex, endIndexInclusive));
    }

    public void StopPlayback()
    {
        ResolveReferences();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (dialogueManager != null)
            dialogueManager.StopAllCoroutines();
    }

    public void Hide()
    {
        ResolveReferences();
        StopPlayback();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    public void ShowLine(int lineIndex)
    {
        ResolveReferences();

        if (dialogueManager == null || dialogueManager.dialogueLines == null)
            return;

        if (lineIndex < 0 || lineIndex >= dialogueManager.dialogueLines.Count)
            return;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);

        if (dialogueManager != null)
            dialogueManager.StopAllCoroutines();

        DialogueLine line = dialogueManager.dialogueLines[lineIndex];

        if (dialogueManager.speakerText != null)
            dialogueManager.speakerText.text = line.speakerName;

        if (dialogueManager.bodyText != null)
            dialogueManager.bodyText.text = line.content;

        ApplySpeakerStyle(line.speakerName);
        onLineDisplayed?.Invoke(lineIndex);
    }

    private void ResolveReferences()
    {
        if (dialogueManager == null)
            dialogueManager = GetComponent<NewDialogueManager>();

        if (dialogueRoot == null && dialogueManager != null)
            dialogueRoot = dialogueManager.gameObject;
    }

    private IEnumerator PlayRangeRoutine(int startIndex, int endIndexInclusive)
    {
        for (int i = startIndex; i <= endIndexInclusive; i++)
        {
            ShowLine(i);
            yield return new WaitForSeconds(Mathf.Max(0.1f, defaultLineDuration));
        }

        playRoutine = null;
        onRangeFinished?.Invoke();

        if (hideWhenFinished)
            Hide();
    }

    private void ApplySpeakerStyle(string speakerName)
    {
        if (speakerName == "媽媽" || speakerName == "媽咪")
        {
            if (dialogueManager.speakerPanel != null)
                dialogueManager.speakerPanel.color = dialogueManager.momPanelColor;

            if (dialogueManager.speakerText != null)
                dialogueManager.speakerText.color = dialogueManager.momTextColor;

            if (dialogueManager.panelOutline != null)
                dialogueManager.panelOutline.effectColor = dialogueManager.momOutlineColor;

            return;
        }

        if (speakerName == "芽芽" || speakerName == "牙牙")
        {
            if (dialogueManager.speakerPanel != null)
                dialogueManager.speakerPanel.color = dialogueManager.yayaPanelColor;

            if (dialogueManager.speakerText != null)
                dialogueManager.speakerText.color = dialogueManager.yayaTextColor;

            if (dialogueManager.panelOutline != null)
                dialogueManager.panelOutline.effectColor = dialogueManager.yayaOutlineColor;

            return;
        }

        if (dialogueManager.speakerPanel != null)
            dialogueManager.speakerPanel.color = Color.white;

        if (dialogueManager.speakerText != null)
            dialogueManager.speakerText.color = Color.gray;

        if (dialogueManager.panelOutline != null)
            dialogueManager.panelOutline.effectColor = Color.gray;
    }
}
