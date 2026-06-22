using System;
using System.Collections.Generic;
using UnityEngine;
using AngelGuardian.Core;
using AngelGuardian.Player;

namespace AngelGuardian.Baby
{
    /// <summary>
    /// 婴儿AI行为树整合 —— 整合EmotionStateMachine和BabyController
    /// 每0.5秒调用情感状态机刷新，根据当前状态执行对应行为
    /// 可视化状态图标、受伤检测、觉醒充能管理
    /// </summary>
    [RequireComponent(typeof(BabyAttributes))]
    [RequireComponent(typeof(BabyController))]
    [RequireComponent(typeof(EmotionStateMachine))]
    public class BabyAI : MonoBehaviour
    {
        #region ─── Inspector ────────────────────────────────

        [Header("AI Settings")]
        [SerializeField] private float _aiTickInterval = 0.5f;

        [Header("Status Icon")]
        [SerializeField] private GameObject _statusIconRoot;
        [SerializeField] private SpriteRenderer _statusIconRenderer;
        [SerializeField] private Sprite _curiousIcon;
        [SerializeField] private Sprite _fearIcon;
        [SerializeField] private Sprite _angerIcon;
        [SerializeField] private Sprite _tiredIcon;
        [SerializeField] private Sprite _awakeningIcon;

        [Header("Visual Feedback")]
        [SerializeField] private Color _curiousColor = Color.green;
        [SerializeField] private Color _fearColor = Color.yellow;
        [SerializeField] private Color _angerColor = Color.red;
        [SerializeField] private Color _tiredColor = Color.gray;
        [SerializeField] private Color _awakeningColor = Color.magenta;

        [Header("Awakening Charge Sources")]
        [SerializeField] private float _chargePerEnemyKill = 2f;
        [SerializeField] private float _chargePerWave = 10f;
        [SerializeField] private float _chargePerBossKill = 25f;

        #endregion

        #region ─── Components ────────────────────────────────

        private BabyAttributes _attributes;
        private BabyController _controller;
        private EmotionStateMachine _emotion;

        // 外部引用
        private Transform _angelTransform;
        private Player.BabyInteraction _angelInteraction;

        #endregion

        #region ─── Runtime State ─────────────────────────────

        private float _aiTickTimer;
        private EmotionStateMachine.EmotionState _lastEmotionState;
        private SpriteRenderer _babyRenderer;

        // 受伤追踪（用于愤怒触发）
        private int _consecutiveHits;
        private float _lastHitTime;
        private const float HitChainWindow = 3f;

        // 觉醒充能来源追踪
        private float _totalChargeEarned;

        // 状态图标脉冲动画
        private float _iconPulseTimer;
        private Vector3 _iconBaseScale = Vector3.one;

        #endregion

        #region ─── Properties ────────────────────────────────

        /// <summary>当前情感状态</summary>
        public EmotionStateMachine.EmotionState CurrentEmotion => _emotion != null
            ? _emotion.CurrentState
            : EmotionStateMachine.EmotionState.CURIOUS;

        /// <summary>觉醒是否激活</summary>
        public bool IsAwakening => _emotion != null && _emotion.IsAwakeningActive;

        /// <summary>总充能获取量</summary>
        public float TotalChargeEarned => _totalChargeEarned;

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            _attributes = GetComponent<BabyAttributes>();
            _controller = GetComponent<BabyController>();
            _emotion = GetComponent<EmotionStateMachine>();

            _babyRenderer = GetComponent<SpriteRenderer>();

            // 查找状态图标组件
            if (_statusIconRoot == null)
            {
                var iconTransform = transform.Find("StatusIcon");
                if (iconTransform != null)
                {
                    _statusIconRoot = iconTransform.gameObject;
                    _statusIconRenderer = iconTransform.GetComponent<SpriteRenderer>();
                }
            }

            if (_statusIconRoot != null)
            {
                _iconBaseScale = _statusIconRoot.transform.localScale;
            }
        }

        private void Start()
        {
            // 查找天使
            FindAngelReferences();

            // 订阅事件
            SubscribeToEvents();

            // 初始化状态
            _lastEmotionState = _emotion.CurrentState;
            UpdateStatusIcon(_lastEmotionState);
        }

