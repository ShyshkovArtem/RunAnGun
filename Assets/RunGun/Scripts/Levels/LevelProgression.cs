using UnityEngine;

namespace RunGun.Levels
{
    public static class LevelProgression
    {
        public const string BestTimeKeyPrefix = "RunGun.BestTime.";
        private const string CompletionKeyPrefix = "RunGun.Completed.";

        public static bool IsCompleted(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   PlayerPrefs.GetInt(CompletionKeyPrefix + sceneName.Trim(), 0) != 0;
        }

        public static void MarkCompleted(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            PlayerPrefs.SetInt(CompletionKeyPrefix + sceneName.Trim(), 1);
            PlayerPrefs.Save();
        }

        public static bool TryGetBestTime(string levelId, out float bestTime)
        {
            bestTime = 0f;
            if (string.IsNullOrWhiteSpace(levelId))
                return false;

            string key = BestTimeKeyPrefix + levelId.Trim();
            if (!PlayerPrefs.HasKey(key))
                return false;

            bestTime = PlayerPrefs.GetFloat(key);
            return true;
        }
    }
}
