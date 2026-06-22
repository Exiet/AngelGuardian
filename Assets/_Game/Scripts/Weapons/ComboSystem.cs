using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AngelGuardian.Core;

namespace AngelGuardian.Weapons
{
    /// <summary>
    /// Represents a single combo definition: two weapons that can be combined
    /// to trigger a special effect.
    /// </summary>
    [Serializable]
    public class ComboDefinition
    {
        public string comboId;
        public string comboName;
        public string weaponA;          // Weapon ID of first weapon
        public string weaponB;          // Weapon ID of second weapon
        public string description;

        [Header("Effect")]
        public float damageMultiplier = 1.5f;
        public float effectDuration = 3f;
        public float stunDuration = 0f;
        public float rangeBonus = 0f;       // e.g. +40%
        public float attackSpeedBonus = 0f;  // e.g. +50% = 0.5
        public float damageReduction = 0f;   // e.g. 50% = 0.5
        public float pierceBonus = 0f;
        public float freezeMultiplier = 1f;  // Damage multiplier vs frozen enemies

        [Header("Cooldown")]
        public float cooldown = 10f;

        /// <summary>Additional tags for UI / categorization.</summary>
        public string[] tags;

        /// <summary>Current cooldown timer (runtime).</summary>
        [NonSerialized] public float currentCooldown = 0f;

        public bool IsReady => currentCooldown <= 0f;
        public bool IsOnCooldown => currentCooldown > 0f;
    }

    /// <summary>
    /// Core combo system for Angel Guardian.
    /// Manages a global combo accumulator (0-100), 8 defined combos,
    /// independent cooldowns, and triggers via EventBus.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        #region ─── Singleton ────────────────────────────

