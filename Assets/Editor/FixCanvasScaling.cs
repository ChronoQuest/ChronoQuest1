using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click fix: switches every CanvasScaler in every prefab and every scene
/// from Constant Pixel Size to Scale With Screen Size (1920x1080, match 0.5).
/// Run via  Tools > Fix All Canvas Scalers.
/// </summary>
public static class FixCanvasScaling
{
    private const float RefWidth  = 1920f;
    private const float RefHeight = 1080f;
    private const float Match     = 0.5f;

    [MenuItem("Tools/Fix All Canvas Scalers")]
    public static void FixAll()
    {
        int fixedCount = 0;

        // ── 1. Fix prefabs ───────────────────────────────────────────────
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip third-party assets
            if (path.Contains("TextMesh Pro") || path.Contains("2DSimpleUIPack"))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            CanvasScaler[] scalers = prefab.GetComponentsInChildren<CanvasScaler>(true);
            if (scalers.Length == 0) continue;

            bool modified = false;
            foreach (CanvasScaler scaler in scalers)
            {
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                {
                    Apply(scaler);
                    EditorUtility.SetDirty(scaler);
                    modified = true;
                    fixedCount++;
                    Debug.Log($"[FixCanvasScaling] Prefab: {path} — {scaler.gameObject.name}");
                }
            }

            if (modified)
                EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();

        // ── 2. Fix every scene in Assets/Scenes ──────────────────────────
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            bool modified = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                {
                    if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                    {
                        Apply(scaler);
                        EditorUtility.SetDirty(scaler);
                        modified = true;
                        fixedCount++;
                        Debug.Log($"[FixCanvasScaling] Scene: {path} — {scaler.gameObject.name}");
                    }
                }
            }

            if (modified)
                EditorSceneManager.SaveScene(scene);

            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log($"[FixCanvasScaling] Done — fixed {fixedCount} CanvasScaler(s).");
        EditorUtility.DisplayDialog("Fix Canvas Scalers",
            $"Done!\n\nFixed {fixedCount} CanvasScaler(s) across prefabs and scenes.\n\n" +
            "All set to Scale With Screen Size (1920x1080, match 0.5).",
            "OK");
    }

    private static void Apply(CanvasScaler scaler)
    {
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = Match;
    }
}
