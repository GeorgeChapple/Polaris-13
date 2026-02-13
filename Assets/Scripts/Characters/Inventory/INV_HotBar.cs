using UnityEngine;

// Made By: Jason Lodge
// Summary: Hot bar, should set up hotbar slots and use item instance for what item is in it.

public class INV_HotBar : MonoBehaviour
{
    [Tooltip("Amount of spaces in hot bar.")]
    [SerializeField] private int hotbarSpaces;

    [Tooltip("Hotbar parent with HorizontalLayoutGroup")]
    private RectTransform hotBarRoot;

    [Tooltip("Parent all item instances in hotbar will be under")]
    private RectTransform itemHotBarRoot;

    private void Awake()
    {
        BuildSlots();
    }

    private void BuildSlots()
    {

    }
}
