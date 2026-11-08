#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Game ビューを 1920×1080 の横画面にする。スクリプト更新で縦の端末サイズに戻さない。
/// </summary>
[InitializeOnLoad]
static class GameViewResolution
{
    const int Width = 1920;
    const int Height = 1080;
    const int RetryFrames = 180;

    static int _remaining = RetryFrames;
    static bool _warned;

    static GameViewResolution()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += _ =>
        {
            _remaining = 30;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        };
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (IsTarget())
        {
            EditorApplication.update -= Tick;
            return;
        }

        Apply();
        if (--_remaining > 0) return;
        EditorApplication.update -= Tick;
        if (_warned || IsTarget()) return;
        _warned = true;
        Debug.LogWarning("Gameビューを 1920x1080 の横画面に切り替えられませんでした。");
    }

    static bool IsTarget()
    {
        try
        {
            PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
            return width == Width && height == Height;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void Apply()
    {
        try
        {
            PlayModeWindow.SetCustomRenderingResolution((uint)Width, (uint)Height, "1920x1080");
        }
        catch (Exception)
        {
            ApplyByReflection();
        }
    }

    static void ApplyByReflection()
    {
        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var instance = singleton.GetProperty("instance").GetValue(null, null);
        var group = sizesType.GetProperty("currentGroup").GetValue(instance, null);
        var groupType = group.GetType();
        int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int sizeIndex = -1;
        var getSize = groupType.GetMethod("GetGameViewSize");
        for (int i = 0; i < count; i++)
        {
            var size = getSize.Invoke(group, new object[] { i });
            if (!IsLandscapeSize(size)) continue;
            sizeIndex = i;
            break;
        }

        if (sizeIndex < 0)
        {
            var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            var kindType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            var fixedResolution = Enum.Parse(kindType, "FixedResolution");
            var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
            var created = ctor.Invoke(new object[] { fixedResolution, Width, Height, "1920x1080" });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { created });
            count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            sizeIndex = count - 1;
        }

        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
        var callback = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        // ドロップダウンの先頭は Free Aspect なので、サイズ一覧の番号に 1 を足す。
        callback.Invoke(gameView, new object[] { sizeIndex + 1, null });
        gameView.Repaint();
    }

    static bool IsLandscapeSize(object size)
    {
        if (size == null) return false;
        var type = size.GetType();
        int width = (int)type.GetProperty("width").GetValue(size, null);
        int height = (int)type.GetProperty("height").GetValue(size, null);
        return width == Width && height == Height;
    }
}
#endif
