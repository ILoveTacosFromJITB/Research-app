// InteractButton.cs
//
// The on-screen button that replaces Square/X. It appears only while the player is inside a
// usable trigger and shows the label and cost GSC put in the hint string, for example
// "Mystery Box  950" or "Trade Weapons". Touching it calls PlayerInteractor.TryInteract().
//
// Built on Unity UI (uGUI). The hierarchy is:
//   HUD Canvas      Screen Space - Overlay, CanvasScaler, GraphicRaycaster (MobileHudCanvas)
//     SafeArea      SafeAreaFitter keeps buttons clear of notches and the home bar
//       InteractButton   Image + Button + this, and its own nested Canvas
//         Label / Cost   TextMeshProUGUI
// GameObject > Zombies > Mobile HUD builds all of this for you (MobileHudMenu.cs).
//
// Mobile-specific choices:
//  - Own nested Canvas: changing the label or cost rebuilds only this button, not the whole HUD.
//  - Hidden by disabling that Canvas, which is Unity's recommended cheap hide: it doesn't
//    rebuild layout the way SetActive does, and it draws nothing while hidden.
//  - Fires on touch-down, not release. A Button's onClick waits for the finger to lift while
//    still over the button, which feels slow and fails if the thumb slides mid-fight.
//  - Grows to at least minTouchMillimeters on screen, whatever the phone's pixel density.

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZombiesPort
{
    [RequireComponent(typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster))]
    public class InteractButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Tooltip("Leave empty to find the PlayerInteractor in the scene at startup.")]
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text costText;
        [Tooltip("Optional Filled image that shows progress for hold actions (reviving).")]
        [SerializeField] Image progressFill;

        [SerializeField] Color affordableColor = Color.white;
        [SerializeField] Color tooExpensiveColor = new Color(1f, 0.3f, 0.3f);

        [Header("Touch")]
        [Tooltip("Fire when the finger lands (snappier) rather than when it lifts.")]
        [SerializeField] bool triggerOnPress = true;
        [Tooltip("Minimum on-screen size. Apple and Google recommend roughly 9-10 mm for touch targets.")]
        [SerializeField] float minTouchMillimeters = 12f;

        [Header("Feedback when the player can't afford it")]
        [SerializeField] float shakeSeconds = 0.3f;
        [SerializeField] float shakePixels = 14f;
#pragma warning disable 0414 // Only read on iOS/Android builds.
        [SerializeField] bool vibrateOnDenied = true;
#pragma warning restore 0414

        CanvasGroup _group;
        Canvas _canvas;
        RectTransform _rect;
        Vector2 _homePosition;
        float _shakeUntil;
        bool _visible;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _canvas = GetComponent<Canvas>();
            _rect = (RectTransform)transform;
            _homePosition = _rect.anchoredPosition;
            if (button == null) button = GetComponent<Button>();
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();

            // Text never needs to catch touches. Skipping it makes raycasts cheaper and stops
            // the label from blocking the button underneath.
            if (label != null) label.raycastTarget = false;
            if (costText != null) costText.raycastTarget = false;

            if (GetComponentInParent<CanvasScaler>() == null)
                Debug.LogWarning("InteractButton: no CanvasScaler above this button. Put it under a " +
                                 "MobileHudCanvas so it scales correctly on every screen.", this);

            Apply(InteractPrompt.Hidden);
        }

        void Start()
        {
            EnforceMinimumTouchSize();
        }

        void OnEnable()
        {
            if (interactor == null) return;
            interactor.PromptChanged += Apply;
            interactor.Interacted += OnResult;
            Apply(interactor.Prompt);
        }

        void OnDisable()
        {
            if (interactor == null) return;
            interactor.SetHolding(false);
            interactor.PromptChanged -= Apply;
            interactor.Interacted -= OnResult;
        }

        // uGUI tracks each finger separately, so this works while the other thumb is still
        // holding the movement stick or dragging to aim.
        public void OnPointerDown(PointerEventData e)
        {
            if (_visible && interactor != null) interactor.SetHolding(true);
            if (triggerOnPress) Press();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (interactor != null) interactor.SetHolding(false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!triggerOnPress) Press();
        }

        void Press()
        {
            if (!_visible || interactor == null) return;
            interactor.TryInteract();
        }

        void Apply(InteractPrompt prompt)
        {
            _visible = prompt.Visible;
            _canvas.enabled = prompt.Visible;
            _group.interactable = prompt.Visible;
            _group.blocksRaycasts = prompt.Visible; // A hidden button mustn't eat aim-drag touches.
            if (button != null) button.interactable = prompt.Visible;

            if (label != null) label.text = prompt.Label ?? "";
            if (progressFill != null)
            {
                progressFill.enabled = prompt.Progress > 0f;
                progressFill.fillAmount = prompt.Progress;
            }
            if (costText != null)
            {
                costText.text = prompt.Cost > 0 ? prompt.Cost.ToString() : "";
                costText.color = prompt.Affordable ? affordableColor : tooExpensiveColor;
            }
        }

        // GSC played the "no_money" voice line. The touch version shakes the button and buzzes.
        void OnResult(InteractResult result)
        {
            if (result != InteractResult.NotEnoughPoints && result != InteractResult.Denied) return;
            _shakeUntil = Time.unscaledTime + shakeSeconds;
#if UNITY_IOS || UNITY_ANDROID
            if (vibrateOnDenied) Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Converts minTouchMillimeters to canvas units with the screen DPI and the Canvas
        /// scale factor, then grows the button if it's smaller. It never shrinks the button.
        /// Some devices report a DPI of 0; those keep the authored size.
        /// </summary>
        void EnforceMinimumTouchSize()
        {
            float dpi = Screen.dpi;
            Canvas root = _canvas.rootCanvas;
            if (dpi <= 0f || root == null || root.scaleFactor <= 0f) return;

            float minPixels = minTouchMillimeters / 25.4f * dpi;
            float minUnits = minPixels / root.scaleFactor;
            Vector2 size = _rect.sizeDelta;
            _rect.sizeDelta = new Vector2(Mathf.Max(size.x, minUnits), Mathf.Max(size.y, minUnits));
        }

        void Update()
        {
            // Desktop testing in the editor: F works like the old PC Use key.
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) { Press(); if (_visible) interactor?.SetHolding(true); }
            if (kb != null && kb.fKey.wasReleasedThisFrame) interactor?.SetHolding(false);
#elif UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F)) { Press(); if (_visible) interactor?.SetHolding(true); }
            if (Input.GetKeyUp(KeyCode.F)) interactor?.SetHolding(false);
#endif
            if (Time.unscaledTime < _shakeUntil)
            {
                float x = Mathf.Sin(Time.unscaledTime * 60f) * shakePixels;
                _rect.anchoredPosition = _homePosition + new Vector2(x, 0f);
            }
            else if (_rect.anchoredPosition != _homePosition)
            {
                _rect.anchoredPosition = _homePosition;
            }
        }
    }
}
