using System;
using UnityEngine;

public class ScenarioCursorController : MonoBehaviour
{
    [Header("Bindings")]
    public ScenarioController controller;
    public Transform cursor;
    public bool deactivateWhenNoTarget = true;
    public float moveLerp = 12f;

    [Header("Targets")]
    public ScenarioCursorTarget[] targets;

    ScenarioCursorTarget _activeTarget;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
    }

    void OnEnable()
    {
        if (controller != null)
            controller.cursorTargetChanged.AddListener(OnCursorTargetChanged);
    }

    void OnDisable()
    {
        if (controller != null)
            controller.cursorTargetChanged.RemoveListener(OnCursorTargetChanged);
    }

    void LateUpdate()
    {
        if (!cursor) return;
        if (_activeTarget == null || _activeTarget.transform == null)
        {
            if (deactivateWhenNoTarget)
                cursor.gameObject.SetActive(false);
            return;
        }

        if (!cursor.gameObject.activeSelf)
            cursor.gameObject.SetActive(true);

        var targetPos = _activeTarget.transform.position + _activeTarget.offset;
        if (moveLerp <= 0f)
            cursor.position = targetPos;
        else
            cursor.position = Vector3.Lerp(cursor.position, targetPos, Time.deltaTime * moveLerp);
    }

    void OnCursorTargetChanged(string id)
    {
        _activeTarget = FindTarget(id);
        if (_activeTarget == null)
        {
            if (cursor && deactivateWhenNoTarget)
                cursor.gameObject.SetActive(false);
        }
    }

    ScenarioCursorTarget FindTarget(string id)
    {
        if (string.IsNullOrEmpty(id) || targets == null) return null;
        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            if (t != null && t.Matches(id))
                return t;
        }
        return null;
    }
}

[System.Serializable]
public class ScenarioCursorTarget
{
    public string id;
    public Transform transform;
    public Vector3 offset;

    public bool Matches(string other)
    {
        return !string.IsNullOrEmpty(id) && string.Equals(id, other, StringComparison.OrdinalIgnoreCase);
    }
}
