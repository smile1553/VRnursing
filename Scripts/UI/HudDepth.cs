using UnityEngine;

// One shared distance for everything that floats in front of the eyes (prompt box, dialogue
// box, medical-record button, sticker, emotion bar, timer, cards...).
//
// Each of those used to sit at its own distance (0.7 m to 1.4 m). On a flat screen that does
// not show, but in the headset the two eyes see them at different depths and the display looks
// uneven. Flatten() moves a spot along the line from the eye to it until it is at the shared
// distance; the returned factor is how much the thing must be scaled so that it still looks the
// same size and stays in the same place in the view.
public static class HudDepth
{
    // Metres in front of the eyes. Change this one number to move the whole display.
    // 0.95 = where the medical record is held (close enough not to go into walls); the record
    // itself also uses this number, so everything is on the same plane.
    public static float Distance = 0.95f;
    public static bool Enabled = true;

    // offset = x right, y up, z forward (camera space).
    public static Vector3 Flatten(Vector3 offset, out float factor)
    {
        factor = 1f;
        if (!Enabled || offset.z < 0.05f || Distance < 0.05f)
            return offset;
        factor = Distance / offset.z;
        return offset * factor;
    }
}
