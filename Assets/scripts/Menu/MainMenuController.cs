using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CozySudoku
{
    /// <summary>
    /// Cozy ana menü: Tek oyuncu, aynı cihazda 2 kişi, online (oda kur / katıl / lobi) ve ayarlar.
    /// Zorluk (Kolay / Orta / Zor) her modda menüden seçilir; online'da kurucu seçer.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        enum Page { Home, Difficulty, Online, Lobby }
        enum DifficultyTarget { Solo, Local, Host }

        Canvas canvas;
        RectTransform safe;
        readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();
        Page current = Page.Home;
        DifficultyTarget diffTarget;

        // Online
        TMP_InputField nameInput, codeInput;
        TextMeshProUGUI onlineStatus;
        CozyButton hostBtn, joinBtn;
        // Lobi
        TextMeshProUGUI roomCodeText, lobbyInfo;
        RectTransform playerList;
        readonly CozyButton[] lobbyDiffButtons = new CozyButton[3];
        CozyButton startBtn;
        TextMeshProUGUI difficultyTitle;
        readonly TextMeshProUGUI[] bestTimes = new TextMeshProUGUI[3];

        OnlineManager net;

        void Start()
        {
            canvas = UI.Canvas("Menu Canvas", 0, transform);
            UI.Background(canvas.transform);
            FloatingDigits.Create(canvas.transform);
            safe = UI.Rect("SafeArea", canvas.transform).Stretch();
            safe.gameObject.AddComponent<SafeArea>();

            pages[Page.Home] = BuildHome();
            pages[Page.Difficulty] = BuildDifficulty();
            pages[Page.Online] = BuildOnline();
            pages[Page.Lobby] = BuildLobby();

            net = OnlineManager.Instance;
            net.Status += OnStatus;
            net.ReadyForRooms += OnReady;
            net.RoomChanged += OnRoomChanged;
            net.LeftRoomEvent += OnLeftRoom;

            foreach (var kv in pages) kv.Value.gameObject.SetActive(false);
            if (PhotonNetwork.InRoom) { Show(Page.Lobby, false); OnRoomChanged(); }
            else Show(Page.Home, false);

            SceneTransition.PlayIntro();
        }

        void OnDestroy()
        {
            if (net == null) return;
            net.Status -= OnStatus;
            net.ReadyForRooms -= OnReady;
            net.RoomChanged -= OnRoomChanged;
            net.LeftRoomEvent -= OnLeftRoom;
        }

        // ------------------------------------------------------------------
        // Sayfa iskeleti
        // ------------------------------------------------------------------

        RectTransform NewPage(string name, out RectTransform column, float width = 900f)
        {
            var page = UI.Rect(name, safe).Stretch();
            column = UI.Rect("Column", page);
            column.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0));
            UI.VStack(column, 26, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            page.gameObject.AddComponent<FitInParent>().Target = column;
            page.gameObject.AddComponent<CanvasGroup>();
            return page;
        }

        RectTransform Card(Transform parent, string title, string ribbon, out RectTransform body)
        {
            var card = UI.Image(parent, "panel_cream", null, 1f, "Card");
            UI.VStack(card.rectTransform, 24, new RectOffset(56, 56, 120, 70), TextAnchor.UpperCenter);
            body = card.rectTransform;

            var rib = UI.Image(card.transform, "ribbon_" + ribbon, null, 1.4f, "Ribbon");
            rib.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rib.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0, 14), new Vector2(980, 150));
            var t = UI.Text(rib.transform, title, 66, Color.white, FontKind.Text, TextStyle.Title);
            t.rectTransform.Stretch(70, 70, 10, 40);
            t.AutoSize(30, 66);
            if (ribbon == "cream" || ribbon == "yellow") t.color = CozyTheme.Ink;
            return card.rectTransform;
        }

        CozyButton BackButton(RectTransform page, Action onBack)
        {
            var b = UI.IconButton(page, "cream", "arrow_left", onBack, 0.5f);
            b.ClickSound = Sound.Pop;
            ((RectTransform)b.transform).Anchor(new Vector2(0, 1), new Vector2(36, -30), new Vector2(120, 136), new Vector2(0, 1));
            return b;
        }

        void Show(Page page, bool animate = true)
        {
            Dialog.Close(false);
            var from = pages[current];
            var to = pages[page];
            if (animate && from != to)
            {
                var fg = from.GetComponent<CanvasGroup>();
                Tween.Fade(fg, 0f, 0.15f, Ease.OutQuad, () => from.gameObject.SetActive(false));
                Tween.Scale(from, new Vector3(0.94f, 0.94f, 1f), 0.15f, Ease.OutQuad);
            }
            else if (from != to) from.gameObject.SetActive(false);

            current = page;
            to.gameObject.SetActive(true);
            var tg = to.GetComponent<CanvasGroup>();
            tg.alpha = animate ? 0f : 1f;
            to.localScale = Vector3.one;
            if (animate)
            {
                Tween.Fade(tg, 1f, 0.25f, Ease.OutQuad, null, 0.08f);
                Tween.ScaleFrom(to, new Vector3(1.06f, 1.06f, 1f), Vector3.one, 0.35f, Ease.OutBack, null, 0.08f);
            }
        }

        // ------------------------------------------------------------------
        // Ana sayfa
        // ------------------------------------------------------------------

        RectTransform BuildHome()
        {
            var page = NewPage("Home", out var col, 860f);

            // Başlık: harfler renkli karolarda, hafifçe sallanır
            var title = UI.Rect("Title", col);
            title.Layout(-1, 230);
            var tl = UI.HStack(title, 14, null, TextAnchor.MiddleCenter);
            tl.childForceExpandWidth = false;
            string word = "SUDOKU";
            string[] colors = { "pink", "yellow", "blue", "green", "pink", "yellow" };
            var bob = title.gameObject.AddComponent<Bobbing>();
            for (int i = 0; i < word.Length; i++)
            {
                var holder = UI.Rect("Letter", title);
                holder.Layout(124, 150);
                var tile = UI.Image(holder, "btn_square_" + colors[i], null, 1.2f, "Tile");
                tile.rectTransform.Stretch();
                var letter = UI.Text(tile.transform, word[i].ToString(), 96, Color.white, FontKind.Digits, TextStyle.Title);
                letter.rectTransform.Stretch(0, 0, 0, 18);
                bob.Add(tile.rectTransform, i * 0.55f);
                Tween.ScaleFrom(tile.transform, Vector3.zero, Vector3.one, 0.5f, Ease.OutBack, null, 0.25f + i * 0.07f);
            }

            var tag = UI.Text(col, "huzurlu bir bulmaca molası", 46, CozyTheme.InkSoft, FontKind.Text, TextStyle.Soft);
            tag.Layout(-1, 70);
            UI.Spacer(col, 10);

            MenuButton(col, "Tek Oyuncu", "blue", "icon_book", () => OpenDifficulty(DifficultyTarget.Solo));
            MenuButton(col, AppBootstrap.IsMobile ? "2 Kişi • Karşılıklı" : "2 Kişi • Yan Yana", "pink", "icon_people", () => OpenDifficulty(DifficultyTarget.Local));
            MenuButton(col, "Online Oyna", "green", "icon_letter", () => { Show(Page.Online); net.Connect(); RefreshOnlineButtons(); });

            var row = UI.Rect("Row", col);
            row.Layout(-1, 140);
            UI.HStack(row, 40, null, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            var settings = UI.IconButton(row, "yellow", "icon_gear", OpenSettings);
            settings.Layout(124, 140);
            var help = UI.IconButton(row, "turquoise", "icon_question", ShowHelp);
            help.Layout(124, 140);
#if !UNITY_IOS && !UNITY_ANDROID && !UNITY_WEBGL
            var quit = UI.IconButton(row, "red", "icon_close", AskQuit);
            quit.Layout(124, 140);
#endif
            return page;
        }

        CozyButton MenuButton(Transform parent, string label, string color, string icon, Action click)
        {
            var b = UI.Button(parent, label, color, click, icon, 60);
            b.Layout(-1, 150);
            return b;
        }

        // ------------------------------------------------------------------
        // Zorluk seçimi
        // ------------------------------------------------------------------

        RectTransform BuildDifficulty()
        {
            var page = NewPage("Difficulty", out var col, 860f);
            var card = Card(col, "Zorluk Seç", "yellow", out var body);
            difficultyTitle = UI.Text(body, "", 40, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain);
            difficultyTitle.Layout(-1, 56);

            string[] colors = { "green", "yellow", "red" };
            for (int i = 0; i < 3; i++)
            {
                var d = (Difficulty)i;
                var img = UI.Image(body, "btn_pill_" + colors[i], null, 1f, "Diff " + d);
                img.raycastTarget = true;
                img.Layout(-1, 170);
                var btn = img.gameObject.AddComponent<CozyButton>();
                btn.Init(img, () => OnDifficultyPicked(d));

                var row = UI.Rect("Row", img.transform).Stretch(50, 50, 6, 24);
                var h = UI.HStack(row, 24, null, TextAnchor.MiddleLeft);
                h.childForceExpandWidth = false;

                var stars = UI.Rect("Stars", row);
                UI.HStack(stars, 0).childForceExpandWidth = false;
                for (int s = 0; s < 3; s++)
                    UI.Image(stars, "icon_star", s <= i ? Color.white : new Color(1, 1, 1, 0.28f), 1f, "Star").Layout(58, 58);

                var texts = UI.Rect("Texts", row);
                texts.Layout(-1, -1, 1, -1);
                var v = UI.VStack(texts, 0, null, TextAnchor.MiddleLeft);
                v.childForceExpandWidth = true;
                bool light = i == 1;
                var name = UI.Text(texts, DifficultyInfo.Name(d), 62, light ? CozyTheme.Ink : Color.white, FontKind.Text, light ? TextStyle.Soft : TextStyle.Title, TextAlignmentOptions.Left);
                name.Layout(-1, 72);
                bestTimes[i] = UI.Text(texts, "", 32, light ? CozyTheme.InkSoft : new Color(1, 1, 1, 0.9f), FontKind.Text, TextStyle.Plain, TextAlignmentOptions.Left);
                bestTimes[i].Layout(-1, 40);
            }

            BackButton(page, Back);
            return page;
        }

        void OpenDifficulty(DifficultyTarget target)
        {
            diffTarget = target;
            switch (target)
            {
                case DifficultyTarget.Solo: difficultyTitle.text = "Tek oyuncu"; break;
                case DifficultyTarget.Local: difficultyTitle.text = AppBootstrap.IsMobile ? "2 kişi • telefonu aranıza koyun" : "2 kişi • aynı klavye ve fare"; break;
                default: difficultyTitle.text = "Oda kuruluyor • zorluğu sen seç"; break;
            }
            for (int i = 0; i < 3; i++)
            {
                float best = PlayerPrefs.GetFloat("cs_best_" + i, float.MaxValue);
                string clues = $"{81 - DifficultyInfo.CellsToRemove((Difficulty)i)} ipucu";
                bestTimes[i].text = best < float.MaxValue && target == DifficultyTarget.Solo ? $"{clues}  •  en iyi {BoardView.FormatTime(best)}" : clues;
            }
            Show(Page.Difficulty);
        }

        void OnDifficultyPicked(Difficulty d)
        {
            switch (diffTarget)
            {
                case DifficultyTarget.Solo: GameFlow.StartOffline(GameMode.Solo, d); break;
                case DifficultyTarget.Local: GameFlow.StartOffline(GameMode.LocalVersus, d); break;
                case DifficultyTarget.Host:
                    Settings.LastDifficulty = d;
                    net.Host(PlayerNameOrDefault(), d);
                    Show(Page.Online);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Online
        // ------------------------------------------------------------------

        RectTransform BuildOnline()
        {
            var page = NewPage("Online", out var col, 860f);
            var card = Card(col, "Online", "green", out var body);

            UI.Text(body, "Adın", 38, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain).Layout(-1, 46);
            nameInput = UI.Input(body, "İsmini yaz", TMP_InputField.ContentType.Standard, 14);
            nameInput.Layout(-1, 130);
            nameInput.text = PlayerNameOrDefault();
            nameInput.onEndEdit.AddListener(v => { Settings.PlayerName = v.Trim(); Settings.Save(); });

            UI.Spacer(body, 6);
            hostBtn = UI.Button(body, "Oda Kur", "green", OnHostClicked, "icon_flat_plus", 56);
            hostBtn.Layout(-1, 140);

            var sepRow = UI.Rect("Or", body);
            sepRow.Layout(-1, 50);
            UI.Text(sepRow, "— ya da bir odaya katıl —", 34, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain).rectTransform.Stretch();

            var joinRow = UI.Rect("JoinRow", body);
            joinRow.Layout(-1, 135);
            var h = UI.HStack(joinRow, 20, null, TextAnchor.MiddleCenter, true);
            codeInput = UI.Input(joinRow, "Oda kodu", TMP_InputField.ContentType.IntegerNumber, 5, 56);
            codeInput.Layout(-1, 130, 1.2f, -1);
            joinBtn = UI.Button(joinRow, "Katıl", "blue", OnJoinClicked, null, 56);
            joinBtn.Layout(-1, 135, 1f, -1);

            onlineStatus = UI.Text(body, "", 36, CozyTheme.Red, FontKind.Text, TextStyle.Plain);
            onlineStatus.textWrappingMode = TextWrappingModes.Normal;
            onlineStatus.Layout(-1, 90);

            BackButton(page, Back);
            return page;
        }

        string PlayerNameOrDefault()
        {
            string n = nameInput != null ? nameInput.text.Trim() : Settings.PlayerName;
            if (string.IsNullOrEmpty(n)) n = Settings.PlayerName;
            if (string.IsNullOrEmpty(n))
            {
                n = "Oyuncu" + UnityEngine.Random.Range(100, 1000);
                Settings.PlayerName = n;
            }
            return n;
        }

        void OnHostClicked()
        {
            if (!ValidateName()) return;
            OpenDifficulty(DifficultyTarget.Host);
        }

        void OnJoinClicked()
        {
            if (!ValidateName()) return;
            string code = codeInput.text.Trim();
            if (code.Length != 5)
            {
                OnStatus("Oda kodu 5 haneli olmalı.");
                Tween.Shake((RectTransform)codeInput.transform);
                return;
            }
            net.Join(PlayerNameOrDefault(), code);
        }

        bool ValidateName()
        {
            string n = nameInput.text.Trim();
            if (n.Length < 2)
            {
                OnStatus("Lütfen en az 2 harfli bir isim gir.");
                Tween.Shake((RectTransform)nameInput.transform);
                return false;
            }
            Settings.PlayerName = n;
            Settings.Save();
            return true;
        }

        void RefreshOnlineButtons()
        {
            bool ready = net != null && net.IsReady;
            if (hostBtn != null) hostBtn.Interactable = ready;
            if (joinBtn != null) joinBtn.Interactable = ready;
        }

        void OnStatus(string msg)
        {
            if (onlineStatus == null) return;
            onlineStatus.text = msg;
            onlineStatus.color = msg.EndsWith("...") ? CozyTheme.InkSoft : CozyTheme.Red;
            if (!string.IsNullOrEmpty(msg)) Tween.Punch(onlineStatus.transform, 0.06f, 0.2f);
            RefreshOnlineButtons();
        }

        void OnReady()
        {
            RefreshOnlineButtons();
            if (onlineStatus != null && onlineStatus.text.StartsWith("Sunucuya")) onlineStatus.text = "";
        }

        // ------------------------------------------------------------------
        // Lobi
        // ------------------------------------------------------------------

        RectTransform BuildLobby()
        {
            var page = NewPage("Lobby", out var col, 860f);
            var card = Card(col, "Oda", "blue", out var body);

            var codeRow = UI.Rect("CodeRow", body);
            codeRow.Layout(-1, 130);
            var h = UI.HStack(codeRow, 20, null, TextAnchor.MiddleCenter);
            h.childForceExpandWidth = false;
            var codeBg = UI.Image(codeRow, "slot_blue", null, 1.2f, "CodeBg");
            codeBg.Layout(420, 125);
            roomCodeText = UI.Text(codeBg.transform, "-----", 74, CozyTheme.Ink, FontKind.Digits, TextStyle.Soft);
            roomCodeText.rectTransform.Stretch(10, 10, 6, 20);
            roomCodeText.characterSpacing = 12;
            var copy = UI.Button(codeRow, "Kopyala", "yellow", CopyCode, null, 40);
            copy.Layout(250, 120);

            UI.Text(body, "Kodu arkadaşlarınla paylaş (en fazla 4 kişi)", 32, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain).Layout(-1, 44);

            playerList = UI.Rect("Players", body);
            UI.VStack(playerList, 12, null, TextAnchor.UpperCenter);
            playerList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UI.Text(body, "Zorluk", 38, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain).Layout(-1, 46);
            var diffRow = UI.Rect("DiffRow", body);
            diffRow.Layout(-1, 120);
            UI.HStack(diffRow, 16, null, TextAnchor.MiddleCenter, true);
            for (int i = 0; i < 3; i++)
            {
                var d = (Difficulty)i;
                var b = UI.Button(diffRow, DifficultyInfo.Name(d), "cream", () => net.SetDifficulty(d), null, 44);
                b.Layout(-1, 115, 1, -1);
                lobbyDiffButtons[i] = b;
            }

            lobbyInfo = UI.Text(body, "", 36, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain);
            lobbyInfo.textWrappingMode = TextWrappingModes.Normal;
            lobbyInfo.Layout(-1, 56);

            startBtn = UI.Button(body, "Oyunu Başlat", "green", () => { startBtn.Interactable = false; net.StartGame(); }, "icon_flat_check", 56);
            startBtn.Layout(-1, 145);

            var leave = UI.Button(body, "Odadan Çık", "cream", AskLeaveRoom, "icon_home", 44);
            leave.Layout(-1, 115);
            return page;
        }

        void OnRoomChanged()
        {
            if (this == null || !PhotonNetwork.InRoom) return;
            if (current != Page.Lobby) Show(Page.Lobby);

            var room = PhotonNetwork.CurrentRoom;
            roomCodeText.text = room.Name;
            bool master = PhotonNetwork.IsMasterClient;
            var diff = OnlineManager.RoomDifficulty;
            bool playing = OnlineManager.RoomState == NetKeys.StatePlaying;

            for (int i = 0; i < 3; i++)
            {
                var b = lobbyDiffButtons[i];
                bool sel = i == (int)diff;
                string color = i == 0 ? "green" : i == 1 ? "yellow" : "red";
                b.SetSprite(sel ? "btn_pill_" + color : "btn_pill_cream");
                b.Label.color = sel && i != 1 ? Color.white : CozyTheme.Ink;
                b.Label.fontSharedMaterial = CozyTheme.Material(b.Label.font, sel && i != 1 ? TextStyle.Title : TextStyle.Soft);
                b.Interactable = master || sel;
                b.enabled = master;
            }

            startBtn.gameObject.SetActive(master);
            startBtn.Interactable = master && !playing;
            if (playing) lobbyInfo.text = "Oyun sürüyor... yeni oyun başlayınca otomatik katılacaksın.";
            else if (master) lobbyInfo.text = room.PlayerCount < 2 ? "Arkadaşların bekleniyor... (tek başına da başlatabilirsin)" : "Herkes hazırsa başlat!";
            else lobbyInfo.text = "Kurucunun oyunu başlatması bekleniyor...";

            RebuildPlayers();
        }

        void RebuildPlayers()
        {
            for (int i = playerList.childCount - 1; i >= 0; i--) Destroy(playerList.GetChild(i).gameObject);
            var players = PhotonNetwork.PlayerList;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                var row = UI.Image(playerList, "slot", null, 1.6f, "Player");
                row.Layout(-1, 96);
                var h = UI.HStack(row.rectTransform, 18, new RectOffset(30, 30, 6, 16), TextAnchor.MiddleLeft);
                h.childForceExpandWidth = false;
                var dot = UI.Image(row.transform, "btn_round_" + CozyTheme.PlayerSpriteColors[i % 4], null, 1f, "Dot");
                dot.Layout(54, 60);
                var name = UI.Text(row.transform, p.NickName, 44, CozyTheme.Ink, FontKind.Text, TextStyle.Soft, TextAlignmentOptions.Left);
                name.Layout(-1, 60, 1, -1);
                name.overflowMode = TextOverflowModes.Ellipsis;
                if (p.IsMasterClient) UI.Text(row.transform, "Kurucu", 32, CozyTheme.Teal, FontKind.Text, TextStyle.Plain).Layout(140, 60);
                if (p.IsLocal) UI.Text(row.transform, "Sen", 32, CozyTheme.Pink, FontKind.Text, TextStyle.Plain).Layout(80, 60);
            }
        }

        void CopyCode()
        {
            if (!PhotonNetwork.InRoom) return;
            GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
            Tween.Punch(roomCodeText.transform, 0.2f, 0.3f);
            lobbyInfo.text = "Kod kopyalandı!";
        }

        void AskLeaveRoom()
        {
            Dialog.Show(canvas.transform, "Odadan Çık", "Odadan ayrılmak istiyor musun?", null,
                new ButtonSpec("Kal", "cream", null),
                new ButtonSpec("Çık", "red", () => net.Leave()));
        }

        void OnLeftRoom()
        {
            if (this == null) return;
            if (current == Page.Lobby) Show(Page.Online);
            RefreshOnlineButtons();
        }

        // ------------------------------------------------------------------
        // Ayarlar / yardım / çıkış
        // ------------------------------------------------------------------

        void OpenSettings()
        {
            var root = UI.Rect("SettingsDialog", canvas.transform).Stretch();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            Tween.Fade(group, 1, 0.2f);
            var dim = UI.Rounded(root, CozyTheme.Ink.WithAlpha(0.45f), 0.3f, "Dim");
            dim.rectTransform.Stretch(-50, -50, -50, -50);
            dim.raycastTarget = true;

            var holder = UI.Rect("Holder", root).Stretch();
            var column = UI.Rect("Column", holder);
            column.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 0));
            UI.VStack(column, 0, null, TextAnchor.UpperCenter);
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            holder.gameObject.AddComponent<FitInParent>().Target = column;

            Card(column, "Ayarlar", "pink", out var body);
            SettingRow(body, "icon_sound_on", "Ses", Settings.Sound, v => Settings.Sound = v);
            if (AppBootstrap.IsMobile) SettingRow(body, "icon_bell", "Titreşim", Settings.Haptics, v => Settings.Haptics = v);
            SettingRow(body, "icon_star", "Aynı sayıları vurgula", Settings.HighlightSame, v => Settings.HighlightSame = v);
            SettingRow(body, "icon_pencil", "Akıllı not (çakışanı engelle)", Settings.SmartNotes, v => Settings.SmartNotes = v);

            UI.Spacer(body, 10);
            var close = UI.Button(body, "Tamam", "green", () =>
            {
                Settings.Save();
                Tween.Fade(group, 0, 0.15f, Ease.OutQuad, () => Destroy(root.gameObject));
            }, "icon_flat_check", 52);
            close.Layout(-1, 135);
            Tween.ScaleFrom(column, new Vector3(0.7f, 0.7f, 1), Vector3.one, 0.4f, Ease.OutBack);
        }

        void SettingRow(Transform parent, string icon, string label, bool value, Action<bool> changed)
        {
            var row = UI.Rect("Setting", parent);
            row.Layout(-1, 110);
            var h = UI.HStack(row, 20, null, TextAnchor.MiddleLeft);
            h.childForceExpandWidth = false;
            UI.Image(row, icon, null, 1f, "Icon").Layout(70, 70);
            var t = UI.Text(row, label, 42, CozyTheme.Ink, FontKind.Text, TextStyle.Soft, TextAlignmentOptions.Left);
            t.Layout(-1, 70, 1, -1);
            t.AutoSize(26, 42);
            CozyToggle.Create(row, value, changed).Layout(170, 86);
        }

        void ShowHelp()
        {
            Dialog.Show(canvas.transform, "Nasıl Oynanır?",
                "Her satır, sütun ve 3x3 kutuya 1-9 arası sayıları birer kez yerleştir.\n" +
                "<color=#4C93C9>Not</color> moduyla aday sayıları işaretle. 3 hatada oyun biter.\n\n" +
                "<size=80%>2 kişilik modda ilk bitiren kazanır. Online'da en hızlı bitiren birinci olur.</size>",
                null, new ButtonSpec("Anladım", "green", null));
        }

        void AskQuit()
        {
            Dialog.Show(canvas.transform, "Çıkış", "Oyundan çıkmak istiyor musun?", null,
                new ButtonSpec("Kal", "cream", null),
                new ButtonSpec("Çık", "red", Application.Quit));
        }

        void Back()
        {
            switch (current)
            {
                case Page.Difficulty:
                    Show(diffTarget == DifficultyTarget.Host ? Page.Online : Page.Home);
                    break;
                case Page.Online:
                    net.Disconnect(); // menüde boşuna bağlı kalma (pil/veri)
                    Show(Page.Home);
                    break;
                case Page.Lobby:
                    AskLeaveRoom();
                    break;
                default:
#if !UNITY_IOS
                    AskQuit();
#endif
                    break;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                var settings = canvas.transform.Find("SettingsDialog");
                if (Dialog.IsOpen) Dialog.Close(true);
                else if (settings != null) Destroy(settings.gameObject);
                else Back();
            }
        }
    }

    /// <summary>İçerik ekrana sığmıyorsa (yatay telefon vb.) orantılı küçültür.</summary>
    public sealed class FitInParent : MonoBehaviour
    {
        public RectTransform Target;
        public float Margin = 40f;
        RectTransform rt;
        Vector2 lastParent, lastTarget;

        void Awake() => rt = (RectTransform)transform;

        void LateUpdate()
        {
            if (Target == null) return;
            Vector2 p = rt.rect.size, t = Target.rect.size;
            if (p == lastParent && t == lastTarget) return;
            lastParent = p; lastTarget = t;
            float s = Mathf.Min(1f, (p.x - Margin) / Mathf.Max(1f, t.x), (p.y - Margin * 2f) / Mathf.Max(1f, t.y));
            Target.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Başlık harflerinin yumuşak sallanması.</summary>
    public sealed class Bobbing : MonoBehaviour
    {
        readonly List<RectTransform> items = new List<RectTransform>();
        readonly List<float> phases = new List<float>();

        public void Add(RectTransform r, float phase) { items.Add(r); phases.Add(phase); }

        void Update()
        {
            float t = Time.unscaledTime * 2.2f;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                items[i].anchoredPosition = new Vector2(0, Mathf.Sin(t + phases[i]) * 8f);
                items[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.7f + phases[i]) * 3f);
            }
        }
    }

    /// <summary>Menü arka planında yavaşça süzülen soluk sayılar (ayrı alt canvas → ana UI yeniden çizilmez).</summary>
    public sealed class FloatingDigits : MonoBehaviour
    {
        struct Digit { public RectTransform Rt; public float Speed, Sway, Phase, X; }
        readonly List<Digit> digits = new List<Digit>();
        RectTransform rt;

        public static FloatingDigits Create(Transform parent)
        {
            var layer = UI.Rect("FloatingDigits", parent).Stretch();
            var sub = layer.gameObject.AddComponent<Canvas>();
            sub.overrideSorting = false;
            var f = layer.gameObject.AddComponent<FloatingDigits>();
            f.rt = layer;
            var colors = new[] { CozyTheme.Pink, CozyTheme.Blue, CozyTheme.Green, CozyTheme.Yellow, CozyTheme.InkSoft };
            for (int i = 0; i < 16; i++)
            {
                var t = UI.Text(layer, ((i * 7) % 9 + 1).ToString(), UnityEngine.Random.Range(70f, 150f), colors[i % colors.Length].WithAlpha(0.28f), FontKind.Digits, TextStyle.Plain);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 0);
                t.rectTransform.sizeDelta = new Vector2(160, 160);
                f.digits.Add(new Digit
                {
                    Rt = t.rectTransform,
                    Speed = UnityEngine.Random.Range(18f, 42f),
                    Sway = UnityEngine.Random.Range(10f, 30f),
                    Phase = UnityEngine.Random.Range(0f, 10f),
                    X = UnityEngine.Random.value
                });
                t.rectTransform.anchoredPosition = new Vector2(-500, -500);
            }
            return f;
        }

        void Update()
        {
            Vector2 size = rt.rect.size;
            if (size.x < 10f) return;
            float time = Time.unscaledTime;
            float h = size.y + 320f;
            for (int i = 0; i < digits.Count; i++)
            {
                var d = digits[i];
                float y = ((time + d.Phase * 37f) * d.Speed) % h - 160f;
                float x = d.X * size.x + Mathf.Sin(time * 0.6f + d.Phase) * d.Sway;
                d.Rt.anchoredPosition = new Vector2(x, y);
                d.Rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 0.4f + d.Phase) * 15f);
            }
        }
    }
}
