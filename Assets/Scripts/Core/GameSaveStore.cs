using System;
using System.IO;
using UnityEngine;

namespace SoccerClub.Core
{
    /// <summary>
    /// 進行を persistentDataPath の JSON へ書く。日のアニメーション中は呼ばない。
    /// その途中は能力の反映と試合結果が半分だけ進んでいる。
    /// </summary>
    public static class GameSaveStore
    {
        public const string FileName = "soccerclub-save.json";

        public static bool Exists => File.Exists(SavePath);

        static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Write(GameSession session)
        {
            if (session == null) return false;
            try
            {
                var data = session.CaptureSave();
                if (data == null) return false;
                var path = SavePath;
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("SoccerClub save failed: " + exception.Message);
                return false;
            }
        }

        public static bool TryRead(out GameSession session)
        {
            session = null;
            try
            {
                var path = SavePath;
                if (!File.Exists(path)) return false;
                var json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return false;
                session = GameSession.RestoreSave(JsonUtility.FromJson<GameSaveFile>(json));
                return session != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("SoccerClub load failed: " + exception.Message);
                session = null;
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                var path = SavePath;
                if (File.Exists(path)) File.Delete(path);
                var temp = path + ".tmp";
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("SoccerClub save delete failed: " + exception.Message);
            }
        }
    }
}
