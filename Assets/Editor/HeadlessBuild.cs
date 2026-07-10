using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Batchmode entry points for the automated build/debug routine (Tools/build.ps1). Invoked as:
//   Unity.exe -batchmode -nographics -quit -projectPath <proj> -logFile <log>
//            -executeMethod HeadlessBuild.CompileCheck   (fast: just verify scripts compile)
//   ... -executeMethod HeadlessBuild.BuildWindows        (full: build a Windows player)
//
// If any script fails to compile, Unity can't even run these methods — it logs the compiler
// errors and the runner detects them from the log. So CompileCheck succeeding == clean build.
public static class HeadlessBuild
{
    // Fast path: opening the project in batchmode already compiled every script. If we got
    // here at all, compilation succeeded. Exit 0 so the runner sees success.
    public static void CompileCheck()
    {
        Debug.Log("[headlessbuild] OK: all scripts compiled");
        EditorApplication.Exit(0);
    }

    // Full path: build a StandaloneWindows64 player. Creates an empty default scene if the
    // project has none (the map spawns itself via Bootstrap's RuntimeInitializeOnLoadMethod,
    // so an empty scene is all a runnable build needs right now).
    public static void BuildWindows()
    {
        // Custom shaders reached only via Shader.Find (not referenced by any scene/material
        // asset) are STRIPPED from standalone builds — so Shader.Find returns null in the
        // player and `new Material(null)` throws at runtime (compiles fine, crashes on run).
        // Force-include the map's shader in the build.
        EnsureShaderAlwaysIncluded("Meridian/FlatVertexColor");
        EnsureShaderAlwaysIncluded("Meridian/ScreenDot");
        EnsureShaderAlwaysIncluded("Meridian/UnlitTexture");
        EnsurePanelSettingsAsset.Ensure();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Directory.CreateDirectory("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Main.unity";
            if (!File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            scenes = new[] { scenePath };
        }

        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build", "Windows");
        Directory.CreateDirectory(outDir);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outDir, "Meridian.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"[headlessbuild] result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} sizeBytes={s.totalSize} time={s.totalTime}");

        EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    // Adds a shader to Project Settings > Graphics > "Always Included Shaders" so it survives
    // the build's shader stripping and Shader.Find can resolve it at runtime in the player.
    static void EnsureShaderAlwaysIncluded(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[headlessbuild] shader '{shaderName}' not found; cannot force-include it");
            return;
        }

        var obj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
        if (obj == null)
        {
            Debug.LogWarning("[headlessbuild] could not load GraphicsSettings.asset");
            return;
        }

        var so = new SerializedObject(obj);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                return; // already included

        int idx = arr.arraySize;
        arr.InsertArrayElementAtIndex(idx);
        arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[headlessbuild] added '{shaderName}' to Always Included Shaders");
    }
}
