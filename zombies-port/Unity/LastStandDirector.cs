// LastStandDirector.cs
//
// Team-level last stand rules and game over. Ports from _zombiemode.gsc and _zombiemode_score.gsc:
//   player_damage_override(): a lethal hit downs the player if a teammate is still standing,
//                             otherwise it's game over (solo: unless a self-revive is left)
//   player_downed_penalty() : going down costs 5% of points (rounded up to 10)
//   player_revive_monitor() : the reviver earns those points back
//   player_died_penalty()   : when someone bleeds out, everyone else loses 10%
//   spectators_respawn()    : bled-out players come back at the end of the round
//   end_game()              : "GAME OVER / You Survived N Rounds", scoring stops

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombiesPort
{
    public class LastStandDirector : MonoBehaviour
    {
        [SerializeField] RoundDirector rounds;
        [Tooltip("How far behind a living teammate a bled-out player respawns (meters).")]
        [SerializeField] float respawnOffset = 1.5f;

        public bool IsGameOver { get; private set; }
        public IReadOnlyList<PlayerHealth> Players => _players;

        /// <summary>Game over, with the round the team reached (level.round_number).</summary>
        public event Action<int> GameOver;

        readonly List<PlayerHealth> _players = new List<PlayerHealth>();
        readonly List<LifeState> _states = new List<LifeState>();

        ZombieGameState State => GameBootstrap.Instance.State;

        void Start()
        {
            if (rounds == null) rounds = FindFirstObjectByType<RoundDirector>();

            var boot = GameBootstrap.Instance;
            if (boot != null)
            {
                foreach (var p in boot.Players)
                {
                    var h = p != null ? p.GetComponent<PlayerHealth>() : null;
                    if (h != null) _players.Add(h);
                }
            }
            if (_players.Count == 0)
                Debug.LogWarning("LastStandDirector: no PlayerHealth on any ZombiePlayer, so zombies can't hurt anyone.", this);
            foreach (var h in _players) h.Init(this);

            if (rounds != null)
            {
                rounds.PlayerAttacked += OnPlayerAttacked;
                rounds.RoundEnded += OnRoundEnded;
            }
        }

        void OnDestroy()
        {
            if (rounds == null) return;
            rounds.PlayerAttacked -= OnPlayerAttacked;
            rounds.RoundEnded -= OnRoundEnded;
        }

        void OnPlayerAttacked(ZombieAgent zombie, ZombiePlayer target)
        {
            if (IsGameOver || target == null) return;
            foreach (var h in _players)
                if (h.Player == target) { h.TakeZombieHit(zombie); return; }
        }

        // ---------------- Called by PlayerHealth ----------------

        internal void HandleLethalHit(PlayerHealth victim)
        {
            _states.Clear();
            foreach (var h in _players) _states.Add(h.Vitals.State);
            int index = _players.IndexOf(victim);
            bool solo = _players.Count == 1;

            var outcome = TeamLifeRules.OnLethalHit(index, _states, victim.Vitals.Lives);
            if (outcome == DownOutcome.GameOver)
            {
                victim.GoDown(0, false, solo); // Shown going down, as in BO1, then the game ends.
                TriggerGameOver();
                return;
            }

            var state = State;
            int lost = state.PointsLostWhenDowned(victim.Player.Score.Score);
            victim.Player.Score.Lose(lost, state.Intermission);
            victim.GoDown(lost, outcome == DownOutcome.DownedSelfRevive, solo);
        }

        internal void AwardRevive(PlayerHealth reviver, PlayerHealth revived)
        {
            var state = State;
            int points = state.PointsForRevive(revived.Vitals.TakePointsLostWhenDowned());
            reviver.Player.Score.Add(points, true, state.Intermission);
        }

        internal void OnBledOut(PlayerHealth victim)
        {
            victim.Spectate();

            // player_died_penalty(): everyone still in the game loses 10%.
            var state = State;
            foreach (var h in _players)
            {
                if (h == victim || h.Vitals.State == LifeState.Spectating) continue;
                h.Player.Score.Lose(state.NoRevivePenalty(h.Player.Score.Score), state.Intermission);
            }

            // Shouldn't happen (the last down ends the game), but never leave a team with nobody standing.
            if (!AnyoneAlive()) TriggerGameOver();
        }

        // ---------------- Rounds ----------------

        /// <summary>spectators_respawn(): bled-out players rejoin behind a living teammate.</summary>
        void OnRoundEnded(int round, float breakSeconds)
        {
            if (IsGameOver) return;
            PlayerHealth anchor = null;
            foreach (var h in _players)
                if (h.Vitals.State == LifeState.Alive) { anchor = h; break; }
            if (anchor == null) return;

            var state = State;
            foreach (var h in _players)
            {
                if (h.Vitals.State != LifeState.Spectating) continue;
                Vector3 pos = anchor.transform.position - anchor.transform.forward * respawnOffset;
                h.Respawn(pos, anchor.transform.rotation);
                h.Player.Score.Lose(state.DiedPenalty(h.Player.Score.Score), state.Intermission);
            }
        }

        // ---------------- Game over ----------------

        void TriggerGameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            State.SetIntermission();         // level.intermission = true: no more points
            if (rounds != null) rounds.Halt();
            var input = FindFirstObjectByType<MobileInput>();
            if (input != null) input.enabled = false; // Nobody keeps shooting under the game-over screen.
            GameOver?.Invoke(State.RoundNumber);
        }

        bool AnyoneAlive()
        {
            foreach (var h in _players)
                if (h.Vitals.State == LifeState.Alive) return true;
            return false;
        }

        /// <summary>The game-over screen's Play Again button. The scene must be in Build Settings for device builds.</summary>
        public void PlayAgain() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
