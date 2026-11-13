using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozySudoku
{
    /// <summary>
    /// Arayüzü koddan kurmak için yardımcılar. Böylece sahnelerde kırılgan
    /// referanslar olmaz ve tüm UI tek bir temadan beslenir.
    /// </summary>
    public static class UI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform AnchorRange(this RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static T Layout<T>(this T c, float preferredW = -1, float preferredH = -1, float flexW = -1, float flexH = -1) where T : Component
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredW;
            le.preferredHeight = preferredH;
            le.flexibleWidth = flexW;
            le.flexibleHeight = flexH;
            if (preferredW >= 0) le.minWidth = preferredW;
            if (preferredH >= 0) le.minHeight = preferredH;
            return c;
        }

        // ---------- Canvas ----------

        public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // "Expand": kısa kenar her zaman en az 1080 birim → telefon, tablet ve masaüstünde tutarlı boyut
            scaler.referenceResolution = new Vector2(1080, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Yumuşak gradyan + nokta desenli arka plan.</summary>
        public static RectTransform Background(Transform parent)
        {
            var bg = Rect("Background", parent).Stretch();
            var grad = bg.gameObject.AddComponent<RawImage>();
            grad.texture = CozyTheme.Gradient;
            grad.raycastTarget = false;

            var pattern = Rect("Pattern", bg).Stretch();
            var raw = pattern.gameObject.AddComponent<RawImage>();
            raw.texture = CozyTheme.Dots;
            raw.color = CozyTheme.Pattern.WithAlpha(0.45f);
            raw.raycastTarget = false;
            pattern.gameObject.AddComponent<TiledPattern>();
            return bg;
        }

        // ---------- Görseller ----------

        public static Image Image(Transform parent, string spriteName, Color? color = null, float ppuMultiplier = 1f, string name = null)
        {
            var rt = Rect(name ?? spriteName ?? "Image", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CozyTheme.Sprite(spriteName);
            img.color = color ?? Color.white;
            img.raycastTarget = false;
            if (img.sprite != null && img.sprite.border != Vector4.zero)
            {
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = ppuMultiplier;
            }
            else img.preserveAspect = true;
            return img;
        }

        public static Image Rounded(Transform parent, Color color, float ppuMultiplier = 1f, string name = "Rounded")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CozyTheme.Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
            FontKind font = FontKind.Text, TextStyle style = TextStyle.Plain, TextAlignmentOptions align = TextAlignmentOptions.Center, string name = "Text")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var fa = CozyTheme.Font(font);
            if (fa != null)
            {
                t.font = fa;
                var mat = CozyTheme.Material(fa, style);
                if (mat != null) t.fontSharedMaterial = mat;
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.extraPadding = style != TextStyle.Plain;
            return t;
        }

        /// <summary>Metin kutuya sığmazsa küçülsün.</summary>
        public static TextMeshProUGUI AutoSize(this TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            return t;
        }

        // ---------- Butonlar ----------

        /// <summary>Cozy paketinden hap şeklinde yazılı buton.</summary>
        public static CozyButton Button(Transform parent, string label, string color, Action onClick, string icon = null, float fontSize = 54f, Color? textColor = null)
        {
            var img = Image(parent, "btn_pill_" + color, null, 1f, "Button " + label);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<CozyButton>();
            btn.Init(img, onClick);

            var content = Rect("Content", img.transform).Stretch(36, 36, 8, 22);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 16;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(icon))
            {
                var ic = Image(content, icon, null, 1f, "Icon");
                ic.Layout(fontSize * 1.35f, fontSize * 1.35f);
            }

            var light = color == "cream" || color == "white" || color == "yellow";
            var t = Text(content, label, fontSize, textColor ?? (light ? CozyTheme.Ink : Color.white), FontKind.Text, light ? TextStyle.Soft : TextStyle.Title);
            t.Layout(-1, fontSize * 1.3f);
            btn.Label = t;
            return btn;
        }

        /// <summary>Yuvarlak, ikonlu buton (kalem, silgi, ayarlar...).</summary>
        public static CozyButton IconButton(Transform parent, string color, string icon, Action onClick, float iconScale = 0.55f)
        {
            var img = Image(parent, "btn_round_" + color, null, 1f, "IconButton " + icon);
            img.raycastTarget = true;
            img.preserveAspect = true;
            var btn = img.gameObject.AddComponent<CozyButton>();
            btn.Init(img, onClick);

            var ic = Image(img.transform, icon, null, 1f, "Icon");
            ic.rectTransform.AnchorRange(new Vector2(0.5f - iconScale / 2f, 0.56f - iconScale / 2f), new Vector2(0.5f + iconScale / 2f, 0.56f + iconScale / 2f));
            btn.Icon = ic;
            return btn;
        }

        /// <summary>Kare, sayı tuşu benzeri buton.</summary>
        public static CozyButton SquareButton(Transform parent, string label, string color, Action onClick, float fontSize, FontKind font = FontKind.Digits)
        {
            var img = Image(parent, "btn_square_" + color, null, 1f, "Key " + label);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<CozyButton>();
            btn.Init(img, onClick);
            var t = Text(img.transform, label, fontSize, CozyTheme.Ink, font, TextStyle.Soft);
            t.rectTransform.Stretch(4, 4, 4, 14);
            btn.Label = t;
            return btn;
        }

        // ---------- Giriş alanı ----------

        public static TMP_InputField Input(Transform parent, string placeholder, TMP_InputField.ContentType type, int charLimit, float fontSize = 52f)
        {
            var bg = Image(parent, "slot", null, 1.2f, "Input");
            bg.raycastTarget = true;

            var area = Rect("Text Area", bg.transform).Stretch(34, 34, 14, 24);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Text(area, placeholder, fontSize, CozyTheme.InkSoft.WithAlpha(0.6f), FontKind.Text, TextStyle.Plain, TextAlignmentOptions.Center, "Placeholder");
            ph.rectTransform.Stretch();
            var txt = Text(area, "", fontSize, CozyTheme.Ink, FontKind.Text, TextStyle.Plain, TextAlignmentOptions.Center, "Text");
            txt.rectTransform.Stretch();

            bg.gameObject.SetActive(false); // referanslar atanmadan OnEnable çalışmasın
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = txt;
            field.placeholder = ph;
            field.fontAsset = txt.font;
            field.pointSize = fontSize;
            field.contentType = type;
            field.characterLimit = charLimit;
            field.caretColor = CozyTheme.Ink;
            field.customCaretColor = true;
            field.caretWidth = 4;
            field.selectionColor = CozyTheme.Yellow.WithAlpha(0.6f);
            field.onFocusSelectAll = true;
            field.shouldHideMobileInput = false;
            var colors = field.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.9f);
            colors.pressedColor = new Color(0.95f, 0.9f, 0.82f);
            colors.selectedColor = new Color(1f, 0.97f, 0.88f);
            field.colors = colors;
            bg.gameObject.SetActive(true);
            return field;
        }

        // ---------- Yerleşim ----------

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperCenter, bool expandWidth = true)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleCenter, bool expandWidth = false)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth; h.childForceExpandHeight = false;
            return h;
        }

        public static RectTransform Spacer(Transform parent, float height)
        {
            var rt = Rect("Spacer", parent);
            rt.Layout(-1, height);
            return rt;
        }
    }

    /// <summary>Kart/diyaloglarda kullanılan buton tanımı.</summary>
    public struct ButtonSpec
    {
        public string Label, Color, Icon;
        public Action OnClick;
        public ButtonSpec(string label, string color, Action onClick, string icon = null) { Label = label; Color = color; OnClick = onClick; Icon = icon; }
    }

    /// <summary>
    /// Yumuşak, "squishy" buton: basınca hafifçe ezilir, bırakınca esneyerek geri gelir.
    /// </summary>
    public sealed class CozyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
    {
        public Image Background { get; private set; }
        public TextMeshProUGUI Label { get; set; }
        public Image Icon { get; set; }
        public Sound ClickSound = Sound.Tap;

        Action onClick;
        bool interactable = true;
        bool pressed;
        CanvasGroup group;

        public void Init(Image background, Action click)
        {
            Background = background;
            onClick = click;
        }

        public void SetOnClick(Action click) => onClick = click;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
                group.alpha = value ? 1f : 0.45f;
                if (!value && pressed) Release();
            }
        }

        public void SetSprite(string spriteName)
        {
            var s = CozyTheme.Sprite(spriteName);
            if (s != null && Background != null) Background.sprite = s;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!interactable) return;
            pressed = true;
            Tween.Scale(transform, new Vector3(0.9f, 0.88f, 1f), 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData e) { if (pressed) Release(); }
        public void OnPointerExit(PointerEventData e) { if (pressed) Release(); }

        void Release()
        {
            pressed = false;
            Tween.Scale(transform, Vector3.one, 0.35f, Ease.OutElastic);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable || e.button != PointerEventData.InputButton.Left) return;
            Click();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (!interactable) return;
            Tween.Punch(transform, 0.1f, 0.25f);
            Click();
        }

        void Click()
        {
            Sfx.Play(ClickSound);
            onClick?.Invoke();
        }

        void OnDisable()
        {
            pressed = false;
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>Nokta desenini ekran boyutuna göre döşer (esnemez).</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class TiledPattern : MonoBehaviour
    {
        public float TileSize = 90f;
        RawImage raw;
        RectTransform rt;

        void Awake() { raw = GetComponent<RawImage>(); rt = (RectTransform)transform; }
        void OnEnable() => Apply();
        void OnRectTransformDimensionsChange() => Apply();

        void Apply()
        {
            if (raw == null || rt == null) return;
            var size = rt.rect.size;
            raw.uvRect = new Rect(0, 0, Mathf.Max(1f, size.x / TileSize), Mathf.Max(1f, size.y / TileSize));
        }
    }

    /// <summary>
    /// Çentikli telefonlarda içeriği güvenli alana sığdırır.
    /// </summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastScreen;
        RectTransform rt;

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void Update()
        {
            if (Screen.safeArea != last || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
        }

        void Apply()
        {
            last = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = last.position;
            var max = last.position + last.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Açma/kapama anahtarı (paketteki ON/OFF sprite'ları).</summary>
    public sealed class CozyToggle : MonoBehaviour, IPointerClickHandler
    {
        Image image;
        bool value;
        Action<bool> changed;

        public static CozyToggle Create(Transform parent, bool initial, Action<bool> onChanged)
        {
            var img = UI.Image(parent, initial ? "toggle_on" : "toggle_off", null, 1f, "Toggle");
            img.raycastTarget = true;
            img.preserveAspect = true;
            var t = img.gameObject.AddComponent<CozyToggle>();
            t.image = img;
            t.value = initial;
            t.changed = onChanged;
            return t;
        }

        public void OnPointerClick(PointerEventData e)
        {
            value = !value;
            image.sprite = CozyTheme.Sprite(value ? "toggle_on" : "toggle_off");
            Tween.Punch(transform, 0.12f, 0.25f);
            Sfx.Play(Sound.Pop);
            changed?.Invoke(value);
        }
    }
}
