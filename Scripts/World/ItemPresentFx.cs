using System.Collections.Generic;
using UnityEngine;

// Small "game reward" effects built at runtime (no assets needed):
//   - Attach(target): a soft glow + twinkling stars around something that is being handed
//     over or offered (ear thermometer, sticker). Follows the target until Stop().
//   - Burst(point): a quick flash + stars flying out, e.g. where a sticker lands.
//   - Rays(): a slowly turning sunburst behind a reward that just appeared.
// Everything faces the camera.
public class ItemPresentFx : MonoBehaviour
{
    private Transform target;
    private Vector3 targetOffset;
    private float size;
    private Color color;
    private float startTime;
    private float stopTime = -1f;
    private SpriteRenderer glow;
    private SpriteRenderer rays;
    private readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();
    private const float FadeOut = 0.25f;

    public static readonly Color Gold = new Color(1f, 0.86f, 0.40f, 1f);

    // Glow + stars around `target` (size = about the size of the object, m).
    public static ItemPresentFx Attach(Transform target, float size, Color color, bool withRays = false, int sortingOrder = 25)
    {
        GameObject go = new GameObject("ItemPresentFx");
        ItemPresentFx fx = go.AddComponent<ItemPresentFx>();
        fx.target = target;
        fx.size = Mathf.Max(0.02f, size);
        fx.color = color;
        fx.startTime = Time.time;

        if (withRays)
        {
            fx.rays = NewSprite(go.transform, "Rays", RaysSprite(), sortingOrder - 2);
        }
        fx.glow = NewSprite(go.transform, "Glow", GlowSprite(), sortingOrder - 1);
        for (int i = 0; i < 6; i++)
            fx.stars.Add(NewSprite(go.transform, "Star" + i, StarSprite(), sortingOrder + 1));
        fx.LateUpdate();
        return fx;
    }

    // The effect stays at this offset from the target (world space, m).
    public void SetOffset(Vector3 offset)
    {
        targetOffset = offset;
    }

    public void Stop()
    {
        if (stopTime < 0f)
            stopTime = Time.time;
    }

