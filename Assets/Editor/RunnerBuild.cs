using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunnerBuild
{
    [MenuItem("Metro Dash/Create or reset game scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Resources");
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/RunnerBase.mat") == null)
        {
            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, "Assets/Resources/RunnerBase.mat");
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Metro Dash Game").AddComponent<MetroDash.MetroDashGame>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MetroDash.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MetroDash.unity", true) };
        PlayerSettings.companyName = "Metro Dash Studio";
        PlayerSettings.productName = "Metro Dash";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input = settings.FindProperty("activeInputHandler");
        if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
        QualitySettings.SetQualityLevel(2);
        AssetDatabase.SaveAssets();
        Debug.Log("METRO_DASH_SCENE_READY");
    }

    [MenuItem("Metro Dash/Build Windows game")]
    public static void BuildWindows()
    {
        CreateScene();
        string location = Path.GetFullPath("../MetroDash-Windows/MetroDash.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(location));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/MetroDash.unity" },
            locationPathName = location,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        Debug.Log("METRO_DASH_BUILD_OK " + location);
    }
}

