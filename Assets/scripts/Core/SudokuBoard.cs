using System.Collections.Generic;

namespace CozySudoku
{
    public enum PlaceResult { Ignored, Correct, Completed, Wrong, Failed }

    /// <summary>
    /// Bir oyuncunun tahta durumu (saf C#, görselden bağımsız).
    /// Bölünmüş ekranda her oyuncunun kendi SudokuBoard'u vardır.
    /// </summary>
    public sealed class SudokuBoard
    {
        public const int Size = 81;

        readonly byte[] givens;
        readonly byte[] solution;
        readonly int[] values = new int[Size];
        readonly ushort[] notes = new ushort[Size];
        readonly int[] digitCounts = new int[10];
        readonly Stack<NoteChange> undo = new Stack<NoteChange>(64);

        struct NoteChange
        {
            public int Cell;
            public ushort Before;
        }

        public int MaxMistakes { get; }
        public int Mistakes { get; private set; }
        public int EmptyAtStart { get; }
        public int FilledByPlayer { get; private set; }
        public bool IsCompleted => FilledByPlayer >= EmptyAtStart;
        public bool IsFailed => Mistakes >= MaxMistakes;
        public bool IsOver => IsCompleted || IsFailed;
        public bool CanUndo => undo.Count > 0;

        /// <summary>0..1 arası ilerleme (online rakip göstergesi için).</summary>
        public float Progress => EmptyAtStart == 0 ? 1f : (float)FilledByPlayer / EmptyAtStart;

        static readonly int[][] peers = BuildPeers();

        public SudokuBoard(byte[] givens, byte[] solution, int maxMistakes = 3)
        {
            this.givens = givens;
            this.solution = solution;
            MaxMistakes = maxMistakes;
            for (int i = 0; i < Size; i++)
            {
                values[i] = givens[i];
                if (givens[i] == 0) EmptyAtStart++;
                else digitCounts[givens[i]]++;
            }
        }

        public int ValueAt(int i) => values[i];
        public bool IsGiven(int i) => givens[i] != 0;
        public int SolutionAt(int i) => solution[i];
        public ushort NotesAt(int i) => notes[i];
        public bool HasNote(int i, int digit) => (notes[i] & (1 << (digit - 1))) != 0;
        public int DigitCount(int digit) => digitCounts[digit];
        public bool IsDigitComplete(int digit) => digitCounts[digit] >= 9;
        public static int[] PeersOf(int i) => peers[i];

        public PlaceResult Place(int cell, int digit)
        {
            if (IsOver || digit < 1 || digit > 9 || values[cell] != 0) return PlaceResult.Ignored;

            if (solution[cell] == digit)
            {
                values[cell] = digit;
                notes[cell] = 0;
                digitCounts[digit]++;
                FilledByPlayer++;
                RemoveNoteFromPeers(cell, digit);
                return IsCompleted ? PlaceResult.Completed : PlaceResult.Correct;
            }

            Mistakes++;
            return IsFailed ? PlaceResult.Failed : PlaceResult.Wrong;
        }

        /// <summary>İpucu: hücreye doğru sayıyı yazar (hata sayılmaz).</summary>
        public PlaceResult Reveal(int cell) => Place(cell, solution[cell]);

        /// <summary>Not ekler/çıkarır. smart=true ise satır/sütun/blokta zaten olan sayı not alınamaz.</summary>
        public bool ToggleNote(int cell, int digit, bool smart)
        {
            if (IsOver || values[cell] != 0) return false;
            int bit = 1 << (digit - 1);
            bool adding = (notes[cell] & bit) == 0;
            if (adding && smart && ConflictsWithPlaced(cell, digit)) return false;

            undo.Push(new NoteChange { Cell = cell, Before = notes[cell] });
            notes[cell] ^= (ushort)bit;
            return true;
        }

        public bool ClearNotes(int cell)
        {
            if (IsOver || values[cell] != 0 || notes[cell] == 0) return false;
            undo.Push(new NoteChange { Cell = cell, Before = notes[cell] });
            notes[cell] = 0;
            return true;
        }

        /// <summary>Son not değişikliğini geri alır; değişen hücreyi döndürür (-1 = yok).</summary>
        public int Undo()
        {
            while (undo.Count > 0)
            {
                var change = undo.Pop();
                if (values[change.Cell] != 0) continue; // hücre o arada çözüldüyse atla
                notes[change.Cell] = change.Before;
                return change.Cell;
            }
            return -1;
        }

        public bool ConflictsWithPlaced(int cell, int digit)
        {
            var p = peers[cell];
            for (int k = 0; k < p.Length; k++) if (values[p[k]] == digit) return true;
            return false;
        }

        /// <summary>Boş bir hücre (önce verilen indeks, değilse ilk boş) — ipucu için.</summary>
        public int FindEmptyCell(int preferred)
        {
            if (preferred >= 0 && preferred < Size && values[preferred] == 0) return preferred;
            for (int i = 0; i < Size; i++) if (values[i] == 0) return i;
            return -1;
        }

        void RemoveNoteFromPeers(int cell, int digit)
        {
            ushort mask = (ushort)~(1 << (digit - 1));
            var p = peers[cell];
            for (int k = 0; k < p.Length; k++) notes[p[k]] &= mask;
        }

        static int[][] BuildPeers()
        {
            var result = new int[Size][];
            var tmp = new List<int>(20);
            for (int i = 0; i < Size; i++)
            {
                tmp.Clear();
                int r = i / 9, c = i % 9, br = r / 3 * 3, bc = c / 3 * 3;
                for (int j = 0; j < Size; j++)
                {
                    if (j == i) continue;
                    int jr = j / 9, jc = j % 9;
                    if (jr == r || jc == c || (jr / 3 * 3 == br && jc / 3 * 3 == bc)) tmp.Add(j);
                }
                result[i] = tmp.ToArray();
            }
            return result;
        }
    }
}
