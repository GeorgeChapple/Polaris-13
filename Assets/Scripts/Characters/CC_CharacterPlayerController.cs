using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;

// Made by: Jason Lodge
// Summary: Player controller, drives all the local player input, menus, camera and interaction.
// Movement itself is host/server authoritative through movement script.

[RequireComponent(typeof(CC_Movement))]
[RequireComponent(typeof(CC_CameraController))]
[RequireComponent(typeof(CC_Interaction))]
public class CC_CharacterPlayerController : NetworkBehaviour
{
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif

    private CC_PlayerInputManager input;
    private CC_Movement movement;
    private CC_CameraController cameraController;
    private CC_Interaction interaction;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;

    [Tooltip("Simple centered cursor image.")]
    [SerializeField] private Image cursorImage;

    [Tooltip("Default cursor sprite.")]
    [SerializeField] private Sprite defaultCursorSprite;

    [Tooltip("Cursor sprite shown when looking at an interactable.")]
    [SerializeField] private Sprite interactCursorSprite;

    [Header("Menus")]
    [Tooltip("True when any menu is open, will stop movement / camera / interaction and free cursor.")]
    [SerializeField] private bool inMenu;

    [Tooltip("Pause menu root.")]
    [SerializeField] private GameObject pauseMenuRoot;

    [Tooltip("Monitoring menu root. (Inventory, Crafting, and Status)")]
    [SerializeField] private GameObject monitoringMenuRoot;

    [Tooltip("Interact menu ui.")]
    [SerializeField] private UI_InteractMenu interactMenu;

    [Tooltip("Inventory")]
    [SerializeField] private INV_Inventory inventory;

    [Tooltip("Network Inventory Handler")]
    [SerializeField] private INV_PlayerInventoryNet inventoryNet;

    [Tooltip("Crafting")]
    [SerializeField] private INV_Crafting crafting;

    [Tooltip("Character Values")]
    [SerializeField] private CC_CharacterValues values;

    [Tooltip("Hotbar")]
    [SerializeField] private INV_HotBar hotBar;

    private bool cursorLocked;

    // internal press guards so hold wont spam toggle
    private bool useHeld;
    private bool pauseHeld;
    private bool monitoringMenuHeld;
    private bool rotateHeld;
    private bool dropHeld;
    private bool useInInvHeld;

    private bool hotbarSlot1Held;
    private bool hotbarSlot2Held;
    private bool hotbarSlot3Held;
    private bool hotbarSlot4Held;
    private int lastHotbarScrollDirection;

    public bool InMenu => inMenu;

    private bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return playerInput != null && playerInput.currentControlScheme == "KeyboardMouse";
#else
            return false;
#endif
        }
    }

    private bool HasInputAuthority()
    {
        return movement != null && movement.HasInputAuthority();
    }

    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
