using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlayerMana))]
public class PlayerManaEditor : Editor
{
    private float sliderValue;
    private bool initialized;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerMana mana = (PlayerMana)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Enter Play Mode to adjust mana with the slider.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Runtime Mana Controls", EditorStyles.boldLabel);

        if (!initialized)
        {
            sliderValue = mana.MaxMana;
            initialized = true;
        }

        EditorGUI.BeginChangeCheck();
        sliderValue = EditorGUILayout.Slider("Max Mana", sliderValue, 1f, 200f);
        if (EditorGUI.EndChangeCheck())
        {
            mana.SetMaxMana(sliderValue);
        }

        EditorGUILayout.LabelField("Current Mana",
            $"{Mathf.CeilToInt(mana.CurrentMana)} / {Mathf.CeilToInt(mana.MaxMana)}");

        Repaint();
    }
}
