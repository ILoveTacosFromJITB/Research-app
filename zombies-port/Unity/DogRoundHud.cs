// DogRoundHud.cs
//
// What the player sees during a hellhound round:
//   - "HELLHOUNDS!" across the screen when the round starts (the announcer's "Fetch me their
//     souls!" moment in BO1)
//   - a dark red tint over the whole round, BO1's darkened dog-round fog. It fades in at the
//     start and out at the end, and is switched off completely between dog rounds.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas))]
    public class DogRoundHud : MonoBehaviour
    {
        [SerializeField] DogRoundDirector director;
        [SerializeField] Image tint;
        [SerializeField] TMP_Text announcement;
        [SerializeField] float tintAlpha = 0.18f;
        [SerializeField] float fadeSeconds = 2f;
        [SerializeField] float announcementSeconds = 3.5f;

        Coroutine _tintRoutine, _announceRoutine;

        void Awake()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            if (tint != null) tint.enabled = false;
            if (announcement != null) announcement.enabled = false;
        }

        void Start()
        {
            if (director == null) director = FindFirstObjectByType<DogRoundDirector>();
            if (director == null) return;
            director.DogRoundStarting += OnStarting;
            director.DogRoundEnded += OnEnded;
        }

        void OnDestroy()
        {
            if (director == null) return;
            director.DogRoundStarting -= OnStarting;
            director.DogRoundEnded -= OnEnded;
        }

        void OnStarting(int round)
        {
            Fade(tintAlpha);
            if (announcement == null) return;
            if (_announceRoutine != null) StopCoroutine(_announceRoutine);
            _announceRoutine = StartCoroutine(Announce());
        }

        void OnEnded(int round) => Fade(0f);

        void Fade(float to)
        {
            if (tint == null) return;
            if (_tintRoutine != null) StopCoroutine(_tintRoutine);
            _tintRoutine = StartCoroutine(FadeTint(to));
        }

        IEnumerator FadeTint(float to)
        {
            tint.enabled = true;
            var c = tint.color;
            float from = c.a;
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                c.a = Mathf.Lerp(from, to, t / fadeSeconds);
                tint.color = c;
                yield return null;
            }
            c.a = to;
            tint.color = c;
            if (to <= 0.001f) tint.enabled = false;
        }

        IEnumerator Announce()
        {
            announcement.enabled = true;
            var c = announcement.color;
            for (float t = 0f; t < announcementSeconds; t += Time.deltaTime)
            {
                float k = t / announcementSeconds;
                c.a = k < 0.15f ? k / 0.15f : k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
                announcement.color = c;
                yield return null;
            }
            announcement.enabled = false;
        }
    }
}
