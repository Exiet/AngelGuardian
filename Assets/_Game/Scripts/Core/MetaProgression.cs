using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// Permanent progression system that persists across game runs using PlayerPrefs.
    /// Manages soul shards, angel tears, and permanent upgrade levels.
    /// </summary>
    public class MetaProgression : MonoBehaviour
    {
        #region ─── Upgrade Types ───────────────────────

        public enum UpgradeType
        {
            InitialMentalBonus,    // +mental HP at start of each run
            InitialWeaponSlot,     // extra weapon slot at start
            BaseLuckBonus,         // global luck modifier
            CardRerollCount,       // number of free card rerolls per run
            WeaponRerollFree,      // free weapon rerolls per run
            ExtraAttributes        // bonus attribute points
        }

        #endregion

        #region ─── Singleton ───────────────────────────

        private static MetaProgression _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static MetaProgression Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[MetaProgression] Instance accessed after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<MetaProgression>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[MetaProgression]");
                            _instance = go.AddComponent<MetaProgression>();
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

            // Load saved data on creation
            LoadProgress();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            SaveProgress();
            _instance = null;
        }

        #endregion

        #region ─── PlayerPrefs Keys ────────────────────

        private const string KEY_SOUL_SHARDS      = "Meta_SoulShards";
        private const string KEY_ANGEL_TEARS      = "Meta_AngelTears";

        private const string KEY_UPGRADE_MENTAL   = "Meta_Upgrade_InitialMentalBonus";
        private const string KEY_UPGRADE_SLOTS    = "Meta_Upgrade_InitialWeaponSlot";
        private const string KEY_UPGRADE_LUCK     = "Meta_Upgrade_BaseLuckBonus";
        private const string KEY_UPGRADE_REROLL   = "Meta_Upgrade_CardRerollCount";
        private const string KEY_UPGRADE_WREROLL  = "Meta_Upgrade_WeaponRerollFree";
        private const string KEY_UPGRADE_ATTR     = "Meta_Upgrade_ExtraAttributes";

        #endregion

        #region ─── Runtime Data ───────────────────────

        [Header("Resources")]
        [SerializeField] private int _soulShards = 0;
        /// <summary>Soul shards — earned per run, used for meta upgrades.</summary>
        public int SoulShards => _soulShards;

        [SerializeField] private int _angelTears = 0;
        /// <summary>Angel tears — premium meta currency.</summary>
        public int AngelTears => _angelTears;

        [Header("Upgrade Levels")]
        [SerializeField] private int _initialMentalBonus = 0;
        [SerializeField] private int _initialWeaponSlot = 0;
        [SerializeField] private int _baseLuckBonus = 0;
        [SerializeField] private int _cardRerollCount = 0;
        [SerializeField] private int _weaponRerollFree = 0;
        [SerializeField] private int _extraAttributes = 0;

        #endregion

        #region ─── Public API – Resources ─────────────

        /// <summary>
        /// Adds soul shards and auto-saves.
        /// </summary>
        public void AddSoulShards(int amount)
        {
            if (amount <= 0) return;
            _soulShards += amount;
            SaveProgress();
        }

        /// <summary>
        /// Adds angel tears and auto-saves.
        /// </summary>
        public void AddAngelTears(int amount)
        {
            if (amount <= 0) return;
            _angelTears += amount;
            SaveProgress();
        }

        /// <summary>
        /// Checks if the player can afford a meta upgrade.
        /// </summary>
        /// <param name="cost">Cost structure: x = soul shards, y = angel tears.</param>
        /// <returns>True if both resources are sufficient.</returns>
        public bool CanAfford(Vector2Int cost)
        {
            return _soulShards >= cost.x && _angelTears >= cost.y;
        }

        /// <summary>
        /// Spends resources of a given type. Returns true on success.
        /// </summary>
        public bool SpendResource(ResourceType type, int amount)
        {
            if (amount <= 0) return true;

            switch (type)
            {
                case ResourceType.SoulShard:
                    if (_soulShards < amount) return false;
                    _soulShards -= amount;
                    break;

                case ResourceType.AngelTear:
                    if (_angelTears < amount) return false;
                    _angelTears -= amount;
                    break;

                default:
                    return false;
            }

            SaveProgress();
            return true;
        }

        /// <summary>
        /// Spends both resources at once. Returns true on success.
        /// </summary>
        public bool SpendResources(Vector2Int cost)
        {
            if (!CanAfford(cost))
                return false;

            _soulShards -= cost.x;
            _angelTears -= cost.y;
            SaveProgress();
            return true;
        }

        #endregion

        #region ─── Public API – Upgrades ───────────────

        /// <summary>
        /// Returns the current level of the specified upgrade type.
        /// </summary>
        public int GetUpgradeLevel(UpgradeType upgradeType)
        {
            return upgradeType switch
            {
                UpgradeType.InitialMentalBonus  => _initialMentalBonus,
                UpgradeType.InitialWeaponSlot   => _initialWeaponSlot,
                UpgradeType.BaseLuckBonus       => _baseLuckBonus,
                UpgradeType.CardRerollCount     => _cardRerollCount,
                UpgradeType.WeaponRerollFree    => _weaponRerollFree,
                UpgradeType.ExtraAttributes     => _extraAttributes,
                _ => 0
            };
        }

        /// <summary>
        /// Increments the specified upgrade by 1 level. Requires resources to be spent first.
        /// </summary>
        public void LevelUpUpgrade(UpgradeType upgradeType)
        {
            switch (upgradeType)
            {
                case UpgradeType.InitialMentalBonus:
                    _initialMentalBonus++;
                    break;
                case UpgradeType.InitialWeaponSlot:
                    _initialWeaponSlot++;
                    break;
                case UpgradeType.BaseLuckBonus:
                    _baseLuckBonus++;
                    break;
                case UpgradeType.CardRerollCount:
                    _cardRerollCount++;
                    break;
                case UpgradeType.WeaponRerollFree:
                    _weaponRerollFree++;
                    break;
                case UpgradeType.ExtraAttributes:
                    _extraAttributes++;
                    break;
            }

            SaveProgress();
        }

        /// <summary>
        /// Returns the upgrade cost for a given level of a specific upgrade type.
        /// Override this to define custom cost curves.
        /// </summary>
        public Vector2Int GetUpgradeCost(UpgradeType upgradeType)
        {
            int currentLevel = GetUpgradeLevel(upgradeType);

            // Base cost + scaling per level
            return upgradeType switch
            {
                UpgradeType.InitialMentalBonus  => new Vector2Int(10 + currentLevel * 5, 0),
                UpgradeType.InitialWeaponSlot   => new Vector2Int(20 + currentLevel * 10, 1 + currentLevel),
                UpgradeType.BaseLuckBonus       => new Vector2Int(15 + currentLevel * 8, 0),
                UpgradeType.CardRerollCount     => new Vector2Int(5 + currentLevel * 3, 0),
                UpgradeType.WeaponRerollFree    => new Vector2Int(10 + currentLevel * 5, 1),
                UpgradeType.ExtraAttributes     => new Vector2Int(25 + currentLevel * 15, 2 + currentLevel),
                _ => Vector2Int.zero
            };
        }

        #endregion

        #region ─── Persistence ─────────────────────────

        /// <summary>
        /// Saves all progression data to PlayerPrefs.
        /// </summary>
        [ContextMenu("Save Progress")]
        public void SaveProgress()
        {
            PlayerPrefs.SetInt(KEY_SOUL_SHARDS, _soulShards);
            PlayerPrefs.SetInt(KEY_ANGEL_TEARS, _angelTears);

            PlayerPrefs.SetInt(KEY_UPGRADE_MENTAL,  _initialMentalBonus);
            PlayerPrefs.SetInt(KEY_UPGRADE_SLOTS,   _initialWeaponSlot);
            PlayerPrefs.SetInt(KEY_UPGRADE_LUCK,    _baseLuckBonus);
            PlayerPrefs.SetInt(KEY_UPGRADE_REROLL,  _cardRerollCount);
            PlayerPrefs.SetInt(KEY_UPGRADE_WREROLL, _weaponRerollFree);
            PlayerPrefs.SetInt(KEY_UPGRADE_ATTR,    _extraAttributes);

            PlayerPrefs.Save();

            Debug.Log("[MetaProgression] Progress saved.");
        }

        /// <summary>
        /// Loads all progression data from PlayerPrefs.
        /// </summary>
        [ContextMenu("Load Progress")]
        public void LoadProgress()
        {
            _soulShards = PlayerPrefs.GetInt(KEY_SOUL_SHARDS, 0);
            _angelTears = PlayerPrefs.GetInt(KEY_ANGEL_TEARS, 0);

            _initialMentalBonus  = PlayerPrefs.GetInt(KEY_UPGRADE_MENTAL,  0);
            _initialWeaponSlot   = PlayerPrefs.GetInt(KEY_UPGRADE_SLOTS,   0);
            _baseLuckBonus       = PlayerPrefs.GetInt(KEY_UPGRADE_LUCK,    0);
            _cardRerollCount     = PlayerPrefs.GetInt(KEY_UPGRADE_REROLL,  0);
            _weaponRerollFree    = PlayerPrefs.GetInt(KEY_UPGRADE_WREROLL, 0);
            _extraAttributes     = PlayerPrefs.GetInt(KEY_UPGRADE_ATTR,    0);

            Debug.Log("[MetaProgression] Progress loaded.");
        }

        /// <summary>
        /// Fully resets all meta progression data. USE WITH CAUTION.
        /// </summary>
        [ContextMenu("Reset All Progress")]
        public void ResetAllProgress()
        {
            _soulShards = 0;
            _angelTears = 0;

            _initialMentalBonus  = 0;
            _initialWeaponSlot   = 0;
            _baseLuckBonus       = 0;
            _cardRerollCount     = 0;
            _weaponRerollFree    = 0;
            _extraAttributes     = 0;

            SaveProgress();

            Debug.LogWarning("[MetaProgression] ALL progress has been reset!");
        }

        #endregion

        #region ─── Debug ───────────────────────────────

        [ContextMenu("Log Progression")]
        private void LogProgression()
        {
            Debug.Log($"[MetaProgression] Soul Shards: {_soulShards} | Angel Tears: {_angelTears}\n" +
                      $"  InitialMentalBonus:  {_initialMentalBonus}\n" +
                      $"  InitialWeaponSlot:   {_initialWeaponSlot}\n" +
                      $"  BaseLuckBonus:       {_baseLuckBonus}\n" +
                      $"  CardRerollCount:     {_cardRerollCount}\n" +
                      $"  WeaponRerollFree:    {_weaponRerollFree}\n" +
                      $"  ExtraAttributes:     {_extraAttributes}");
        }

        #endregion
    }

    #region ─── ResourceType Enum ──────────────────────

    /// <summary>
    /// Types of meta-progression resources.
    /// </summary>
    public enum ResourceType
    {
        SoulShard,
        AngelTear
    }

    #endregion
}
