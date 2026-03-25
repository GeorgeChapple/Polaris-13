using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Crafting button, spawned by INV_Crafting as a prefab.
// Displays the item name, requirements text and mesh visual,
// then calls back into INV_Crafting when pressed.

public class INV_CraftingButtonUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI requirementsText;

    [Header("Mesh Visual")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    private INV_Crafting crafting;
    private INV_Item item;

    public void Init(INV_Crafting craftingRef, INV_Item itemRef, bool interactable, string requirementString)
    {
        crafting = craftingRef;
        item = itemRef;

        if (itemNameText != null)
        {
            itemNameText.SetText(item != null ? item.Name : "Null Item");
        }

        if (requirementsText != null)
        {
            requirementsText.SetText(requirementString);
        }

        if (meshFilter != null)
        {
            meshFilter.sharedMesh = item != null ? item.Mesh : null;
        }

        if (meshRenderer != null)
        {
            if (item != null && item.Material != null)
            {
                meshRenderer.sharedMaterial = item.Material;
                meshRenderer.enabled = item.Mesh != null;
            }
            else
            {
                meshRenderer.enabled = false;
            }
        }

        if (button != null)
        {
            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnPressed);
        }
    }

    private void OnPressed()
    {
        if (crafting == null || item == null)
        {
            return;
        }

        crafting.TryCraftItem(item);
    }
}