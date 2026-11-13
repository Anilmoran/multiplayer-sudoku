using System.Collections;
using UnityEngine;

namespace CozySudoku
{
    /// <summary>Menü ↔ oyun geçişleri (bulmaca üretimi geçiş ekranı kapalıyken yapılır).</summary>
    public static class GameFlow
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "GameScene";

        public static void StartOffline(GameMode mode, Difficulty difficulty)
        {
            GameSession.Mode = mode;
            GameSession.Difficulty = difficulty;
            Settings.LastDifficulty = difficulty;
            Settings.Save();
            SceneTransition.LoadScene(GameScene, () => Prepare(mode, difficulty));
        }

        /// <summary>Aynı bulmacayı baştan oynat.</summary>
        public static void Restart()
        {
            SceneTransition.LoadScene(GameScene);
        }

        public static void ToMenu()
        {
            SceneTransition.LoadScene(MenuScene, () => OrientationFor(GameMode.Solo));
        }

        static IEnumerator Prepare(GameMode mode, Difficulty difficulty)
        {
            var task = GameSession.GenerateAsync(difficulty);
            float t = 0f;
            while (!task.IsCompleted && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            if (task.IsCompleted && !task.IsFaulted) GameSession.SetPuzzle(task.Result.Givens, task.Result.Solution);
            else GameSession.GenerateNow(difficulty);
            yield return OrientationFor(mode);
        }

        /// <summary>
        /// Mobilde 2 kişilik modda cihaz dikey sabitlenir (karşılıklı oturma düzeni);
        /// diğer modlarda serbest döndürme.
        /// </summary>
        public static IEnumerator OrientationFor(GameMode mode)
        {
            if (!AppBootstrap.IsMobile) yield break;
            var target = mode == GameMode.LocalVersus ? ScreenOrientation.Portrait : ScreenOrientation.AutoRotation;
            if (Screen.orientation == target) yield break;
            Screen.orientation = target;
            // yön değişiminin oturması için birkaç kare bekle
            for (int i = 0; i < 6; i++) yield return null;
        }
    }
}