    private void LateUpdate()
    {
        if (target == null && stopTime < 0f)
            Stop();

        Camera cam = Camera.main;
        float t = Time.time - startTime;
        float fadeIn = Mathf.Clamp01(t / 0.25f);
        float fade = stopTime >= 0f ? 1f - Mathf.Clamp01((Time.time - stopTime) / FadeOut) : 1f;
        if (fade <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        float a = fadeIn * fade;

        if (target != null)
            transform.position = target.position + targetOffset;
        if (cam != null)
            transform.rotation = cam.transform.rotation;
        // Drawn just behind the object (seen from the camera).
        Vector3 back = cam != null ? cam.transform.forward * (size * 0.15f) : Vector3.zero;

        if (glow != null)
        {
            float pulse = 1f + 0.08f * Mathf.Sin(t * 5f);
            glow.transform.position = transform.position + back;
            glow.transform.localScale = Vector3.one * size * 2.4f * pulse;
            Color c = color;
            c.a = 0.55f * a;
            glow.color = c;
        }

        if (rays != null)
        {
            rays.transform.position = transform.position + back * 1.2f;
            rays.transform.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
            float pop = Mathf.Clamp01(t / 0.35f);
            rays.transform.localScale = Vector3.one * size * 3.2f * Mathf.SmoothStep(0.3f, 1f, pop);
            Color c = color;
            c.a = 0.45f * a;
            rays.color = c;
        }

        for (int i = 0; i < stars.Count; i++)
        {
            SpriteRenderer s = stars[i];
            float phase = i / (float)stars.Count;
            float angle = (phase + t * 0.12f) * Mathf.PI * 2f;
            float radius = size * (0.85f + 0.12f * Mathf.Sin(t * 2f + i));
            s.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            float twinkle = Mathf.Clamp01(Mathf.Sin(t * 4.5f + i * 1.7f) * 0.5f + 0.5f);
            s.transform.localScale = Vector3.one * size * Mathf.Lerp(0.12f, 0.32f, twinkle);
            s.transform.localRotation = Quaternion.Euler(0f, 0f, t * 90f + i * 30f);
            Color c = Color.Lerp(Color.white, color, 0.35f);
            c.a = Mathf.Lerp(0.35f, 1f, twinkle) * a;
            s.color = c;
        }
    }

    // A quick flash and a ring of stars flying out (for 0.6 s).
    public static void Burst(Vector3 point, float size, Color color, int sortingOrder = 26)
    {
        GameObject go = new GameObject("ItemPresentFx_Burst");
        go.transform.position = point;
        BurstFx b = go.AddComponent<BurstFx>();
        b.Setup(size, color, sortingOrder);
    }

    private class BurstFx : MonoBehaviour
    {
        private float size;
        private Color color;
        private float start;
        private SpriteRenderer flash;
        private readonly List<SpriteRenderer> sparks = new List<SpriteRenderer>();
        private readonly List<Vector2> dirs = new List<Vector2>();

        public void Setup(float s, Color c, int order)
        {
            size = Mathf.Max(0.02f, s);
            color = c;
            start = Time.time;
            flash = NewSprite(transform, "Flash", GlowSprite(), order);
            for (int i = 0; i < 10; i++)
            {
                sparks.Add(NewSprite(transform, "Spark" + i, StarSprite(), order + 1));
                float ang = (i / 10f + Random.Range(-0.03f, 0.03f)) * Mathf.PI * 2f;
                dirs.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(0.8f, 1.2f));
            }
            LateUpdate();
        }

        private void LateUpdate()
        {
            float u = (Time.time - start) / 0.6f;
            if (u >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            float ease = 1f - (1f - u) * (1f - u);
            Color fc = Color.Lerp(Color.white, color, 0.4f);
            fc.a = (1f - u) * 0.8f;
            flash.color = fc;
            flash.transform.localScale = Vector3.one * size * Mathf.Lerp(0.8f, 2.6f, ease);

            for (int i = 0; i < sparks.Count; i++)
            {
                SpriteRenderer s = sparks[i];
                Vector2 d = dirs[i] * size * Mathf.Lerp(0.2f, 1.5f, ease);
                s.transform.localPosition = new Vector3(d.x, d.y, 0f);
                s.transform.localScale = Vector3.one * size * 0.28f * (1f - u * 0.6f);
                s.transform.localRotation = Quaternion.Euler(0f, 0f, u * 180f + i * 36f);
                Color c = Color.Lerp(Color.white, color, 0.3f);
                c.a = 1f - u * u;
                s.color = c;
            }
        }
    }

    private static SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    // ---- procedural sprites (1 unit wide) ----
    private static Sprite glowSprite, starSprite, raysSprite;

    public static Sprite GlowSprite()
    {
        if (glowSprite == null)
            glowSprite = Paint(64, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float k = Mathf.Clamp01(1f - d);
                return k * k;
            });
        return glowSprite;
    }

    public static Sprite StarSprite()
    {
        if (starSprite == null)
            starSprite = Paint(64, (x, y) =>
            {
                // Four-point sparkle: two thin diamonds + a soft core.
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                float cross = Mathf.Clamp01(1f - (ax * 6f + ay) ) + Mathf.Clamp01(1f - (ay * 6f + ax));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 3f);
                return Mathf.Clamp01(cross + core);
            });
        return starSprite;
    }

    public static Sprite RaysSprite()
    {
        if (raysSprite == null)
            raysSprite = Paint(128, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > 1f)
                    return 0f;
                float ang = Mathf.Atan2(y, x);
                float ray = Mathf.Clamp01(Mathf.Cos(ang * 12f) * 1.6f - 0.4f);
                float falloff = Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 4f);
                return ray * falloff;
            });
        return raysSprite;
    }

    private static Sprite Paint(int n, System.Func<float, float, float> alpha)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        for (int iy = 0; iy < n; iy++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = (ix + 0.5f) / n * 2f - 1f;
                float y = (iy + 0.5f) / n * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(alpha(x, y)) * 255f);
                px[iy * n + ix] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
