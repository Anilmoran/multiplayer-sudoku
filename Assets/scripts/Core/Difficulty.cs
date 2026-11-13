namespace CozySudoku
{
    public enum Difficulty { Easy = 0, Medium = 1, Hard = 2 }

    public static class DifficultyInfo
    {
        /// <summary>Tahtadan silinecek hücre sayısı (tek çözüm korunarak).</summary>
        public static int CellsToRemove(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return 38;   // 43 ipucu
                case Difficulty.Hard: return 56;   // ~25 ipucu
                default: return 48;                // 33 ipucu
            }
        }

        public static string Name(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return "Kolay";
                case Difficulty.Hard: return "Zor";
                default: return "Orta";
            }
        }

        public static int Stars(Difficulty d) => (int)d + 1;

        public static Difficulty Clamp(int value)
        {
            if (value <= 0) return Difficulty.Easy;
            if (value >= 2) return Difficulty.Hard;
            return Difficulty.Medium;
        }
    }
}
