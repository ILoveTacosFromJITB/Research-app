// InteractButton.cs
//
// The on-screen button that replaces Square/X. It appears only while the player is inside a
// usable trigger and shows the label and cost GSC put in the hint string, for example
// "Mystery Box  950" or "Trade Weapons". Tapping it calls PlayerInteractor.TryInteract().
//
// Setup (Canvas set to "Scale With Screen Size"):
//   1. Add a UI Button in the lower right, within easy reach of the right thumb.
//   2. Put this component and a CanvasGroup on it.
//   3. Assign a TMP_Text for the label and another for the cost.
// It hides with CanvasGroup alpha rather than SetActive, so showing and hiding doesn't
// rebuild the Canvas layout.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractButton : MonoBehaviour
    {
        [Tooltip("Leave empty to find the PlayerInteractor in the scene at startup.")]
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text costText;

        [SerializeField] Color affordableColor = Color.white;
        [SerializeField] Color tooExpensiveColor = new Color(1f, 0.3f, 0.3f);

        [Header("Feedback when the player can't afford it")]
        [SerializeField] float shakeSeconds = 0.3f;
        [SerializeField] float shakePixels = 14f;
#pragma warning disable 0414 // Only read on iOS/Android builds.
        [SerializeField] bool vibrateOnDenied = true;
#pragma warning restore 0414

        CanvasGroup _group;
        RectTransform _rect;
        Vector2 _homePosition;
        float _shakeUntil;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;
            _homePosition = _rect.anchoredPosition;
            if (button == null) button = GetComponent<Button>();
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
            Apply(InteractPrompt.Hidden);
        }

        void OnEnable()
        {
            button.onClick.AddListener(OnPressed);
            if (interactor == null) return;
            interactor.PromptChanged += Apply;
            interactor.Interacted += OnResult;
            Apply(interactor.Prompt);
        }

        void OnDisable()
        {
            button.onClick.RemoveListener(OnPressed);
            if (interactor == null) return;
            interactor.PromptChanged -= Apply;
            interactor.Interacted -= OnResult;
        }

        void OnPressed()
        {
            if (interactor != null) interactor.TryInteract();
        }

        void Apply(InteractPrompt prompt)
        {
            _group.alpha = prompt.Visible ? 1f : 0f;
            _group.interactable = prompt.Visible;
            _group.blocksRaycasts = prompt.Visible; // A hidden button mustn't eat aim-drag touches.

            if (label != null) label.text = prompt.Label ?? "";
            if (costText != null)
            {
                costText.text = prompt.Cost > 0 ? prompt.Cost.ToString() : "";
                costText.color = prompt.Affordable ? affordableColor : tooExpensiveColor;
            }
        }

        // GSC played the "no_money" voice line. The touch version shakes the button and buzzes.
        void OnResult(InteractResult result)
        {
            if (result != InteractResult.NotEnoughPoints) return;
            _shakeUntil = Time.unscaledTime + shakeSeconds;
#if UNITY_IOS || UNITY_ANDROID
            if (vibrateOnDenied) Handheld.Vibrate();
#endif
        }

        void Update()
        {
#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
            // Desktop testing: F works like the old PC Use key.
            if (Input.GetKeyDown(KeyCode.F)) OnPressed();
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
