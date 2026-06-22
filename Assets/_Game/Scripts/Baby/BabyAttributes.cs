using System;
using System.Collections.Generic;
using UnityEngine;
using AngelGuardian.Core;

namespace AngelGuardian.Baby
{
    /// <summary>
    /// 婴儿属性系统 —— 管理婴儿的所有属性（13个可见 + 10个隐藏）
    /// 精神力管理、护盾系统、死亡门槛保底
    /// </summary>
    public class BabyAttributes : MonoBehaviour
    {
        #region ─── Visible Attributes (13) ──────────────────

        [Header("=== Visible Attributes ===")]

        [SerializeField] private float _babyMaxMentalPower = 100f;
        [SerializeField] private float _babyRegenPerSec = 0f;
        [SerializeField] private float _babyMoveSpeed = 60f;
        [SerializeField] private float _babyDamageReduction = 0f;
        [SerializeField] private float _babyShield = 0f;
        [SerializeField] private float _angelToBabyLifesteal = 0f;
        [SerializeField] private float _babyLoyalty = 40f;
        [SerializeField] private float _babyCuriosity = 60f;
        [SerializeField] private int _babyFearThreshold = 3;
        [SerializeField] private int _babyAngerThreshold = 2;
        [SerializeField] private float _babyAttackPower = 0f;
        [SerializeField] private float _babyElementAffinity = 0f;
        [SerializeField] private float _babyAwakenCharge = 0f;

        #endregion

        #region ─── Hidden Attributes (10) ───────────────────

        [Header("=== Hidden Attributes ===")]

        [SerializeField] private float _babyMaxWanderDist = 250f;
        [SerializeField] private float _babyFollowWeight = 0.55f;
        [SerializeField] private float _babyExploreBias = 0.5f;
        [SerializeField] private float _emotionTickRate = 0.5f;
        [SerializeField] private float _fearSpeedBonus = 0.4f;
        [SerializeField] private float _angerShockwaveDmg = 30f;
        [SerializeField] private float _angerInvincibleDur = 1f;
        [SerializeField] private float _babyExpCoefficient = 0.3f;
        [SerializeField] private float _elementalDiminishReturn = 0.3f;
        [SerializeField] private float _deathGateThreshold = 0.1f;

        #endregion

        #region ─── Runtime State ─────────────────────────────

        /// <summary>当前精神力值</summary>
        [SerializeField] private float _currentMentalHP;

        /// <summary>护盾当前值</summary>
        [SerializeField] private float _currentShield;

        /// <summary>精神力恢复计时器</summary>
        private float _regenTimer;

        /// <summary>是否处于死亡门槛保护状态</summary>
        private bool _deathGateActive;

        #endregion

        #region ─── Properties ────────────────────────────────

        // Visible
        public float BabyMaxMentalPower   => _babyMaxMentalPower;
        public float BabyRegenPerSec      => _babyRegenPerSec;
        public float BabyMoveSpeed        => _babyMoveSpeed;
        public float BabyDamageReduction  => _babyDamageReduction;
        public float BabyShield           => _babyShield;
        public float AngelToBabyLifesteal => _angelToBabyLifesteal;
        public float BabyLoyalty          => Mathf.Clamp(_babyLoyalty, 0f, 100f);
        public float BabyCuriosity        => Mathf.Clamp(_babyCuriosity, 0f, 100f);
        public int   BabyFearThreshold    => _babyFearThreshold;
        public int   BabyAngerThreshold   => _babyAngerThreshold;
        public float BabyAttackPower      => _babyAttackPower;
        public float BabyElementAffinity  => _babyElementAffinity;
        public float BabyAwakenCharge     => _babyAwakenCharge;

