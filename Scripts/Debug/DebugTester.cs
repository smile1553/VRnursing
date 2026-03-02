using UnityEngine;

/// <summary>
/// Simple keyboard harness to test Mom/Kid beats without Story pipeline.
/// </summary>
public class DebugTester : MonoBehaviour
{
    [SerializeField] private WorldDirector director;

    void Awake()
    {
        if (!director) director = FindObjectOfType<WorldDirector>();
    }

    void Update()
    {
        if (!director) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) director.InitAct1();
        if (Input.GetKeyDown(KeyCode.Alpha2)) director.MomSay(2f);
        if (Input.GetKeyDown(KeyCode.Alpha3)) director.KidReactFear();
        if (Input.GetKeyDown(KeyCode.Alpha4)) director.PrepareTempMeasure();
        if (Input.GetKeyDown(KeyCode.Alpha5)) director.PrepareBPMeasure();
        if (Input.GetKeyDown(KeyCode.Alpha6)) director.MomGestureCheer();
    }
}
