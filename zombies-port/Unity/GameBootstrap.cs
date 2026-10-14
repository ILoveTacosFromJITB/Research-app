// GameBootstrap.cs
//
// Scene entry point: the main() / difficulty_init() part of _zombiemode.gsc. It creates the
// ZombieGameState from Step 3 and gives each ZombiePlayer in the scene their starting points.

using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    [DefaultExecutionOrder(-100)] // Runs before anything that reads scores or the round number.
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [SerializeField] ZombieVars vars = new ZombieVars();

        public ZombieGameState State { get; private set; }
        public IReadOnlyList<ZombiePlayer> Players => _players;
        readonly List<ZombiePlayer> _players = new List<ZombiePlayer>();

        void Awake()
        {
            Instance = this;

            _players.AddRange(FindObjectsByType<ZombiePlayer>(FindObjectsSortMode.None));
            int count = Mathf.Clamp(_players.Count, 1, 4);

            State = new ZombieGameState(vars);
            State.Initialize(count);
            for (int i = 0; i < _players.Count && i < count; i++)
                _players[i].Bind(State.Players[i]);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
