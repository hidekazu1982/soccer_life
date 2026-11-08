using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class AndroidApkBuild
{
    const string FlagFileName = "request-apk.build";
    const string StatusFileName = "apk-build-status.txt";

    static bool switchingTarget;
    static int settleFrames;

    static AndroidApkBuild()
    {
        EditorApplication.update += Tick;
    }

    public static void Build()
    {
        Directory.CreateDirectory(@"C:\gradle");
        Directory.CreateDirectory(@"C:\tmp");
        Environment.SetEnvironmentVariable("GRADLE_USER_HOME", @"C:\gradle");
        Environment.SetEnvironmentVariable("TEMP", @"C:\tmp");
        Environment.SetEnvironmentVariable("TMP", @"C:\tmp");

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.soccerclub.sim");
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.ForceInternal;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        PlayerSettings.Android.optimizedFramePacing = false;
        var versionFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "apk-version.txt"));
        var versionCode = 6;
        if (File.Exists(versionFile) && int.TryParse(File.ReadAllText(versionFile).Trim(), out var parsedVersion))
            versionCode = parsedVersion;
        PlayerSettings.Android.bundleVersionCode = versionCode;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        AssetDatabase.Refresh();
        PrepareBundledFont();
        UseAndroidTextureCompression();

        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "SoccerClubSim.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        WriteStatus("Building APK to " + output);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.Development // Debug.isDebugBuild
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Android build failed: " + report.summary.result + " errors=" + report.summary.totalErrors);

        WriteStatus("SUCCESS " + output);
        UnityEngine.Debug.Log("APK written to " + output);
    }

    static void PrepareBundledFont()
    {
        var importer = AssetImporter.GetAtPath("Assets/Resources/Fonts/MPLUSRounded1c-Regular.ttf") as TrueTypeFontImporter;
        if (importer == null)
            return;

        importer.includeFontData = true;
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.SaveAndReimport();
    }

    static void UseAndroidTextureCompression()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" });
        var changed = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            var settings = importer.GetPlatformTextureSettings("Android");
            if (settings.overridden && settings.format == TextureImporterFormat.ETC2_RGBA8 && settings.maxTextureSize <= 2048)
                continue;

            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = TextureImporterFormat.ETC2_RGBA8;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            changed++;
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            WriteStatus("Compressed Android textures: " + changed);
        }
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (!File.Exists(FlagPath()))
            return;

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            settleFrames = 10;
            WriteStatus("Exiting play mode");
            EditorApplication.isPlaying = false;
            return;
        }

        if (settleFrames > 0)
        {
            settleFrames--;
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (switchingTarget)
                return;

            switchingTarget = true;
            WriteStatus("Switching build target to Android");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                TryDeleteFlag();
                WriteStatus("FAIL could not switch build target to Android");
                EditorApplication.update -= Tick;
            }

            return;
        }

        EditorApplication.update -= Tick;
        TryDeleteFlag();

        try
        {
            Build();
        }
        catch (Exception exception)
        {
            WriteStatus("FAIL " + exception);
            Debug.LogException(exception);
        }
    }

    static string FlagPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", FlagFileName));
    }

    static void TryDeleteFlag()
    {
        try
        {
            var flag = FlagPath();
            if (File.Exists(flag))
                File.Delete(flag);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not delete APK build request: " + exception.Message);
        }
    }

    static void WriteStatus(string message)
    {
        try
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", StatusFileName));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not write APK build status: " + exception.Message);
        }

        Debug.Log(message);
    }
}
