using UnityEngine;

/// <summary>
/// Crosshair (hip fire only, widens with spread), hit marker, pick-up prompt, hit counter and controls.
/// Also handles looking at a WeaponPickup and pressing E.
/// </summary>
public class RangeHUD : MonoBehaviour
{
    public RifleController rifle;
    public Camera mainCamera;
    public float interactDistance = 2.6f;

    WeaponPickup lookingAt;
    GUIStyle big, small, tiny;
    bool showHelp = true;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) showHelp = !showHelp;
        lookingAt = null;
        if (rifle != null && !rifle.Equipped && mainCamera != null)
        {
            var ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~(1 << 2), QueryTriggerInteraction.Collide))
                lookingAt = hit.collider.GetComponentInParent<WeaponPickup>();
            if (lookingAt != null && Input.GetKeyDown(KeyCode.E)) { lookingAt.PickUp(rifle); lookingAt = null; }
        }
    }

    void OnGUI()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            tiny = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperRight };
        }
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        var white = Texture2D.whiteTexture;

        if (rifle != null && rifle.Equipped)
        {
            // crosshair fades out while aiming down sights
            float a = 1f - Mathf.Clamp01(rifle.Aim * 2.5f);
            if (a > 0.01f && mainCamera != null)
            {
                float gap = 6f + Mathf.Tan(rifle.CurrentSpread * Mathf.Deg2Rad) * Screen.height / (2f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
                GUI.color = new Color(1f, 1f, 1f, 0.85f * a);
                GUI.DrawTexture(new Rect(cx - gap - 10, cy - 1, 10, 2), white);
                GUI.DrawTexture(new Rect(cx + gap, cy - 1, 10, 2), white);
                GUI.DrawTexture(new Rect(cx - 1, cy - gap - 10, 2, 10), white);
                GUI.DrawTexture(new Rect(cx - 1, cy + gap, 2, 10), white);
                GUI.DrawTexture(new Rect(cx - 1, cy - 1, 2, 2), white);
            }
            // hit marker
            if (rifle.HitMarker > 0f)
            {
                GUI.color = new Color(1f, 0.25f, 0.2f, rifle.HitMarker);
                var m = GUI.matrix;
                for (int i = 0; i < 4; i++)
                {
                    GUIUtility.RotateAroundPivot(45f + 90f * i, new Vector2(cx, cy));
                    GUI.DrawTexture(new Rect(cx + 8, cy - 1.5f, 9, 3), white);
                    GUI.matrix = m;
                }
            }
            GUI.color = Color.white;
            int acc = rifle.Shots > 0 ? Mathf.RoundToInt(100f * rifle.Hits / rifle.Shots) : 0;
            GUI.Label(new Rect(20, 16, 400, 24), $"Target hits: {rifle.Hits}    Shots: {rifle.Shots}    Accuracy: {acc}%", small);
            if (showHelp)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(Screen.width - 460, 16, 440, 170), "LMB  shoot      RMB  aim\nG  throw weapon\nWASD  move    Shift  sprint\nCtrl  crouch toggle / slide\nC  hold crouch    Space  jump\nEsc  free mouse    F1  hide help", tiny);
                GUI.color = Color.white;
            }
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), white);
            GUI.color = Color.white;
            if (lookingAt != null)
                GUI.Label(new Rect(cx - 300, cy + 40, 600, 34), $"Press E to pick up {lookingAt.displayName}", big);
            else
                GUI.Label(new Rect(20, Screen.height - 34, 900, 24), "Walk to the desk and pick up the rifle (E)   WASD move   Esc free mouse", small);
        }
    }
}
