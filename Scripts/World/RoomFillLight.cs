using UnityEngine;

// Adds a soft fill light to one room at runtime, so the room (and the characters in it) look a
// bit brighter without re-baking the lightmaps. The scene's own lights are all baked.
// Created automatically for DOCTOR_CLINIC_01; add the component to an object yourself
// (the automatic one is then skipped) to tune the values in the Inspector.
public class RoomFillLight : MonoBehaviour
{
    [Tooltip("The light is placed above the first of these objects that exists (names separated by |).")]
    [SerializeField] private string roomCenterNames = "RP_DoctorClinic_01|DOCTOR_CLINIC_01";
    [Tooltip("0 = above the room centre, 1 = above Yaya. In between lights both the room and the characters.")]
    [Range(0f, 1f)] [SerializeField] private float towardYaya = 0.6f;
    [Tooltip("Height of the light above the floor (m).")]
    [SerializeField] private float height = 2.4f;
    [Tooltip("Brightness. A point light fades quickly with distance, but the baked walls wash out quickly: keep it around 0.5 - 1.")]
    [SerializeField] private float intensity = 0.8f;
    [Tooltip("Reach of the light (m). Larger = brighter far from the light, but it also reaches the corridor.")]
    [SerializeField] private float range = 8f;
    [SerializeField] private Color color = new Color(1f, 0.97f, 0.92f);

    private Light fill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<RoomFillLight>(true) != null)
            return;
        new GameObject("RoomFillLight (auto)").AddComponent<RoomFillLight>();
    }

    private void Start()
    {
        Transform center = null;
        foreach (string raw in roomCenterNames.Split('|'))
        {
            GameObject go = GameObject.Find(raw.Trim());
            if (go != null)
            {
                center = go.transform;
                break;
            }
        }

        if (center == null)
        {
            Debug.LogWarning("[RoomFillLight] Room not found: " + roomCenterNames, this);
            return;
        }

        GameObject lightObject = new GameObject("Fill Light");
        lightObject.transform.SetParent(transform, false);
        Vector3 spot = center.position;
        YayaAnimationPlayer yaya = FindObjectOfType<YayaAnimationPlayer>(true);
        if (yaya != null)
            spot = Vector3.Lerp(spot, yaya.transform.position, towardYaya);
        lightObject.transform.position = new Vector3(spot.x, height, spot.z);
        fill = lightObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.shadows = LightShadows.None;
        fill.renderMode = LightRenderMode.ForcePixel;
        Apply();
        Debug.Log($"[RoomFillLight] Fill light at {lightObject.transform.position:F2} (above {center.name}).", lightObject);
    }

    private void Apply()
    {
        if (fill == null)
            return;
        fill.intensity = intensity;
        fill.range = range;
        fill.color = color;
    }

    // Lets you tune the values in the Inspector while playing.
    private void OnValidate()
    {
        Apply();
    }
}
