using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace CozySudoku
{
    /// <summary>Photon oda/oyuncu özellik anahtarları (kısa tutulur → daha az veri).</summary>
    public static class NetKeys
    {
        // Oda
        public const string Difficulty = "d";
        public const string State = "st";    // 0 = lobi, 1 = oyunda
        public const string GameId = "g";
        public const string Givens = "pz";
        public const string Solution = "sl";
        // Oyuncu
        public const string PGame = "g";
        public const string PFilled = "p";
        public const string PMistakes = "m";
        public const string PTime = "t";     // bitirme süresi (sn), -1 = bitirmedi
        public const string PDnf = "x";

        public const int StateLobby = 0, StatePlaying = 1;
    }

    /// <summary>
    /// Tüm Photon akışını yöneten kalıcı servis.
    /// Eski sürümdeki sorunlar burada çözülür:
    ///  • Sahnede PhotonView yoktu → RPC'ler çöküyordu. Artık RPC yok; bulmaca ve skorlar
    ///    oda/oyuncu özellikleriyle (custom properties) taşınıyor — geç yüklenen istemci de veriyi görür.
    ///  • Oyuncular farklı bölgelere bağlanınca oda kodu bulunamıyordu → sabit bölge.
    ///  • Oyun başlayınca oda kapatılır (ortaya oyuncu katılamaz), kod çakışmasında yeniden denenir.
    ///  • Lobiye dönünce tekrar ConnectUsingSettings çağrılıp hata alınmıyor.
    /// </summary>
    public sealed class OnlineManager : MonoBehaviourPunCallbacks
    {
        public const int MaxPlayers = 4;
        const string DefaultRegion = "eu";
        public const string MenuScene = "MainMenu";
        public const string GameScene = "GameScene";

        static OnlineManager instance;
        public static OnlineManager Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("OnlineManager");
                if (AppBootstrap.Root != null) go.transform.SetParent(AppBootstrap.Root.transform, false);
                else DontDestroyOnLoad(go);
                instance = go.AddComponent<OnlineManager>();
                return instance;
            }
        }

        /// <summary>Var olan örnek (yoksa oluşturmaz — OnDestroy gibi yerlerde güvenli).</summary>
        public static OnlineManager Existing => instance;
        public static bool InRoom => PhotonNetwork.InRoom;
        public static bool IsMaster => PhotonNetwork.IsMasterClient;

        public event Action<string> Status;      // kullanıcıya gösterilecek durum
        public event Action ReadyForRooms;      // bağlandı, oda kurulabilir
        public event Action RoomChanged;        // odaya girildi / oyuncu / özellik değişti
        public event Action LeftRoomEvent;

        bool wantConnect;
        int createAttempts;
        string pendingName;
        Difficulty pendingDifficulty;
        bool starting;

        public bool IsReady => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer;

        // ------------------------------------------------------------------
        public void Connect()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            if (PhotonNetwork.IsConnected)
            {
                if (IsReady) ReadyForRooms?.Invoke();
                return;
            }
            wantConnect = true;
            Status?.Invoke("Sunucuya bağlanılıyor...");

            // Ayar dosyasını değiştirmeden kopya üzerinden bağlan (editörde asset kirlenmesin)
            var settings = new AppSettings();
            PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(settings);
            if (string.IsNullOrEmpty(settings.FixedRegion)) settings.FixedRegion = DefaultRegion;
            if (string.IsNullOrEmpty(settings.AppVersion)) settings.AppVersion = "cozy-" + Application.version;
            PhotonNetwork.ConnectUsingSettings(settings);
        }

        public void Disconnect()
        {
            wantConnect = false;
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        }

        public void Host(string playerName, Difficulty difficulty)
        {
            if (!IsReady) { Connect(); return; }
            pendingName = playerName;
            pendingDifficulty = difficulty;
            createAttempts = 0;
            PhotonNetwork.NickName = playerName;
            CreateRoomWithRandomCode();
        }

        void CreateRoomWithRandomCode()
        {
            createAttempts++;
            string code = UnityEngine.Random.Range(10000, 100000).ToString();
            var options = new RoomOptions
            {
                MaxPlayers = MaxPlayers,
                IsVisible = false, // sadece kodla katılınır
                IsOpen = true,
                CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable
                {
                    { NetKeys.Difficulty, (int)pendingDifficulty },
                    { NetKeys.State, NetKeys.StateLobby },
                    { NetKeys.GameId, 0 }
                }
            };
            PhotonNetwork.CreateRoom(code, options);
            Status?.Invoke("Oda kuruluyor...");
        }

        public void Join(string playerName, string code)
        {
            if (!IsReady) { Connect(); return; }
            PhotonNetwork.NickName = playerName;
            PhotonNetwork.JoinRoom(code.Trim());
            Status?.Invoke("Odaya katılınıyor...");
        }

        public void Leave()
        {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        }

        public static Difficulty RoomDifficulty
        {
            get
            {
                var room = PhotonNetwork.CurrentRoom;
                if (room != null && room.CustomProperties.TryGetValue(NetKeys.Difficulty, out var v) && v is int i) return DifficultyInfo.Clamp(i);
                return Difficulty.Medium;
            }
        }

        public static int RoomState
        {
            get
            {
                var room = PhotonNetwork.CurrentRoom;
                if (room != null && room.CustomProperties.TryGetValue(NetKeys.State, out var v) && v is int i) return i;
                return NetKeys.StateLobby;
            }
        }

        public static int RoomGameId
        {
            get
            {
                var room = PhotonNetwork.CurrentRoom;
                if (room != null && room.CustomProperties.TryGetValue(NetKeys.GameId, out var v) && v is int i) return i;
                return 0;
            }
        }

        public void SetDifficulty(Difficulty d)
        {
            if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.Difficulty, (int)d } });
        }

        /// <summary>Kurucu: bulmacayı arka planda üretir ve odaya yayınlar; herkes oyuna geçer.</summary>
        public void StartGame()
        {
            if (!PhotonNetwork.IsMasterClient || starting) return;
            StartCoroutine(StartGameRoutine());
        }

        IEnumerator StartGameRoutine()
        {
            starting = true;
            var difficulty = RoomDifficulty;
            var task = GameSession.GenerateAsync(difficulty);
            while (!task.IsCompleted) yield return null;
            starting = false;
            if (task.IsFaulted || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient) yield break;

            var room = PhotonNetwork.CurrentRoom;
            room.IsOpen = false;
            room.SetCustomProperties(new Hashtable
            {
                { NetKeys.Givens, task.Result.Givens },
                { NetKeys.Solution, task.Result.Solution },
                { NetKeys.State, NetKeys.StatePlaying },
                { NetKeys.GameId, RoomGameId + 1 }
            });
        }

        /// <summary>Kurucu: herkesi lobiye geri götürür (yeni oyun için).</summary>
        public void ReturnEveryoneToLobby()
        {
            if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
            PhotonNetwork.CurrentRoom.IsOpen = true;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.State, NetKeys.StateLobby } });
        }

        public static void SetLocalProgress(int gameId, int filled, int mistakes)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { NetKeys.PGame, gameId }, { NetKeys.PFilled, filled }, { NetKeys.PMistakes, mistakes }
            });
        }

        public static void ResetLocalProgress(int gameId)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { NetKeys.PGame, gameId }, { NetKeys.PFilled, 0 }, { NetKeys.PMistakes, 0 },
                { NetKeys.PTime, -1f }, { NetKeys.PDnf, false }
            });
        }

        public static void SetLocalFinished(int gameId, bool dnf, float time, int filled, int mistakes)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { NetKeys.PGame, gameId }, { NetKeys.PFilled, filled }, { NetKeys.PMistakes, mistakes },
                { NetKeys.PTime, dnf ? -1f : time }, { NetKeys.PDnf, dnf }
            });
        }

        static void LoadPuzzleFromRoom()
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (props.TryGetValue(NetKeys.Givens, out var g) && props.TryGetValue(NetKeys.Solution, out var s) && g is byte[] gb && s is byte[] sb)
                GameSession.SetPuzzle(gb, sb);
            GameSession.Difficulty = RoomDifficulty;
            GameSession.Mode = GameMode.Online;
        }

        // ------------------------------------------------------------------
        // Photon geri çağrıları
        // ------------------------------------------------------------------

        public override void OnConnectedToMaster()
        {
            Status?.Invoke("");
            ReadyForRooms?.Invoke();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            bool expected = !wantConnect || cause == DisconnectCause.DisconnectByClientLogic;
            wantConnect = false;
            if (!expected) Status?.Invoke("Bağlantı koptu: " + CauseText(cause));
            LeftRoomEvent?.Invoke();

            // Online oyundaysak menüye dön
            if (!expected && GameSession.Mode == GameMode.Online && SceneManager.GetActiveScene().name == GameScene)
                SceneTransition.LoadScene(MenuScene);
        }

        static string CauseText(DisconnectCause cause)
        {
            switch (cause)
            {
                case DisconnectCause.DnsExceptionOnConnect:
                case DisconnectCause.ExceptionOnConnect:
                case DisconnectCause.ServerAddressInvalid:
                    return "İnternet bağlantını kontrol et.";
                case DisconnectCause.ClientTimeout:
                case DisconnectCause.ServerTimeout:
                    return "Zaman aşımı.";
                case DisconnectCause.MaxCcuReached:
                    return "Sunucu dolu, biraz sonra dene.";
                case DisconnectCause.InvalidAuthentication:
                    return "Photon AppId geçersiz.";
                default:
                    return cause.ToString();
            }
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (returnCode == ErrorCode.GameIdAlreadyExists && createAttempts < 5)
            {
                CreateRoomWithRandomCode();
                return;
            }
            Status?.Invoke("Oda kurulamadı: " + message);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            switch (returnCode)
            {
                case ErrorCode.GameDoesNotExist: Status?.Invoke("Bu kodla bir oda bulunamadı."); break;
                case ErrorCode.GameFull: Status?.Invoke("Oda dolu (en fazla 4 kişi)."); break;
                case ErrorCode.GameClosed: Status?.Invoke("Bu odada oyun başlamış, biraz bekle."); break;
                default: Status?.Invoke("Katılınamadı: " + message); break;
            }
        }

        public override void OnJoinedRoom()
        {
            Status?.Invoke("");
            ResetLocalProgress(-1);
            RoomChanged?.Invoke();
        }

        public override void OnLeftRoom()
        {
            LeftRoomEvent?.Invoke();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer) => RoomChanged?.Invoke();
        public override void OnPlayerLeftRoom(Player otherPlayer) => RoomChanged?.Invoke();
        public override void OnMasterClientSwitched(Player newMasterClient) => RoomChanged?.Invoke();
        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) => RoomChanged?.Invoke();

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            RoomChanged?.Invoke();
            string active = SceneManager.GetActiveScene().name;

            // Yeni oyun başladı → herkes (menüde ya da önceki oyunun sonuç ekranında) oyuna geçer
            if (changed.ContainsKey(NetKeys.GameId) && RoomState == NetKeys.StatePlaying)
            {
                LoadPuzzleFromRoom();
                if (GameSession.HasPuzzle) SceneTransition.LoadScene(GameScene);
                return;
            }

            // Kurucu herkesi lobiye çağırdı
            if (changed.ContainsKey(NetKeys.State) && RoomState == NetKeys.StateLobby && active == GameScene)
                SceneTransition.LoadScene(MenuScene);
        }
    }
}
