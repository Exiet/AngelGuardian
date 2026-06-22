using System;
using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// Core game state machine. Controls the entire game lifecycle from Loading through Victory.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region ─── Game States ─────────────────────────

        public enum GameState
        {
            Loading,
            Playing,
            Paused,
            GameOver,
            Victory
        }

        #endregion

        #region ─── Singleton ───────────────────────────

        private static GameManager _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static GameManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[GameManager] Instance accessed after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<GameManager>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[GameManager]");
                            _instance = go.AddComponent<GameManager>();
                            DontDestroyOnLoad(go);
                        }
                        else if (_instance.transform.parent == null)
                        {
                            DontDestroyOnLoad(_instance.gameObject);
                        }
                    }

                    return _instance;
                }
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            _instance = null;
        }

        #endregion

        #region ─── Inspector ───────────────────────────

        [Header("Config")]
        [SerializeField] private GameConfig _config;
        public GameConfig Config
        {
            get
            {
                if (_config == null)
                    _config = Resources.Load<GameConfig>("GameConfig");
                return _config;
            }
            set => _config = value;
        }

        #endregion

        #region ─── State ───────────────────────────────

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private GameState _currentState = GameState.Loading;
        public GameState CurrentState => _currentState;

        [SerializeField] private float _elapsedTime = 0f;
        /// <summary>Total elapsed game time in seconds (paused time excluded).</summary>
        public float ElapsedTime => _elapsedTime;

        [SerializeField] private int _currentWave = 0;
        /// <summary>Current wave number (0 = pre-game, 1-25 = active waves).</summary>
        public int CurrentWave => _currentWave;

        /// <summary>Total number of waves in a full run.</summary>
        public const int TotalWaves = 25;

        [SerializeField] private int _totalKills = 0;
        /// <summary>Total enemies killed this run.</summary>
        public int TotalKills => _totalKills;

        [SerializeField] private int _currentLevel = 1;
        /// <summary>Player's current level.</summary>
        public int CurrentLevel => _currentLevel;

        [SerializeField] private float _currentExp = 0f;
        /// <summary>Current experience points.</summary>
        public float CurrentExp => _currentExp;

        [SerializeField] private float _expToNextLevel = 100f;
        /// <summary>Experience required to reach the next level.</summary>
        public float ExpToNextLevel => _expToNextLevel;

        [SerializeField] private int _gold = 0;
        /// <summary>Current gold amount.</summary>
        public int Gold => _gold;

        [SerializeField] private float _gameSpeed = 1.0f;
        /// <summary>
        /// Global game speed multiplier (supports time-warp disasters).
        /// Setting this also updates Time.timeScale.
        /// </summary>
        public float GameSpeed
        {
            get => _gameSpeed;
            set
            {
                _gameSpeed = Mathf.Max(0.1f, value);
                Time.timeScale = (_currentState == GameState.Paused) ? 0f : _gameSpeed;
            }
        }

        // Internal
        private float _waveTimer = 0f;
        private const float WaveDuration = 60f; // seconds per wave (adjust as needed)

        #endregion

        #region ─── Events / Delegates ──────────────────

        /// <summary>Fired when the game state changes. (old state, new state)</summary>
        public event Action<GameState, GameState> OnStateChanged;

        /// <summary>Fired when a new wave begins. (wave number)</summary>
        public event Action<int> OnWaveChanged;

        /// <summary>Fired when the player levels up. (new level)</summary>
        public event Action<int> OnPlayerLevelUp;

        /// <summary>Fired when gold changes. (new total)</summary>
        public event Action<int> OnGoldChanged;

        #endregion

        #region ─── Unity Messages ──────────────────────

        private void Start()
        {
            // Default to Loading state; StartGame() transitions to Playing.
            SetState(GameState.Loading);
            Time.timeScale = 1f;
            _gameSpeed = 1f;
        }

        private void Update()
        {
            if (_currentState != GameState.Playing)
                return;

            // Advance elapsed time
            _elapsedTime += Time.deltaTime;

            // Wave progression check
            if (_currentWave > 0 && _currentWave <= TotalWaves)
            {
                _waveTimer += Time.deltaTime;
                if (_waveTimer >= WaveDuration)
                {
                    AdvanceWave();
                }
            }
        }

        #endregion

        #region ─── Public API – Game Lifecycle ─────────

        /// <summary>
        /// Starts (or restarts) the game. Transitions from Loading to Playing.
        /// </summary>
        public void StartGame()
        {
            if (_currentState != GameState.Loading && _currentState != GameState.GameOver && _currentState != GameState.Victory)
            {
                Debug.LogWarning($"[GameManager] Cannot start game from state: {_currentState}");
                return;
            }

            // Reset all runtime values
            _elapsedTime = 0f;
            _currentWave = 0;
            _totalKills = 0;
            _currentLevel = 1;
            _currentExp = 0f;
            _expToNextLevel = CalculateExpToNextLevel(1);
            _gold = 0;
            _gameSpeed = 1f;
            _waveTimer = 0f;
            Time.timeScale = 1f;

            SetState(GameState.Playing);

            // Kick off wave 1 immediately
            AdvanceWave();

            Debug.Log("[GameManager] Game started!");
        }

        /// <summary>
        /// Pauses the game. Time scale set to 0.
        /// </summary>
        public void PauseGame()
        {
            if (_currentState != GameState.Playing)
                return;

            Time.timeScale = 0f;
            SetState(GameState.Paused);
            Debug.Log("[GameManager] Game paused.");
        }

        /// <summary>
        /// Resumes the game from a paused state.
        /// </summary>
        public void ResumeGame()
        {
            if (_currentState != GameState.Paused)
                return;

            Time.timeScale = _gameSpeed;
            SetState(GameState.Playing);
            Debug.Log("[GameManager] Game resumed.");
        }

        /// <summary>
        /// Triggers game over with a failure type.
        /// </summary>
        /// <param name="failType">Reason for failure (e.g., "BabyMentalZero", "PlayerDied").</param>
        public void GameOver(string failType = "Unknown")
        {
            if (_currentState == GameState.GameOver || _currentState == GameState.Victory)
                return;

            Time.timeScale = 0f;
            SetState(GameState.GameOver);

            // Fire global event
            EventBus.Instance?.FireGameOver(failType);

            Debug.Log($"[GameManager] Game Over! Reason: {failType}");
        }

        /// <summary>
        /// Triggers victory state (all 25 waves cleared).
        /// </summary>
        public void Victory()
        {
            if (_currentState == GameState.GameOver || _currentState == GameState.Victory)
                return;

            Time.timeScale = 0f;
            SetState(GameState.Victory);

            Debug.Log("[GameManager] Victory! All waves cleared.");
        }

        #endregion

        #region ─── Public API – Gameplay ───────────────

        /// <summary>
        /// Adds experience points. Triggers level-up(s) if threshold is reached.
        /// </summary>
        public void AddExp(float amount)
        {
            if (_currentState != GameState.Playing)
                return;

            float multiplier = Config != null ? Config.ExpGrowthMultiplier : 1f;
            _currentExp += amount * multiplier;

            // Handle potential multiple level-ups in one call
            while (_currentExp >= _expToNextLevel)
            {
                _currentExp -= _expToNextLevel;
                LevelUp();
            }
        }

        /// <summary>
        /// Adds gold to the player's wallet.
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            _gold += amount;
            OnGoldChanged?.Invoke(_gold);
        }

        /// <summary>
        /// Spends gold. Returns true if the player could afford it.
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (_gold < amount)
                return false;

            _gold -= amount;
            OnGoldChanged?.Invoke(_gold);
            return true;
        }

        /// <summary>
        /// Increments the kill counter.
        /// </summary>
        public void RegisterKill()
        {
            _totalKills++;
        }

        #endregion

        #region ─── Internal ────────────────────────────

        private void SetState(GameState newState)
        {
            if (_currentState == newState)
                return;

            GameState oldState = _currentState;
            _currentState = newState;

            OnStateChanged?.Invoke(oldState, newState);
            Debug.Log($"[GameManager] State: {oldState} → {newState}");
        }

        private void LevelUp()
        {
            _currentLevel++;
            _expToNextLevel = CalculateExpToNextLevel(_currentLevel);

            // Fire local event
            OnPlayerLevelUp?.Invoke(_currentLevel);

            // Fire global event
            EventBus.Instance?.FireLevelUp(_currentLevel);

            Debug.Log($"[GameManager] Level Up! Now level {_currentLevel}. Next at {_expToNextLevel} EXP.");
        }

        private void AdvanceWave()
        {
            if (_currentWave >= TotalWaves)
            {
                Victory();
                return;
            }

            _currentWave++;
            _waveTimer = 0f;

            // Fire local event
            OnWaveChanged?.Invoke(_currentWave);

            // Fire global event
            EventBus.Instance?.FireWaveStart(_currentWave);

            Debug.Log($"[GameManager] Wave {_currentWave}/{TotalWaves} started!");
        }

        /// <summary>
        /// Calculates EXP required to go from currentLevel → currentLevel+1.
        /// Uses a quadratic growth formula: base + level^2 * factor.
        /// </summary>
        private float CalculateExpToNextLevel(int level)
        {
            // Base 100 + quadratic scaling
            float multiplier = Config != null ? Config.ExpGrowthMultiplier : 1f;
            return (100f + level * level * 15f) / multiplier;
        }

        #endregion

        #region ─── Debug ───────────────────────────────

        [ContextMenu("Log State")]
        private void LogState()
        {
            Debug.Log($"[GameManager] State: {_currentState} | Wave: {_currentWave}/{TotalWaves} | " +
                      $"Level: {_currentLevel} | EXP: {_currentExp:F0}/{_expToNextLevel:F0} | " +
                      $"Kills: {_totalKills} | Gold: {_gold} | Time: {_elapsedTime:F1}s | " +
                      $"Speed: {_gameSpeed:F2}x");
        }

        #endregion
    }
}
