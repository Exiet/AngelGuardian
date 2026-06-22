using System;
using UnityEngine;
using UnityEngine.Events;
using AngelGuardian.Enemies;

namespace AngelGuardian.Core
{
    // ──────────────────────────────────────────────────
    //  Typed Event Classes (serializable in Inspector)
    // ──────────────────────────────────────────────────

    [Serializable]
    public class EnemyKilledEvent : UnityEvent<GameObject, Vector3> { }

    [Serializable]
    public class LevelUpEvent : UnityEvent<int> { }

    [Serializable]
    public class BabyHurtEvent : UnityEvent<float, float> { }

    [Serializable]
    public class BabyMentalZeroEvent : UnityEvent { }

    [Serializable]
    public class ComboTriggeredEvent : UnityEvent<string, float> { }

    [Serializable]
    public class TerrainStageChangeEvent : UnityEvent<int> { }

    [Serializable]
    public class GameOverEvent : UnityEvent<string> { }

    [Serializable]
    public class WaveStartEvent : UnityEvent<int> { }

    [Serializable]
    public class WeaponPickedUpEvent : UnityEvent<string> { }

    [Serializable]
    public class CardPickedUpEvent : UnityEvent<string> { }

    [Serializable]
    public class BossSpawnedEvent : UnityEvent<string> { }

    [Serializable]
    public class BabyEmotionChangedEvent : UnityEvent<string> { }

    [Serializable]
    public class DisasterTriggeredEvent : UnityEvent<string> { }

    [Serializable]
    public class ShopOpenedEvent : UnityEvent { }

    [Serializable]
    public class ShopClosedEvent : UnityEvent { }

    // ──────────────────────────────────────────────────
    //  EventBus – Global Singleton
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Global event bus for Angel Guardian.
    /// Use <see cref="Instance"/> to access and subscribe/unsubscribe.
    /// 
    /// Usage:
    ///   EventBus.Instance.OnEnemyKilled.AddListener(HandleEnemyKilled);
    ///   EventBus.Instance.OnEnemyKilled.Invoke(enemy, pos);
    /// </summary>
    public class EventBus : MonoBehaviour
    {
        #region Singleton

        private static EventBus _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        /// <summary>
        /// Thread-safe singleton accessor. Creates a new GameObject if none exists.
        /// </summary>
        public static EventBus Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[EventBus] Instance accessed after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // Search scene for an existing instance
                        _instance = FindObjectOfType<EventBus>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[EventBus]");
                            _instance = go.AddComponent<EventBus>();
                            DontDestroyOnLoad(go);
                        }
                        else if (_instance.transform.parent == null)
                        {
                            // If found but not marked, make it persistent
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
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            _instance = null;
        }

        #endregion

        #region Event Definitions

        /// <summary>Fired when an enemy is killed. (enemy GameObject, kill position)</summary>
        public EnemyKilledEvent OnEnemyKilled { get; private set; } = new EnemyKilledEvent();

        /// <summary>Fired when the player levels up. (new level)</summary>
        public LevelUpEvent OnLevelUp { get; private set; } = new LevelUpEvent();

        /// <summary>Fired when the Baby takes damage. (damage amount, current mental HP)</summary>
        public BabyHurtEvent OnBabyHurt { get; private set; } = new BabyHurtEvent();

        /// <summary>Fired when the Baby's mental power reaches zero.</summary>
        public BabyMentalZeroEvent OnBabyMentalZero { get; private set; } = new BabyMentalZeroEvent();

        /// <summary>Fired when a combo is triggered. (combo name, duration in seconds)</summary>
        public ComboTriggeredEvent OnComboTriggered { get; private set; } = new ComboTriggeredEvent();

        /// <summary>Fired when the terrain destruction stage changes. (new stage index)</summary>
        public TerrainStageChangeEvent OnTerrainStageChange { get; private set; } = new TerrainStageChangeEvent();

        /// <summary>Fired when the game is over. (failure type string)</summary>
        public GameOverEvent OnGameOver { get; private set; } = new GameOverEvent();

        /// <summary>Fired when a new wave starts. (wave number)</summary>
        public WaveStartEvent OnWaveStart { get; private set; } = new WaveStartEvent();

