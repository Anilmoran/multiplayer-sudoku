using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CozySudoku
{
    /// <summary>
    /// Sudoku temalı sahne geçişi: ekran, köşegen dalga halinde "pıt pıt" beliren
    /// sayılı karelerle dolar (geçerli bir sudoku deseni), sahne yüklenir,
    /// sonra kareler aynı yönde eriyerek yeni sahneyi açar.
    /// Tüm kareler tek bir coroutine ile canlandırılır (200 ayrı tween yerine).
    /// </summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        const float TileGrow = 0.26f;   // tek karenin büyüme süresi
        const float WaveTime = 0.42f;   // dalganın ekranı geçme süresi
        const float HoldTime = 0.08f;

        static SceneTransition instance;
        public static bool IsBusy { get; private set; }
        public static bool IsCovered { get; private set; }

        Canvas canvas;
        RectTransform root;
        Image blocker;
        readonly List<Tile> tiles = new List<Tile>();
        int activeTiles;
        int maxWave = 1;

        struct Tile
        {
            public RectTransform Rt;
            public Image Img;
            public TextMeshProUGUI Digit;
            public int Wave;
        }

        static SceneTransition Instance
        {
            get
            {
                if (instance != null) return instance;
                var parent = AppBootstrap.Root != null ? AppBootstrap.Root.transform : null;
                var canvas = UI.Canvas("[Scene Transition]", 900, parent);
                if (parent == null) DontDestroyOnLoad(canvas.gameObject);
                instance = canvas.gameObject.AddComponent<SceneTransition>();
                instance.canvas = canvas;
                instance.root = (RectTransform)canvas.transform;
                instance.blocker = UI.Rounded(canvas.transform, new Color(0, 0, 0, 0), 1f, "Blocker");
                instance.blocker.rectTransform.Stretch();
                instance.blocker.raycastTarget = true;
                canvas.enabled = false;
                return instance;
            }
        }

        /// <summary>Geçişli sahne yükleme. <paramref name="whileCovered"/> ekran kapalıyken çalışır (ör. bulmaca üretimi).</summary>
        public static void LoadScene(string sceneName, Func<IEnumerator> whileCovered = null)
        {
            if (IsBusy)
            {
                // Başka bir geçiş sürüyorsa bitince sıradakini çalıştır (online'da önemli)
                Runner.Instance.StartCoroutine(Instance.QueueRoutine(sceneName, whileCovered));
                return;
            }
            Runner.Instance.StartCoroutine(Instance.LoadRoutine(sceneName, whileCovered));
        }

        /// <summary>Uygulama ilk açıldığında: ekran dolu başlar ve açılır.</summary>
        public static void PlayIntro()
        {
            if (IsBusy || IsCovered) return;
            Runner.Instance.StartCoroutine(Instance.IntroRoutine());
        }

        IEnumerator QueueRoutine(string sceneName, Func<IEnumerator> whileCovered)
        {
            while (IsBusy) yield return null;
            yield return LoadRoutine(sceneName, whileCovered);
        }

        IEnumerator IntroRoutine()
        {
            IsBusy = true;
            // İlk kareden itibaren ekranı kapat (menü bir anlığına görünmesin)
            BuildTiles();
            for (int i = 0; i < activeTiles; i++) { tiles[i].Rt.localScale = Vector3.one; SetAlpha(tiles[i], 1f); }
            canvas.enabled = true;
            yield return null; // CanvasScaler ölçeği otursun, kareleri doğru boyutla yeniden diz
            BuildTiles();
            for (int i = 0; i < activeTiles; i++) { tiles[i].Rt.localScale = Vector3.one; SetAlpha(tiles[i], 1f); }
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Animate(false);
            canvas.enabled = false;
            IsBusy = false;
        }

        IEnumerator LoadRoutine(string sceneName, Func<IEnumerator> whileCovered)
        {
            IsBusy = true;
            BuildTiles();
            canvas.enabled = true;
            Sfx.Play(Sound.Whoosh, 0.8f);
            yield return Animate(true);
            IsCovered = true;

            if (whileCovered != null) yield return whileCovered();

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;
            yield return null; // yeni sahnenin Start'ları çalışsın
            yield return new WaitForSecondsRealtime(HoldTime);

            IsCovered = false;
            BuildTiles(); // ekran yönü değişmiş olabilir (ör. yerel 2 kişilik mod)
            for (int i = 0; i < activeTiles; i++) { tiles[i].Rt.localScale = Vector3.one; SetAlpha(tiles[i], 1f); }
            yield return Animate(false);
            canvas.enabled = false;
            IsBusy = false;
        }

        void BuildTiles()
        {
            Canvas.ForceUpdateCanvases();
            Vector2 size = root.rect.size;
            if (size.x < 10f || size.y < 10f) size = new Vector2(1080, 1920);

            float tile = Mathf.Min(size.x, size.y) / 9f;
            int cols = Mathf.CeilToInt(size.x / tile) + 1;
            int rows = Mathf.CeilToInt(size.y / tile) + 1;
            Vector2 origin = new Vector2(-cols * tile / 2f, rows * tile / 2f);

            activeTiles = cols * rows;
            maxWave = Mathf.Max(1, cols + rows - 2);
            while (tiles.Count < activeTiles) tiles.Add(CreateTile(tiles.Count));

            float gap = tile * 0.06f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                bool active = i < activeTiles;
                t.Rt.gameObject.SetActive(active);
                if (!active) continue;

                int r = i / cols, c = i % cols;
                t.Rt.anchorMin = t.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                t.Rt.sizeDelta = new Vector2(tile - gap, tile - gap);
                t.Rt.anchoredPosition = origin + new Vector2(c * tile + tile / 2f, -r * tile - tile / 2f);
                t.Rt.localScale = Vector3.zero;

                bool alt = ((r / 3) + (c / 3)) % 2 == 0;
                t.Img.color = alt ? CozyTheme.Hex("FFF4DE") : CozyTheme.Hex("FFD9B8");
                t.Img.pixelsPerUnitMultiplier = 64f / Mathf.Max(8f, tile * 0.5f);
                int digit = ((r % 9) * 3 + (r % 9) / 3 + c) % 9 + 1;
                t.Digit.text = digit.ToString();
                t.Digit.fontSize = tile * 0.52f;
                t.Digit.color = ((r + c) % 4 == 0) ? CozyTheme.PlayerText : CozyTheme.Ink;
                t.Wave = r + c;
                tiles[i] = t;
            }
            blocker.transform.SetAsFirstSibling();
        }

        Tile CreateTile(int index)
        {
            var img = UI.Rounded(root, Color.white, 1f, "Tile");
            var digit = UI.Text(img.transform, "", 40, CozyTheme.Ink, FontKind.Digits, TextStyle.Plain);
            digit.rectTransform.Stretch();
            return new Tile { Rt = img.rectTransform, Img = img, Digit = digit };
        }

        static void SetAlpha(Tile t, float a)
        {
            var c = t.Digit.color; c.a = a; t.Digit.color = c;
        }

        /// <summary>cover=true: kareler büyür; false: küçülerek kaybolur.</summary>
        IEnumerator Animate(bool cover)
        {
            float total = WaveTime + TileGrow;
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < activeTiles; i++)
                {
                    var t = tiles[i];
                    float start = (float)t.Wave / maxWave * WaveTime;
                    float k = Mathf.Clamp01((elapsed - start) / TileGrow);
                    float s = cover ? Tween.Evaluate(Ease.OutBack, k) : 1f - Tween.Evaluate(Ease.InCubic, k);
                    t.Rt.localScale = new Vector3(s, s, 1f);
                    SetAlpha(t, cover ? Mathf.Clamp01(k * 1.5f - 0.3f) : 1f - k);
                }
                yield return null;
            }
            for (int i = 0; i < activeTiles; i++) tiles[i].Rt.localScale = cover ? Vector3.one : Vector3.zero;
        }
    }
}
