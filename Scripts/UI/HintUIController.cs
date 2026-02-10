using UnityEngine;

[DisallowMultipleComponent]
public class HintUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool setActiveOnShow = true;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    public void ShowHint()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (setActiveOnShow)
        {
            gameObject.SetActive(true);
        }
    }

    public void HideHint()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (setActiveOnShow)
        {
            gameObject.SetActive(false);
        }
    }
}
