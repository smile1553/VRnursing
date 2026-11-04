using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Characters missing from the project's TextMeshPro font (shown as empty squares) are taken
// from a Chinese font installed on the device instead. Created automatically; does nothing
// if no suitable system font is found.
public static class TmpGlyphFallback
{
    private static readonly string[] Preferred =
    {
        "msjh", "microsoftjhenghei", "notosanscjk", "notosanstc", "notosanshant", "pingfang",
        "mingliu", "simhei", "msyh", "droidsansfallback", "sourcehansans"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        try
        {
            string best = null;
            int bestRank = int.MaxValue;
            foreach (string path in Font.GetPathsToOSFonts())
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace("-", string.Empty).Replace(" ", string.Empty);
                for (int i = 0; i < Preferred.Length; i++)
                {
                    // Prefer the regular weight (shortest name) of the best family.
                    int rank = i * 100 + Mathf.Min(99, name.Length);
                    if (name.Contains(Preferred[i]) && rank < bestRank)
                    {
                        bestRank = rank;
                        best = path;
                    }
                }
            }

            if (best == null)
            {
                Debug.Log("[TmpGlyphFallback] No Chinese system font found: missing characters stay as squares.");
                return;
            }

            Font font = new Font(best);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
            if (asset == null)
            {
                Debug.LogWarning("[TmpGlyphFallback] Could not use the system font " + best);
                return;
            }

            asset.name = "SystemChineseFallback";

            // Make the borrowed characters the same size as the project's own font: compare a
            // character both fonts have and scale the system font to match.
            const string probe = "\u7684";   // a very common character
            if (asset.TryAddCharacters(probe) && asset.characterLookupTable.TryGetValue(probe[0], out TMP_Character mine)
                && mine.glyph != null && mine.glyph.metrics.height > 0f && asset.faceInfo.pointSize > 0)
            {
                float mySize = mine.glyph.metrics.height / asset.faceInfo.pointSize;
                List<float> ratios = new List<float>();
                foreach (TMP_FontAsset existing in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (existing == null || existing == asset || existing.characterLookupTable == null || existing.faceInfo.pointSize <= 0)
                        continue;
                    if (existing.characterLookupTable.TryGetValue(probe[0], out TMP_Character theirs) && theirs.glyph != null && theirs.glyph.metrics.height > 0f)
                        ratios.Add(theirs.glyph.metrics.height / existing.faceInfo.pointSize * (existing.faceInfo.scale > 0f ? existing.faceInfo.scale : 1f) / mySize);
                }
                if (ratios.Count > 0)
                {
                    ratios.Sort();
                    float match = Mathf.Clamp(ratios[ratios.Count / 2], 0.5f, 1.5f);
                    UnityEngine.TextCore.FaceInfo face = asset.faceInfo;
                    face.scale = match;
                    asset.faceInfo = face;
                    Debug.Log($"[TmpGlyphFallback] System font scaled x{match:0.00} to match the project's font.");
                }
            }
            List<List<TMP_FontAsset>> tables = new List<List<TMP_FontAsset>>();
            if (TMP_Settings.fallbackFontAssets != null)
                tables.Add(TMP_Settings.fallbackFontAssets);
            foreach (TMP_FontAsset existing in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (existing == null || existing == asset)
                    continue;
                if (existing.fallbackFontAssetTable == null)
                    existing.fallbackFontAssetTable = new List<TMP_FontAsset>();
                tables.Add(existing.fallbackFontAssetTable);
            }
            foreach (List<TMP_FontAsset> table in tables)
                table.Add(asset);
            // Leave the project's font settings as they were when play stops.
            Application.quitting += () =>
            {
                foreach (List<TMP_FontAsset> table in tables)
                    table.Remove(asset);
            };

            // Texts already on screen pick the new fallback up when they are redrawn.
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true))
                text.ForceMeshUpdate(false, true);
            Debug.Log("[TmpGlyphFallback] Missing characters now come from the system font: " + best);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TmpGlyphFallback] Not available here: " + e.Message);
        }
    }
}
