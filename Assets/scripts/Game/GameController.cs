using System.Collections;
using System.Collections.Generic;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CozySudoku
{
    /// <summary>
    /// Oyun sahnesinin tamamını kurar ve yönetir:
    ///  • Solo: tek tahta
    ///  • Yerel 2 kişi: mobilde karşılıklı (üst yarı 180° ters), masaüstünde yan yana
    ///  • Online: tek tahta + rakiplerin canlı ilerlemesi + sıralama
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        Canvas canvas;
        RectTransform safeRoot;
        RectTransform contentRoot;
        readonly List<BoardView> boards = new List<BoardView>();
        readonly List<RectTransform> halves = new List<RectTransform>();
        RectTransform divider;
        bool sideBySide;
        Vector2Int lastScreen;
        bool gameOver;

        // online
        int gameId;
        RectTransform opponentsStrip;
        TextMeshProUGUI resultText;
        bool finalShown;
        bool subscribed;
        sealed class Chip { public int Actor; public Image Fill; public Image Badge; public int ColorIndex; }
        readonly List<Chip> chips = new List<Chip>();

        GameMode Mode => GameSession.Mode;

        void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (Mode == GameMode.Online && !PhotonNetwork.InRoom)
                GameSession.Mode = GameMode.Solo; // bağlantı yoksa güvenli varsayılan
            if (!GameSession.HasPuzzle)
                GameSession.GenerateNow(GameSession.Difficulty); // sahne doğrudan açıldıysa

            canvas = UI.Canvas("Game Canvas", 0, transform);
            UI.Background(canvas.transform);
            safeRoot = UI.Rect("SafeArea", canvas.transform).Stretch();
            safeRoot.gameObject.AddComponent<SafeArea>();

            switch (Mode)
            {
                case GameMode.LocalVersus: BuildLocalVersus(); break;
                case GameMode.Online: BuildOnline(); break;
                default: BuildSolo(); break;
            }

            lastScreen = new Vector2Int(Screen.width, Screen.height);
            StartCoroutine(Countdown());
        }

        void OnDestroy()
        {
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            if (subscribed && OnlineManager.Existing != null) OnlineManager.Existing.RoomChanged -= OnRoomChanged;
        }

        // ------------------------------------------------------------------
        // Kurulum
        // ------------------------------------------------------------------

        SudokuBoard NewBoard() => new SudokuBoard(GameSession.Givens, GameSession.Solution);

        RectTransform TopBar(Transform parent, float height)
        {
            var bar = UI.Rect("TopBar", parent);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0, height);
            bar.anchoredPosition = Vector2.zero;

            var home = UI.IconButton(bar, "cream", "icon_home", AskExit, 0.6f);
            home.ClickSound = Sound.Pop;
            ((RectTransform)home.transform).Anchor(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(height * 0.8f, height * 0.9f), new Vector2(0, 0.5f));

            // Zorluk etiketi: yıldızlar + isim
            var chip = UI.Image(bar, "label_cream", null, 1f, "DifficultyChip");
            ((RectTransform)chip.transform).Anchor(new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(340, height * 0.62f), new Vector2(1, 0.5f));
            var row = UI.Rect("Row", chip.transform).Stretch(30, 30, 0, 6);
            UI.HStack(row, 6);
            int stars = DifficultyInfo.Stars(GameSession.Difficulty);
            for (int i = 0; i < 3; i++)
                UI.Image(row, "icon_star", i < stars ? Color.white : new Color(1, 1, 1, 0.25f), 1f, "Star").Layout(48, 48);
            UI.Text(row, DifficultyInfo.Name(GameSession.Difficulty), 40, CozyTheme.Ink, FontKind.Text, TextStyle.Soft).Layout(-1, 50);
            return bar;
        }

        void BuildSolo()
        {
            const float barH = 120f;
            TopBar(safeRoot, barH);
            contentRoot = UI.Rect("Content", safeRoot).Stretch(0, 0, barH, 0);
            var board = BoardView.Create(contentRoot, NewBoard(), new BoardOptions { AllowHints = true, Keys = KeyScheme.All });
            board.Finished += OnSoloFinished;
            boards.Add(board);
        }

        void BuildLocalVersus()
        {
            bool desktop = !AppBootstrap.IsMobile;
            for (int p = 0; p < 2; p++)
            {
                var half = UI.Rect("Player " + (p + 1), safeRoot);
                var tint = UI.Rounded(half, CozyTheme.PlayerColors[p].WithAlpha(0.13f), 0.25f, "Tint");
                tint.rectTransform.Stretch(12, 12, 12, 12);
                halves.Add(half);

                var opts = new BoardOptions
                {
                    PlayerIndex = p,
                    ShowName = true,
                    PlayerName = "Oyuncu " + (p + 1),
                    Keys = desktop ? (p == 0 ? KeyScheme.Left : KeyScheme.Right) : KeyScheme.None,
                    KeyLegend = desktop ? (p == 0 ? "WASD hareket  •  1-9 sayı  •  Q not  •  E sil  •  Z geri al"
                                                  : "Oklar hareket  •  Numpad 1-9  •  + not  •  0 sil  •  - geri al") : null
                };
                var board = BoardView.Create(half, NewBoard(), opts);
                board.Finished += OnLocalFinished;
                boards.Add(board);
            }

            // Ortadaki ayraç + menü butonu
            divider = UI.Rect("Divider", safeRoot);
            var line = UI.Rounded(divider, CozyTheme.InkSoft.WithAlpha(0.35f), 0.2f, "Line");
            line.rectTransform.Stretch();
            var home = UI.IconButton(divider, "cream", "icon_home", AskExit, 0.6f);
            ((RectTransform)home.transform).Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 120));

            ApplySplitLayout();
        }

        /// <summary>Ekran oranına göre karşılıklı ya da yan yana yerleşim.</summary>
        void ApplySplitLayout()
        {
            if (halves.Count < 2) return;
            bool mobile = AppBootstrap.IsMobile;
            sideBySide = !mobile && Screen.width >= Screen.height;

            var p1 = halves[0];
            var p2 = halves[1];
            if (sideBySide)
            {
                p1.AnchorRange(new Vector2(0, 0), new Vector2(0.5f, 1));
                p2.AnchorRange(new Vector2(0.5f, 0), new Vector2(1, 1));
                p1.localRotation = p2.localRotation = Quaternion.identity;
                divider.anchorMin = new Vector2(0.5f, 0.06f); divider.anchorMax = new Vector2(0.5f, 0.94f);
                divider.sizeDelta = new Vector2(6, 0);
            }
            else
            {
                // Oyuncu 1 altta (cihazı tutan), Oyuncu 2 üstte ve ters → karşı karşıya oturanlar
                p1.AnchorRange(new Vector2(0, 0), new Vector2(1, 0.5f));
                p2.AnchorRange(new Vector2(0, 0.5f), new Vector2(1, 1));
                p1.localRotation = Quaternion.identity;
                p2.localRotation = mobile ? Quaternion.Euler(0, 0, 180f) : Quaternion.identity;
                divider.anchorMin = new Vector2(0.06f, 0.5f); divider.anchorMax = new Vector2(0.94f, 0.5f);
                divider.sizeDelta = new Vector2(0, 6);
            }
            divider.anchoredPosition = Vector2.zero;
            divider.SetAsLastSibling();
        }

        void BuildOnline()
        {
            gameId = OnlineManager.RoomGameId;
            OnlineManager.ResetLocalProgress(gameId);
            OnlineManager.Instance.RoomChanged += OnRoomChanged;
            subscribed = true;

            const float barH = 120f, stripH = 110f;
            TopBar(safeRoot, barH);
            opponentsStrip = UI.Rect("Opponents", safeRoot);
            opponentsStrip.anchorMin = new Vector2(0, 1); opponentsStrip.anchorMax = new Vector2(1, 1);
            opponentsStrip.pivot = new Vector2(0.5f, 1);
            opponentsStrip.sizeDelta = new Vector2(-40, stripH);
            opponentsStrip.anchoredPosition = new Vector2(0, -barH);
            var h = UI.HStack(opponentsStrip, 16, null, TextAnchor.MiddleCenter);
            h.childForceExpandWidth = false;

            contentRoot = UI.Rect("Content", safeRoot).Stretch(0, 0, barH + stripH, 0);
            var board = BoardView.Create(contentRoot, NewBoard(), new BoardOptions
            {
                Keys = KeyScheme.All,
                ShowName = true,
                PlayerName = PhotonNetwork.NickName,
                PlayerIndex = PlayerColorIndex(PhotonNetwork.LocalPlayer)
            });
            board.Moved += OnOnlineMoved;
            board.Finished += OnOnlineFinished;
            boards.Add(board);
            RefreshOpponents();
        }

        // ------------------------------------------------------------------
        // Akış
        // ------------------------------------------------------------------

        IEnumerator Countdown()
        {
            foreach (var b in boards) { b.Lock(); b.PlayIntro(); }
            yield return new WaitWhile(() => SceneTransition.IsBusy);

            if (Mode == GameMode.Solo)
            {
                yield return new WaitForSecondsRealtime(0.2f);
            }
            else
            {
                var labels = new List<TextMeshProUGUI>();
                foreach (var b in boards)
                {
                    var t = UI.Text(b.transform, "3", 260, Color.white, FontKind.Digits, TextStyle.Title, TextAlignmentOptions.Center, "Countdown");
                    t.rectTransform.Stretch();
                    labels.Add(t);
                }
                string[] steps = { "3", "2", "1", "Başla!" };
                for (int i = 0; i < steps.Length; i++)
                {
                    foreach (var t in labels)
                    {
                        t.text = steps[i];
                        t.fontSize = i == 3 ? 160 : 260;
                        Tween.ScaleFrom(t.transform, new Vector3(1.6f, 1.6f, 1), Vector3.one, 0.35f, Ease.OutBack);
                    }
                    Sfx.Play(i == 3 ? Sound.Correct : Sound.Tap);
                    yield return new WaitForSecondsRealtime(i == 3 ? 0.45f : 0.7f);
                }
                foreach (var t in labels) Destroy(t.gameObject);
            }

            foreach (var b in boards) { b.Unlock(); b.Running = true; }
        }

        void Update()
        {
            if (halves.Count == 2 && (Screen.width != lastScreen.x || Screen.height != lastScreen.y))
            {
                lastScreen = new Vector2Int(Screen.width, Screen.height);
                ApplySplitLayout();
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) // Android geri tuşu da Escape'tir
            {
                if (Dialog.IsOpen) Dialog.Close(true);
                else AskExit();
            }
        }

        void AskExit()
        {
            if (Dialog.IsOpen) return;
            string msg = Mode == GameMode.Online ? "Odadan ayrılıp ana menüye dönmek istiyor musun?" : "İlerlemen kaydedilmeyecek. Ana menüye dönülsün mü?";
            Dialog.Show(canvas.transform, "Çıkılsın mı?", msg, null,
                new ButtonSpec("Kal", "cream", null),
                new ButtonSpec("Çık", "red", ExitToMenu));
        }

        void ExitToMenu()
        {
            if (Mode == GameMode.Online) OnlineManager.Instance.Leave();
            GameFlow.ToMenu();
        }

        // ---------------- Solo ----------------

        void OnSoloFinished(BoardView b)
        {
            gameOver = true;
            var d = GameSession.Difficulty;
            if (b.Board.IsCompleted)
            {
                string key = "cs_best_" + (int)d;
                float best = PlayerPrefs.GetFloat(key, float.MaxValue);
                bool record = b.ElapsedTime < best;
                if (record) { PlayerPrefs.SetFloat(key, b.ElapsedTime); PlayerPrefs.Save(); }
                string sub = $"{DifficultyInfo.Name(d)} bulmacayı <color=#4C93C9>{BoardView.FormatTime(b.ElapsedTime)}</color> sürede çözdün!" +
                             (record ? "\n<color=#E0485C>Yeni rekor!</color>" : $"\nEn iyi: {BoardView.FormatTime(Mathf.Min(best, b.ElapsedTime))}");
                Sfx.Play(Sound.Win);
                StartCoroutine(Delayed(0.6f, () => b.ShowResult("Tebrikler!", sub, "green", true,
                    new ButtonSpec("Yeni Oyun", "green", () => GameFlow.StartOffline(GameMode.Solo, d), "icon_flat_plus"),
                    new ButtonSpec("Ana Menü", "cream", GameFlow.ToMenu, "icon_home"))));
            }
            else
            {
                Sfx.Play(Sound.Lose);
                StartCoroutine(Delayed(0.6f, () => b.ShowResult("Olamaz!", "3 hata yaptın.\nBir nefes al ve tekrar dene!", "red", false,
                    new ButtonSpec("Tekrar Dene", "yellow", GameFlow.Restart),
                    new ButtonSpec("Yeni Oyun", "green", () => GameFlow.StartOffline(GameMode.Solo, d)),
                    new ButtonSpec("Ana Menü", "cream", GameFlow.ToMenu, "icon_home"))));
            }
        }

        // ---------------- Yerel 2 kişi ----------------

        void OnLocalFinished(BoardView b)
        {
            if (gameOver) return;
            int idx = boards.IndexOf(b);
            var other = boards[1 - idx];

            // Biri bitirirse kazanır; biri 3 hata yaparsa diğeri kazanır
            BoardView winner = b.Board.IsCompleted ? b : other;
            BoardView loser = winner == b ? other : b;
            gameOver = true;
            winner.Running = loser.Running = false;
            winner.Lock(); loser.Lock();
            Sfx.Play(Sound.Win);

            var d = GameSession.Difficulty;
            ButtonSpec rematch = new ButtonSpec("Rövanş", "green", () => GameFlow.StartOffline(GameMode.LocalVersus, d));
            ButtonSpec menu = new ButtonSpec("Menü", "cream", GameFlow.ToMenu, "icon_home");

            string winSub = winner.Board.IsCompleted
                ? $"Bulmacayı <color=#4C93C9>{BoardView.FormatTime(winner.ElapsedTime)}</color> sürede bitirdin!"
                : "Rakibin 3 hata yaptı, kazandın!";
            string loseSub = winner.Board.IsCompleted
                ? $"{winner.Options.PlayerName} daha hızlıydı.\nSen %{Mathf.RoundToInt(loser.Board.Progress * 100)} tamamladın."
                : "3 hata yaptın. Bir dahakine!";

            StartCoroutine(Delayed(0.5f, () =>
            {
                winner.ShowResult("Kazandın!", winSub, "green", true, rematch, menu);
                loser.ShowResult("Kaybettin", loseSub, "red", false, rematch, menu);
            }));
        }

        // ---------------- Online ----------------

        void OnOnlineMoved(BoardView b, PlaceResult r)
        {
            if (b.Board.IsOver) return; // bitiş ayrıca gönderilir
            OnlineManager.SetLocalProgress(gameId, b.Board.FilledByPlayer, b.Board.Mistakes);
        }

        void OnOnlineFinished(BoardView b)
        {
            gameOver = true;
            bool dnf = !b.Board.IsCompleted;
            OnlineManager.SetLocalFinished(gameId, dnf, b.ElapsedTime, b.Board.FilledByPlayer, b.Board.Mistakes);
            Sfx.Play(dnf ? Sound.Lose : Sound.Win);
            StartCoroutine(Delayed(0.5f, ShowOnlineResult));
        }

        void ShowOnlineResult()
        {
            if (boards.Count == 0) return;
            var b = boards[0];
            bool done = AllPlayersDone();
            finalShown = done;
            var buttons = new List<ButtonSpec>();
            if (PhotonNetwork.IsMasterClient)
                buttons.Add(new ButtonSpec("Herkes Lobiye", "green", () => OnlineManager.Instance.ReturnEveryoneToLobby(), "icon_people"));
            else
                buttons.Add(new ButtonSpec("Lobiye Dön", "green", () => SceneTransition.LoadScene(GameFlow.MenuScene), "icon_people"));
            buttons.Add(new ButtonSpec("Odadan Çık", "cream", ExitToMenu, "icon_home"));

            string title = !b.Board.IsCompleted ? "Elendin" : (MyRank() == 1 && done ? "Birincisin!" : "Bitirdin!");
            resultText = b.ShowResult(title, RankingText(), b.Board.IsCompleted ? "green" : "red", b.Board.IsCompleted, buttons.ToArray());
        }

        void OnRoomChanged()
        {
            if (this == null) return;
            RefreshOpponents();
            if (gameOver && resultText != null)
            {
                bool done = AllPlayersDone();
                if (done && !finalShown) ShowOnlineResult(); // başlık/butonlar güncellensin
                else resultText.text = RankingText();
            }
        }

        static int PlayerColorIndex(Player p)
        {
            var list = PhotonNetwork.PlayerList;
            for (int i = 0; i < list.Length; i++) if (list[i] == p) return i % 4;
            return 0;
        }

        static T Prop<T>(Player p, string key, T fallback)
        {
            if (p.CustomProperties.TryGetValue(key, out var v) && v is T t) return t;
            return fallback;
        }

        bool IsCurrent(Player p) => Prop(p, NetKeys.PGame, -1) == gameId;
        bool IsDone(Player p) => IsCurrent(p) && (Prop(p, NetKeys.PDnf, false) || Prop(p, NetKeys.PTime, -1f) >= 0f);

        bool AllPlayersDone()
        {
            foreach (var p in PhotonNetwork.PlayerList) if (!IsDone(p)) return false;
            return true;
        }

        List<Player> Ranked()
        {
            var list = new List<Player>(PhotonNetwork.PlayerList);
            list.Sort((a, b) =>
            {
                int Group(Player p)
                {
                    if (!IsCurrent(p)) return 2;
                    if (Prop(p, NetKeys.PTime, -1f) >= 0f) return 0;
                    if (Prop(p, NetKeys.PDnf, false)) return 3;
                    return 1; // hâlâ oynuyor
                }
                int ga = Group(a), gb = Group(b);
                if (ga != gb) return ga.CompareTo(gb);
                if (ga == 0) return Prop(a, NetKeys.PTime, 0f).CompareTo(Prop(b, NetKeys.PTime, 0f));
                return Prop(b, NetKeys.PFilled, 0).CompareTo(Prop(a, NetKeys.PFilled, 0));
            });
            return list;
        }

        int MyRank()
        {
            var r = Ranked();
            return r.IndexOf(PhotonNetwork.LocalPlayer) + 1;
        }

        string RankingText()
        {
            var sb = new StringBuilder();
            var ranked = Ranked();
            int empty = boards.Count > 0 ? boards[0].Board.EmptyAtStart : 1;
            for (int i = 0; i < ranked.Count; i++)
            {
                var p = ranked[i];
                string name = p.IsLocal ? $"<color=#4C93C9>{p.NickName}</color>" : p.NickName;
                string status;
                if (!IsCurrent(p)) status = "bekleniyor";
                else if (Prop(p, NetKeys.PTime, -1f) >= 0f) status = BoardView.FormatTime(Prop(p, NetKeys.PTime, 0f));
                else if (Prop(p, NetKeys.PDnf, false)) status = "<color=#E0485C>elendi</color>";
                else status = $"oynuyor %{Mathf.RoundToInt(100f * Prop(p, NetKeys.PFilled, 0) / Mathf.Max(1, empty))}";
                sb.Append(i + 1).Append(". ").Append(name).Append("  <size=80%>").Append(status).Append("</size>");
                if (i < ranked.Count - 1) sb.Append('\n');
            }
            if (!AllPlayersDone()) sb.Append("\n\n<size=75%>Diğer oyuncular bekleniyor...</size>");
            return sb.ToString();
        }

        void RefreshOpponents()
        {
            if (opponentsStrip == null) return;

            // Oyuncu listesi değiştiyse kartları yeniden kur, değilse sadece ilerlemeyi güncelle
            var others = new List<Player>();
            foreach (var p in PhotonNetwork.PlayerList) if (!p.IsLocal) others.Add(p);
            bool same = others.Count == chips.Count;
            for (int i = 0; same && i < others.Count; i++) same = others[i].ActorNumber == chips[i].Actor;
            if (!same) RebuildChips(others);

            int empty = boards.Count > 0 ? boards[0].Board.EmptyAtStart : 1;
            for (int i = 0; i < others.Count; i++)
            {
                var p = others[i];
                var chip = chips[i];
                float progress = IsCurrent(p) ? Prop(p, NetKeys.PFilled, 0) / (float)Mathf.Max(1, empty) : 0f;
                bool dnf = IsCurrent(p) && Prop(p, NetKeys.PDnf, false);
                bool fin = IsCurrent(p) && Prop(p, NetKeys.PTime, -1f) >= 0f;
                chip.Fill.color = dnf ? CozyTheme.Red : CozyTheme.PlayerColors[chip.ColorIndex];
                float target = Mathf.Clamp01(fin ? 1f : progress);
                var fillRt = chip.Fill.rectTransform;
                float from = fillRt.anchorMax.x;
                Tween.Run(fillRt, Tween.ChExtra, 0.35f, Ease.OutCubic, k => fillRt.anchorMax = new Vector2(Mathf.Lerp(from, target, k), 1f));
                bool showBadge = fin || dnf;
                if (chip.Badge.gameObject.activeSelf != showBadge || (showBadge && chip.Badge.sprite.name != (fin ? "icon_check" : "icon_close")))
                {
                    chip.Badge.sprite = CozyTheme.Sprite(fin ? "icon_check" : "icon_close");
                    chip.Badge.gameObject.SetActive(showBadge);
                    if (showBadge) Tween.ScaleFrom(chip.Badge.transform, Vector3.zero, Vector3.one, 0.35f);
                }
            }
        }

        void RebuildChips(List<Player> others)
        {
            for (int i = opponentsStrip.childCount - 1; i >= 0; i--) Destroy(opponentsStrip.GetChild(i).gameObject);
            chips.Clear();
            foreach (var p in others)
            {
                int ci = PlayerColorIndex(p);
                var bg = UI.Image(opponentsStrip, "slot", null, 1.6f, "Opponent");
                bg.Layout(300, 96);
                var v = UI.VStack(bg.rectTransform, 2, new RectOffset(24, 24, 8, 18), TextAnchor.MiddleCenter);
                v.childForceExpandWidth = true;

                var name = UI.Text(bg.transform, p.NickName, 32, CozyTheme.Ink, FontKind.Text, TextStyle.Soft);
                name.Layout(-1, 38);
                name.overflowMode = TextOverflowModes.Ellipsis;

                var barBg = UI.Rounded(bg.transform, CozyTheme.BoardGap.WithAlpha(0.6f), 2f, "Bar");
                barBg.Layout(-1, 22);
                var fill = UI.Rounded(barBg.transform, CozyTheme.PlayerColors[ci], 2f, "Fill");
                fill.rectTransform.AnchorRange(Vector2.zero, new Vector2(0f, 1f));

                var badge = UI.Image(bg.transform, "icon_check", null, 1f, "Badge");
                badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                badge.rectTransform.Anchor(new Vector2(1, 1), new Vector2(10, 10), new Vector2(48, 48));
                badge.gameObject.SetActive(false);

                chips.Add(new Chip { Actor = p.ActorNumber, Fill = fill, Badge = badge, ColorIndex = ci });
                Tween.ScaleFrom(bg.transform, new Vector3(0.6f, 0.6f, 1f), Vector3.one, 0.35f);
            }
        }

        IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
        }
    }
}
