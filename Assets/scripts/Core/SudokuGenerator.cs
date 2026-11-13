using System;

namespace CozySudoku
{
    /// <summary>
    /// Tek çözümlü sudoku üretir. Unity API'si kullanmaz (System.Random), bu yüzden
    /// arka plan thread'inde güvenle çalışır. Hücreler 0..80 indeksli tek boyutlu dizidir
    /// (indeks = satır * 9 + sütun), 0 = boş.
    /// </summary>
    public sealed class SudokuGenerator
    {
        const int AllDigits = 0x1FF; // 9 bit: 1..9

        readonly Random rng;
        readonly int[] rowMask = new int[9];
        readonly int[] colMask = new int[9];
        readonly int[] boxMask = new int[9];

        public SudokuGenerator(int seed)
        {
            rng = new Random(seed);
        }

        public struct Puzzle
        {
            public byte[] Givens;   // 81 hücre, 0 = boş
            public byte[] Solution; // 81 hücre, tam çözüm
        }

        public Puzzle Generate(Difficulty difficulty)
        {
            var solution = new int[81];
            Array.Clear(rowMask, 0, 9);
            Array.Clear(colMask, 0, 9);
            Array.Clear(boxMask, 0, 9);
            FillRandom(solution, 0);

            var puzzle = (int[])solution.Clone();
            RemoveCells(puzzle, DifficultyInfo.CellsToRemove(difficulty));

            var result = new Puzzle { Givens = new byte[81], Solution = new byte[81] };
            for (int i = 0; i < 81; i++)
            {
                result.Givens[i] = (byte)puzzle[i];
                result.Solution[i] = (byte)solution[i];
            }
            return result;
        }

        static int Box(int r, int c) => (r / 3) * 3 + c / 3;

        // ---------------- Tam tahta üretimi ----------------

        bool FillRandom(int[] b, int index)
        {
            if (index == 81) return true;
            int r = index / 9, c = index % 9, bx = Box(r, c);
            int candidates = AllDigits & ~(rowMask[r] | colMask[c] | boxMask[bx]);
            if (candidates == 0) return false;

            // Adayları rastgele sırada dene (her seferinde farklı tahta)
            Span<int> digits = stackalloc int[9];
            int n = 0;
            for (int d = 0; d < 9; d++) if ((candidates & (1 << d)) != 0) digits[n++] = d;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = digits[i]; digits[i] = digits[j]; digits[j] = t;
            }

            for (int i = 0; i < n; i++)
            {
                int bit = 1 << digits[i];
                b[index] = digits[i] + 1;
                rowMask[r] |= bit; colMask[c] |= bit; boxMask[bx] |= bit;
                if (FillRandom(b, index + 1)) return true;
                rowMask[r] &= ~bit; colMask[c] &= ~bit; boxMask[bx] &= ~bit;
            }
            b[index] = 0;
            return false;
        }

        // ---------------- Hücre silme (tek çözüm garantili) ----------------

        void RemoveCells(int[] puzzle, int target)
        {
            var order = new int[81];
            for (int i = 0; i < 81; i++) order[i] = i;
            for (int i = 80; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }

            int removed = 0;
            var work = new int[81];
            for (int k = 0; k < 81 && removed < target; k++)
            {
                int idx = order[k];
                int backup = puzzle[idx];
                if (backup == 0) continue;
                puzzle[idx] = 0;

                Array.Copy(puzzle, work, 81);
                if (CountSolutions(work, 2) == 1) removed++;
                else puzzle[idx] = backup; // birden fazla çözüm olurdu → geri koy
            }
        }

        /// <summary>Çözüm sayısını en fazla <paramref name="limit"/> kadar sayar. Diziyi bozmaz.</summary>
        public static int CountSolutions(int[] board, int limit)
        {
            var rows = new int[9];
            var cols = new int[9];
            var boxes = new int[9];
            for (int i = 0; i < 81; i++)
            {
                int v = board[i];
                if (v == 0) continue;
                int bit = 1 << (v - 1);
                int r = i / 9, c = i % 9, bx = Box(r, c);
                if ((rows[r] & bit) != 0 || (cols[c] & bit) != 0 || (boxes[bx] & bit) != 0) return 0;
                rows[r] |= bit; cols[c] |= bit; boxes[bx] |= bit;
            }
            int count = 0;
            Solve(board, rows, cols, boxes, ref count, limit);
            return count;
        }

        static void Solve(int[] b, int[] rows, int[] cols, int[] boxes, ref int count, int limit)
        {
            // En az adaylı boş hücreyi seç (MRV) — arama ağacını ciddi biçimde küçültür
            int best = -1, bestCandidates = 0, bestCount = 10;
            for (int i = 0; i < 81; i++)
            {
                if (b[i] != 0) continue;
                int r = i / 9, c = i % 9;
                int cand = AllDigits & ~(rows[r] | cols[c] | boxes[Box(r, c)]);
                int pc = PopCount(cand);
                if (pc == 0) return; // çıkmaz
                if (pc < bestCount)
                {
                    best = i; bestCandidates = cand; bestCount = pc;
                    if (pc == 1) break;
                }
            }

            if (best < 0) { count++; return; } // tahta doldu → bir çözüm

            int br = best / 9, bc = best % 9, bb = Box(br, bc);
            while (bestCandidates != 0 && count < limit)
            {
                int bit = bestCandidates & -bestCandidates;
                bestCandidates &= ~bit;
                b[best] = BitIndex(bit) + 1;
                rows[br] |= bit; cols[bc] |= bit; boxes[bb] |= bit;
                Solve(b, rows, cols, boxes, ref count, limit);
                rows[br] &= ~bit; cols[bc] &= ~bit; boxes[bb] &= ~bit;
            }
            b[best] = 0;
        }

        static int PopCount(int v)
        {
            int c = 0;
            while (v != 0) { v &= v - 1; c++; }
            return c;
        }

        static int BitIndex(int singleBit)
        {
            int i = 0;
            while ((singleBit >>= 1) != 0) i++;
            return i;
        }
    }
}
