using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CozySudoku
{
    public enum KeyScheme { None, All, Left, Right }

    public sealed class BoardOptions
    {
        public string PlayerName = "";
        public int PlayerIndex;
        public bool ShowName;
        public bool AllowHints;
        public int Hints = 3;
        public KeyScheme Keys = KeyScheme.All;
        public string KeyLegend;
    }

    /// <summary>
    /// Bir oyuncunun tüm oyun arayüzü: başlık (isim, süre, kalpler), 9x9 tahta,
    /// araçlar (geri al / sil / not / ipucu) ve sayı tuşları.
    /// Verilen alana göre kendini dikey (telefon) ya da yatay (masaüstü) yerleştirir;
    /// bu sayede tam ekranda da, bölünmüş ekranın yarısında da (ters çevrilmiş olsa bile) çalışır.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public SudokuBoard Board { get; private set; }
        public BoardOptions Options { get; private set; }
        public float ElapsedTime { get; private set; }
        public bool Running { get; set; }
        public bool Locked { get; private set; }

        /// <summary>Doğru/yanlış her hamleden sonra (online ilerleme senkronu için).</summary>
        public event Action<BoardView, PlaceResult> Moved;
        /// <summary>Tahta bitti (tamamlandı ya da 3 hata).</summary>
        public event Action<BoardView> Finished;

        RectTransform rt;
        RectTransform header, boardFrame, boardInner, toolbar, numpadRoot, legendRt;
        Image frameImg, innerImg;
        TextMeshProUGUI timerText, nameText, legendText;
        Image[] hearts;
        readonly CellView[] cells = new CellView[81];
        readonly CozyButton[] keys = new CozyButton[9];
        readonly TextMeshProUGUI[] keyCounts = new TextMeshProUGUI[9];
        CozyButton pencilBtn, eraseBtn, undoBtn, hintBtn;
        TextMeshProUGUI hintCaption;
        readonly List<RectTransform> toolItems = new List<RectTransform>();
        readonly List<TextMeshProUGUI> toolCaptions = new List<TextMeshProUGUI>();

        int selected = -1;
        bool pencil;
        int hintsLeft;
        int shownSecond = -1;
        Vector2 lastSize;
        RectTransform overlay;

        // ------------------------------------------------------------------
        public static BoardView Create(RectTransform container, SudokuBoard board, BoardOptions options)
        {
            var root = UI.Rect("BoardView", container).Stretch();
            var view = root.gameObject.AddComponent<BoardView>();
            view.rt = root;
            view.Board = board;
            view.Options = options ?? new BoardOptions();
            view.hintsLeft = view.Options.AllowHints ? view.Options.Hints : 0;
            view.Build();
            view.RefreshAll();
            return view;
        }

        void Build()
        {
            // --- Başlık ---
            header = UI.Rect("Header", rt);
            var hl = UI.HStack(header, 18, null, TextAnchor.MiddleCenter);
            hl.childForceExpandWidth = false;

            if (Options.ShowName)
            {
                var color = CozyTheme.PlayerSpriteColors[Mathf.Abs(Options.PlayerIndex) % CozyTheme.PlayerSpriteColors.Length];
                var chip = UI.Image(header, "label_" + color, null, 1f, "NameChip");
                var chipLayout = UI.HStack(chip.rectTransform, 0, new RectOffset(40, 40, 0, 6));
                chipLayout.childForceExpandWidth = false;
                nameText = UI.Text(chip.transform, Options.PlayerName, 40, Color.white, FontKind.Text, TextStyle.Title);
                nameText.Layout(-1, 60);
                UI.Rect("Flex", header).Layout(-1, -1, 1, -1);
            }

            var timerBox = UI.Rect("Timer", header);
            UI.HStack(timerBox, 8).childForceExpandWidth = false;
            UI.Image(timerBox, "icon_timer", null, 1f, "TimerIcon").Layout(56, 56);
            timerText = UI.Text(timerBox, "00:00", 44, CozyTheme.Ink, FontKind.Digits, TextStyle.Soft);
            timerText.Layout(130, 56);

            UI.Rect("Flex", header).Layout(-1, -1, 1, -1);

            var heartBox = UI.Rect("Hearts", header);
            UI.HStack(heartBox, 4).childForceExpandWidth = false;
            hearts = new Image[Board.MaxMistakes];
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i] = UI.Image(heartBox, "icon_heart", null, 1f, "Heart");
                hearts[i].Layout(54, 54);
            }

            // --- Tahta ---
            frameImg = UI.Image(rt, "frame_wood", null, 1f, "BoardFrame");
            boardFrame = frameImg.rectTransform;
            innerImg = UI.Rounded(boardFrame, CozyTheme.BoardGap, 1f, "Inner");
            boardInner = innerImg.rectTransform;
            for (int i = 0; i < 81; i++) cells[i] = CellView.Create(boardInner, i, OnCellPressed);

            // --- Araçlar ---
            toolbar = UI.Rect("Toolbar", rt);
            var tl = UI.HStack(toolbar, 26, null, TextAnchor.MiddleCenter);
            tl.childForceExpandWidth = false;
            undoBtn = AddTool("blue", "icon_undo", "Geri Al", Undo, out _);
            eraseBtn = AddTool("red", "icon_erase", "Sil", Erase, out _);
            pencilBtn = AddTool("yellow", "icon_pencil", "Not", TogglePencil, out _);
            if (Options.AllowHints) hintBtn = AddTool("green", "icon_potion", "İpucu", UseHint, out hintCaption);

            // --- Sayı tuşları ---
            numpadRoot = UI.Rect("Numpad", rt);
            for (int d = 1; d <= 9; d++)
            {
                int digit = d;
                var key = UI.SquareButton(numpadRoot, d.ToString(), "cream", () => OnDigit(digit), 60);
                key.ClickSound = Sound.Select;
                keys[d - 1] = key;
                var count = UI.Text(key.transform, "9", 22, CozyTheme.InkSoft, FontKind.Digits, TextStyle.Plain, TextAlignmentOptions.TopRight, "Count");
                count.rectTransform.Stretch(8, 14, 10, 0);
                keyCounts[d - 1] = count;
            }

            if (!string.IsNullOrEmpty(Options.KeyLegend))
            {
                legendText = UI.Text(rt, Options.KeyLegend, 26, CozyTheme.InkSoft, FontKind.Text, TextStyle.Plain);
                legendRt = legendText.rectTransform;
            }
        }

        CozyButton AddTool(string color, string icon, string caption, Action action, out TextMeshProUGUI captionText)
        {
            var item = UI.Rect("Tool " + caption, toolbar);
            var v = UI.VStack(item, 2, null, TextAnchor.UpperCenter, false);
            v.childForceExpandWidth = false;
            var btn = UI.IconButton(item, color, icon, action);
            btn.ClickSound = Sound.Pop;
            captionText = UI.Text(item, caption, 26, CozyTheme.Ink, FontKind.Text, TextStyle.Soft);
            toolItems.Add(item);
            toolCaptions.Add(captionText);
            return btn;
        }

        // ------------------------------------------------------------------
        // Yerleşim
        // ------------------------------------------------------------------

        void OnRectTransformDimensionsChange()
        {
            if (rt == null) return;
            if ((rt.rect.size - lastSize).sqrMagnitude > 1f) Relayout();
        }

        void Start() => Relayout();

        static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        public void Relayout()
        {
            Vector2 size = rt.rect.size;
            if (size.x < 50f || size.y < 50f) return;
            lastSize = size;

            float pad = Mathf.Min(size.x, size.y) * 0.035f;
            float W = size.x - pad * 2f, H = size.y - pad * 2f;
            float legendH = legendText != null ? 40f : 0f;
            int toolCount = toolItems.Count;

            // --- Dikey seçenek: başlık / tahta / araçlar / tek sıra sayılar ---
            float gV = H * 0.018f;
            float headerV = Mathf.Clamp(W * 0.08f, 54f, 92f);
            float toolBtnV = Mathf.Clamp(W * 0.11f, 74f, 128f);
            float toolV = toolBtnV + toolBtnV * 0.34f;
            float keyGapV = W * 0.012f;
            float keyV = (W - 8f * keyGapV) / 9f;
            float boardV = Mathf.Min(W, H - headerV - toolV - keyV * 1.08f - legendH - gV * 4f);
            // tuşları tahta genişliğine yakın tut, kazanılan yeri tahtaya ver
            float padWidth = Mathf.Min(W, boardV * 1.06f);
            keyV = (padWidth - 8f * keyGapV) / 9f;
            boardV = Mathf.Min(W, H - headerV - toolV - keyV * 1.08f - legendH - gV * 4f);

            // --- Yatay seçenek: solda tahta, sağda sütun (başlık / araçlar / 3x3 sayılar) ---
            float gH = W * 0.025f;
            float boardH = Mathf.Min(H, W * 0.6f);
            float colW = Mathf.Min(W - boardH - gH, boardH * 0.75f);
            float headerH = Mathf.Clamp(colW * 0.16f, 54f, 92f);
            float toolBtnH = Mathf.Clamp(colW * 0.2f, 74f, 128f);
            float toolH = toolBtnH * 1.34f;
            float keyGapH = colW * 0.035f;
            float keyH = Mathf.Min((colW - 2f * keyGapH) / 3f, (H - headerH - toolH - legendH - gH * 3f) / 3.3f);
            bool horizontalFits = colW > 200f && keyH > 50f;

            bool vertical = !horizontalFits || boardV >= boardH * 0.92f;

            if (vertical)
            {
                float totalH = headerV + gV + boardV + gV + toolV + gV + keyV * 1.08f + (legendH > 0 ? gV + legendH : 0f);
                float y = pad + Mathf.Max(0f, (H - totalH) / 2f);
                float cx = pad + W / 2f;

                float headerW = Mathf.Min(W, boardV * 1.25f);
                Place(header, cx - headerW / 2f, y, headerW, headerV); y += headerV + gV;
                LayoutBoard(cx - boardV / 2f, y, boardV); y += boardV + gV;
                LayoutTools(cx - boardV / 2f, y, boardV, toolBtnV); y += toolV + gV;
                float pw = keyV * 9f + keyGapV * 8f;
                for (int i = 0; i < 9; i++)
                    Place((RectTransform)keys[i].transform, cx - pw / 2f + i * (keyV + keyGapV), y, keyV, keyV * 1.08f);
                Place(numpadRoot, 0, 0, 0, 0);
                y += keyV * 1.08f;
                if (legendRt != null) { y += gV; Place(legendRt, cx - W / 2f, y, W, legendH); }
                ApplyFontScale(headerV, toolBtnV, keyV);
            }
            else
            {
                float colH = headerH + gH + toolH + gH + keyH * 3.24f + (legendH > 0 ? gH + legendH : 0f);
                float totalW = boardH + gH + colW;
                float x = pad + (W - totalW) / 2f;
                float by = pad + (H - boardH) / 2f;
                LayoutBoard(x, by, boardH);

                float cx = x + boardH + gH;
                float y = pad + Mathf.Max(0f, (H - colH) / 2f);
                Place(header, cx, y, colW, headerH); y += headerH + gH;
                LayoutTools(cx, y, colW, toolBtnH); y += toolH + gH;
                float gridW = keyH * 3f + keyGapH * 2f;
                float gx = cx + (colW - gridW) / 2f;
                for (int i = 0; i < 9; i++)
                {
                    int r = i / 3, c = i % 3;
                    Place((RectTransform)keys[i].transform, gx + c * (keyH + keyGapH), y + r * (keyH * 1.08f + keyGapH * 0.5f), keyH, keyH * 1.08f);
                }
                Place(numpadRoot, 0, 0, 0, 0);
                y += keyH * 3.24f + keyGapH;
                if (legendRt != null) { y += gH; Place(legendRt, cx, y, colW, legendH); }
                ApplyFontScale(headerH, toolBtnH, keyH);
            }

            if (overlay != null) overlay.SetAsLastSibling();
        }

        void LayoutBoard(float x, float y, float size)
        {
            Place(boardFrame, x, y, size, size);
            frameImg.pixelsPerUnitMultiplier = 76f / Mathf.Max(10f, size * 0.075f);

            float fp = size * 0.045f;               // çerçeve kalınlığı
            float bottomExtra = size * 0.012f;       // çerçevenin alt gölgesi
            float inner = size - fp * 2f - bottomExtra;
            Place(boardInner, fp, fp, inner, inner);
            innerImg.pixelsPerUnitMultiplier = 22f / Mathf.Max(4f, inner * 0.03f);

            float pad = inner * 0.012f;
            float blockGap = inner * 0.016f;
            float cellGap = inner * 0.006f;
            float area = inner - pad * 2f;
            float cell = (area - blockGap * 2f - cellGap * 6f) / 9f;
            for (int i = 0; i < 81; i++)
            {
                int r = i / 9, c = i % 9;
                float cxp = pad + c * (cell + cellGap) + (c / 3) * (blockGap - cellGap) + cell / 2f;
                float cyp = pad + r * (cell + cellGap) + (r / 3) * (blockGap - cellGap) + cell / 2f;
                cells[i].Layout(new Vector2(cxp, -cyp), cell);
            }
        }

        void LayoutTools(float x, float y, float w, float btn)
        {
            Place(toolbar, x, y, w, btn * 1.34f);
            var hl = toolbar.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = Mathf.Min(btn * 0.45f, (w - btn * toolItems.Count) / Mathf.Max(1, toolItems.Count - 1));
            foreach (var item in toolItems)
            {
                var btnRt = (RectTransform)item.GetChild(0);
                btnRt.Layout(btn, btn);
            }
            foreach (var cap in toolCaptions)
            {
                cap.fontSize = btn * 0.26f;
                cap.Layout(btn * 1.4f, btn * 0.32f);
            }
        }

        void ApplyFontScale(float headerH, float toolBtn, float key)
        {
            float hs = headerH * 0.62f;
            timerText.fontSize = hs * 0.95f;
            timerText.Layout(hs * 3.1f, headerH);
            ((RectTransform)timerText.transform.parent.GetChild(0)).Layout(hs * 1.15f, hs * 1.15f);
            foreach (var h in hearts) h.Layout(hs * 1.15f, hs * 1.15f);
            if (nameText != null)
            {
                nameText.fontSize = hs * 0.85f;
                nameText.Layout(-1, headerH * 0.8f);
            }
            for (int i = 0; i < 9; i++)
            {
                keys[i].Label.fontSize = key * 0.56f;
                keys[i].Background.pixelsPerUnitMultiplier = 50f / Mathf.Max(8f, key * 0.24f);
                keyCounts[i].fontSize = key * 0.2f;
            }
            if (legendText != null) legendText.fontSize = Mathf.Clamp(headerH * 0.36f, 18f, 30f);
        }

        // ------------------------------------------------------------------
        // Etkileşim
        // ------------------------------------------------------------------

        void OnCellPressed(int index)
        {
            if (Locked) return;
            Select(index, true);
        }

        public void Select(int index, bool sound)
        {
            if (index < 0 || index > 80) return;
            if (selected == index)
            {
                RefreshHighlights();
                return;
            }
            selected = index;
            if (sound) Sfx.Play(Sound.Select, 0.6f);
            Tween.Punch(cells[index].transform, 0.08f, 0.2f);
            RefreshHighlights();
        }

        void OnDigit(int digit)
        {
            if (Locked || Board.IsOver) return;
            if (selected < 0)
            {
                // seçili hücre yoksa: o sayının bulunduğu yerleri vurgula
                HighlightDigit(digit);
                return;
            }
            if (Board.ValueAt(selected) != 0)
            {
                HighlightDigit(digit);
                return;
            }

            if (pencil)
            {
                if (Board.ToggleNote(selected, digit, Settings.SmartNotes))
                {
                    cells[selected].SetNotes(Board.NotesAt(selected));
                    Sfx.Play(Sound.Note, 0.7f);
                    RefreshTools();
                }
                else Tween.Shake(cells[selected].Rt, 6f, 0.25f);
                return;
            }

            int cell = selected;
            var result = Board.Place(cell, digit);
            HandlePlaceResult(cell, digit, result);
        }

        void HandlePlaceResult(int cell, int digit, PlaceResult result)
        {
            switch (result)
            {
                case PlaceResult.Correct:
                case PlaceResult.Completed:
                    cells[cell].CancelFlash();
                    cells[cell].SetValue(digit, false);
                    cells[cell].PopIn();
                    Sfx.Play(Sound.Correct, 0.8f);
                    // komşulardaki notlar silindi → görselleri tazele
                    foreach (int p in SudokuBoard.PeersOf(cell)) cells[p].SetNotes(Board.NotesAt(p));
                    CelebrateUnits(cell);
                    RefreshKeys();
                    RefreshHighlights();
                    RefreshTools();
                    break;

                case PlaceResult.Wrong:
                case PlaceResult.Failed:
                    cells[cell].FlashWrong(digit);
                    Sfx.Play(Sound.Wrong, 0.9f);
                    Haptics.Bump();
                    LoseHeart();
                    Tween.Shake(boardFrame, 10f, 0.3f);
                    break;
            }

            if (result != PlaceResult.Ignored) Moved?.Invoke(this, result);
            if (result == PlaceResult.Completed || result == PlaceResult.Failed)
            {
                Running = false;
                Lock();
                Finished?.Invoke(this);
            }
        }

        /// <summary>Tamamlanan satır/sütun/blokta küçük dalga animasyonu.</summary>
        void CelebrateUnits(int cell)
        {
            int r = cell / 9, c = cell % 9;
            bool rowDone = true, colDone = true, boxDone = true;
            for (int i = 0; i < 9; i++)
            {
                if (Board.ValueAt(r * 9 + i) == 0) rowDone = false;
                if (Board.ValueAt(i * 9 + c) == 0) colDone = false;
                int br = r / 3 * 3 + i / 3, bc = c / 3 * 3 + i % 3;
                if (Board.ValueAt(br * 9 + bc) == 0) boxDone = false;
            }
            if (!rowDone && !colDone && !boxDone) return;
            for (int i = 0; i < 81; i++)
            {
                int ir = i / 9, ic = i % 9;
                bool inUnit = (rowDone && ir == r) || (colDone && ic == c) || (boxDone && ir / 3 == r / 3 && ic / 3 == c / 3);
                if (!inUnit || i == cell) continue;
                int dist = Mathf.Abs(ir - r) + Mathf.Abs(ic - c);
                cells[i].PopIn(dist * 0.035f);
            }
            Sfx.Play(Sound.Pop, 0.6f);
        }

        void LoseHeart()
        {
            int idx = Board.Mistakes - 1;
            if (idx < 0 || idx >= hearts.Length) return;
            var h = hearts[hearts.Length - 1 - idx];
            h.sprite = CozyTheme.Sprite("icon_heart_empty");
            Tween.Punch(h.transform, 0.35f, 0.4f);
        }

        void Erase()
        {
            if (Locked || selected < 0) return;
            if (Board.ClearNotes(selected))
            {
                cells[selected].SetNotes(0);
                RefreshTools();
            }
            else Tween.Shake(cells[selected].Rt, 5f, 0.2f);
        }

        void Undo()
        {
            if (Locked) return;
            int cell = Board.Undo();
            if (cell >= 0)
            {
                cells[cell].SetNotes(Board.NotesAt(cell));
                Tween.Punch(cells[cell].transform, 0.1f, 0.2f);
            }
            RefreshTools();
        }

        void TogglePencil()
        {
            if (Locked) return;
            pencil = !pencil;
            pencilBtn.SetSprite(pencil ? "btn_round_green" : "btn_round_yellow");
            Tween.Punch(pencilBtn.transform, 0.15f, 0.3f);
        }

        void UseHint()
        {
            if (Locked || hintsLeft <= 0 || Board.IsOver) return;
            int cell = Board.FindEmptyCell(selected);
            if (cell < 0) return;
            hintsLeft--;
            Select(cell, false);
            int digit = Board.SolutionAt(cell);
            HandlePlaceResult(cell, digit, Board.Reveal(cell));
            RefreshTools();
        }

        void HighlightDigit(int digit)
        {
            for (int i = 0; i < 81; i++)
                cells[i].SetHighlight(Board.ValueAt(i) == digit ? CellHighlight.SameDigit : CellHighlight.None);
            if (selected >= 0) cells[selected].SetHighlight(CellHighlight.Selected);
        }

        public void Unlock() => Locked = Board.IsOver;

        /// <summary>Hücreler köşegen dalgayla "pıt pıt" belirir.</summary>
        public void PlayIntro()
        {
            for (int i = 0; i < 81; i++) cells[i].PopIn(((i / 9) + (i % 9)) * 0.018f);
        }

        public void Lock()
        {
            Locked = true;
            selected = -1;
            for (int i = 0; i < 81; i++) cells[i].SetHighlight(CellHighlight.None);
        }

        // ------------------------------------------------------------------
        // Görsel tazeleme
        // ------------------------------------------------------------------

        void RefreshAll()
        {
            for (int i = 0; i < 81; i++)
            {
                cells[i].SetValue(Board.ValueAt(i), Board.IsGiven(i));
                cells[i].SetNotes(Board.NotesAt(i));
            }
            for (int i = 0; i < hearts.Length; i++)
                hearts[i].sprite = CozyTheme.Sprite(i < hearts.Length - Board.Mistakes ? "icon_heart" : "icon_heart_empty");
            RefreshKeys();
            RefreshHighlights();
            RefreshTools();
            UpdateTimerText(true);
        }

        void RefreshHighlights()
        {
            int sel = selected;
            int selValue = sel >= 0 ? Board.ValueAt(sel) : 0;
            int sr = sel / 9, sc = sel % 9;
            bool same = Settings.HighlightSame;
            for (int i = 0; i < 81; i++)
            {
                CellHighlight h = CellHighlight.None;
                if (sel >= 0)
                {
                    int r = i / 9, c = i % 9;
                    if (i == sel) h = CellHighlight.Selected;
                    else if (same && selValue != 0 && Board.ValueAt(i) == selValue) h = CellHighlight.SameDigit;
                    else if (r == sr || c == sc || (r / 3 == sr / 3 && c / 3 == sc / 3)) h = CellHighlight.Related;
                }
                cells[i].SetHighlight(h);
            }
        }

        void RefreshKeys()
        {
            for (int d = 1; d <= 9; d++)
            {
                int left = 9 - Board.DigitCount(d);
                keyCounts[d - 1].text = left > 0 ? left.ToString() : "";
                keys[d - 1].Interactable = left > 0;
            }
        }

        void RefreshTools()
        {
            undoBtn.Interactable = Board.CanUndo;
            if (hintBtn != null)
            {
                hintBtn.Interactable = hintsLeft > 0;
                if (hintCaption != null) hintCaption.text = $"İpucu ({hintsLeft})";
            }
        }

        void UpdateTimerText(bool force)
        {
            int sec = Mathf.FloorToInt(ElapsedTime);
            if (!force && sec == shownSecond) return;
            shownSecond = sec;
            timerText.SetText("{0:00}:{1:00}", sec / 60, sec % 60);
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60:00}:{s % 60:00}";
        }

        // ------------------------------------------------------------------
        // Döngü: süre + klavye
        // ------------------------------------------------------------------

        void Update()
        {
            if (Running && !Board.IsOver)
            {
                ElapsedTime += Time.unscaledDeltaTime;
                UpdateTimerText(false);
            }
            if (!Locked && Options.Keys != KeyScheme.None) HandleKeyboard();
        }

        void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            var scheme = Options.Keys;
            bool left = scheme == KeyScheme.All || scheme == KeyScheme.Left;
            bool right = scheme == KeyScheme.All || scheme == KeyScheme.Right;

            // Yön tuşları
            int dr = 0, dc = 0;
            if (left)
            {
                if (kb.wKey.wasPressedThisFrame) dr = -1;
                if (kb.sKey.wasPressedThisFrame) dr = 1;
                if (kb.aKey.wasPressedThisFrame) dc = -1;
                if (kb.dKey.wasPressedThisFrame) dc = 1;
            }
            if (right)
            {
                if (kb.upArrowKey.wasPressedThisFrame) dr = -1;
                if (kb.downArrowKey.wasPressedThisFrame) dr = 1;
                if (kb.leftArrowKey.wasPressedThisFrame) dc = -1;
                if (kb.rightArrowKey.wasPressedThisFrame) dc = 1;
            }
            if (dr != 0 || dc != 0)
            {
                int cur = selected < 0 ? 40 : selected;
                int r = (cur / 9 + dr + 9) % 9, c = (cur % 9 + dc + 9) % 9;
                Select(r * 9 + c, true);
            }

            // Sayılar
            for (int d = 1; d <= 9; d++)
            {
                bool pressed = false;
                if (left && kb[DigitKeys[d]].wasPressedThisFrame) pressed = true;
                if (right && kb[NumpadKeys[d]].wasPressedThisFrame) pressed = true;
                if (pressed)
                {
                    Tween.Punch(keys[d - 1].transform, 0.12f, 0.2f);
                    OnDigit(d);
                }
            }

            // Araçlar
            if (scheme == KeyScheme.All)
            {
                if (kb.nKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) TogglePencil();
                if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame || kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) Erase();
                if (kb.zKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame) Undo();
                if (kb.hKey.wasPressedThisFrame) UseHint();
            }
            else if (scheme == KeyScheme.Left)
            {
                if (kb.qKey.wasPressedThisFrame) TogglePencil();
                if (kb.eKey.wasPressedThisFrame) Erase();
                if (kb.zKey.wasPressedThisFrame) Undo();
            }
            else if (scheme == KeyScheme.Right)
            {
                if (kb.numpadPlusKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) TogglePencil();
                if (kb.numpad0Key.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame || kb.numpadPeriodKey.wasPressedThisFrame) Erase();
                if (kb.numpadMinusKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Undo();
            }
        }

        static readonly Key[] DigitKeys = { Key.None, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] NumpadKeys = { Key.None, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        // ------------------------------------------------------------------
        // Sonuç kartı (bu oyuncunun alanında; ters çevrilmiş yarıda da doğru yönde görünür)
        // ------------------------------------------------------------------

        public TextMeshProUGUI ShowResult(string title, string subtitle, string ribbonColor, bool happy, params ButtonSpec[] buttons)
        {
            HideResult();
            overlay = UI.Rect("Result", rt).Stretch();
            var dim = UI.Rounded(overlay, CozyTheme.Ink.WithAlpha(0.35f), 0.5f, "Dim");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Tween.Fade(group, 1f, 0.25f);

            Vector2 size = rt.rect.size;
            float cardW = Mathf.Min(size.x * 0.86f, 820f);
            float scale = Mathf.Clamp(Mathf.Min(size.x, size.y) / 1000f, 0.55f, 1f);

            var card = UI.Image(overlay, "panel_cream", null, 1f / Mathf.Max(0.6f, scale), "Card");
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cardW, 0));
            var v = UI.VStack(card.rectTransform, 22f * scale, new RectOffset((int)(60 * scale), (int)(60 * scale), (int)(110 * scale), (int)(80 * scale)), TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Kurdele başlık kartın üst kenarına oturur
            var ribbon = UI.Image(card.transform, "ribbon_" + ribbonColor, null, 1.4f / scale, "Ribbon");
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ribbon.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0, 10f * scale), new Vector2(cardW * 1.08f, 150f * scale));
            var rt2 = UI.Text(ribbon.transform, title, 70f * scale, Color.white, FontKind.Text, TextStyle.Title);
            rt2.rectTransform.Stretch(60 * scale, 60 * scale, 10 * scale, 40 * scale);
            rt2.AutoSize(30f * scale, 70f * scale);

            var icon = UI.Image(card.transform, happy ? "icon_trophy" : "icon_heart_empty", null, 1f, "Icon");
            icon.Layout(-1, 150f * scale);
            Tween.ScaleFrom(icon.transform, Vector3.zero, Vector3.one, 0.5f, Ease.OutBack, null, 0.15f);

            var sub = UI.Text(card.transform, subtitle, 44f * scale, CozyTheme.Ink, FontKind.Text, TextStyle.Soft);
            sub.textWrappingMode = TextWrappingModes.Normal;

            foreach (var b in buttons)
            {
                var btn = UI.Button(card.transform, b.Label, b.Color, b.OnClick, b.Icon, 46f * scale);
                btn.Background.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.6f, scale);
                btn.Layout(-1, 120f * scale);
            }

            Tween.ScaleFrom(card.transform, new Vector3(0.6f, 0.6f, 1f), Vector3.one, 0.45f, Ease.OutBack);
            return sub;
        }

        public void HideResult()
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
        }
    }
}
