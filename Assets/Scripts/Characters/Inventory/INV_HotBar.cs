using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Hot bar, should just select the item in the corresponding slot
// for use using the CC_INV_EquippedItem script.

public class INV_HotBar : MonoBehaviour
{
    [Tooltip("Amount of spaces in hot bar.")]
    [SerializeField] private int hotbarSpaces = 4;

    [Tooltip("Hotbar parent with HorizontalLayoutGroup.")]
    [SerializeField] private RectTransform hotBarRoot;

    [Tooltip("Individual Hotbar slots. Please keep at max of four.")]
    [SerializeField] private List<RectTransform> hotBarSlots = new List<RectTransform>(4);

    [Tooltip("Inventory component on player.")]
    [SerializeField] private INV_Inventory inventory;

    [Tooltip("Network inventory bridge on player.")]
    [SerializeField] private INV_PlayerInventoryNet playerInventoryNet;

    [Tooltip("Logs hotbar actions.")]
    [SerializeField] private bool logHotbar;

    private INV_Inventory.ItemInstance[] slotItems;
    private int selectedSlot = -1;

    public int SelectedSlot => selectedSlot;
    public int SlotCount => hotbarSpaces;

    private void Awake()
    {
        hotbarSpaces = Mathf.Clamp(hotbarSpaces, 1, 4);

        if (inventory == null)
        {
            inventory = GetComponentInParent<INV_Inventory>();
        }

        if (playerInventoryNet == null)
        {
            playerInventoryNet = GetComponentInParent<INV_PlayerInventoryNet>();
        }

        slotItems = new INV_Inventory.ItemInstance[hotbarSpaces];

        RefreshAllVisuals();
    }

    public bool AssignHoverItemToSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) { return false; }
        if (inventory == null) { return false; }
        if (inventory.hoverItem == null) { return false; }
        if (inventory.hoverItem.Instance == null) { return false; }

        INV_Inventory.ItemInstance inst = inventory.hoverItem.Instance;

        // remove old reference if this item was already assigned elsewhere
        for (int i = 0; i < slotItems.Length; i++)
        {
            if (slotItems[i] == inst)
            {
                slotItems[i] = null;
            }
        }

        slotItems[slotIndex] = inst;

        if (logHotbar && inst.data != null)
        {
            Debug.Log($"Hotbar, Assigned: {inst.data.Name} to slot {slotIndex}");
        }

        RefreshAllVisuals();

        return true;
    }

    public bool SelectSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) { return false; }

        selectedSlot = slotIndex;
        RefreshAllVisuals();
        RefreshEquippedItem();
        return true;
    }

    public bool CycleSelection(int direction)
    {
        if (hotbarSpaces <= 0) { return false; }
        if (direction == 0) { return false; }

        if (selectedSlot < 0)
        {
            selectedSlot = 0;
            RefreshAllVisuals();
            RefreshEquippedItem();
            return true;
        }

        if (direction > 0)
        {
            selectedSlot++;
            if (selectedSlot >= hotbarSpaces)
            {
                selectedSlot = 0;
            }
        }
        else
        {
            selectedSlot--;
            if (selectedSlot < 0)
            {
                selectedSlot = hotbarSpaces - 1;
            }
        }

        RefreshAllVisuals();
        RefreshEquippedItem();
        return true;
    }

    public INV_Inventory.ItemInstance GetItemInSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) { return null; }
        return slotItems[slotIndex];
    }

    public int GetAssignedSlotForItem(INV_Inventory.ItemInstance item)
    {
        if (item == null || slotItems == null) { return -1; }

        for (int i = 0; i < slotItems.Length; i++)
        {
            if (slotItems[i] == item)
            {
                return i;
            }
        }

        return -1;
    }

    public void ClearReferencesToItem(INV_Inventory.ItemInstance item)
    {
        if (item == null || slotItems == null) { return; }

        bool selectedChanged = false;
        bool anyChanged = false;

        for (int i = 0; i < slotItems.Length; i++)
        {
            if (slotItems[i] == item)
            {
                slotItems[i] = null;
                anyChanged = true;

                if (i == selectedSlot)
                {
                    selectedChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            RefreshAllVisuals();
        }

        if (selectedChanged)
        {
            RefreshEquippedItem();
        }
    }

    public void RefreshAllVisuals()
    {
        RefreshInventoryItemSlotLabels();
        RefreshHotbarSlotVisuals();
    }

    private void RefreshInventoryItemSlotLabels()
    {
        if (inventory == null) { return; }

        List<INV_Inventory.ItemInstance> items = inventory.Items;
        if (items == null) { return; }

        for (int i = 0; i < items.Count; i++)
        {
            INV_Inventory.ItemInstance inst = items[i];
            if (inst == null || inst.uiHandler == null) { continue; }

            int slotIndex = GetAssignedSlotForItem(inst);
            inst.uiHandler.SetHotbarSlotLabel(slotIndex);
        }
    }

    private void RefreshHotbarSlotVisuals()
    {
        for (int i = 0; i < hotBarSlots.Count; i++)
        {
            RectTransform slot = hotBarSlots[i];
            if (slot == null) { continue; }

            Transform visualRoot = slot.transform.GetChild(0);
            if (visualRoot == null) { continue; }

            MeshFilter mf = visualRoot.GetComponent<MeshFilter>();
            MeshRenderer mr = visualRoot.GetComponent<MeshRenderer>();

            if (mf == null) { mf = visualRoot.gameObject.AddComponent<MeshFilter>(); }
            if (mr == null) { mr = visualRoot.gameObject.AddComponent<MeshRenderer>(); }

            INV_Inventory.ItemInstance inst = GetItemInSlot(i);

            if (inst == null || inst.data == null)
            {
                mf.sharedMesh = null;
                mr.sharedMaterial = null;
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
                continue;
            }

            mf.sharedMesh = inst.data.Mesh;
            mr.sharedMaterial = inst.data.Material;

            // hotbar uses inventory scale
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one * inst.data.InventoryMeshScale;
        }
    }

    private void RefreshEquippedItem()
    {
        if (playerInventoryNet == null)
        {
            playerInventoryNet = GetComponentInParent<INV_PlayerInventoryNet>();
        }

        if (playerInventoryNet == null)
        {
            Debug.LogError("INV_HotBar could not find INV_PlayerInventoryNet.", this);
            return;
        }

        INV_Inventory.ItemInstance selectedItem = GetItemInSlot(selectedSlot);

        if (selectedItem == null || selectedItem.data == null || string.IsNullOrWhiteSpace(selectedItem.data.ItemID))
        {
            if (logHotbar)
            {
                Debug.Log($"Hotbar, Cleared equipped item from slot {selectedSlot}");
            }

            playerInventoryNet.RequestClearEquippedItem();
            return;
        }

        if (logHotbar)
        {
            Debug.Log($"Hotbar, Equipped slot {selectedSlot}: {selectedItem.data.Name}");
        }

        playerInventoryNet.RequestEquipItem(selectedItem.data.ItemID);
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < hotbarSpaces;
    }
}