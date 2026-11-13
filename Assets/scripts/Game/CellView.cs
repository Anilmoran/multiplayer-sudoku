using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozySudoku
{
    public enum CellHighlight { None, Related, SameDigit, Selected }

    /// <summary>
    /// Tek bir sudoku hücresinin görseli. Notlar tek bir TMP metninde (3x3 eş aralıklı)
    /// tutulur ve ancak ilk not alındığında oluşturulur → hücre başına en fazla 3 grafik.
    /// </summary>
    public sealed class CellView : MonoBehaviour, IPointerDownHandler
    {
        public int Index { get; private set; }
        public RectTransform Rt { get; private set; }

        Image bg;
        TextMeshProUGUI valueText;
        TextMeshProUGUI notesText;
        Action<int> onPressed;
        CellHighlight highlight = CellHighlight.None;
        bool flashing;
        float size = 90f;

        static string[] noteStrings;

        public static CellView Create(Transform parent, int index, Action<int> pressed)
        {
            var img = UI.Rounded(parent, CozyTheme.CellBg, 1f, "Cell " + index);
            img.raycastTarget = true;
            var cell = img.gameObject.AddComponent<CellView>();
            cell.Index = index;
            cell.Rt = img.rectTransform;
            cell.bg = img;
            cell.onPressed = pressed;
            cell.valueText = UI.Text(img.transform, "", 50, CozyTheme.GivenText, FontKind.Digits, TextStyle.Plain, TextAlignmentOptions.Center, "Value");
            cell.valueText.rectTransform.Stretch(0, 0, 0, 0);
            return cell;
        }

        public void OnPointerDown(PointerEventData e) => onPressed?.Invoke(Index);

        public void Layout(Vector2 pos, float cellSize)
        {
            size = cellSize;
            Rt.anchorMin = Rt.anchorMax = new Vector2(0f, 1f);
            Rt.pivot = new Vector2(0.5f, 0.5f);
            Rt.sizeDelta = new Vector2(cellSize, cellSize);
            Rt.anchoredPosition = pos;
            bg.pixelsPerUnitMultiplier = 22f / Mathf.Max(4f, cellSize * 0.18f);
            valueText.fontSize = cellSize * 0.66f;
            if (notesText != null) notesText.fontSize = cellSize * 0.27f;
        }

        public void SetValue(int value, bool given)
        {
            valueText.text = value == 0 ? string.Empty : DigitString(value);
            valueText.color = given ? CozyTheme.GivenText : CozyTheme.PlayerText;
            if (value != 0 && notesText != null) notesText.text = string.Empty;
        }

        public void SetNotes(ushort mask)
        {
            if (mask == 0)
            {
                if (notesText != null) notesText.text = string.Empty;
                return;
            }
            if (notesText == null)
            {
                notesText = UI.Text(transform, "", size * 0.27f, CozyTheme.NoteText, FontKind.Digits, TextStyle.Plain, TextAlignmentOptions.Center, "Notes");
                notesText.rectTransform.Stretch(2, 2, 2, 2);
                notesText.lineSpacing = -8f;
                notesText.textWrappingMode = TextWrappingModes.NoWrap;
                notesText.richText = true;
                notesText.transform.SetAsFirstSibling();
            }
            notesText.text = NoteString(mask);
        }

        public void SetHighlight(CellHighlight h)
        {
            highlight = h;
            if (!flashing) bg.color = HighlightColor(h);
        }

        static Color HighlightColor(CellHighlight h)
        {
            switch (h)
            {
                case CellHighlight.Selected: return CozyTheme.CellSelected;
                case CellHighlight.SameDigit: return CozyTheme.CellSameDigit;
                case CellHighlight.Related: return CozyTheme.CellRelated;
                default: return CozyTheme.CellBg;
            }
        }

        /// <summary>Yanlış sayı: kırmızı göster, titret, sonra yumuşakça eski haline dön.</summary>
        public void FlashWrong(int digit)
        {
            flashing = true;
            valueText.text = DigitString(digit);
            valueText.color = CozyTheme.WrongText;
            bg.color = CozyTheme.CellWrong;
            Tween.Punch(transform, 0.12f, 0.25f);
            Tween.Run(this, Tween.ChExtra, 0.75f, Ease.Linear, k =>
            {
                if (k < 0.55f) return;
                float f = (k - 0.55f) / 0.45f;
                bg.color = Color.Lerp(CozyTheme.CellWrong, HighlightColor(highlight), f);
                valueText.color = CozyTheme.WrongText.WithAlpha(1f - f);
            }, () =>
            {
                flashing = false;
                valueText.text = string.Empty;
                valueText.color = CozyTheme.PlayerText;
                bg.color = HighlightColor(highlight);
            });
        }

        /// <summary>Flash yarıda kalırsa (ör. doğru sayı girildi) durumu temizle.</summary>
        public void CancelFlash()
        {
            if (!flashing) return;
            Tween.Stop(this, Tween.ChExtra);
            flashing = false;
            bg.color = HighlightColor(highlight);
        }

        public void PopIn(float delay = 0f)
        {
            Tween.Run(transform, Tween.ChScale, 0.35f, Ease.Linear, k =>
            {
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.18f * (1f - k * 0.4f);
                transform.localScale = new Vector3(s, s, 1f);
            }, null, delay);
        }

        // ---------- Önbelleğe alınmış metinler (GC'siz) ----------

        static readonly string[] digitStrings = { "", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        static string DigitString(int d) => digitStrings[Mathf.Clamp(d, 0, 9)];

        static string NoteString(ushort mask)
        {
            if (noteStrings == null)
            {
                noteStrings = new string[512];
                var sb = new StringBuilder(64);
                for (int m = 0; m < 512; m++)
                {
                    sb.Clear();
                    sb.Append("<mspace=0.78em>");
                    for (int d = 1; d <= 9; d++)
                    {
                        sb.Append((m & (1 << (d - 1))) != 0 ? (char)('0' + d) : ' ');
                        if (d % 3 == 0 && d < 9) sb.Append('\n');
                    }
                    sb.Append("</mspace>");
                    noteStrings[m] = sb.ToString();
                }
            }
            return noteStrings[mask & 0x1FF];
        }
    }
}
