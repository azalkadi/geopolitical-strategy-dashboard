using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// UI Toolkit's Advanced Text Generation system needs ICU (Unicode) data embedded into a real
// PanelSettings *asset* at import time — a PanelSettings created purely at runtime via
// ScriptableObject.CreateInstance never gets this data, and every Label then throws a
// NullReferenceException deep in the text shaper (UITKTextHandle.ShapeText) on every frame.
// This creates a real project asset once, under Resources/ so it's always bundled into any
// build and loadable at runtime via Resources.Load — see GameUIRoot.Awake().
public static class EnsurePanelSettingsAsset
{
    const string Dir = "Assets/Resources";
    const string Path = "Assets/Resources/GamePanelSettings.asset";

    public static void Ensure()
    {
        if (AssetDatabase.LoadAssetAtPath<PanelSettings>(Path) != null) return;

        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        var textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();

        AssetDatabase.CreateAsset(settings, Path);
        AssetDatabase.AddObjectToAsset(textSettings, settings);
        settings.textSettings = textSettings;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[headlessbuild] created PanelSettings asset at {Path}");
    }
}
