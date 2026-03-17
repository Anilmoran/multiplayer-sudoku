using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SudokuCell : MonoBehaviour
{
    public int row;
    public int col;
    public int currentValue = 0;
    public bool isStartingClue = false;
    public TextMeshProUGUI numberText;

    // Kalem modu için not objeleri
    public GameObject[] noteObjects;
    private bool[] notesActive = new bool[9];

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnCellClicked);
    }

    public void Setup(int r, int c)
    {
        row = r;
        col = c;
    }

    public void SetValue(int value, bool isClue = false)
    {
        currentValue = value;
        isStartingClue = isClue;
        numberText.text = value == 0 ? "" : value.ToString();

        if (isClue)
        {
            numberText.color = Color.black;
            button.interactable = false;
        }
        else
        {
            numberText.color = Color.blue;
            button.interactable = true;
        }

        // Kesin sayý girilince tüm notlarý temizle
        if (value != 0)
        {
            ClearAllNotes();
        }
    }

    public void ToggleNote(int number)
    {
        int index = number - 1;
        notesActive[index] = !notesActive[index];
        noteObjects[index].SetActive(notesActive[index]);
    }

    public void ClearAllNotes()
    {
        for (int i = 0; i < 9; i++)
        {
            notesActive[i] = false;
            noteObjects[i].SetActive(false);
        }
    }

    // Doðru sayý girildiðinde çevredeki o sayýya ait notlarý silmek için
    public void RemoveNote(int number)
    {
        int index = number - 1;
        if (notesActive[index])
        {
            notesActive[index] = false;
            noteObjects[index].SetActive(false);
        }
    }

    void OnCellClicked()
    {
        GridManager.Instance.SelectCell(this);
    }
}