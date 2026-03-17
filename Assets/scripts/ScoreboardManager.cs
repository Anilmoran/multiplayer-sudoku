using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Photon.Pun;
using UnityEngine.UI;

// Bir oyuncunun sonucunu tutacak basit bir sýnýf
public class PlayerFinishData
{
    public string nickName;
    public bool isDNF;
    public float finishTime; // DNF deðilse ne kadar sürede bitirdi (Sýralama için)

    // Normal bitiren için constructor
    public PlayerFinishData(string name, float time)
    {
        nickName = name;
        isDNF = false;
        finishTime = time;
    }

    // DNF olan için constructor
    public PlayerFinishData(string name)
    {
        nickName = name;
        isDNF = true;
        finishTime = float.MaxValue; // Sýralamada en sona atmak için büyük bir süre veriyoruz
    }
}

public class ScoreboardManager : MonoBehaviourPunCallbacks
{
    public static ScoreboardManager Instance;

    [Header("UI References")]
    public GameObject scoreboardPanel;
    public TextMeshProUGUI rankingText;

    // Bitiren oyuncularýn verilerini tutacak liste (Tüm oyuncularda senkronize olmalý)
    private List<PlayerFinishData> finishDataList = new List<PlayerFinishData>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        scoreboardPanel.SetActive(false);
    }

    // --- BUTON FONKSÝYONLARI ---
    public void BackToLobby()
    {
        PhotonNetwork.LeaveRoom(); // Odadan çýk
    }

    public override void OnLeftRoom()
    {
        // Odadan baþarýyla çýkýnca Lobi sahnesine dön (Build Settings'de 0 olmalý)
        SceneManager.LoadScene(0);
    }

    public void QuitGame()
    {
        Application.Quit(); // Oyundan çýk
    }

    // --- SIRALAMA MANTIÐI ---

    // Bir oyuncu bitirdiðinde veya DNF olduðunda GridManager bu fonksiyonu çaðýracak
    public void AddFinishData(string nick, bool dnf, float time)
    {
        PlayerFinishData data = dnf ? new PlayerFinishData(nick) : new PlayerFinishData(nick, time);
        finishDataList.Add(data);

        // Eðer herkes bitirdiyse (veya DNF olduysa) sýralamayý göster
        if (finishDataList.Count == PhotonNetwork.CurrentRoom.PlayerCount)
        {
            ShowRanking();
        }
    }

    private void ShowRanking()
    {
        // 1. DNF OLMAYANLARI kendi içinde bitirme süresine göre sýrala (Küçükten büyüðe)
        List<PlayerFinishData> winners = finishDataList.FindAll(x => !x.isDNF);
        winners.Sort((a, b) => a.finishTime.CompareTo(b.finishTime));

        // 2. DNF OLANLARI kendi içinde bitirme sýrasýna göre sýrala (Zaten listeye eklendikleri sýra)
        List<PlayerFinishData> dnfPlayers = finishDataList.FindAll(x => x.isDNF);

        // 3. Ýki listeyi birleþtir (Önce kazananlar, sonra elenenler)
        List<PlayerFinishData> sortedList = new List<PlayerFinishData>();
        sortedList.AddRange(winners);
        sortedList.AddRange(dnfPlayers);

        // 4. Ekrana yazdýr
        rankingText.text = "";
        for (int i = 0; i < sortedList.Count; i++)
        {
            string rank = (i + 1).ToString();
            string status = sortedList[i].isDNF ? " (DNF)" : "";
            rankingText.text += $"{rank}. {sortedList[i].nickName}{status}\n";
        }

        scoreboardPanel.SetActive(true); // Paneli göster
    }
}
