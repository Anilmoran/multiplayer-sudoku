using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Photon.Pun; // Photon için gerekli
using Photon.Realtime;
public enum Difficulty { Easy, Medium, Hard }
public class GridManager : MonoBehaviourPunCallbacks // MonoBehaviour yerine PunCallbacks
{
    public static GridManager Instance;

    [Header("Board Settings")]
    public GameObject blockPrefab;
    public GameObject cellPrefab;
    public Transform boardPanel;

    public Color easyColor = Color.blue;
    public Color mediumColor = Color.green;
    public Color hardColor = Color.red;

    private SudokuCell[,] cells = new SudokuCell[9, 9];
    private SudokuGenerator generator;
    private List<Image> blockImages = new List<Image>();

    public int[,] solvedBoard = new int[9, 9];
    public SudokuCell selectedCell;

    [Header("Pencil Mode")]
    public bool isPencilMode = false;
    public Image pencilButtonImage;

    [Header("Mistakes & Game State")]
    public int maxMistakes = 3;
    private int currentMistakes = 0;
    public bool isGameOver = false; // Yerel oyun bitti mi?

    public TextMeshProUGUI mistakesText;

    [Header("Win State")]
    private int emptyCellsCount = 0;
    private float gameStartTime; // Oyuna ne zaman baþladýk? (Sýralama için)

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        gameStartTime = Time.time; // Süreyi baþlat

        // ÇOK ÖNEMLÝ: Multiplayer moddaysa tahtayý sadece Master Client (Kurucu) üretmeli!
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            CreateBoard();

