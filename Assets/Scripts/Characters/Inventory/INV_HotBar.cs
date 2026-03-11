using UnityEngine;

// Made By: Jason Lodge
// Summary: Hot bar, should just select the item in the corresponding slot
// for use using the CC_INV_EquippedItem script.

public class INV_HotBar : MonoBehaviour
{
    [Tooltip("Amount of spaces in hot bar.")]
    [SerializeField] private int hotbarSpaces;

    [Tooltip("Hotbar parent with HorizontalLayoutGroup.")]
    private RectTransform hotBarRoot;

    [Tooltip("Inventory component on player.")]
    private INV_Inventory inventory;

    private void Awake()
    {
        
    }
}
