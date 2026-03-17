using System.Collections.Generic;
using UnityEngine;

public class SudokuGenerator
{
    public int[,] board = new int[9, 9];

    // Arka planda tam ve kurallara uygun bir tahta oluþturur
    public void GenerateFullBoard()
    {
        // Önce tahtayý sýfýrla
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                board[i, j] = 0;

        FillBoard();
    }

    // Backtracking algoritmasý ile tahtayý doldurur
    private bool FillBoard()
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (board[row, col] == 0) // Eðer hücre boþsa
                {
                    // 1'den 9'a kadar sayýlarý rastgele sýrayla dene (her seferinde farklý tahta çýksýn diye)
                    List<int> numbers = GetRandomizedNumbers();
                    foreach (int num in numbers)
                    {
                        if (IsValid(row, col, num)) // Kurala uygun mu?
                        {
                            board[row, col] = num; // Sayýyý koy

                            if (FillBoard()) // Sonraki hücrelere geç
                                return true;

                            board[row, col] = 0; // Týkanýrsak sayýyý geri al (Backtrack)
                        }
                    }
                    return false; // Hiçbir sayý uymadýysa geri dön
                }
            }
        }
        return true; // Tüm tahta doldu!
    }

    // Sudoku kurallarý: Satýrda, sütunda ve 3x3 blokta ayný sayý var mý?
    public bool IsValid(int row, int col, int num)
    {
        // Satýr ve Sütun kontrolü
        for (int i = 0; i < 9; i++)
        {
            if (board[row, i] == num || board[i, col] == num)
                return false;
        }

        // 3x3 Blok kontrolü
        int startRow = (row / 3) * 3;
        int startCol = (col / 3) * 3;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (board[startRow + i, startCol + j] == num)
                    return false;
            }
        }

        return true; // Hiçbir çakýþma yoksa sayý geçerlidir
    }

    // 1-9 arasý sayýlarý karýþtýrýp liste olarak verir
    private List<int> GetRandomizedNumbers()
    {
        List<int> nums = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        for (int i = 0; i < nums.Count; i++)
        {
            int temp = nums[i];
            int randomIndex = Random.Range(i, nums.Count);
            nums[i] = nums[randomIndex];
            nums[randomIndex] = temp;
        }
        return nums;
    }

    // Zorluk derecesine göre tahtadan rastgele sayý siler
    public void RemoveNumbers(int countToRemove)
    {
        int removed = 0;
        while (removed < countToRemove)
        {
            int r = Random.Range(0, 9);
            int c = Random.Range(0, 9);

            if (board[r, c] != 0) // Eðer hücre doluysa sil
            {
                board[r, c] = 0;
                removed++;
            }
        }
    }
}
