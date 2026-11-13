using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CozySudoku
{
    public enum GameMode
    {
        Solo,        // Tek oyuncu
        LocalVersus, // Aynı cihazda bölünmüş ekran (2 kişi)
        Online       // Photon üzerinden
    }

    /// <summary>Sahneler arasında taşınan oyun bilgisi (menü → oyun).</summary>
    public static class GameSession
    {
        public static GameMode Mode = GameMode.Solo;
        public static Difficulty Difficulty = Difficulty.Medium;
        public static byte[] Givens;
        public static byte[] Solution;

        public static bool HasPuzzle => Givens != null && Solution != null && Givens.Length == 81 && Solution.Length == 81;

        public static void SetPuzzle(byte[] givens, byte[] solution)
        {
            Givens = givens;
            Solution = solution;
        }

        /// <summary>Bulmacayı arka planda üretir (ana thread'i kilitlemez).</summary>
        public static Task<SudokuGenerator.Puzzle> GenerateAsync(Difficulty difficulty)
        {
            int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            return Task.Run(() => new SudokuGenerator(seed).Generate(difficulty));
        }

        /// <summary>Senkron üretim (gerekirse yedek olarak).</summary>
        public static void GenerateNow(Difficulty difficulty)
        {
            var p = new SudokuGenerator(Environment.TickCount).Generate(difficulty);
            SetPuzzle(p.Givens, p.Solution);
        }
    }

    /// <summary>Oyuncu ayarları (PlayerPrefs ile kalıcı).</summary>
    public static class Settings
    {
        const string KSound = "cs_sound", KHaptics = "cs_haptics", KHighlight = "cs_highlight",
                     KSmartNotes = "cs_smartnotes", KName = "cs_name", KDifficulty = "cs_diff";

        public static bool Sound
        {
            get => PlayerPrefs.GetInt(KSound, 1) == 1;
            set { PlayerPrefs.SetInt(KSound, value ? 1 : 0); AudioListener.volume = value ? 1f : 0f; }
        }

        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(KHaptics, 1) == 1;
            set => PlayerPrefs.SetInt(KHaptics, value ? 1 : 0);
        }

        public static bool HighlightSame
        {
            get => PlayerPrefs.GetInt(KHighlight, 1) == 1;
            set => PlayerPrefs.SetInt(KHighlight, value ? 1 : 0);
        }

        public static bool SmartNotes
        {
            get => PlayerPrefs.GetInt(KSmartNotes, 1) == 1;
            set => PlayerPrefs.SetInt(KSmartNotes, value ? 1 : 0);
        }

        public static string PlayerName
        {
            get => PlayerPrefs.GetString(KName, "");
            set => PlayerPrefs.SetString(KName, value ?? "");
        }

        public static Difficulty LastDifficulty
        {
            get => DifficultyInfo.Clamp(PlayerPrefs.GetInt(KDifficulty, 1));
            set => PlayerPrefs.SetInt(KDifficulty, (int)value);
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
