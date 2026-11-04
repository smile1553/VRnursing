using UnityEngine;
using UnityEngine.UI;

// Puts Yaya's face in the round picture of the medical record (instead of the bunny icon).
// The photo is taken once, by a small hidden camera in front of Yaya's face, when the nurse
// first gets close to her. Created automatically.
public class MedicalRecordAvatar : MonoBehaviour
{
    [SerializeField] private string panelName = "MedicalRecord_Panel";
    [Tooltip("Centre of the round picture on the record: x from the left, y from the bottom (0-1).")]
    [SerializeField] private Vector2 pictureCentre = new Vector2(0.192f, 0.505f);
    [Tooltip("Diameter of the round picture, as a part of the record's height.")]
    [SerializeField] private float pictureDiameter = 0.36f;
    [Tooltip("Take the photo once the nurse is this close to Yaya (m).")]
    [SerializeField] private float photoDistance = 4f;
    [SerializeField] private Color background = new Color(0.80f, 0.88f, 0.95f, 1f);

    private const bool AutoCreate = false;

    private Sprite photo;
    private Image picture;
    private float nextTry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Not created automatically any more: Yaya's picture is now part of the record image
        // itself. (Add this component to an object by hand to use the live photo instead.)
        if (!AutoCreate || FindObjectOfType<MedicalRecordAvatar>(true) != null)
            return;
        new GameObject("MedicalRecordAvatar (auto)").AddComponent<MedicalRecordAvatar>();
    }

    private void LateUpdate()
    {
        if (picture != null || Time.unscaledTime < nextTry)
            return;
        nextTry = Time.unscaledTime + 0.5f;

        if (photo == null)
            photo = TakePhoto();
        if (photo == null)
            return;

        // The record panel can be hidden, so look through hidden objects too.
        foreach (RectTransform rect in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rect == null || rect.name != panelName || !rect.gameObject.scene.IsValid())
                continue;

            GameObject child = new GameObject("YayaPhoto", typeof(RectTransform));
            child.layer = rect.gameObject.layer;
            RectTransform t = (RectTransform)child.transform;
            t.SetParent(rect, false);
            t.SetAsFirstSibling();   // above the record picture, below its buttons
            t.anchorMin = t.anchorMax = pictureCentre;
            t.pivot = new Vector2(0.5f, 0.5f);
            t.anchoredPosition = Vector2.zero;
            float size = rect.rect.height * pictureDiameter;
            t.sizeDelta = new Vector2(size, size);
            picture = child.AddComponent<Image>();
            picture.sprite = photo;
            picture.raycastTarget = false;
            picture.preserveAspect = true;
            Debug.Log("[MedicalRecordAvatar] Yaya's photo is on the medical record.", child);
            return;
        }
    }

    private Sprite TakePhoto()
    {
        YayaAnimationPlayer yaya = FindObjectOfType<YayaAnimationPlayer>();
        Camera main = Camera.main;
        if (yaya == null || main == null)
            return null;
        if (photoDistance > 0f && Vector3.Distance(main.transform.position, yaya.transform.position) > photoDistance)
            return null;

        Transform head = MixamoHumanoidAvatarBuilder.FindBone(yaya.transform, "Head");
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(yaya.transform, "Hips");
        if (head == null)
            return null;

        Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(yaya.transform);
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
            forward = yaya.transform.forward;
        forward.Normalize();
        float torso = hips != null ? Vector3.Distance(hips.position, head.position) : 0.35f;
        Vector3 face = head.position + Vector3.up * (torso * 0.22f);

        GameObject camObject = new GameObject("YayaPhotoCamera");
        Camera cam = camObject.AddComponent<Camera>();
        cam.enabled = false;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView = 24f;
        cam.nearClipPlane = 0.05f;
        // Only what is right in front of the camera (Yaya), not the room behind her.
        float distance = torso * 1.9f;
        cam.farClipPlane = distance + torso * 0.9f;
        cam.transform.position = face + forward * distance + Vector3.up * (torso * 0.05f);
        cam.transform.LookAt(face);

        const int size = 256;
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = previous;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(camObject);

        // Round picture: everything outside the circle is see-through.
        Color32[] pixels = texture.GetPixels32();
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                int i = y * size + x;
                pixels[i].a = (byte)(Mathf.Clamp01(r - 1f - d) * 255f);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
