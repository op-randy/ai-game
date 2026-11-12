using UnityEngine;

/// <summary>Small on-screen controls overlay for the third-person test level. F1 hides it.</summary>
public class ControlsHelp : MonoBehaviour
{
    bool show = true;
    GUIStyle style;

    void Update() { if (Input.GetKeyDown(KeyCode.F1)) show = !show; }

    void OnGUI()
    {
        if (!show) return;
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 15 };
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.Label(new Rect(20, Screen.height - 58, 1200, 24),
            "WASD walk   Shift run   Ctrl toggle crouch / C hold crouch   Space jump / climb   LMB punch, punch, kick", style);
        GUI.Label(new Rect(20, Screen.height - 34, 1200, 24),
            "V first / third person   Mouse look   Scroll zoom   Esc free mouse   F1 hide help", style);
    }
}
