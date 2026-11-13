using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CozySudoku
{
    public enum FontKind { Text, Digits }
    public enum TextStyle { Plain, Soft, Title }

    /// <summary>
    /// Oyunun yumuşak/cozy görsel dili: renkler, sprite'lar ve kalın yuvarlak fontlar.
    /// Fontlar (Nunito Black + Fredoka Bold) çalışma anında dinamik TMP font asset'ine
    /// dönüştürülür; böylece her platformda aynı görünür ve önceden atlas üretmek gerekmez.
    /// </summary>
    public static class CozyTheme
    {
        // ---------- Renk paleti ----------
        public static readonly Color BgTop = Hex("FFF6E6");
        public static readonly Color BgBottom = Hex("FCDCC2");
        public static readonly Color Pattern = Hex("F3C9A6");
        public static readonly Color Ink = Hex("6B4234");
        public static readonly Color InkSoft = Hex("A07560");
        public static readonly Color Cream = Hex("FFFBEF");
        public static readonly Color White = Color.white;

        public static readonly Color Blue = Hex("6BB8E6");
        public static readonly Color Pink = Hex("FF8FB4");
        public static readonly Color Green = Hex("74C766");
        public static readonly Color Yellow = Hex("FFD978");
        public static readonly Color Red = Hex("EE6677");
        public static readonly Color Teal = Hex("4FB5A8");

        // Tahta
        public static readonly Color BoardGap = Hex("E2B993");
        public static readonly Color CellBg = Hex("FFFCF4");
        public static readonly Color CellRelated = Hex("FCEBD5");
        public static readonly Color CellSameDigit = Hex("FFDDB0");
        public static readonly Color CellSelected = Hex("FFD27A");
        public static readonly Color CellWrong = Hex("FFC2C6");
        public static readonly Color GivenText = Hex("6B4234");
        public static readonly Color PlayerText = Hex("4C93C9");
        public static readonly Color WrongText = Hex("E0485C");
        public static readonly Color NoteText = Hex("B08C78");

        // Bölünmüş ekranda oyuncu renkleri
        public static readonly Color[] PlayerColors = { Hex("6BB8E6"), Hex("FF8FB4"), Hex("74C766"), Hex("FFC35C") };
        public static readonly string[] PlayerSpriteColors = { "blue", "pink", "green", "yellow" };

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        // ---------- Sprite'lar ----------
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Sprite Sprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (sprites.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("CozyUI/" + name);
            if (s == null) Debug.LogWarning($"[CozyTheme] Sprite bulunamadı: {name}");
            sprites[name] = s;
            return s;
        }

        static Sprite rounded, circle;
        static Texture2D gradient, dots;

        /// <summary>Kodla üretilmiş yuvarlak köşeli beyaz kare (9-slice). Hücreler için hafif ve renklendirilebilir.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeRounded(64, 22, out _);
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = MakeRounded(64, 32, out _, false);
                return circle;
            }
        }

        static Sprite MakeRounded(int size, int radius, out Texture2D tex, bool sliced = true)
        {
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "cozy_rounded" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>Arka plan için dikey yumuşak gradyan.</summary>
        public static Texture2D Gradient
        {
            get
            {
                if (gradient != null) return gradient;
                gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "cozy_gradient" };
                for (int y = 0; y < 64; y++)
                    gradient.SetPixel(0, y, Color.Lerp(BgBottom, BgTop, Mathf.SmoothStep(0f, 1f, y / 63f)));
                gradient.Apply(false, true);
                return gradient;
            }
        }

        /// <summary>Tekrarlanan yumuşak nokta deseni (defter/kumaş hissi).</summary>
        public static Texture2D Dots
        {
            get
            {
                if (dots != null) return dots;
                const int size = 64;
                dots = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "cozy_dots" };
                var px = new Color32[size * size];
                Vector2 a = new Vector2(16, 16), b = new Vector2(48, 48);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(Vector2.Distance(p, a), Vector2.Distance(p, b));
                    float alpha = Mathf.Clamp01(5.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
                dots.SetPixels32(px);
                dots.Apply(false, true);
                return dots;
            }
        }

        // ---------- Fontlar ----------
        static TMP_FontAsset textFont, digitFont;
        static readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();

        public static TMP_FontAsset Font(FontKind kind)
        {
            if (kind == FontKind.Digits)
            {
                if (digitFont == null)
                {
                    digitFont = Create("Fonts/FredokaBold");
                    if (digitFont != null && Font(FontKind.Text) != null) AddFallback(digitFont, textFont);
                }
                return digitFont != null ? digitFont : Font(FontKind.Text);
            }

            if (textFont == null)
            {
                textFont = Create("Fonts/NunitoBlack");
                if (textFont == null) textFont = TMP_Settings.defaultFontAsset; // son çare
            }
            return textFont;
        }

        static TMP_FontAsset Create(string path)
        {
            var source = Resources.Load<Font>(path);
            if (source == null)
            {
                Debug.LogWarning($"[CozyTheme] Font bulunamadı: {path}");
                return null;
            }
            var fa = TMP_FontAsset.CreateFontAsset(source, 72, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null) return null;
            fa.name = source.name + " (Cozy SDF)";
            // Sık kullanılan karakterleri önceden ekle → oyun sırasında takılma olmaz
            fa.TryAddCharacters("0123456789/:×x ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZabcçdefgğhıijklmnoöprsştuüvyz.,!?-+()%'\"");
            return fa;
        }

        static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
        }

        /// <summary>Başlıklarda kahverengi kontur + yumuşak gölge; Soft'ta sadece hafif gölge.</summary>
        public static Material Material(TMP_FontAsset font, TextStyle style)
        {
            if (font == null) return null;
            if (style == TextStyle.Plain) return font.material;

            long key = ((long)font.GetInstanceID() << 2) | (long)style;
            if (materials.TryGetValue(key, out var m) && m != null) return m;

            // Şablon materyaller Resources'ta durur → build'de doğru shader varyantları korunur
            var template = Resources.Load<Material>(style == TextStyle.Title ? "Fonts/TMP_CozyShadow" : "Fonts/TMP_CozySoft");
            m = new Material(font.material) { name = font.name + " " + style };
            if (template != null) m.shader = template.shader;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);

            if (style == TextStyle.Title)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                m.SetColor(ShaderUtilities.ID_OutlineColor, Hex("8A4B36"));
                m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.18f);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, Hex("7A3E2B").WithAlpha(0.6f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.75f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            }
            else
            {
                m.DisableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, Hex("7A3E2B").WithAlpha(0.22f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            }
            materials[key] = m;
            return m;
        }
    }
}
