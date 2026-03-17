using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public GameObject settingsPanel;

    [Header("Main Menu UI")]
    public TMP_InputField nameInput;
    public TMP_InputField joinCodeInput;
    public TextMeshProUGUI warningText;
    public Button hostButton;
    public Button joinButton;

    [Header("Lobby UI")]
    public TextMeshProUGUI roomCodeText;
    public TextMeshProUGUI playerListText;
    public TMP_Dropdown difficultyDropdown;
    public Button startGameButton;

    void Start()
    {
        // Tüm oyuncularýn ayný anda ayný sahneye geçmesini saðlar!
        PhotonNetwork.AutomaticallySyncScene = true;

        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        settingsPanel.SetActive(false);
        warningText.text = "Sunucuya baðlanýlýyor...";
        SetButtonsInteractable(false);

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
        warningText.text = "";
        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(bool state)
    {
        hostButton.interactable = state;
        joinButton.interactable = state;
    }

    // --- BUTON FONKSÝYONLARI ---

    public void OnHostButtonClicked()
    {
        if (string.IsNullOrEmpty(nameInput.text))
        {
            warningText.text = "Lütfen önce bir isim girin!";
            return;
        }

        PhotonNetwork.NickName = nameInput.text;

        // 5 haneli rastgele bir þifre/oda kodu oluþtur
        string randomRoomCode = Random.Range(10000, 99999).ToString();

        RoomOptions options = new RoomOptions();
        options.MaxPlayers = 4;

        // Baþlangýç zorluðunu Orta (1) olarak odaya kaydet
        options.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable() { { "Difficulty", 1 } };
        options.CustomRoomPropertiesForLobby = new string[] { "Difficulty" };

        PhotonNetwork.CreateRoom(randomRoomCode, options);
        warningText.text = "Oda kuruluyor...";
    }

    public void OnJoinButtonClicked()
    {
        if (string.IsNullOrEmpty(nameInput.text))
        {
            warningText.text = "Lütfen önce bir isim girin!";
            return;
        }
        if (string.IsNullOrEmpty(joinCodeInput.text))
        {
            warningText.text = "Lütfen bir oda þifresi girin!";
            return;
        }

        PhotonNetwork.NickName = nameInput.text;
        PhotonNetwork.JoinRoom(joinCodeInput.text);
        warningText.text = "Odaya katýlýnýyor...";
    }

    // --- AYARLAR MENÜSÜ ---
    public void OpenSettings() { settingsPanel.SetActive(true); }
    public void CloseSettings() { settingsPanel.SetActive(false); }

    // --- LOBÝ (ODA) FONKSÝYONLARI ---

    public override void OnJoinedRoom()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        warningText.text = "";

        roomCodeText.text = "Oda Þifresi: " + PhotonNetwork.CurrentRoom.Name;

        // Kurucu (Master Client) biz miyiz?
        bool isMaster = PhotonNetwork.IsMasterClient;
        startGameButton.gameObject.SetActive(isMaster); // Sadece kurucu baþlatabilir
        difficultyDropdown.interactable = isMaster; // Sadece kurucu zorluðu deðiþtirebilir

        // Odadaki mevcut zorluðu çek ve dropdown'ý güncelle
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("Difficulty"))
        {
            difficultyDropdown.value = (int)PhotonNetwork.CurrentRoom.CustomProperties["Difficulty"];
        }

        UpdatePlayerList();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        warningText.text = "Oda bulunamadý veya dolu!";
    }

    // Odaya biri girdiðinde veya çýktýðýnda listeyi yenile
    public override void OnPlayerEnteredRoom(Player newPlayer) { UpdatePlayerList(); }
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
        // Eðer kurucu çýkarsa ve yeni kurucu biz olursak butonlarý bize ver
        if (PhotonNetwork.IsMasterClient)
        {
            startGameButton.gameObject.SetActive(true);
            difficultyDropdown.interactable = true;
        }
    }

    private void UpdatePlayerList()
    {
        playerListText.text = "Oyuncular:\n";
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            playerListText.text += "- " + p.NickName;
            if (p.IsMasterClient) playerListText.text += " (Kurucu)";
            playerListText.text += "\n";
        }
    }

    public void LeaveLobby()
    {
        PhotonNetwork.LeaveRoom();
        lobbyPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // Zorluk deðiþtiðinde (Sadece kurucu tetikler)
    public void OnDifficultyChanged()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            ExitGames.Client.Photon.Hashtable newProp = new ExitGames.Client.Photon.Hashtable();
            newProp.Add("Difficulty", difficultyDropdown.value);
            PhotonNetwork.CurrentRoom.SetCustomProperties(newProp);
        }
    }

    // Odadaki zorluk özelliði deðiþince diðer oyuncularýn ekranýný güncelle
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("Difficulty"))
        {
            difficultyDropdown.value = (int)propertiesThatChanged["Difficulty"];
        }
    }

    // Sadece Kurucu basabilir
    public void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Tüm oyuncularý GameScene'e yükler
            PhotonNetwork.LoadLevel("GameScene");
        }
    }
}
