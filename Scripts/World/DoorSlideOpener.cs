using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DoorSlideOpener : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] Transform doorLeaf;
    [SerializeField] Vector3 localOpenOffset = new Vector3(-1.2f, 0f, 0f);
    [SerializeField] float duration = 1.2f;

    [Header("State")]
    [SerializeField] bool startOpened;

    Vector3 closedLocalPosition;
    Coroutine moveRoutine;
    bool initialized;

    void Awake()
    {
        if (doorLeaf == null)
            doorLeaf = transform;

        closedLocalPosition = doorLeaf.localPosition;
        initialized = true;

        if (startOpened)
            doorLeaf.localPosition = closedLocalPosition + localOpenOffset;
    }

    public void Open()
    {
        EnsureInitialized();
        MoveTo(closedLocalPosition + localOpenOffset);
    }

    public void Close()
    {
        EnsureInitialized();
        MoveTo(closedLocalPosition);
    }

    public void Toggle()
    {
        EnsureInitialized();
        Vector3 openPosition = closedLocalPosition + localOpenOffset;
        float distanceToOpen = Vector3.Distance(doorLeaf.localPosition, openPosition);
        float distanceToClosed = Vector3.Distance(doorLeaf.localPosition, closedLocalPosition);
        MoveTo(distanceToOpen > distanceToClosed ? openPosition : closedLocalPosition);
    }

    void MoveTo(Vector3 targetLocalPosition)
    {
        if (doorLeaf == null)
            return;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveRoutine(targetLocalPosition));
    }

    IEnumerator MoveRoutine(Vector3 targetLocalPosition)
    {
        Vector3 start = doorLeaf.localPosition;
        float elapsed = 0f;
        float moveDuration = Mathf.Max(0.01f, duration);

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            t = t * t * (3f - 2f * t);
            doorLeaf.localPosition = Vector3.Lerp(start, targetLocalPosition, t);
            yield return null;
        }

        doorLeaf.localPosition = targetLocalPosition;
        moveRoutine = null;
    }

    void EnsureInitialized()
    {
        if (initialized)
            return;

        if (doorLeaf == null)
            doorLeaf = transform;

        closedLocalPosition = doorLeaf.localPosition;
        initialized = true;
    }
}