        /// <summary>Fired when a weapon is picked up. (weapon ID)</summary>
        public WeaponPickedUpEvent OnWeaponPickedUp { get; private set; } = new WeaponPickedUpEvent();

        /// <summary>Fired when a card is picked up. (card ID)</summary>
        public CardPickedUpEvent OnCardPickedUp { get; private set; } = new CardPickedUpEvent();

        /// <summary>Fired when a boss spawns. (boss name)</summary>
        public BossSpawnedEvent OnBossSpawned { get; private set; } = new BossSpawnedEvent();

        /// <summary>Fired when the Baby's emotion state changes. (emotion state name)</summary>
        public BabyEmotionChangedEvent OnBabyEmotionChanged { get; private set; } = new BabyEmotionChangedEvent();

        /// <summary>Fired when a disaster is triggered. (disaster name)</summary>
        public DisasterTriggeredEvent OnDisasterTriggered { get; private set; } = new DisasterTriggeredEvent();

        /// <summary>Fired when the shop UI is opened.</summary>
        public ShopOpenedEvent OnShopOpened { get; private set; } = new ShopOpenedEvent();

        /// <summary>Fired when the shop UI is closed.</summary>
        public ShopClosedEvent OnShopClosed { get; private set; } = new ShopClosedEvent();

        #endregion

        #region Public API – Convenience Fire Methods

        public void FireEnemyKilled(GameObject enemy, Vector3 position)
        {
            var eb = enemy.GetComponent<EnemyBase>();
            if (eb != null)
                EnemyBase.TriggerEnemyKilled(eb.enemyId, eb.enemyName, position);
        }

        public void FireLevelUp(int newLevel)
            => OnLevelUp?.Invoke(newLevel);

        public void FireBabyHurt(float damage, float currentMentalHP)
            => OnBabyHurt?.Invoke(damage, currentMentalHP);

        public void FireBabyMentalZero()
            => OnBabyMentalZero?.Invoke();

        public void FireComboTriggered(string comboName, float duration)
            => OnComboTriggered?.Invoke(comboName, duration);

        public void FireTerrainStageChange(int newStage)
            => OnTerrainStageChange?.Invoke(newStage);

        public void FireGameOver(string failType)
            => OnGameOver?.Invoke(failType);

        public void FireWaveStart(int waveNumber)
            => OnWaveStart?.Invoke(waveNumber);

        public void FireWeaponPickedUp(string weaponId)
            => OnWeaponPickedUp?.Invoke(weaponId);

        public void FireCardPickedUp(string cardId)
            => OnCardPickedUp?.Invoke(cardId);

        public void FireBossSpawned(string bossName)
            => OnBossSpawned?.Invoke(bossName);

        public void FireBabyEmotionChanged(string emotionState)
            => OnBabyEmotionChanged?.Invoke(emotionState);

        public void FireDisasterTriggered(string disasterName)
            => OnDisasterTriggered?.Invoke(disasterName);

        public void FireShopOpened()
            => OnShopOpened?.Invoke();

        public void FireShopClosed()
            => OnShopClosed?.Invoke();

        #endregion

        #region Debug

        [ContextMenu("Print Subscriber Counts")]
        private void PrintSubscriberCounts()
        {
            Debug.Log($"[EventBus] OnEnemyKilled:            {OnEnemyKilled.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnLevelUp:               {OnLevelUp.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnBabyHurt:              {OnBabyHurt.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnBabyMentalZero:        {OnBabyMentalZero.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnComboTriggered:        {OnComboTriggered.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnTerrainStageChange:    {OnTerrainStageChange.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnGameOver:              {OnGameOver.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnWaveStart:             {OnWaveStart.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnWeaponPickedUp:        {OnWeaponPickedUp.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnCardPickedUp:          {OnCardPickedUp.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnBossSpawned:           {OnBossSpawned.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnBabyEmotionChanged:    {OnBabyEmotionChanged.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnDisasterTriggered:     {OnDisasterTriggered.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnShopOpened:            {OnShopOpened.GetPersistentEventCount()}");
            Debug.Log($"[EventBus] OnShopClosed:            {OnShopClosed.GetPersistentEventCount()}");
        }

        #endregion
    }
}
