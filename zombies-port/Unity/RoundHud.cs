// RoundHud.cs
//
// The chalk round counter and the points display. Ports chalk_one_up() and chalk_round_over()
// from _zombiemode.gsc:
//   Rounds 1-5 : one tally image (1 to 5 marks)
//   Rounds 6-10: a five-mark tally plus a second tally image (1 to 5 marks)
//   Round 11+  : the number, written in chalk
// When a round is cleared the counter turns white and blinks through the break, then fades
// out; the next round fades in, in red.
//
// Assign five tally sprites (1 to 5 marks) for the classic look. Without sprites, the
// number is shown for every round.
//
// Mobile layout: BO1 drew the chalk bottom-left, but that's where the movement stick goes
// on a phone. GameObject > Zombies > Round HUD puts it top-left, with points top-right.
// The HUD has its own nested Canvas, so the points ticking up every hit don't force the
// interact button or other controls to rebuild.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas))]
    public class RoundHud : MonoBehaviour
    {
        [SerializeField] RoundDirector director;

        [Header("Chalk round counter")]
        [SerializeField] Image tally1;
        [SerializeField] Image tally2;
        [Tooltip("Five sprites: 1, 2, 3, 4 and 5 chalk marks (hud_chalk_1..5).")]
        [SerializeField] Sprite[] tallySprites = new Sprite[5];
        [SerializeField] TMP_Text roundNumber;
        [Tooltip("GSC used (0.21, 0, 0), which is too dark to read on a phone outdoors.")]
        [SerializeField] Color chalkRed = new Color(0.6f, 0.04f, 0.04f);
        [SerializeField] Color chalkWhite = Color.white;
        [SerializeField] float fadeInSeconds = 1f;

        [Header("Points")]
        [SerializeField] TMP_Text points;
        [Tooltip("Which player's points to show (0 = the local player).")]
        [SerializeField] int playerIndex;

        PlayerScore _score;
        Coroutine _anim;
        float _alpha;
        Color _color;

        void Awake()
        {
            // Nothing here is touchable. Keep it out of the raycaster entirely.
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            _color = chalkRed;
            SetChalk(0f, chalkRed);
        }

        void Start()
        {
            if (director == null) director = FindFirstObjectByType<RoundDirector>();
            if (director != null)
            {
                director.RoundStarted += OnRoundStarted;
                director.RoundEnded += OnRoundEnded;
            }

            var boot = GameBootstrap.Instance;
            if (boot != null && playerIndex < boot.State.Players.Count)
            {
                _score = boot.State.Players[playerIndex];
                _score.OnScoreChanged += OnScoreChanged;
                SetPoints(_score.Score);
            }
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.RoundStarted -= OnRoundStarted;
                director.RoundEnded -= OnRoundEnded;
            }
            if (_score != null) _score.OnScoreChanged -= OnScoreChanged;
        }

        // ---------------- Points ----------------

        void OnScoreChanged(PlayerScore score, int delta) => SetPoints(score.Score);

        void SetPoints(int value)
        {
            if (points != null) points.SetText("{0}", value); // SetText with an arg avoids a string allocation per hit.
        }

        // ---------------- Chalk ----------------

        void OnRoundStarted(int round)
        {
            ShowRound(round);
            Play(FadeIn());
        }

        void OnRoundEnded(int round, float breakSeconds) => Play(RoundOver(breakSeconds));

        /// <summary>chalk_one_up(): pick the tally images or the number for this round.</summary>
        void ShowRound(int round)
        {
            bool haveSprites = tallySprites != null && tallySprites.Length >= 5 && tallySprites[4] != null;
            bool useTally = haveSprites && round <= 10;

            if (tally1 != null)
            {
                tally1.enabled = useTally;
                if (useTally) tally1.sprite = tallySprites[Mathf.Min(round, 5) - 1];
            }
            if (tally2 != null)
            {
                tally2.enabled = useTally && round > 5;
                if (useTally && round > 5) tally2.sprite = tallySprites[round - 6];
            }
            if (roundNumber != null)
            {
                roundNumber.enabled = !useTally;
                roundNumber.SetText("{0}", round);
            }
        }

        IEnumerator FadeIn()
        {
            float from = _alpha;
            for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
            {
                SetChalk(Mathf.Lerp(from, 1f, t / fadeInSeconds), chalkRed);
                yield return null;
            }
            SetChalk(1f, chalkRed);
        }

        /// <summary>
        /// chalk_round_over(): with a 10 s break, GSC used 8 s for the effect (time - 2):
        /// fade to white over the first quarter while blinking (0.5 s off, 0.5 s on) for half
        /// the time, then fade back to red and out over the last quarter, then a 2 s pause.
        /// </summary>
        IEnumerator RoundOver(float breakSeconds)
        {
            float time = breakSeconds > 3f ? breakSeconds - 2f : breakSeconds;
            float colorFade = time * 0.25f;
            const float blink = 0.5f;
            int steps = Mathf.Max(1, Mathf.RoundToInt(time * 0.5f / blink));

            float elapsed = 0f;
            Color startColor = _color;
            for (int s = 0; s < steps; s++)
            {
                for (float t = 0f; t < blink * 2f; t += Time.unscaledDeltaTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    Color c = Color.Lerp(startColor, chalkWhite, colorFade > 0f ? elapsed / colorFade : 1f);
                    float a = t < blink ? 1f - t / blink : (t - blink) / blink; // off, then back on
                    SetChalk(a, c);
                    yield return null;
                }
            }

            Color white = _color;
            for (float t = 0f; t < colorFade; t += Time.unscaledDeltaTime)
            {
                float k = t / colorFade;
                SetChalk(1f - k, Color.Lerp(white, chalkRed, k));
                yield return null;
            }
            SetChalk(0f, chalkRed);
        }

        void Play(IEnumerator routine)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(routine);
        }

        void SetChalk(float alpha, Color color)
        {
            _alpha = alpha;
            _color = color;
            color.a = alpha;
            if (tally1 != null) tally1.color = color;
            if (tally2 != null) tally2.color = color;
            if (roundNumber != null) roundNumber.color = color;
        }
    }
}