        // Hidden
        public float BabyMaxWanderDist      => _babyMaxWanderDist;
        public float BabyFollowWeight       => _babyFollowWeight;
        public float BabyExploreBias        => _babyExploreBias;
        public float EmotionTickRate        => _emotionTickRate;
        public float FearSpeedBonus         => _fearSpeedBonus;
        public float AngerShockwaveDmg      => _angerShockwaveDmg;
        public float AngerInvincibleDur     => _angerInvincibleDur;
        public float BabyExpCoefficient     => _babyExpCoefficient;
        public float ElementalDiminishReturn => _elementalDiminishReturn;
        public float DeathGateThreshold     => _deathGateThreshold;

        /// <summary>当前精神力</summary>
        public float CurrentMentalHP
        {
            get => _currentMentalHP;
            private set
            {
                float old = _currentMentalHP;
                _currentMentalHP = Mathf.Clamp(value, 0f, _babyMaxMentalPower);

                // 检测死亡门槛
                float deathGate = _babyMaxMentalPower * _deathGateThreshold;
                _deathGateActive = _currentMentalHP <= deathGate && _currentMentalHP > 0f;

                // 归零时触发事件
                if (_currentMentalHP <= 0f && old > 0f)
                {
                    OnMentalZero();
                }
            }
        }

        /// <summary>精神力百分比 [0, 1]</summary>
        public float MentalHPPercent => _babyMaxMentalPower > 0f
            ? _currentMentalHP / _babyMaxMentalPower
            : 0f;

        /// <summary>是否处于死亡门槛保护</summary>
        public bool IsDeathGateActive => _deathGateActive;

        /// <summary>当前护盾值</summary>
        public float CurrentShield => _currentShield;

        /// <summary>是否精神归零</summary>
        public bool IsMentalZero => _currentMentalHP <= 0f;

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            // 从GameConfig读取默认值
            var config = GameManager.Instance?.Config;
            if (config != null)
            {
                _babyMaxMentalPower = config.babyMaxMentalPower;
                _emotionTickRate = config.EmotionTickRate;
            }

            _currentMentalHP = _babyMaxMentalPower;
            _currentShield = _babyShield;
        }

        private void Update()
        {
            // 精神力恢复
            if (_babyRegenPerSec > 0f && _currentMentalHP < _babyMaxMentalPower)
            {
                _regenTimer += Time.deltaTime;
                if (_regenTimer >= 1f)
                {
                    _regenTimer -= 1f;
                    float regenAmount = _babyRegenPerSec;
                    CurrentMentalHP += regenAmount;
                }
            }
        }

        #endregion

        #region ─── Damage & Healing ──────────────────────────

        /// <summary>
        /// 婴儿受到伤害
        /// </summary>
        /// <param name="rawDamage">原始伤害值</param>
        /// <returns>实际造成的伤害</returns>
        public float TakeDamage(float rawDamage)
        {
            if (rawDamage <= 0f) return 0f;
            if (IsMentalZero) return 0f;

            float damage = rawDamage;

            // 1. 护盾吸收
            if (_currentShield > 0f)
            {
                float shieldAbsorb = Mathf.Min(_currentShield, damage);
                _currentShield -= shieldAbsorb;
                damage -= shieldAbsorb;
            }

            // 2. 伤害减免
            if (_babyDamageReduction > 0f)
            {
                damage *= (1f - Mathf.Clamp01(_babyDamageReduction));
            }

            // 3. 死亡门槛保底：精神力<10%时，新伤害降低50%
            if (_deathGateActive)
            {
                damage *= 0.5f;
            }

            // 4. 应用伤害
            float oldHP = _currentMentalHP;
            CurrentMentalHP -= damage;
            float actualDamage = oldHP - _currentMentalHP;

            // 5. 发送受伤事件
            if (actualDamage > 0f)
            {
                EventBus.Instance?.FireBabyHurt(actualDamage, _currentMentalHP);
            }

            return actualDamage;
        }

        /// <summary>
        /// 恢复精神力
        /// </summary>
        public void HealMentalHP(float amount)
        {
            if (amount <= 0f) return;
            CurrentMentalHP += amount;
        }

