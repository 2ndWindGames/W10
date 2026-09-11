using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenedNote;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenedBuild
{
    public const string ScenePath = "Assets/OpenedNote/Scenes/OpenedNote.unity";
    const string CommandPath = "BuildArtifacts/editor-command.txt";
    static bool busy;
    static double nextPoll;
    static OpenedBuild() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.7;
        if (!File.Exists(CommandPath)) return;
        var command = File.ReadAllText(CommandPath).Trim(); File.Delete(CommandPath); busy = true;
        try
        {
            if (command == "setup") Setup();
            else if (command == "test") OpenedChecks.Run();
            else if (command == "export") ExportUnsigned();
            else if (command == "apk") BuildTestApk();
            else if (command == "play") { Setup(); SetGameSize(390, 844); EditorApplication.isPlaying = true; }
            else if (command == "qaplay") { Setup(); File.WriteAllText("BuildArtifacts/qa-mode.flag", "Isolated QA data"); SetGameSize(390, 844); EditorApplication.isPlaying = true; }
            else if (command == "stop") { EditorApplication.isPlaying = false; if (File.Exists("BuildArtifacts/qa-mode.flag")) File.Delete("BuildArtifacts/qa-mode.flag"); }
            else if (command == "qa") UnityEngine.Object.FindFirstObjectByType<OpenedApp>().gameObject.AddComponent<OpenedVisualQA>();
            else if (command.StartsWith("size:")) { var p = command.Substring(5).Split('x'); SetGameSize(int.Parse(p[0]), int.Parse(p[1])); }
            else if (command == "capture") ScreenCapture.CaptureScreenshot("BuildArtifacts/QA/editor.png");
            else if (command == "logs") DumpLogs();
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "quit") EditorApplication.Exit(0);
            else throw new ArgumentException("Unknown command: " + command);
            File.WriteAllText("BuildArtifacts/editor-result.txt", command + " OK " + DateTime.UtcNow.ToString("o"));
        }
        catch (Exception e) { File.WriteAllText("BuildArtifacts/editor-result.txt", command + " FAILED\n" + e); Debug.LogException(e); }
        finally { busy = false; }
    }
    [MenuItem("OpenedNote/1. Configure Android")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory("Assets/OpenedNote/Scenes");
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("UI Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white; camera.cullingMask = 0; camera.orthographic = true;
            new GameObject("OpenedNote", typeof(OpenedApp));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        PlayerSettings.companyName = "SecondWindGames"; PlayerSettings.productName = "개봉노트";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.secondwindgames.openednote");
        PlayerSettings.bundleVersion = "1.0.0"; PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.defaultScreenWidth = 390; PlayerSettings.defaultScreenHeight = 844;
        PlayerSettings.runInBackground = false;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.renderOutsideSafeArea = true;
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.ForceInternal;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.colorSpace = ColorSpace.Gamma; QualitySettings.vSyncCount = 0;
        MakeIcon();
        var logoPath = "Assets/OpenedNote/Branding/SecondWindGamesLogo.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(logoPath);
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport(); }
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);
        PlayerSettings.SplashScreen.show = true; PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = Color.white; PlayerSettings.SplashScreen.background = null; PlayerSettings.SplashScreen.backgroundPortrait = null;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2, logo) };
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        Set(settings, "androidApplicationEntry", 1); Set(settings, "AndroidIsGame", false); Set(settings, "forceInternetPermission", false); Set(settings, "allowBackup", false);
        Set(settings, "androidStartInFullscreen", false); Set(settings, "androidResizeableActivity", true);
        Set(settings, "useCustomMainManifest", true); Set(settings, "androidAppCategory", 7);
        settings.ApplyModifiedPropertiesWithoutUndo();
        var connect = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/UnityConnectSettings.asset")[0]);
        Set(connect, "InsightsSettings.m_EngineDiagnosticsEnabled", false); Set(connect, "UnityAnalyticsSettings.m_InitializeOnStartup", false);
        Set(connect, "CrashReportingSettings.m_Enabled", false); connect.ApplyModifiedPropertiesWithoutUndo();
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
    }
    static void Set(SerializedObject obj, string name, bool value) { var p = obj.FindProperty(name); if (p != null) p.boolValue = value; }
    static void Set(SerializedObject obj, string name, int value) { var p = obj.FindProperty(name); if (p != null) p.intValue = value; }
    static void MakeIcon()
    {
        const string path = "Assets/OpenedNote/Branding/OpenedNoteIcon.png";
        if (!File.Exists(path)) throw new FileNotFoundException("Generate the OpenedNote branding with Tools/create-store-assets.cjs first.", path);
        foreach (var texturePath in new[] { path, "Assets/OpenedNote/Branding/AdaptiveForeground.png", "Assets/OpenedNote/Branding/AdaptiveBackground.png" })
        {
            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null && textureImporter.textureType != TextureImporterType.Default)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                textureImporter.alphaIsTransparency = true;
                textureImporter.SaveAndReimport();
            }
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OpenedNote/Branding/AdaptiveForeground.png");
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OpenedNote/Branding/AdaptiveBackground.png");
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            if (kind == UnityEditor.Android.AndroidPlatformIconKind.Adaptive) continue;
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var slot in slots) slot.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
        }
        var adaptiveKind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
        var adaptiveIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, adaptiveKind);
        foreach (var slot in adaptiveIcons) slot.SetTextures(new[] { background, foreground });
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, adaptiveKind, adaptiveIcons);
    }
    [MenuItem("OpenedNote/2. Run Data Checks")]
    public static void Check() => OpenedChecks.Run();
    [MenuItem("OpenedNote/3. Export Android (No Upload Signing)")]
    public static void ExportUnsigned()
    {
        Setup(); OpenedChecks.Run();
        // No publisher keystore is read or created. Gradle finalization removes the
        // automatically generated debug signing configuration from release builds.
        PlayerSettings.Android.useCustomKeystore = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        EditorUserBuildSettings.buildAppBundle = true;
        Build("Builds/Android/Gradle", BuildOptions.AcceptExternalModificationsToPlayer, "export");
    }
    [MenuItem("OpenedNote/Build QA APK (Unity Default Test Key)")]
    public static void BuildTestApk()
    {
        Setup(); PlayerSettings.Android.useCustomKeystore = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false; EditorUserBuildSettings.buildAppBundle = false;
        Build("Builds/Android/OpenedNote-1.0.0-qa.apk", BuildOptions.None, "qa-apk");
    }
    static void Build(string path, BuildOptions options, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.Android, locationPathName = path, options = options });
        Directory.CreateDirectory("BuildArtifacts/QA");
        File.WriteAllText("BuildArtifacts/QA/build-"+name+".txt", report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime);
        if(report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed: "+report.summary.result);
    }
    public static void SetGameSize(int width, int height)
    {
        var a = typeof(Editor).Assembly; var t = a.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(t);
        var instance = singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var groupType = a.GetType("UnityEditor.GameViewSizeGroupType");
        var group = t.GetMethod("GetGroup").Invoke(instance,new[]{Enum.Parse(groupType,"Android")});
        var size = Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),new[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),(object)width,height,"OpenedNote "+width+"x"+height});
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var type=a.GetType("UnityEditor.GameView"); var view=EditorWindow.GetWindow(type);
        type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,count-1);
        view.Show(); view.Repaint();
    }
    static void DumpLogs()
    {
        var type=typeof(Editor).Assembly.GetType("UnityEditor.LogEntries"); var entryType=typeof(Editor).Assembly.GetType("UnityEditor.LogEntry");
        var entry=Activator.CreateInstance(entryType);int count=(int)type.GetMethod("GetCount").Invoke(null,null);
        var output=new System.Text.StringBuilder();type.GetMethod("StartGettingEntries").Invoke(null,null);
        try { for(int i=Math.Max(0,count-70);i<count;i++){type.GetMethod("GetEntryInternal").Invoke(null,new[]{(object)i,entry});output.AppendLine((string)entryType.GetField("message").GetValue(entry));} }
        finally { type.GetMethod("EndGettingEntries").Invoke(null,null); }
        File.WriteAllText("BuildArtifacts/QA/editor-console.txt",output.ToString());
    }
}

public sealed class OpenedAssetImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/OpenedNote/Branding/")) return;
        var t=(TextureImporter)assetImporter; t.mipmapEnabled=false; t.textureCompression=TextureImporterCompression.Uncompressed;
        t.maxTextureSize=1024; t.npotScale=TextureImporterNPOTScale.None;
        if (!assetPath.EndsWith("SecondWindGamesLogo.png")) t.textureType = TextureImporterType.Default;
        t.alphaIsTransparency = true;
    }
}
