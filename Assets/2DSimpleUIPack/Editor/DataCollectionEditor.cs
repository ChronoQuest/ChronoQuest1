using UnityEditor;
using UnityEngine;

public static class DataCollectionEditor
{
    [MenuItem("Tools/Data Collection/Open data folder")]
    public static void OpenDataFolder()
    {
        string path = Application.persistentDataPath;
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/Data Collection/Log data folder path")]
    public static void LogDataFolderPath()
    {
        string path = Application.persistentDataPath;
        string filePath = System.IO.Path.Combine(path, "session_data.jsonl");
        Debug.Log($"[DataCollection] Folder: {path}\nFile: {filePath}");
    }
}
