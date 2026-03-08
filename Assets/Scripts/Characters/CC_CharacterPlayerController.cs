using UnityEngine;
using UnityEngine.InputSystem;

// Made by: Jason Lodge
// Summary: Player controller, drives all the locomotion code in the character base and things like interaction.
// This is separated as the character base will be used by AI too to be modular.
[RequireComponent(typeof(CC_CharacterBase))]
public class CC_CharacterPlayerController : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif

    private CC_PlayerInputManager input;
    private CC_CharacterBase characterBase;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;

    [Header("Menus")]
    [Tooltip("True when any menu is open, will stop TickFixed/TickLate and free cursor.")]
    [SerializeField] private bool inMenu;

    [Tooltip("Pause menu root.")]
    [SerializeField] private GameObject pauseMenuRoot;

    [Tooltip("Inventory menu root.")]
    [SerializeField] private GameObject inventoryMenuRoot;

    [Tooltip("Inventory")]
    [SerializeField] private INV_Inventory inventory;

    private bool cursorLocked;

    // internal press guards so hold wont spam toggle
    private bool pauseHeld;
    private bool inventoryHeld;
    private bool rotateHeld;
    private bool dropHeld;

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

    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
#endif
        input = GetComponent<CC_PlayerInputManager>();
        characterBase = GetComponent<CC_CharacterBase>();
    }

    private void Start()
    {
        // if we start in a menu, dont lock
        if (inMenu)
        {
            SetCursorLocked(false);
            SetMenuRoots(false, false);
            return;
        }

        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }

        SetMenuRoots(false, false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // if a menu is open, dont re-lock cursor
        if (hasFocus && cursorLocked && !inMenu)
        {
            SetCursorLocked(true);
        }
    }

    private void Update()
    {
        // open/close menu logic
        HandleMenuInput();

        // one-off rotate/drop while inventory menu is open
        HandleInventoryActions();
    }

    private void FixedUpdate()
    {
        if (inMenu)
        {
            characterBase.TickFixed(new Vector2(0, 0), false, 0, false, false);
            return;
        }

        // movement / physics
        characterBase.TickFixed(input.move, input.jump, input.roll, input.sprint, input.crouch);
    }

    private void LateUpdate()
    {
        if (inMenu)
        {
            characterBase.TickLate(new Vector2(0, 0), IsCurrentDeviceMouse);
            characterBase.TickInteract(false);
            return;
        }

        // camera / rotation
        characterBase.TickLate(input.look, IsCurrentDeviceMouse);

        // interaction
        characterBase.TickInteract(input.interact);
    }
    private void HandleInventoryActions()
    {
        // only allow these when we're in a menu and inventory is actually open
        if (!inMenu || !IsInventoryOpen() || inventory == null)
        {
            rotateHeld = false;
            dropHeld = false;
            return;
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
            inventory.DropHoverItem();
        }
        else if (!input.dropItem && dropHeld)
        {
            dropHeld = false;
        }
    }

    private void HandleMenuInput()
    {
        // pause
        if (input.pause && !pauseHeld)
        {
            pauseHeld = true;

            // if inventory is open, close it and open pause
            if (inventoryMenuRoot != null && inventoryMenuRoot.activeSelf)
            {
                SetInventoryMenu(false);
            }

            SetPauseMenu(!IsPauseOpen());
        }
        else if (!input.pause && pauseHeld)
        {
            pauseHeld = false;
        }

        // inventory (toggle)
        if (input.inventory && !inventoryHeld)
        {
            inventoryHeld = true;

            // if pause is open, close it and open inventory
            if (pauseMenuRoot != null && pauseMenuRoot.activeSelf)
            {
                SetPauseMenu(false);
            }

            SetInventoryMenu(!IsInventoryOpen());
        }
        else if (!input.inventory && inventoryHeld)
        {
            inventoryHeld = false;
        }

        // update in menu bool from actual roots so its always right
        bool anyMenuOpen = IsPauseOpen() || IsInventoryOpen();
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
        if (inMenu)
        {
            SetCursorLocked(false);
        }
        else
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        // clear look when entering/leaving menu
        input.look = Vector2.zero;
    }

    public void SetPauseMenu(bool state)
    {
        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(state);
        }

        // when opening pause, force inventory closed
        if (state)
        {
            if (inventoryMenuRoot != null) { inventoryMenuRoot.SetActive(false); }
        }

        SetInMenu(state || IsInventoryOpen());
    }

    public void SetInventoryMenu(bool state)
    {
        if (inventoryMenuRoot != null)
        {
            inventoryMenuRoot.SetActive(state);
        }

        // when opening inventory, force pause closed
        if (state)
        {
            if (pauseMenuRoot != null) { pauseMenuRoot.SetActive(false); }
        }

        SetInMenu(state || IsPauseOpen());
    }

    private bool IsPauseOpen()
    {
        return pauseMenuRoot != null && pauseMenuRoot.activeSelf;
    }

    private bool IsInventoryOpen()
    {
        return inventoryMenuRoot != null && inventoryMenuRoot.activeSelf;
    }

    private void SetMenuRoots(bool pauseState, bool invState)
    {
        if (pauseMenuRoot != null) { pauseMenuRoot.SetActive(pauseState); }
        if (inventoryMenuRoot != null) { inventoryMenuRoot.SetActive(invState); }
    }

    private void SetCursorLocked(bool shouldLock)
    {
        cursorLocked = shouldLock;

        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
        if (!cursorLocked)
        {
            input.look = Vector2.zero;
        }
    }
}