            // Odadaki zorluðu çek
            int difficultyValue = 1; // Varsayýlan Orta
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("Difficulty"))
            {
                difficultyValue = (int)PhotonNetwork.CurrentRoom.CustomProperties["Difficulty"];
            }
            Difficulty difficulty = (Difficulty)difficultyValue;

            SetupNewGame(difficulty);
        }
        else if (!PhotonNetwork.IsConnected)
        {
            // Singleplayer test için
            CreateBoard();
            SetupNewGame(Difficulty.Medium);
        }

        // Tahta oluþtuktan sonra herkes hata UI'ýný güncellesin
        UpdateMistakesUI();
    }

    public void SetupNewGame(Difficulty difficulty)
    {
        generator = new SudokuGenerator();
        generator.GenerateFullBoard();

        // 1. Çözülmüþ tahtayý solvedBoard'a kopyala
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                solvedBoard[r, c] = generator.board[r, c];
            }
        }

        int numbersToRemove = difficulty == Difficulty.Easy ? 30 : difficulty == Difficulty.Medium ? 45 : 60;

        // Singleplayer test için sayaç
        emptyCellsCount = numbersToRemove;

        generator.RemoveNumbers(numbersToRemove);

        // 2. Tahtayý ekrana yansýt (Singleplayer için)
        ApplyBoardToCells(generator.board);

        // 3. ÇÖZÜMÜ VE SÝLÝNMÝÞ TAHTAYI DÝÐER OYUNCULARA GÖNDER!
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            int[] solvedArray = MatrixToArray(solvedBoard);
            int[] removedArray = MatrixToArray(generator.board);
            photonView.RPC("RPC_SyncInitialBoard", RpcTarget.OthersBuffered, solvedArray, removedArray, (int)difficulty);
        }
    }

    // --- MULTIPLAYER SENKRONÝZASYON (RPC) ---

    // Tahtayý ve çözümü tüm oyuncularda ayný yapan RPC
    [PunRPC]
    void RPC_SyncInitialBoard(int[] solvedArray, int[] removedArray, int difficultyValue)
    {
        // Master Client deðilsek, gelen tahtayý uygula
        if (!PhotonNetwork.IsMasterClient)
        {
            CreateBoard(); // Önce görsel tahtayý oluþtur

            solvedBoard = ArrayToMatrix(solvedArray);
            int[,] removedBoard = ArrayToMatrix(removedArray);

            ApplyBoardToCells(removedBoard);

            Difficulty difficulty = (Difficulty)difficultyValue;
            SetBlockColors(difficulty);

            // Geriye bulmamýz gereken boþ kutu sayýsýný hesapla
            emptyCellsCount = 0;
            foreach (int val in removedArray) if (val == 0) emptyCellsCount++;
        }
    }

    // 2D diziyi 1D diziye çevirir (Photon 2D dizi gönderemez)
    int[] MatrixToArray(int[,] matrix)
    {
        int[] arr = new int[81];
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++) arr[i * 9 + j] = matrix[i, j];
        return arr;
    }

    // 1D diziyi 2D diziye çevirir
    int[,] ArrayToMatrix(int[] arr)
    {
        int[,] matrix = new int[9, 9];
        for (int i = 0; i < 81; i++) matrix[i / 9, i % 9] = arr[i];
        return matrix;
    }

    // Tahta verisini ekrandaki hücrelere uygular
    void ApplyBoardToCells(int[,] boardData)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                int value = boardData[row, col];
                cells[row, col].SetValue(value, value != 0);
            }
        }
    }

    // --- TEMEL OYUN MANTIÐI (Ayný Kalýyor) ---

    void CreateBoard()
    {
        // Eðer tahta zaten oluþtuysa tekrar oluþturma (Hile korumasý)
        if (cells[0, 0] != null) return;

        for (int blockRow = 0; blockRow < 3; blockRow++)
        {
            for (int blockCol = 0; blockCol < 3; blockCol++)
            {
                GameObject newBlock = Instantiate(blockPrefab, boardPanel);
                newBlock.name = $"Block_{blockRow}_{blockCol}";
                blockImages.Add(newBlock.GetComponent<Image>());

                for (int cellRow = 0; cellRow < 3; cellRow++)
                {
                    for (int cellCol = 0; cellCol < 3; cellCol++)
                    {
                        int realRow = blockRow * 3 + cellRow;
                        int realCol = blockCol * 3 + cellCol;

                        GameObject newCellObj = Instantiate(cellPrefab, newBlock.transform);
                        newCellObj.name = $"Cell_{realRow}_{realCol}";

                        SudokuCell cellScript = newCellObj.GetComponent<SudokuCell>();
                        cellScript.Setup(realRow, realCol);
                        cells[realRow, realCol] = cellScript;
                    }
                }
            }
        }
    }

    void SetBlockColors(Difficulty difficulty)
    {
        Color targetColor = difficulty == Difficulty.Easy ? easyColor : difficulty == Difficulty.Medium ? mediumColor : hardColor;
        foreach (var blockImage in blockImages) blockImage.color = targetColor;
    }

    public void SelectCell(SudokuCell cell)
    {
        if (isGameOver) return;

        if (selectedCell != null)
        {
            selectedCell.GetComponent<Image>().color = Color.white;
        }

        selectedCell = cell;
        selectedCell.GetComponent<Image>().color = Color.yellow;
    }

    public void TogglePencilMode()
    {
        if (isGameOver) return;

        isPencilMode = !isPencilMode;
        if (pencilButtonImage != null) pencilButtonImage.color = isPencilMode ? Color.yellow : Color.white;
    }

    private bool IsSmartNoteValid(int row, int col, int number)
    {
        for (int i = 0; i < 9; i++)
        {
            if (cells[row, i].currentValue == number) return false;
            if (cells[i, col].currentValue == number) return false;
        }

        int startRow = (row / 3) * 3;
        int startCol = (col / 3) * 3;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (cells[startRow + i, startCol + j].currentValue == number) return false;
            }
        }
        return true;
    }

    public void OnNumberButtonPressed(int number)
    {
        if (isGameOver || selectedCell == null || selectedCell.isStartingClue) return;

        int r = selectedCell.row;
        int c = selectedCell.col;

        if (isPencilMode)
        {
            if (IsSmartNoteValid(r, c, number)) selectedCell.ToggleNote(number);
        }
        else
        {
            if (solvedBoard[r, c] == number)
            {
                if (selectedCell.currentValue == 0)
                {
                    emptyCellsCount--;
                }

                selectedCell.SetValue(number, false);
                selectedCell.GetComponent<Image>().color = Color.white;
                CleanUpPencilMarks(r, c, number);
                selectedCell = null;

                // KAZANMA KONTROLÜ
                if (emptyCellsCount <= 0)
                {
                    TriggerWin();
                }
            }
            else
            {
                selectedCell.GetComponent<Image>().color = Color.red;
                WrongAnswerMade();
            }
        }
    }

    private void CleanUpPencilMarks(int row, int col, int number)
    {
        for (int i = 0; i < 9; i++)
        {
            cells[row, i].RemoveNote(number);
            cells[i, col].RemoveNote(number);
        }

        int startRow = (row / 3) * 3;
        int startCol = (col / 3) * 3;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++) cells[startRow + i, startCol + j].RemoveNote(number);
        }
    }

    // --- YANLIÞ CEVAP VE DNF MANTIÐI ---

    private void WrongAnswerMade()
    {
        currentMistakes++;
        UpdateMistakesUI();

        if (currentMistakes >= maxMistakes)
        {
            TriggerDNF();
        }
    }

    private void UpdateMistakesUI()
    {
        if (mistakesText != null)
        {
            mistakesText.text = $"Hata: {currentMistakes} / {maxMistakes}";
        }
    }

    // KAZANMA DURUMU
    private void TriggerWin()
    {
        if (isGameOver) return; // Zaten bittiyse (DNF vb.) iþlem yapma

        isGameOver = true; // Kendi tahtaný kilitle
        if (selectedCell != null) selectedCell.GetComponent<Image>().color = Color.white;

        float finishTime = Time.time - gameStartTime; // Ne kadar sürede bitirdi?
        string myNick = PhotonNetwork.NickName;

        // BÝTÝRME BÝLGÝSÝNÝ DÝÐER OYUNCULARA GÖNDER!
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_PlayerFinished", RpcTarget.All, myNick, false, finishTime);
        }
        else
        {
            // Singleplayer için
            ScoreboardManager.Instance.AddFinishData(myNick, false, finishTime);
        }

        Debug.Log("Tebrikler, tüm tahtayý çözdün!");
    }

    // DNF DURUMU (ELENDÝ)
    private void TriggerDNF()
    {
        if (isGameOver) return; // Zaten bittiyse iþlem yapma

        isGameOver = true; // Kendi tahtaný kilitle
        if (selectedCell != null) selectedCell.GetComponent<Image>().color = Color.white;

        string myNick = PhotonNetwork.NickName;

        // DNF BÝLGÝSÝNÝ DÝÐER OYUNCULARA GÖNDER!
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_PlayerFinished", RpcTarget.All, myNick, true, 0f);
        }
        else
        {
            // Singleplayer için
            ScoreboardManager.Instance.AddFinishData(myNick, true, 0f);
        }

        Debug.Log("DNF - 3 Hata Yaptýn ve Elendin!");
    }

    // --- SKOR TABLOSU SENKRONÝZASYONU (RPC) ---

    // Bir oyuncu bitirdiðinde veya elendiðinde tüm oyuncularýn ekranýna bilgi gönderir
    [PunRPC]
    void RPC_PlayerFinished(string nick, bool dnf, float time)
    {
        // Gelen bilgiyi skor tablosuna ekle
        ScoreboardManager.Instance.AddFinishData(nick, dnf, time);
    }
}