        private void Update()
        {
            // AI Tick
            _aiTickTimer += Time.deltaTime;
            if (_aiTickTimer >= _aiTickInterval)
            {
                _aiTickTimer = 0f;
                OnAITick();
            }

            // 更新状态图标
            UpdateStatusIconVisual();

            // 检测情感状态变化
            if (_emotion.CurrentState != _lastEmotionState)
            {
                OnEmotionChanged(_lastEmotionState, _emotion.CurrentState);
                _lastEmotionState = _emotion.CurrentState;
            }

            // 清理过期受伤记录
            if (Time.time - _lastHitTime > HitChainWindow)
            {
                _consecutiveHits = 0;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region ─── Event Subscriptions ────────────────────────

        private void SubscribeToEvents()
        {
            if (EventBus.Instance == null) return;

            EventBus.Instance.OnEnemyKilled.AddListener(OnEnemyKilled);
            EventBus.Instance.OnWaveStart.AddListener(OnWaveStart);
            EventBus.Instance.OnBossSpawned.AddListener(OnBossSpawned);
            EventBus.Instance.OnBabyHurt.AddListener(OnBabyHurtGlobal);
            EventBus.Instance.OnLevelUp.AddListener(OnLevelUp);
        }

        private void UnsubscribeFromEvents()
        {
            if (EventBus.Instance == null) return;

            EventBus.Instance.OnEnemyKilled.RemoveListener(OnEnemyKilled);
            EventBus.Instance.OnWaveStart.RemoveListener(OnWaveStart);
            EventBus.Instance.OnBossSpawned.RemoveListener(OnBossSpawned);
            EventBus.Instance.OnBabyHurt.RemoveListener(OnBabyHurtGlobal);
            EventBus.Instance.OnLevelUp.RemoveListener(OnLevelUp);
        }

        #endregion

        #region ─── AI Tick ───────────────────────────────────

        /// <summary>
        /// 每0.5秒AI Tick
        /// </summary>
        private void OnAITick()
        {
            // 情感状态机已在EmotionStateMachine.Update中自动刷新
            // 这里执行基于当前状态的额外AI行为

            switch (_emotion.CurrentState)
            {
                case EmotionStateMachine.EmotionState.CURIOUS:
                    ExecuteCuriousAI();
                    break;
                case EmotionStateMachine.EmotionState.FEAR:
                    ExecuteFearAI();
                    break;
                case EmotionStateMachine.EmotionState.ANGER:
                    ExecuteAngerAI();
                    break;
                case EmotionStateMachine.EmotionState.TIRED:
                    ExecuteTiredAI();
                    break;
                case EmotionStateMachine.EmotionState.AWAKENING:
                    ExecuteAwakeningAI();
                    break;
            }
        }

        #endregion

        #region ─── State-Specific AI ──────────────────────────

        private void ExecuteCuriousAI()
        {
            // CURIOUS状态的行为由EmotionStateMachine.ExecuteCuriousBehavior处理
            // 这里做额外的检测
            CheckAngelDistance();
        }

        private void ExecuteFearAI()
        {
            // FEAR状态的行为由EmotionStateMachine.ExecuteFearBehavior处理
            CheckAngelDistance();
        }

        private void ExecuteAngerAI()
        {
            // ANGER状态的行为由EmotionStateMachine.ExecuteAngerBehavior处理
        }

        private void ExecuteTiredAI()
        {
            // TIRED状态 —— 完全不动，只恢复
            _controller.StopMoving();
        }

        private void ExecuteAwakeningAI()
        {
            // AWAKENING状态的行为由EmotionStateMachine.ExecuteAwakeningBehavior处理
            // 全屏光照显形所有敌人（由光照系统处理）
        }

        /// <summary>
        /// 检测天使距离
        /// </summary>
        private void CheckAngelDistance()
        {
            if (_angelTransform == null)
            {
                FindAngelReferences();
                return;
            }

            float dist = Vector2.Distance(transform.position, _angelTransform.position);
            if (dist > _attributes.BabyMaxWanderDist)
            {
                _controller.SetMoveTarget(_angelTransform.position);
            }
        }

        #endregion

        #region ─── Emotion Changed Handler ───────────────────

        private void OnEmotionChanged(EmotionStateMachine.EmotionState oldState, EmotionStateMachine.EmotionState newState)
        {
            UpdateStatusIcon(newState);

            // 状态切换时的视觉反馈
            switch (newState)
            {
                case EmotionStateMachine.EmotionState.ANGER:
                    FlashColor(_angerColor, 0.5f);
                    break;
                case EmotionStateMachine.EmotionState.AWAKENING:
                    FlashColor(_awakeningColor, 1f);
                    break;
                case EmotionStateMachine.EmotionState.FEAR:
                    FlashColor(_fearColor, 0.3f);
                    break;
            }
        }

        #endregion

        #region ─── Status Icon ───────────────────────────────

        /// <summary>
        /// 更新头顶状态图标
        /// </summary>
        private void UpdateStatusIcon(EmotionStateMachine.EmotionState state)
        {
            if (_statusIconRenderer == null) return;

            Sprite icon = null;
            switch (state)
            {
                case EmotionStateMachine.EmotionState.CURIOUS:   icon = _curiousIcon;   break;
                case EmotionStateMachine.EmotionState.FEAR:      icon = _fearIcon;      break;
                case EmotionStateMachine.EmotionState.ANGER:     icon = _angerIcon;     break;
                case EmotionStateMachine.EmotionState.TIRED:     icon = _tiredIcon;     break;
                case EmotionStateMachine.EmotionState.AWAKENING: icon = _awakeningIcon; break;
            }

            _statusIconRenderer.sprite = icon;

            // 设置颜色
            switch (state)
            {
                case EmotionStateMachine.EmotionState.CURIOUS:   _statusIconRenderer.color = _curiousColor;   break;
                case EmotionStateMachine.EmotionState.FEAR:      _statusIconRenderer.color = _fearColor;      break;
                case EmotionStateMachine.EmotionState.ANGER:     _statusIconRenderer.color = _angerColor;     break;
                case EmotionStateMachine.EmotionState.TIRED:     _statusIconRenderer.color = _tiredColor;     break;
                case EmotionStateMachine.EmotionState.AWAKENING: _statusIconRenderer.color = _awakeningColor; break;
            }
        }

        /// <summary>
        /// 状态图标脉冲动画
        /// </summary>
        private void UpdateStatusIconVisual()
        {
            if (_statusIconRoot == null) return;

            _iconPulseTimer += Time.deltaTime;

            float pulseScale = 1f;
            Color iconColor = _statusIconRenderer != null ? _statusIconRenderer.color : Color.white;

            switch (_emotion.CurrentState)
            {
                case EmotionStateMachine.EmotionState.AWAKENING:
                    // 觉醒：强烈脉冲
                    pulseScale = 1f + Mathf.Sin(_iconPulseTimer * 4f) * 0.3f;
                    iconColor.a = 0.8f + Mathf.Sin(_iconPulseTimer * 6f) * 0.2f;
                    break;
                case EmotionStateMachine.EmotionState.ANGER:
                    // 愤怒：快速脉冲
                    pulseScale = 1f + Mathf.Sin(_iconPulseTimer * 3f) * 0.15f;
                    break;
                case EmotionStateMachine.EmotionState.FEAR:
                    // 恐惧：颤抖
                    pulseScale = 1f + Mathf.PerlinNoise(_iconPulseTimer * 5f, 0f) * 0.2f;
                    break;
                case EmotionStateMachine.EmotionState.TIRED:
                    // 疲惫：缓慢缩小
                    pulseScale = 0.85f + Mathf.Sin(_iconPulseTimer * 0.5f) * 0.05f;
                    iconColor.a = 0.6f;
                    break;
                default:
                    // 好奇：轻微浮动
                    pulseScale = 1f + Mathf.Sin(_iconPulseTimer * 1.5f) * 0.05f;
                    break;
            }

            _statusIconRoot.transform.localScale = _iconBaseScale * pulseScale;

            if (_statusIconRenderer != null)
                _statusIconRenderer.color = iconColor;
        }

        #endregion

        #region ─── Hit Detection (for ANGER trigger) ──────────

        /// <summary>
        /// 婴儿受到伤害时调用
        /// </summary>
        public void OnBabyHurt(float damage, float currentMentalHP)
        {
            float now = Time.time;

            // 连续受伤检测（3秒窗口）
            if (now - _lastHitTime <= HitChainWindow)
            {
                _consecutiveHits++;
            }
            else
            {
                _consecutiveHits = 1;
            }

            _lastHitTime = now;

            // 通知情感状态机记录受伤
            _emotion.RecordHit(damage);

            // 视觉反馈
            FlashColor(Color.white, 0.1f);
        }

        /// <summary>
        /// 全局BabyHurt事件处理
        /// </summary>
        private void OnBabyHurtGlobal(float damage, float currentMentalHP)
        {
            // 只处理自己的受伤事件
            // 此回调是全局的，如果场景中有多个婴儿则需要过滤
            OnBabyHurt(damage, currentMentalHP);
        }

        #endregion

        #region ─── Awakening Charge Management ───────────────

        private void OnEnemyKilled(GameObject enemy, Vector3 position)
        {
            // 检查是否在合理范围内（避免远距离击杀也充能）
            float dist = Vector2.Distance(transform.position, position);
            float maxChargeRange = 500f;

            if (dist <= maxChargeRange)
            {
                float charge = _chargePerEnemyKill;

                // 觉醒期间击杀额外充能（但不延长）
                if (_emotion.IsAwakeningActive)
                {
                    _emotion.OnAwakeningKill();
                    charge *= 0.5f; // 觉醒期间充能减半
                }

                AddAwakenCharge(charge);
            }
        }

        private void OnWaveStart(int waveNumber)
        {
            AddAwakenCharge(_chargePerWave);
            _emotion.UpdateAwakeningDuration(); // 更新觉醒时长
        }

        private void OnBossSpawned(string bossName)
        {
            AddAwakenCharge(_chargePerBossKill);
        }

        private void OnLevelUp(int newLevel)
        {
            _emotion.UpdateAwakeningDuration();
        }

        /// <summary>
        /// 增加觉醒充能
        /// </summary>
        public void AddAwakenCharge(float amount)
        {
            _attributes.AddAwakenCharge(amount);
            _totalChargeEarned += amount;
        }

        #endregion

        #region ─── Visual Feedback ───────────────────────────

        /// <summary>
        /// 闪烁颜色
        /// </summary>
        private void FlashColor(Color color, float duration)
        {
            if (_babyRenderer == null) return;

            StartCoroutine(FlashColorRoutine(color, duration));
        }

        private System.Collections.IEnumerator FlashColorRoutine(Color color, float duration)
        {
            if (_babyRenderer == null) yield break;

            Color original = _babyRenderer.color;
            _babyRenderer.color = color;

            yield return new WaitForSeconds(duration);

            if (_babyRenderer != null)
                _babyRenderer.color = original;
        }

        #endregion

        #region ─── Utility ───────────────────────────────────

        private void FindAngelReferences()
        {
            var angelObj = GameObject.FindGameObjectWithTag("Angel");
            if (angelObj == null) angelObj = GameObject.FindGameObjectWithTag("Player");
            if (angelObj == null) angelObj = GameObject.Find("Angel");

            if (angelObj != null)
            {
                _angelTransform = angelObj.transform;
                _angelInteraction = angelObj.GetComponent<Player.BabyInteraction>();
            }
        }

        /// <summary>
        /// 获取到天使的距离
        /// </summary>
        public float GetDistanceToAngel()
        {
            if (_angelTransform == null) return float.MaxValue;
            return Vector2.Distance(transform.position, _angelTransform.position);
        }

        /// <summary>
        /// 获取当前情感状态的调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[BabyAI] State: {_emotion.CurrentState} | " +
                   $"Mental: {_attributes.CurrentMentalHP:F0}/{_attributes.babyMaxMentalPower} | " +
                   $"Awaken: {_attributes.BabyAwakenCharge:F0}% | " +
                   $"ConsecHits: {_consecutiveHits} | " +
                   $"DistToAngel: {GetDistanceToAngel():F0}";
        }

        #endregion

        #region ─── Gizmos ────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 显示AI状态信息
            if (_emotion != null)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 3f,
                    _emotion.CurrentState.ToString()
                );
            }

            // 觉醒充能指示器
            if (_attributes != null)
            {
                float chargePercent = _attributes.BabyAwakenCharge / 100f;
                Gizmos.color = Color.Lerp(Color.blue, Color.magenta, chargePercent);
                Gizmos.DrawWireSphere(transform.position, 2f + chargePercent * 2f);
            }
        }

        #endregion
    }
}