        private static ComboSystem _instance;
        public static ComboSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<ComboSystem>();
                return _instance;
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
            InitializeCombos();
        }

        #endregion

        #region ─── Constants ────────────────────────────

        /// <summary>Threshold at which a combo triggers.</summary>
        public const float COMBO_TRIGGER_THRESHOLD = 100f;

        /// <summary>Minimum cooldown floor for any combo.</summary>
        public const float MIN_COMBO_COOLDOWN = 1f;

        #endregion

        #region ─── Runtime State ────────────────────────

        [Header("Accumulator")]
        [SerializeField, Range(0f, 100f)] private float _comboAccumulator = 0f;

        [Header("Combo Definitions")]
        [SerializeField] private List<ComboDefinition> _comboDefinitions = new List<ComboDefinition>();

        [Header("Runtime (Read-Only)")]
        [SerializeField] private int _totalCombosTriggered = 0;
        [SerializeField] private string _lastTriggeredCombo = "";

        /// <summary>Current combo accumulator value (0-100).</summary>
        public float ComboAccumulator => _comboAccumulator;

        /// <summary>Total combos triggered this run.</summary>
        public int TotalCombosTriggered => _totalCombosTriggered;

        /// <summary>Name of the last triggered combo.</summary>
        public string LastTriggeredCombo => _lastTriggeredCombo;

        /// <summary>All combo definitions.</summary>
        public List<ComboDefinition> ComboDefinitions => _comboDefinitions;

        #endregion

        #region ─── Events ───────────────────────────────

        public event Action<ComboDefinition> OnComboTriggeredLocal;
        public event Action<float> OnAccumulatorChanged;

        #endregion

        #region ─── Unity Messages ───────────────────────

        private void Update()
        {
            // Tick all combo cooldowns
            float dt = Time.deltaTime;
            foreach (var combo in _comboDefinitions)
            {
                if (combo.currentCooldown > 0f)
                {
                    combo.currentCooldown = Mathf.Max(0f, combo.currentCooldown - dt);
                }
            }

            // Auto-decay accumulator slowly over time
            if (_comboAccumulator > 0f)
            {
                _comboAccumulator = Mathf.Max(0f, _comboAccumulator - dt * 2f);
            }
        }

        #endregion

        #region ─── Initialization ────────────────────────

        /// <summary>
        /// Initializes all 8 combo definitions based on design document.
        /// </summary>
        private void InitializeCombos()
        {
            if (_comboDefinitions.Count > 0) return;

            _comboDefinitions = new List<ComboDefinition>
            {
                // COMBO-01: 蒸汽爆炸 - 烈焰风暴(W07) + 冰霜新星(W08)
                new ComboDefinition
                {
                    comboId = "COMBO-01", comboName = "蒸汽爆炸",
                    weaponA = "W07", weaponB = "W08",
                    description = "烈焰风暴+冰霜新星 → 150%混合伤害+2秒眩晕",
                    damageMultiplier = 1.5f, effectDuration = 2f,
                    stunDuration = 2f, cooldown = 8f,
                    tags = new[] { "fire", "ice", "explosive", "stun" }
                },
                // COMBO-02: 光棘螺旋 - 圣光之环(W11) + 荆棘之环(C05-like)
                new ComboDefinition
                {
                    comboId = "COMBO-02", comboName = "光棘螺旋",
                    weaponA = "W11", weaponB = "W10", // 守护之盾 as proxy for荆棘
                    description = "圣光之环+荆棘之环 → 范围+40%+流血×2",
                    damageMultiplier = 1.4f, effectDuration = 3f,
                    rangeBonus = 0.4f, cooldown = 15f,
                    tags = new[] { "holy", "thorn", "bleed", "range" }
                },
                // COMBO-03: 弹幕同步 - 圣光连弩(W05) + 圣光霰弹(W03)
                new ComboDefinition
                {
                    comboId = "COMBO-03", comboName = "弹幕同步",
                    weaponA = "W05", weaponB = "W03",
                    description = "圣光连弩+圣光霰弹 → 3秒全远程攻速+50%",
                    damageMultiplier = 1.0f, effectDuration = 3f,
                    attackSpeedBonus = 0.5f, cooldown = 12f,
                    tags = new[] { "ranged", "rapid_fire", "spread", "attack_speed" }
                },
                // COMBO-04: 雷火交加 - 雷电之链(W09) + 烈焰风暴(W07)
                new ComboDefinition
                {
                    comboId = "COMBO-04", comboName = "雷火交加",
                    weaponA = "W09", weaponB = "W07",
                    description = "雷电之链+烈焰风暴 → 弹跳翻倍+灼烧",
                    damageMultiplier = 1.3f, effectDuration = 4f,
                    cooldown = 6f,
                    tags = new[] { "thunder", "fire", "chain", "burn", "bounce" }
                },
                // COMBO-05: 神圣壁垒 - 守护之盾(W10) + 圣光之环(W11)
                new ComboDefinition
                {
                    comboId = "COMBO-05", comboName = "神圣壁垒",
                    weaponA = "W10", weaponB = "W11",
                    description = "守护之盾+圣光之环 → 护婴儿+减伤50%",
                    damageMultiplier = 0.5f, effectDuration = 5f,
                    damageReduction = 0.5f, cooldown = 20f,
                    tags = new[] { "holy", "defense", "shield", "baby_protect" }
                },
                // COMBO-06: 冰封绝境 - 冰霜新星(W08) + 圣光审判(W04 as proxy)
                new ComboDefinition
                {
                    comboId = "COMBO-06", comboName = "冰封绝境",
                    weaponA = "W08", weaponB = "W04", // 圣光十字弩 as审判 proxy
                    description = "冰霜新星+圣光审判 → 冰冻敌人伤害×3",
                    damageMultiplier = 1.5f, effectDuration = 3f,
                    freezeMultiplier = 3f, cooldown = 30f,
                    tags = new[] { "ice", "holy", "freeze", "judgment" }
                },
                // COMBO-07: 风暴之眼 - 天使刺剑(W12) + 雷电之链(W09)
                new ComboDefinition
                {
                    comboId = "COMBO-07", comboName = "风暴之眼",
                    weaponA = "W12", weaponB = "W09",
                    description = "天使刺剑+雷电之链 → 雷电风暴3秒",
                    damageMultiplier = 1.8f, effectDuration = 3f,
                    cooldown = 10f,
                    tags = new[] { "melee", "thunder", "storm", "combo" }
                },
                // COMBO-08: 圣痕追击 - 追踪圣光(W06) + 天使步枪(W02)
                new ComboDefinition
                {
                    comboId = "COMBO-08", comboName = "圣痕追击",
                    weaponA = "W06", weaponB = "W02",
                    description = "追踪圣光+天使步枪 → 穿透追伤+100%",
                    damageMultiplier = 2.0f, effectDuration = 4f,
                    pierceBonus = 2f, cooldown = 5f,
                    tags = new[] { "holy", "homing", "precision", "pierce", "tracking" }
                }
            };
        }

        #endregion

        #region ─── Public API – Combo Charge ────────────

        /// <summary>
        /// Adds combo charge to the global accumulator.
        /// Called each time a weapon attacks.
        /// </summary>
        /// <param name="weight">Weapon rarity weight (1/2/3/5/7).</param>
        public void AddComboCharge(int weight)
        {
            _comboAccumulator += weight;

            Debug.Log($"[ComboSystem] Charge +{weight} → Accumulator: {_comboAccumulator:F0}/{COMBO_TRIGGER_THRESHOLD}");

            OnAccumulatorChanged?.Invoke(_comboAccumulator);

            if (_comboAccumulator >= COMBO_TRIGGER_THRESHOLD)
            {
                TryTriggerCombo();
            }
        }

        /// <summary>
        /// Attempts to trigger a combo from available (off-cooldown) combos
        /// that match the player's currently owned weapons.
        /// </summary>
        public void TryTriggerCombo()
        {
            WeaponManager weaponManager = WeaponManager.Instance;
            if (weaponManager == null)
            {
                Debug.LogWarning("[ComboSystem] No WeaponManager found.");
                return;
            }

            List<WeaponBase> ownedWeapons = weaponManager.GetAllWeapons();
            if (ownedWeapons.Count < 2)
            {
                Debug.Log("[ComboSystem] Need at least 2 weapons for combos.");
                _comboAccumulator = 0f; // Reset accumulator
                return;
            }

            // Find all available combos (both weapons owned AND off cooldown)
            List<ComboDefinition> availableCombos = new List<ComboDefinition>();
            foreach (var combo in _comboDefinitions)
            {
                if (!combo.IsReady) continue;

                if (CheckComboAvailable(combo.weaponA, combo.weaponB))
                {
                    availableCombos.Add(combo);
                }
            }

            if (availableCombos.Count == 0)
            {
                Debug.Log("[ComboSystem] No available combos. Accumulator reset.");
                _comboAccumulator = 0f;
                return;
            }

            // Randomly select one available combo
            ComboDefinition selectedCombo = availableCombos[UnityEngine.Random.Range(0, availableCombos.Count)];

            // Trigger it!
            TriggerCombo(selectedCombo);
        }

        /// <summary>
        /// Executes a specific combo: applies cooldown, fires events, resets accumulator.
        /// </summary>
        public void TriggerCombo(ComboDefinition combo)
        {
            if (combo == null) return;

            // Apply cooldown (with floor)
            combo.currentCooldown = Mathf.Max(MIN_COMBO_COOLDOWN, combo.cooldown);

            // Reset accumulator
            _comboAccumulator = 0f;

            // Track
            _totalCombosTriggered++;
            _lastTriggeredCombo = combo.comboName;

            // Notify the two weapons
            WeaponManager wm = WeaponManager.Instance;
            if (wm != null)
            {
                var weaponA = wm.GetWeaponById(combo.weaponA);
                var weaponB = wm.GetWeaponById(combo.weaponB);
                weaponA?.OnComboParticipated(combo.comboName);
                weaponB?.OnComboParticipated(combo.comboName);
            }

            // Fire local event
            OnComboTriggeredLocal?.Invoke(combo);

            // Fire global EventBus event
            EventBus.Instance?.FireComboTriggered(combo.comboName, combo.effectDuration);

            Debug.Log($"[ComboSystem] ⚡ COMBO TRIGGERED: {combo.comboName}! " +
                      $"({combo.weaponA}+{combo.weaponB}) " +
                      $"DMG×{combo.damageMultiplier}, Duration:{combo.effectDuration}s, " +
                      $"CD:{combo.cooldown}s | Total: {_totalCombosTriggered}");
        }

        #endregion

        #region ─── Public API – Combo Queries ───────────

        /// <summary>
        /// Checks if a combo between two specific weapons is available
        /// (both owned AND the combo is off cooldown).
        /// </summary>
        public bool CheckComboAvailable(string weaponIdA, string weaponIdB)
        {
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null) return false;

            // Both weapons must be owned
            if (!wm.HasWeapon(weaponIdA) || !wm.HasWeapon(weaponIdB))
                return false;

            // Find matching combo definition
            ComboDefinition combo = FindCombo(weaponIdA, weaponIdB);
            if (combo == null) return false;

            return combo.IsReady;
        }

        /// <summary>
        /// Finds a combo definition for the given weapon pair (order independent).
        /// </summary>
        public ComboDefinition FindCombo(string weaponIdA, string weaponIdB)
        {
            return _comboDefinitions.FirstOrDefault(c =>
                (c.weaponA == weaponIdA && c.weaponB == weaponIdB) ||
                (c.weaponA == weaponIdB && c.weaponB == weaponIdA));
        }

        /// <summary>
        /// Returns all combos that are currently available for the player.
        /// </summary>
        public List<ComboDefinition> GetAvailableCombos()
        {
            WeaponManager wm = WeaponManager.Instance;
            if (wm == null) return new List<ComboDefinition>();

            return _comboDefinitions.Where(c =>
                c.IsReady &&
                wm.HasWeapon(c.weaponA) &&
                wm.HasWeapon(c.weaponB)
            ).ToList();
        }

        /// <summary>
        /// Returns the cooldown remaining for a specific combo pair.
        /// </summary>
        public float GetComboCooldown(string weaponIdA, string weaponIdB)
        {
            ComboDefinition combo = FindCombo(weaponIdA, weaponIdB);
            return combo?.currentCooldown ?? 0f;
        }

        /// <summary>
        /// Forces a combo cooldown reset (e.g., from a card effect).
        /// </summary>
        public void ResetComboCooldown(string comboId)
        {
            ComboDefinition combo = _comboDefinitions.FirstOrDefault(c => c.comboId == comboId);
            if (combo != null)
            {
                combo.currentCooldown = 0f;
                Debug.Log($"[ComboSystem] Reset cooldown for {combo.comboName}");
            }
        }

        /// <summary>
        /// Reduces all combo cooldowns by a percentage (0-1).
        /// </summary>
        public void ReduceAllCooldowns(float percent)
        {
            foreach (var combo in _comboDefinitions)
            {
                combo.currentCooldown *= (1f - Mathf.Clamp01(percent));
            }
        }

        #endregion

        #region ─── Reset ────────────────────────────────

        /// <summary>
        /// Resets all combo state for a new run.
        /// </summary>
        public void ResetAll()
        {
            _comboAccumulator = 0f;
            _totalCombosTriggered = 0;
            _lastTriggeredCombo = "";

            foreach (var combo in _comboDefinitions)
            {
                combo.currentCooldown = 0f;
            }
        }

        #endregion

        #region ─── Debug ────────────────────────────────

        [ContextMenu("Log Combo Status")]
        private void LogComboStatus()
        {
            Debug.Log($"[ComboSystem] Accumulator: {_comboAccumulator:F0}/{COMBO_TRIGGER_THRESHOLD} | " +
                      $"Total Triggered: {_totalCombosTriggered} | Last: {_lastTriggeredCombo}");

            foreach (var combo in _comboDefinitions)
            {
                bool owned = CheckComboAvailable(combo.weaponA, combo.weaponB) || 
                             (WeaponManager.Instance != null && 
                              WeaponManager.Instance.HasWeapon(combo.weaponA) && 
                              WeaponManager.Instance.HasWeapon(combo.weaponB));
                
                string status = combo.IsReady ? "READY" : $"CD:{combo.currentCooldown:F1}s";
                string ownedStr = owned ? "OWNED" : "MISSING";
                Debug.Log($"  [{combo.comboId}] {combo.comboName}: {status} | {ownedStr} | " +
                          $"{combo.weaponA}+{combo.weaponB} | CD:{combo.cooldown}s");
            }
        }

        #endregion
    }
}
