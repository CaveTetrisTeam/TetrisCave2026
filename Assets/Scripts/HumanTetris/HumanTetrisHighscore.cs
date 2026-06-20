using System;
using UnityEngine;

namespace HumanTetris
{
    public static class HumanTetrisHighscore
    {
        public const string PlayerPrefsKey = "HumanTetris.Highscore";

        public static event Action<int> BestScoreChanged;

        public static int BestScore => PlayerPrefs.GetInt(PlayerPrefsKey, 0);
        public static int CurrentScore { get; private set; }

        public static void SetCurrentScore(int score)
        {
            CurrentScore = Mathf.Max(0, score);
            SaveIfHigher(CurrentScore);
        }

        public static void AddToCurrentScore(int points)
        {
            SetCurrentScore(CurrentScore + points);
        }

        public static void ResetCurrentScore()
        {
            CurrentScore = 0;
        }

        public static bool SaveIfHigher(int score)
        {
            score = Mathf.Max(0, score);
            if (score <= BestScore)
            {
                return false;
            }

            PlayerPrefs.SetInt(PlayerPrefsKey, score);
            PlayerPrefs.Save();
            BestScoreChanged?.Invoke(score);
            return true;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
            BestScoreChanged?.Invoke(0);
        }
    }
}
