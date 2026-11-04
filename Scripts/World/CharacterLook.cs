using System.Collections.Generic;
using UnityEngine;

// Makes the characters (Yaya, Mom and the performance copies of them) look softer and less dark:
//   1) a soft "portrait" light that only lights the characters, coming from where the nurse is
//      looking, so faces are evenly lit instead of dark with black eye sockets;
//   2) matte skin / clothes instead of the default shiny plastic look.
// The room itself is not affected. Created automatically; add the component to an object
// yourself (the automatic one is then skipped) to tune the values in the Inspector.
public class CharacterLook : MonoBehaviour
{
    [Header("Portrait light (characters only)")]
    [Tooltip("Brightness of the light on the characters. 0 = off.")]
    [SerializeField] private float lightIntensity = 0.9f;
    [SerializeField] private Color lightColor = new Color(1f, 0.96f, 0.9f);
    [Tooltip("How much the light comes from above (degrees).")]
    [SerializeField] private float lightFromAbove = 20f;
    [Tooltip("Unused layer number (8-31) the character meshes are moved to, so the light can pick only them.")]
    [SerializeField] private int characterLayer = 30;

    [Header("Softer materials")]
    [SerializeField] private bool softenMaterials = true;
    [Tooltip("Maximum smoothness of character materials (lower = more matte).")]
    [SerializeField] private float maxSmoothness = 0.15f;

    [Header("Eyelashes")]
    [Tooltip("Mesh / material names (separated by |) that are see-through cards, e.g. Yaya's eyelashes.")]
    [SerializeField] private string transparentObjectNames = "Material021";
    [SerializeField] private Color lashColor = new Color(0.14f, 0.09f, 0.07f, 1f);

    private static bool NameMatches(string name, string list)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(list))
            return false;
        foreach (string raw in list.Split('|'))
        {
            if (raw.Trim().Length > 0 && name.Contains(raw.Trim()))
                return true;
        }
        return false;
    }

    private Light portraitLight;
    private readonly HashSet<int> prepared = new HashSet<int>();
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<CharacterLook>(true) != null)
            return;
        new GameObject("CharacterLook (auto)").AddComponent<CharacterLook>();
    }

    private void Start()
    {
        characterLayer = Mathf.Clamp(characterLayer, 8, 31);
        GameObject lightObject = new GameObject("Character Portrait Light");
        lightObject.transform.SetParent(transform, false);
        portraitLight = lightObject.AddComponent<Light>();
        portraitLight.type = LightType.Directional;
        portraitLight.shadows = LightShadows.None;
        portraitLight.renderMode = LightRenderMode.ForcePixel;
        portraitLight.cullingMask = 1 << characterLayer;
        ApplyLightSettings();
    }

    private void ApplyLightSettings()
    {
        if (portraitLight == null)
            return;
        portraitLight.intensity = lightIntensity;
        portraitLight.color = lightColor;
        portraitLight.enabled = lightIntensity > 0.001f;
    }

    private void OnValidate()
    {
        ApplyLightSettings();
    }

    private void LateUpdate()
    {
        // Characters can appear later (performance prefabs), so look for new ones now and then.
        if (Time.unscaledTime >= nextScanTime)
        {
            // Hidden ones too (true): a performance copy is then already prepared when it is
            // switched on, instead of showing up dark until the next scan.
            nextScanTime = Time.unscaledTime + 0.1f;
            foreach (SkinnedMeshRenderer renderer in FindObjectsOfType<SkinnedMeshRenderer>(true))
                Prepare(renderer);
        }

        // Light follows the nurse's view (turning smoothly), tilted to come from a bit above.
        Camera cam = Camera.main;
        if (cam == null || portraitLight == null)
            return;

        Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (flat.sqrMagnitude < 1e-4f)
            return;

        Quaternion wanted = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(lightFromAbove, 0f, 0f);
        Transform t = portraitLight.transform;
        t.rotation = Quaternion.Slerp(t.rotation, wanted, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
    }

    private void Prepare(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || !prepared.Add(renderer.GetInstanceID()))
            return;

        // Only the mesh object changes layer (no colliders live there), so clicking / physics stay as they are.
        if (renderer.GetComponent<Collider>() == null)
            renderer.gameObject.layer = characterLayer;

        // Eyelash cards: their texture is see-through, but the material inside the model file is
        // opaque (grey cards). Draw them see-through and dark.
        bool lashObject = NameMatches(renderer.name, transparentObjectNames);
        foreach (Material material in renderer.materials)
        {
            if (material != null && (lashObject || NameMatches(material.name, transparentObjectNames)) && material.HasProperty("_Mode"))
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Mode", 2f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                material.color = lashColor;
            }
        }

        if (!softenMaterials)
            return;

        // .materials = per-renderer copies, so the material files in the project are not changed.
        foreach (Material material in renderer.materials)
        {
            if (material == null)
                continue;
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", Mathf.Min(material.GetFloat("_Glossiness"), maxSmoothness));
            if (material.HasProperty("_GlossMapScale"))
                material.SetFloat("_GlossMapScale", Mathf.Min(material.GetFloat("_GlossMapScale"), maxSmoothness));
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
        }
    }
}