#endif
        input = GetComponent<CC_PlayerInputManager>();
        movement = GetComponent<CC_Movement>();
        cameraController = GetComponent<CC_CameraController>();
        interaction = GetComponent<CC_Interaction>();

        if (interaction != null && interaction.playerController == null)
        {
            interaction.playerController = this;
        }

        if (inventory == null)
        {
            inventory = GetComponentInChildren<INV_Inventory>();
        }

        if (inventoryNet == null)
        {
            inventoryNet = GetComponentInChildren<INV_PlayerInventoryNet>();
        }

        if (crafting == null)
        {
            crafting = GetComponentInChildren<INV_Crafting>();
        }

        if (values == null)
        {
            values = GetComponent<CC_CharacterValues>();
        }

        if (hotBar == null)
        {
            hotBar = GetComponentInChildren<INV_HotBar>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ApplyOwnershipState();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        ApplyOwnershipState();
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        ApplyOwnershipState();
    }

    private void ApplyOwnershipState()
    {
        bool local = HasInputAuthority();

#if ENABLE_INPUT_SYSTEM
        if (playerInput != null)
        {
            playerInput.enabled = local;
        }
#endif

        // disable this whole controller for non local players
        enabled = local;

        if (!local)
        {
            return;
        }

        if (inMenu)
        {
            SetCursorLocked(false);
        }
        else if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void Start()
    {
        if (!HasInputAuthority()) { return; }

        // if we start in a menu, dont lock
        if (inMenu)
        {
            SetCursorLocked(false);
            SetMenuRoots(false, false);
            if (interactMenu != null) { interactMenu.CloseMenu(false); }
            return;
        }

        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }

        SetMenuRoots(false, false);

        if (interactMenu != null)
        {
            interactMenu.CloseMenu(false);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // if a menu is open, dont re-lock cursor
        if (!HasInputAuthority()) { return; }

        if (hasFocus && cursorLocked && !inMenu)
        {
            SetCursorLocked(true);
        }
    }

    private void Update()
    {
        if (!HasInputAuthority())
        {
            return;
        }

        // dead players cannot use input
        if (values != null && values.isDead)
        {
            HandleDeadState();
            UpdateCursorUI();
            SendMovementInput();
            return;
        }

        // open/close menu logic
        HandleMenuInput();

        HandleItemUse();

        // one-off rotate/drop while monitoring menu is open
        HandleInventoryActions();

        // hotbar assign / selection
        HandleHotbarInput();

        // cursor
        UpdateCursorUI();

        // owner sends input snapshot to the server every frame
        SendMovementInput();
    }

    private void LateUpdate()
    {
        bool isMouse = IsCurrentDeviceMouse;

        if (values != null && values.isDead)
        {
            if (cameraController != null) { cameraController.TickLate(Vector2.zero, isMouse); }
            if (interaction != null) { interaction.TickInteract(false); }
            return;
        }

        if (inMenu)
        {
            if (cameraController != null) { cameraController.TickLate(Vector2.zero, isMouse); }
            if (interaction != null) { interaction.TickInteract(false); }
            return;
        }

        if (cameraController != null) { cameraController.TickLate(input.look, isMouse); }
        if (interaction != null) { interaction.TickInteract(input.interact); }
    }

    private void SendMovementInput()
    {
        if (movement == null || input == null)
        {
            return;
        }

        CC_PlayerMoveInput moveInput = new CC_PlayerMoveInput
        {
            move = inMenu ? Vector2.zero : input.move,
            look = inMenu ? Vector2.zero : input.look,
            roll = inMenu ? 0f : input.roll,
            jump = !inMenu && input.jump,
            sprint = !inMenu && input.sprint,
            crouch = !inMenu && input.crouch,
            stabiliseThrusters = !inMenu && input.stabiliseThrusters,
            tick = GetLocalTick()
        };

        movement.SubmitOwnerInput(moveInput);
    }

    private uint GetLocalTick()
    {
        if (NetworkManager == null || NetworkManager.NetworkTickSystem == null)
        {
            return 0;
        }

        return (uint)NetworkManager.NetworkTickSystem.LocalTime.Tick;
    }

    private void UpdateCursorUI()
    {
        if (!HasInputAuthority()) { return; }
        if (cursorImage == null) { return; }

        // hide cursor while menu is open
        if (inMenu)
        {
            if (cursorImage.gameObject.activeSelf)
            {
                cursorImage.gameObject.SetActive(false);
            }

            return;
        }

        if (!cursorImage.gameObject.activeSelf)
        {
            cursorImage.gameObject.SetActive(true);
        }

        bool lookingAtInteractable = interaction != null && interaction.HasLookInteractable();

        if (lookingAtInteractable && interactCursorSprite != null)
        {
            cursorImage.sprite = interactCursorSprite;
        }
        else
        {
            cursorImage.sprite = defaultCursorSprite;
        }
    }

    private void HandleItemUse()
    {
        if (!HasInputAuthority() || inMenu || inventoryNet == null)
        {
            useHeld = false;
            return;
        }

        bool pressed = input.useItemPrimary;

        if (pressed && !useHeld)
        {
            useHeld = true;
            TryUseEquippedItem();
        }
        else if (!pressed && useHeld)
        {
            useHeld = false;
        }
    }

    private void TryUseEquippedItem()
    {
        if (inventoryNet == null) { return; }

        string equippedItemId = inventoryNet.GetEquippedItemId();
        if (string.IsNullOrWhiteSpace(equippedItemId)) { return; }

        inventoryNet.RequestUseEquippedItem();
    }

    private void HandleInventoryActions()
    {
        // only allow these when we're in a menu and monitoring menu is actually open
        if (!inMenu || !IsMonitoringMenuOpen() || inventory == null)
        {
            rotateHeld = false;
            dropHeld = false;
            useInInvHeld = false;
            return;
        }

        // use item in inventory (consume)
        if (input.interact && !useInInvHeld)
        {
            useInInvHeld = true;

            if (inventory.hoverItem != null &&
                inventory.hoverItem.Instance != null &&
                inventory.hoverItem.Instance.data != null &&
                !inventory.hoverItem.Instance.isChestItem)
            {
                inventoryNet.RequestUseItemInInventory(inventory.hoverItem.Instance.data.ItemID);
            }
        }
        else if (!input.interact && useInInvHeld)
        {
            useInInvHeld = false;
        }

        // rotate
        if (input.rotateItem && !rotateHeld)
        {
            rotateHeld = true;
            inventory.RotateItem();
        }
        else if (!input.rotateItem && rotateHeld)
        {
            rotateHeld = false;
        }

        // drop
        if (input.dropItem && !dropHeld)
        {
            dropHeld = true;
            inventory.DropHeldOrHoverItem();
        }
        else if (!input.dropItem && dropHeld)
        {
            dropHeld = false;
        }
    }

    private void HandleHotbarInput()
    {
        if (hotBar == null) { return; }

        bool monitoringMenuOpen = inMenu && IsMonitoringMenuOpen();

        HandleHotbarSlotPress(input.hotbarSlot1, ref hotbarSlot1Held, 0, monitoringMenuOpen);
        HandleHotbarSlotPress(input.hotbarSlot2, ref hotbarSlot2Held, 1, monitoringMenuOpen);
        HandleHotbarSlotPress(input.hotbarSlot3, ref hotbarSlot3Held, 2, monitoringMenuOpen);
        HandleHotbarSlotPress(input.hotbarSlot4, ref hotbarSlot4Held, 3, monitoringMenuOpen);

        int scrollDirection = 0;
        if (input.hotBar > 0f) { scrollDirection = 1; }
        else if (input.hotBar < 0f) { scrollDirection = -1; }

        if (scrollDirection != 0 && lastHotbarScrollDirection == 0)
        {
            hotBar.CycleSelection(scrollDirection);
        }

        lastHotbarScrollDirection = scrollDirection;

        // drop currently selected hotbar item
        if (input.dropItem && !dropHeld)
        {
            dropHeld = true;

            if (!monitoringMenuOpen)
            {
                hotBar.DropSelectedItem();
            }
        }
        else if (!input.dropItem && dropHeld && !monitoringMenuOpen)
        {
            dropHeld = false;
        }
    }

    private void HandleHotbarSlotPress(bool pressed, ref bool held, int slotIndex, bool monitoringMenuOpen)
    {
        if (pressed && !held)
        {
            held = true;

            // if monitoring menu is open and we're hovering an item,
            // assign that hovered item into the slot.
            if (monitoringMenuOpen && inventory != null && inventory.hoverItem != null)
            {
                if (!inventory.hoverItem.Instance.isChestItem)
                {
                    hotBar.AssignHoverItemToSlot(slotIndex);
                }
            }
            else
            {
                hotBar.SelectSlot(slotIndex);
            }
        }
        else if (!pressed && held)
        {
            held = false;
        }
    }

    private void HandleMenuInput()
    {
        // pause
        if (input.pause && !pauseHeld)
        {
            pauseHeld = true;

            if (IsInteractMenuOpen())
            {
                CloseInteractMenu();
                return;
            }

            // if monitoring menu is open, close it and open pause
            if (IsMonitoringMenuOpen())
            {
                SetMonitoringMenu(false, true);
            }

            SetPauseMenu(!IsPauseOpen());
        }
        else if (!input.pause && pauseHeld)
        {
            pauseHeld = false;
        }

        // monitoring menu (toggle)
        if (input.monitoringMenu && !monitoringMenuHeld)
        {
            monitoringMenuHeld = true;

            if (IsInteractMenuOpen())
            {
                CloseInteractMenu();
                return;
            }

            // if pause is open, close it and open monitoring menu
            if (pauseMenuRoot != null && pauseMenuRoot.activeSelf)
            {
                SetPauseMenu(false);
            }

            SetMonitoringMenu(!IsMonitoringMenuOpen(), true);
        }
        else if (!input.monitoringMenu && monitoringMenuHeld)
        {
            monitoringMenuHeld = false;
        }

        // update in menu bool from actual roots so its always right
        bool anyMenuOpen = IsPauseOpen() || IsMonitoringMenuOpen() || IsInteractMenuOpen();
        if (inMenu != anyMenuOpen)
        {
            SetInMenu(anyMenuOpen);
        }
    }

    // public calls for UI buttons etc
    public void SetInMenu(bool state)
    {
        inMenu = state;

        // menu open then free cursor, menu close then lock cursor
        if (!inMenu && inventory != null)
        {
            inventory.HideContextMenu();
        }

        if (inMenu)
        {
            SetCursorLocked(false);
        }
        else if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }

        // clear look when entering/leaving menu
        if (input != null)
        {
            input.look = Vector2.zero;
        }
    }

    public void SetPauseMenu(bool state)
    {
        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(state);
        }

        // when opening pause, force inventory closed
        if (state && monitoringMenuRoot != null)
        {
            monitoringMenuRoot.SetActive(false);
        }

        if (state && interactMenu != null)
        {
            interactMenu.CloseMenu(false);
        }

        SetInMenu(state || IsMonitoringMenuOpen() || IsInteractMenuOpen());
    }

    public void SetMonitoringMenu(bool state, bool openedByThis)
    {
        if (monitoringMenuRoot != null)
        {
            monitoringMenuRoot.SetActive(state);
        }

        // make sure chest visuals are cleaned up when the menu closes
        if (!state && inventory != null)
        {
            inventory.HideContextMenu();
            inventory.CloseChestView();
        }

        // when opening inventory, force pause closed
        if (state && pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }

        if (state && interactMenu != null)
        {
            interactMenu.CloseMenu(false);
        }

        if (openedByThis)
        {
            if (state && crafting != null)
            {
                crafting.SetupEverything(false);
            }
        }

        SetInMenu(state || IsPauseOpen() || IsInteractMenuOpen());
    }

    public void OpenInteractMenu(InteractableObject interactable, GameObject interactor)
    {
        if (interactMenu == null) { return; }
        if (interactable == null) { return; }

        if (pauseMenuRoot != null) { pauseMenuRoot.SetActive(false); }
        if (monitoringMenuRoot != null) { monitoringMenuRoot.SetActive(false); }

        interactMenu.OpenMenu(this, interactable, interactor);
        SetInMenu(true);
    }

    public void CloseInteractMenu()
    {
        if (interactMenu != null)
        {
            interactMenu.CloseMenu();
        }

        SetInMenu(IsPauseOpen() || IsMonitoringMenuOpen() || IsInteractMenuOpen());
    }

    private bool IsPauseOpen()
    {
        return pauseMenuRoot != null && pauseMenuRoot.activeSelf;
    }

    private bool IsMonitoringMenuOpen()
    {
        return monitoringMenuRoot != null && monitoringMenuRoot.activeSelf;
    }

    private bool IsInteractMenuOpen()
    {
        return interactMenu != null && interactMenu.IsOpen();
    }

    private void SetMenuRoots(bool pauseState, bool invState)
    {
        if (pauseMenuRoot != null) { pauseMenuRoot.SetActive(pauseState); }
        if (monitoringMenuRoot != null) { monitoringMenuRoot.SetActive(invState); }
    }

    private void SetCursorLocked(bool shouldLock)
    {
        cursorLocked = shouldLock;

        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;

        if (!cursorLocked && input != null)
        {
            input.look = Vector2.zero;
        }
    }

    private void HandleDeadState()
    {
        useHeld = false;
        pauseHeld = false;
        monitoringMenuHeld = false;
        rotateHeld = false;
        dropHeld = false;

        hotbarSlot1Held = false;
        hotbarSlot2Held = false;
        hotbarSlot3Held = false;
        hotbarSlot4Held = false;
        lastHotbarScrollDirection = 0;

        if (pauseMenuRoot != null && pauseMenuRoot.activeSelf)
        {
            pauseMenuRoot.SetActive(false);
        }

        if (monitoringMenuRoot != null && monitoringMenuRoot.activeSelf)
        {
            monitoringMenuRoot.SetActive(false);
        }

        if (interactMenu != null && interactMenu.IsOpen())
        {
            interactMenu.CloseMenu(false);
        }

        if (inventory != null)
        {
            inventory.CloseChestView();
        }

        SetInMenu(false);

        if (input != null)
        {
            input.look = Vector2.zero;
        }
    }
}