        /// <summary>
        /// 添加护盾
        /// </summary>
        public void AddShield(float amount)
        {
            _currentShield += amount;
            _currentShield = Mathf.Max(_currentShield, 0f);
        }

        /// <summary>
        /// 设置护盾值
        /// </summary>
        public void SetShield(float amount)
        {
            _currentShield = Mathf.Max(amount, 0f);
        }

        #endregion

        #region ─── Mental Zero ───────────────────────────────

        /// <summary>
        /// 精神力归零处理
        /// </summary>
        private void OnMentalZero()
        {
            Debug.LogWarning("[BabyAttributes] Mental HP reached zero! Triggering EventBus.OnBabyMentalZero");

            // 发送全局事件
            EventBus.Instance?.FireBabyMentalZero();

            // 发送GameOver
            GameManager.Instance?.GameOver("BabyMentalZero");

            OnMentalZeroEvent?.Invoke();
        }

        /// <summary>精神归零事件</summary>
        public event Action OnMentalZeroEvent;

        #endregion

        #region ─── Awakening Charge ──────────────────────────

        /// <summary>
        /// 增加觉醒充能
        /// </summary>
        public void AddAwakenCharge(float amount)
        {
            _babyAwakenCharge = Mathf.Min(_babyAwakenCharge + amount, 100f);

            if (_babyAwakenCharge >= 100f)
            {
                OnAwakenReady?.Invoke();
            }
        }

        /// <summary>
        /// 消耗觉醒充能
        /// </summary>
        public void ConsumeAwakenCharge()
        {
            _babyAwakenCharge = 0f;
        }

        /// <summary>觉醒就绪事件</summary>
        public event Action OnAwakenReady;

        #endregion

        #region ─── Stat Modification ─────────────────────────

        /// <summary>
        /// 修改属性值（加法）
        /// </summary>
        public void ModifyStat(string fieldName, float delta)
        {
            switch (fieldName)
            {
                case "babyRegenPerSec":       _babyRegenPerSec       += delta; break;
                case "babyMoveSpeed":         _babyMoveSpeed         += delta; break;
                case "babyDamageReduction":   _babyDamageReduction   += delta; break;
                case "babyShield":            AddShield(delta);                break;
                case "angelToBabyLifesteal":  _angelToBabyLifesteal  += delta; break;
                case "babyLoyalty":           _babyLoyalty           += delta; break;
                case "babyCuriosity":         _babyCuriosity         += delta; break;
                case "babyAttackPower":       _babyAttackPower       += delta; break;
                case "babyElementAffinity":   _babyElementAffinity   += delta; break;
                default:
                    Debug.LogWarning($"[BabyAttributes] Unknown stat: {fieldName}");
                    break;
            }
        }

        /// <summary>
        /// 获取属性快照
        /// </summary>
        public Dictionary<string, float> GetStatsSnapshot()
        {
            return new Dictionary<string, float>
            {
                ["currentMentalHP"]       = _currentMentalHP,
                ["maxMentalPower"]        = _babyMaxMentalPower,
                ["mentalPercent"]         = MentalHPPercent,
                ["regenPerSec"]           = _babyRegenPerSec,
                ["moveSpeed"]             = _babyMoveSpeed,
                ["damageReduction"]       = _babyDamageReduction,
                ["shield"]                = _currentShield,
                ["angelToBabyLifesteal"]  = _angelToBabyLifesteal,
                ["loyalty"]               = _babyLoyalty,
                ["curiosity"]             = _babyCuriosity,
                ["fearThreshold"]         = _babyFearThreshold,
                ["angerThreshold"]        = _babyAngerThreshold,
                ["attackPower"]           = _babyAttackPower,
                ["elementAffinity"]       = _babyElementAffinity,
                ["awakenCharge"]          = _babyAwakenCharge,
            };
        }

        #endregion
    }
}
