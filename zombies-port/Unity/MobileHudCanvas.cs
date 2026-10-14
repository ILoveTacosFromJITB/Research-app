// MobileHudCanvas.cs
//
// The root Canvas for every on-screen touch control (the interact button now; the move stick,
// fire button and points display later). It sets the Canvas up for phones and makes sure the
// scene can receive touches.
//
//  - Screen Space - Overlay: drawn on top of the 3D view without a UI camera, the cheapest mode.
//  - CanvasScaler "Scale With Screen Size": the layout is authored once at referenceResolution
//    and scales to any phone. In landscape, matching height keeps buttons the same size
//    relative to the player's thumbs on tall 19.5:9 phones and squarer tablets alike.
//  - An EventSystem with the input module that matches the project's input backend. Without
//    one, uGUI ignores touches entirely, which is the most common "my button does nothing" bug.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public class MobileHudCanvas : MonoBehaviour
    {
        [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("0 = match width, 1 = match height. Use 1 for a landscape shooter.")]
        [Range(0f, 1f)] [SerializeField] float matchWidthOrHeight = 1f;
        [Tooltip("Draw order against other overlay canvases, such as menus.")]
        [SerializeField] int sortingOrder = 10;
        [Tooltip("Lock the device to landscape. Leave off if Player Settings already does this.")]
        [SerializeField] bool forceLandscape = true;

        void Reset() => Configure();   // When the component is added in the editor.
        void OnValidate() => Configure();

        void Awake()
        {
            Configure();
            EnsureEventSystem();

            if (forceLandscape)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }

        void Configure()
        {
            var canvas = GetComponent<Canvas>();
            var scaler = GetComponent<CanvasScaler>();
            if (canvas == null || scaler == null) return; // OnValidate can run before RequireComponent adds them.

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false; // Pixel-perfect re-snaps every element on change, which costs CPU on mobile.

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        /// <summary>Creates an EventSystem if the scene has none, with the right input module.</summary>
        public static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            // New Input System (Unity 6 default): touches come through InputSystemUIInputModule.
            go.AddComponent<InputSystemUIInputModule>();
#else
            // Legacy Input Manager: StandaloneInputModule turns touches into pointer events.
            go.AddComponent<StandaloneInputModule>();
            Input.multiTouchEnabled = true;
#endif
        }
    }
}
