// MobileInput.cs
//
// The one place gameplay reads input from. The player controller and weapon ask this for
// "move", "look", "fire held" and so on, never touching the touch widgets directly. That way
// a gamepad or keyboard can be added here later without changing gameplay code.
//
// In the editor (and desktop builds) a keyboard and mouse fallback is built in for testing:
//   click the game view to start playing (locks the cursor for mouse look; Esc frees it, Tab toggles),
//   left mouse fire, right mouse aim, R reload, V knife, Q or mouse wheel swap weapon, WASD move, Shift sprint.
// With the cursor free, the mouse clicks the on-screen touch buttons like a finger.

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZombiesPort
{
    [DefaultExecutionOrder(-50)] // Read input before the controller and weapon use it this frame.
    public class MobileInput : MonoBehaviour
    {
        [Header("Touch widgets")]
        [SerializeField] VirtualJoystick moveStick;
        [SerializeField] TouchLookArea lookArea;
        [SerializeField] TouchButton fireButton;
        [SerializeField] TouchButton aimButton;
        [SerializeField] TouchButton reloadButton;
        [SerializeField] TouchButton knifeButton;
        [SerializeField] TouchButton swapButton;

        [Header("Feel")]
        [Tooltip("Degrees the view turns per inch of finger travel. Same feel on every screen size.")]
        [SerializeField] float lookDegreesPerInch = 110f;
        [Tooltip("Tap Aim to toggle (easier on touch), or turn off to hold.")]
        [SerializeField] bool aimIsToggle = true;
        [Tooltip("Pushing the stick this far forward sprints (replaces clicking the left stick).")]
        [Range(0.5f, 1f)] [SerializeField] float sprintThreshold = 0.92f;

        [Header("Editor / desktop testing")]
#pragma warning disable 0414 // Only read in the editor and desktop builds.
        [SerializeField] bool keyboardMouseFallback = true;
        [SerializeField] float mouseDegreesPerCount = 0.12f;
#pragma warning restore 0414

        public Vector2 Move { get; private set; }
        public bool Sprint { get; private set; }
        public bool FireHeld { get; private set; }
        /// <summary>True while the trigger is the mouse button (semi-autos then need one click per shot, as in BO1 on PC).</summary>
        public bool FireFromMouse { get; private set; }
        public bool AimHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool KnifePressed { get; private set; }
        public bool SwapPressed { get; private set; }

        Vector2 _lookDegrees;
        bool _aimToggled;

        /// <summary>Degrees to turn (x = yaw, y = pitch) accumulated since the last call.</summary>
        public Vector2 ConsumeLook()
        {
            Vector2 v = _lookDegrees;
            _lookDegrees = Vector2.zero;
            return v;
        }

        /// <summary>Drop out of a toggled aim (for example when knifing or swapping weapons).</summary>
        public void CancelAim() => _aimToggled = false;

        // Disabled (game over, pause menu): release everything so nothing stays held.
        void OnDisable()
        {
            Move = Vector2.zero;
            Sprint = FireHeld = AimHeld = ReloadPressed = KnifePressed = SwapPressed = false;
            _lookDegrees = Vector2.zero;
            _aimToggled = false;
        }

        void Update()
        {
            Move = moveStick != null ? moveStick.Value : Vector2.zero;

            if (lookArea != null)
            {
                // Some Android devices report a DPI of 0; 160 is Android's baseline density.
                float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
                _lookDegrees += lookArea.ConsumeDelta() / dpi * lookDegreesPerInch;
            }

            FireHeld = fireButton != null && fireButton.IsHeld;
            FireFromMouse = false;

            if (aimIsToggle)
            {
                if (aimButton != null && aimButton.ConsumePress()) _aimToggled = !_aimToggled;
                AimHeld = _aimToggled;
            }
            else
            {
                AimHeld = aimButton != null && aimButton.IsHeld;
            }

            ReloadPressed = reloadButton != null && reloadButton.ConsumePress();
            KnifePressed = knifeButton != null && knifeButton.ConsumePress();
            SwapPressed = swapButton != null && swapButton.ConsumePress();
            Sprint = Move.y >= sprintThreshold;

#if UNITY_EDITOR || UNITY_STANDALONE
            if (keyboardMouseFallback) ReadKeyboardMouse();
#endif
            FireHeld |= ScriptedFire;
            AimHeld |= ScriptedAim;
        }

        /// <summary>Held inputs driven by automated tests and demos (same path as the touch buttons).</summary>
        public bool ScriptedFire { get; set; }
        public bool ScriptedAim { get; set; }

#if UNITY_EDITOR || UNITY_STANDALONE
        bool _swallowClick;

        static bool PointerOverUi() =>
            UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        void ReadKeyboardMouse()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            Vector2 keys = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (kb.tabKey.wasPressedThisFrame) Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            // Click into the game (not on a touch button) to grab the mouse; that click doesn't shoot.
            if (!locked && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && !PointerOverUi())
            {
                Cursor.lockState = CursorLockMode.Locked;
                _swallowClick = true;
            }
            if (_swallowClick && !mouse.leftButton.isPressed && !mouse.rightButton.isPressed) _swallowClick = false;
            locked = Cursor.lockState == CursorLockMode.Locked && !_swallowClick;
            if (Cursor.lockState == CursorLockMode.Locked) _lookDegrees += mouse.delta.ReadValue() * mouseDegreesPerCount;
            SwapPressed |= Cursor.lockState == CursorLockMode.Locked && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;

            if (keys != Vector2.zero) Move = Vector2.ClampMagnitude(keys, 1f);
            Sprint |= kb.leftShiftKey.isPressed && Move.y > 0f;
            if (locked && mouse.leftButton.isPressed) { FireFromMouse = !FireHeld; FireHeld = true; }
            AimHeld |= locked && mouse.rightButton.isPressed;
            ReloadPressed |= kb.rKey.wasPressedThisFrame;
            KnifePressed |= kb.vKey.wasPressedThisFrame;
            SwapPressed |= kb.qKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            Vector2 keys = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (Input.GetKeyDown(KeyCode.Tab)) Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (!locked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !PointerOverUi())
            {
                Cursor.lockState = CursorLockMode.Locked;
                _swallowClick = true;
            }
            if (_swallowClick && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)) _swallowClick = false;
            locked = Cursor.lockState == CursorLockMode.Locked && !_swallowClick;
            SwapPressed |= Cursor.lockState == CursorLockMode.Locked && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
            if (locked)
                _lookDegrees += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * (mouseDegreesPerCount * 10f);

            if (keys != Vector2.zero) Move = Vector2.ClampMagnitude(keys, 1f);
            Sprint |= Input.GetKey(KeyCode.LeftShift) && Move.y > 0f;
            if (locked && Input.GetMouseButton(0)) { FireFromMouse = !FireHeld; FireHeld = true; }
            AimHeld |= locked && Input.GetMouseButton(1);
            ReloadPressed |= Input.GetKeyDown(KeyCode.R);
            KnifePressed |= Input.GetKeyDown(KeyCode.V);
            SwapPressed |= Input.GetKeyDown(KeyCode.Q);
#endif
        }
#endif
    }
}
