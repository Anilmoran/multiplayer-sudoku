using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CozySudoku
{
    /// <summary>Yumuşak açılan onay/bilgi penceresi.</summary>
    public static class Dialog
    {
        static GameObject current;
        static Action onCancel;

        public static bool IsOpen => current != null;

        public static void Show(Transform parent, string title, string message, Action cancel, params ButtonSpec[] buttons)
        {
            Close(false);
            onCancel = cancel;

            var root = UI.Rect("Dialog", parent).Stretch();
            current = root.gameObject;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Tween.Fade(group, 1f, 0.2f);

            var dim = UI.Rounded(root, CozyTheme.Ink.WithAlpha(0.45f), 0.3f, "Dim");
            dim.rectTransform.Stretch(-50, -50, -50, -50);
            dim.raycastTarget = true;

            Vector2 size = ((RectTransform)parent).rect.size;
            float cardW = Mathf.Min(size.x * 0.88f, 860f);

            var card = UI.Image(root, "panel_cream", null, 1f, "Card");
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cardW, 0));
            UI.VStack(card.rectTransform, 26, new RectOffset(60, 60, 120, 80), TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var ribbon = UI.Image(card.transform, "ribbon_pink", null, 1.4f, "Ribbon");
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ribbon.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0, 10f), new Vector2(cardW * 1.08f, 150f));
            var t = UI.Text(ribbon.transform, title, 64, Color.white, FontKind.Text, TextStyle.Title);
            t.rectTransform.Stretch(60, 60, 10, 40);
            t.AutoSize(30, 64);

            var msg = UI.Text(card.transform, message, 44, CozyTheme.Ink, FontKind.Text, TextStyle.Soft);
            msg.textWrappingMode = TextWrappingModes.Normal;

            var row = UI.Rect("Buttons", card.transform);
            var h = UI.HStack(row, 24, null, TextAnchor.MiddleCenter, true);
            h.childForceExpandWidth = true;
            row.Layout(-1, 130);
            foreach (var spec in buttons)
            {
                var s = spec;
                var b = UI.Button(row, s.Label, s.Color, () => { Close(false); s.OnClick?.Invoke(); }, s.Icon, 46);
                b.Layout(-1, 125, 1, -1);
            }

            Tween.ScaleFrom(card.transform, new Vector3(0.7f, 0.7f, 1f), Vector3.one, 0.4f, Ease.OutBack);
            Sfx.Play(Sound.Pop);
        }

        /// <summary>Pencereyi kapatır. invokeCancel=true ise "vazgeç" eylemi çalışır (geri tuşu).</summary>
        public static void Close(bool invokeCancel)
        {
            if (current != null) UnityEngine.Object.Destroy(current);
            current = null;
            var c = onCancel;
            onCancel = null;
            if (invokeCancel) c?.Invoke();
        }
    }
}
