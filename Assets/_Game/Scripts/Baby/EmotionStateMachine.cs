using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AngelGuardian.Core;

namespace AngelGuardian.Baby
{
    /// <summary>
    /// 婴儿情感状态机 —— 游戏最核心的情感系统
    /// 
    /// 状态: [CURIOUS, FEAR, ANGER, TIRED, AWAKENING]
    /// 刷新频率: 每0.5秒
    /// 优先级: AWAKENING > ANGER > TIRED > FEAR > CURIOUS
    /// 状态锁定: 0.5秒（觉醒除外）
    /// </summary>
    [RequireComponent(typeof(BabyAttributes))]
    [RequireComponent(typeof(BabyController))]
    public class EmotionStateMachine : MonoBehaviour
    {
        #region ─── Emotion States Enum ───────────────────────

        public enum EmotionState
        {
            CURIOUS,    // 好奇（默认）
            FEAR,       // 恐惧
            ANGER,      // 愤怒
            TIRED,      // 疲惫
            AWAKENING   // 觉醒
        }

        #endregion

        #region ─── Inspector ────────────────────────────────

        [Header("State Configuration")]
        [SerializeField] private EmotionState _initialState = EmotionState.CURIOUS;

        [Header("Tick")]
        [SerializeField] private float _tickInterval = 0.5f;

        [Header("CURIOUS")]
        [SerializeField] private float _curiousWanderMin = 100f;
        [SerializeField] private float _curiousWanderMax = 300f;
        [SerializeField] private float _curiousIdleChance = 0.1f;
        [SerializeField] private float _curiousIdleDuration = 1.5f;
        [SerializeField] private float _curiousForceReturnDist = 400f;

        [Header("FEAR")]
        [SerializeField] private float _fearDetectionRadius = 150f;
        [SerializeField] private float _fearExitCooldown = 10f;

        [Header("ANGER")]
        [SerializeField] private float _angerHitWindow = 3f;
        [SerializeField] private float _angerDuration = 3f;
        [SerializeField] private float _angerCooldown = 30f;
        [SerializeField] private float _angerShockwaveRadius = 8f;
        [SerializeField] private float _angerAttackMultiplier = 2f;
        [SerializeField] private float _angerRangeBonus = 0.5f;

        [Header("TIRED")]
        [SerializeField] private float _tiredThreshold = 0.3f;
        [SerializeField] private float _tiredRegenMultiplier = 2f;

        [Header("AWAKENING")]
        [SerializeField] private float _awakeningMinDuration = 30f;
        [SerializeField] private float _awakeningMaxDuration = 54f;
        [SerializeField] private float _awakeningCooldown = 120f;
        [SerializeField] private float _awakeningKillExtension = 1f;
        [SerializeField] private float _awakeningSpeedMultiplier = 1.5f;
        [SerializeField] private float _awakeningDamageMultiplier = 3f;

        #endregion

        #region ─── Components ────────────────────────────────

        private BabyAttributes _attributes;
        private BabyController _controller;
        private BabyAI _ai;

        #endregion

        #region ─── Runtime State ─────────────────────────────

        // 当前状态
        private EmotionState _currentState;
        private EmotionState _previousState;

        // 状态锁定计时器
        private float _stateLockTimer;

        // 状态进入时间
        private float _stateEnterTime;

        // Tick计时器
        private float _tickTimer;

        // 恐惧冷却
        private float _fearCooldownTimer;

        // 愤怒相关
        private float _angerCooldownTimer;
        private float _angerTimer;
        private bool _isAngerActive;
        private List<HitRecord> _recentHits = new List<HitRecord>();

        // 觉醒相关
        private float _awakeningCooldownTimer;
        private float _awakeningTimer;
        private float _awakeningDuration;
        private bool _isAwakeningActive;
        private int _awakeningKillCount;

        // 好奇：空闲状态
        private bool _isCuriousIdle;
        private float _curiousIdleTimer;

        #endregion

        #region ─── Properties ────────────────────────────────

        /// <summary>当前情感状态</summary>
        public EmotionState CurrentState => _currentState;

        /// <summary>上一帧的情感状态</summary>
        public EmotionState PreviousState => _previousState;

        /// <summary>状态名称字符串</summary>
        public string CurrentStateName => _currentState.ToString();

        /// <summary>愤怒是否激活</summary>
        public bool IsAngerActive => _isAngerActive;

        /// <summary>觉醒是否激活</summary>
        public bool IsAwakeningActive => _isAwakeningActive;

        /// <summary>觉醒剩余时间</summary>
        public float AwakeningTimeRemaining => _isAwakeningActive ? _awakeningTimer : 0f;

        /// <summary>觉醒击杀数</summary>
        public int AwakeningKillCount => _awakeningKillCount;

        /// <summary>恐惧冷却剩余</summary>
        public float FearCooldownRemaining => Mathf.Max(0f, _fearCooldownTimer);

        /// <summary>愤怒冷却剩余</summary>
        public float AngerCooldownRemaining => Mathf.Max(0f, _angerCooldownTimer);

        /// <summary>觉醒冷却剩余</summary>
        public float AwakeningCooldownRemaining => Mathf.Max(0f, _awakeningCooldownTimer);

        #endregion

        #region ─── Events ────────────────────────────────────

        /// <summary>状态改变事件 (旧状态, 新状态)</summary>
        public event Action<EmotionState, EmotionState> OnStateChanged;

        /// <summary>愤怒冲击波释放事件</summary>
        public event Action<Vector2, float, float> OnAngerShockwave;

        /// <summary>觉醒开始事件</summary>
        public event Action OnAwakeningStarted;

        /// <summary>觉醒结束事件</summary>
        public event Action OnAwakeningEnded;

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            _attributes = GetComponent<BabyAttributes>();
            _controller = GetComponent<BabyController>();
            _ai = GetComponent<BabyAI>();
        }

        private void Start()
        {
            _currentState = _initialState;
            _previousState = _initialState;
            _stateEnterTime = Time.time;

            // 从配置读取tick间隔
            if (GameManager.Instance?.Config != null)
            {
                _tickInterval = GameManager.Instance.Config.EmotionTickRate;
            }

            // 初始化觉醒时长（随等级）
            UpdateAwakeningDuration();
        }

        private void Update()
        {
            // 更新冷却计时器
            UpdateCooldowns();

            // Tick刷新
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= _tickInterval)
            {
                _tickTimer = 0f;
                EvaluateState();
            }

            // 执行当前状态的行为
            ExecuteStateBehavior();

            // 检测状态锁定释放
            if (_stateLockTimer > 0f)
                _stateLockTimer -= Time.deltaTime;
        }

        #endregion

        #region ─── State Evaluation ──────────────────────────

        /// <summary>
        /// 评估并切换状态
        /// 优先级: AWAKENING > ANGER > TIRED > FEAR > CURIOUS
        /// </summary>
        private void EvaluateState()
        {
            // 状态锁定中不切换（觉醒除外）
            if (_stateLockTimer > 0f && _currentState != EmotionState.AWAKENING)
                return;

            EmotionState newState = _currentState;

            // 1. 检查觉醒（最高优先级）
            if (CheckAwakeningCondition())
            {
                newState = EmotionState.AWAKENING;
            }
            // 2. 检查愤怒
            else if (_currentState != EmotionState.AWAKENING && CheckAngerCondition())
            {
                newState = EmotionState.ANGER;
            }
            // 3. 检查疲惫
            else if (_currentState != EmotionState.AWAKENING && _currentState != EmotionState.ANGER && CheckTiredCondition())
            {
                newState = EmotionState.TIRED;
            }
            // 4. 检查恐惧
            else if (_currentState != EmotionState.AWAKENING && _currentState != EmotionState.ANGER && CheckFearCondition())
            {
                newState = EmotionState.FEAR;
            }
            // 5. 默认为好奇
            else if (_currentState != EmotionState.AWAKENING && _currentState != EmotionState.ANGER)
            {
                // 如果当前不是觉醒/愤怒/恐惧/疲惫 → 回到好奇
                if (_currentState != EmotionState.CURIOUS &&
                    _currentState != EmotionState.FEAR &&
                    _currentState != EmotionState.TIRED)
                {
                    newState = EmotionState.CURIOUS;
                }
            }

            // 切换状态
            if (newState != _currentState)
            {
                TransitionTo(newState);
            }
        }

        /// <summary>
        /// 检查觉醒条件：觉醒充能 ≥ 100 且 冷却完毕
        /// </summary>
        private bool CheckAwakeningCondition()
        {
            if (_isAwakeningActive) return true; // 保持觉醒
            if (_awakeningCooldownTimer > 0f) return false;

            return _attributes.BabyAwakenCharge >= 100f;
        }

        /// <summary>
        /// 检查愤怒条件：3秒内连续受伤 ≥ 愤怒阈值
        /// </summary>
        private bool CheckAngerCondition()
        {
            if (_isAngerActive) return true; // 保持愤怒
            if (_angerCooldownTimer > 0f) return false;

            // 清理过期记录
            _recentHits.RemoveAll(h => Time.time - h.Time > _angerHitWindow);

            return _recentHits.Count >= _attributes.BabyAngerThreshold;
        }

        /// <summary>
        /// 检查疲惫条件：精神力 < 30%
        /// </summary>
        private bool CheckTiredCondition()
        {
            return _attributes.MentalHPPercent < _tiredThreshold;
        }

        /// <summary>
        /// 检查恐惧条件：附近150px内敌人 ≥ 恐惧阈值 且 冷却完毕
        /// </summary>
        private bool CheckFearCondition()
        {
            if (_fearCooldownTimer > 0f) return false;

            int nearbyEnemies = CountNearbyEnemies(_fearDetectionRadius);
            return nearbyEnemies >= _attributes.BabyFearThreshold;
        }

        #endregion

        #region ─── State Transition ──────────────────────────

        /// <summary>
        /// 切换到新状态
        /// </summary>
        private void TransitionTo(EmotionState newState)
        {
            _previousState = _currentState;

            // 退出当前状态
            ExitState(_currentState);

            // 进入新状态
            _currentState = newState;
            _stateEnterTime = Time.time;

            // 状态锁定（觉醒除外）
            if (newState != EmotionState.AWAKENING)
            {
                _stateLockTimer = _tickInterval;
            }

            EnterState(newState);

            // 发送事件
            OnStateChanged?.Invoke(_previousState, _currentState);
            EventBus.Instance?.FireBabyEmotionChanged(newState.ToString());

            Debug.Log($"[EmotionStateMachine] State: {_previousState} → {_currentState}");
        }

        private void EnterState(EmotionState state)
        {
            switch (state)
            {
                case EmotionState.CURIOUS:
                    EnterCurious();
                    break;
                case EmotionState.FEAR:
                    EnterFear();
                    break;
                case EmotionState.ANGER:
                    EnterAnger();
                    break;
                case EmotionState.TIRED:
                    EnterTired();
                    break;
                case EmotionState.AWAKENING:
                    EnterAwakening();
                    break;
            }
        }

        private void ExitState(EmotionState state)
        {
            switch (state)
            {
                case EmotionState.FEAR:
                    ExitFear();
                    break;
                case EmotionState.ANGER:
                    ExitAnger();
                    break;
                case EmotionState.TIRED:
                    ExitTired();
                    break;
                case EmotionState.AWAKENING:
                    ExitAwakening();
                    break;
            }
        }

        #endregion

        #region ─── CURIOUS (好奇 - 默认) ──────────────────────

        private void EnterCurious()
        {
            _controller.EmotionSpeedMultiplier = 0.8f; // 移速=天使移速×80%
            _controller.ResumeMoving();
            _isCuriousIdle = false;

            Debug.Log("[EmotionStateMachine] Enter CURIOUS");
        }

        private void ExecuteCuriousBehavior()
        {
            // 决策：忠诚度% → 跟随天使；好奇度% → 随机探索
            float loyaltyChance = _attributes.BabyLoyalty / 100f;
            float curiosityChance = _attributes.BabyCuriosity / 100f;

            float roll = UnityEngine.Random.value;
            float totalWeight = loyaltyChance + curiosityChance;

            if (totalWeight <= 0f)
            {
                // 默认跟随
                FollowAngel();
                return;
            }

            float normalizedLoyalty = loyaltyChance / totalWeight;

            if (roll < normalizedLoyalty)
            {
                // 跟随天使
                FollowAngel();
            }
            else
            {
                // 随机探索
                if (_isCuriousIdle)
                {
                    // 空闲中
                    _curiousIdleTimer -= Time.deltaTime;
                    if (_curiousIdleTimer <= 0f)
                    {
                        _isCuriousIdle = false;
                        StartWander();
                    }
                }
                else
                {
                    // 10%概率原地停留
                    if (UnityEngine.Random.value < _curiousIdleChance)
                    {
                        _isCuriousIdle = true;
                        _curiousIdleTimer = _curiousIdleDuration;
                        _controller.StopMoving();
                    }
                    else
                    {
                        StartWander();
                    }
                }
            }

            // 距离约束：>400px强制向天使移动
            if (_controller.DistanceToAngel > _curiousForceReturnDist)
            {
                FollowAngel();
                _isCuriousIdle = false;
            }
        }

        private void FollowAngel()
        {
            Transform angel = FindAngel();
            if (angel != null)
            {
                _controller.SetMoveTarget(angel.position);
            }
        }

        private void StartWander()
        {
            Transform angel = FindAngel();
            if (angel == null)
            {
                _controller.SetMoveTarget(_controller.GetPosition() + _controller.GetWanderDirection() * _curiousWanderMax);
                return;
            }

            // 随机探索范围：100-300px，以天使为中心
            float wanderDist = UnityEngine.Random.Range(_curiousWanderMin, _curiousWanderMax);
            Vector2 wanderDir = _controller.GetWanderDirection();

            // 探索方向随机（每1-3秒更换，由BabyController管理）
            Vector2 target = (Vector2)angel.position + wanderDir * wanderDist;
            _controller.SetMoveTarget(target);
        }

        #endregion

        #region ─── FEAR (恐惧) ────────────────────────────────

        private void EnterFear()
        {
            // 移速=天使移速×(1+恐惧移速加成)=天使移速×1.4
            float fearSpeedMult = 1f + _attributes.FearSpeedBonus;
            _controller.EmotionSpeedMultiplier = fearSpeedMult;

            Debug.Log($"[EmotionStateMachine] Enter FEAR (speed ×{fearSpeedMult})");
        }

        private void ExitFear()
        {
            // 退出后10秒冷却
            _fearCooldownTimer = _fearExitCooldown;
            Debug.Log("[EmotionStateMachine] Exit FEAR (10s cooldown)");
        }

        private void ExecuteFearBehavior()
        {
            // 移动方向：逃离最近敌人50% + 趋向天使50%
            Transform angel = FindAngel();
            GameObject nearestEnemy = FindNearestEnemy(_fearDetectionRadius * 2f);

            Vector2 fleeDir = Vector2.zero;
            Vector2 toAngelDir = Vector2.zero;

            if (nearestEnemy != null)
            {
                fleeDir = ((Vector2)transform.position - (Vector2)nearestEnemy.transform.position).normalized;
            }

            if (angel != null)
            {
                toAngelDir = ((Vector2)angel.position - (Vector2)transform.position).normalized;
            }

            // 50%逃离 + 50%趋向
            Vector2 moveDir = (fleeDir * 0.5f + toAngelDir * 0.5f).normalized;
            _controller.SetMoveTarget((Vector2)transform.position + moveDir * 200f);

            // 恐惧路径对敌人减速30%（通过物理材质或标签标记）
            // 在敌人AI中检测Baby的FEAR状态并减速
        }

        #endregion

        #region ─── ANGER (愤怒) ───────────────────────────────

        private void EnterAnger()
        {
            _isAngerActive = true;
            _angerTimer = _angerDuration;

            // 无敌1秒
            StartCoroutine(AngerInvincibilityRoutine());

            // 释放冲击波
            float shockwaveDmg = _attributes.AngerShockwaveDmg * Mathf.Max(_attributes.BabyAttackPower, 1f);
            ReleaseShockwave(shockwaveDmg);

            Debug.Log($"[EmotionStateMachine] Enter ANGER (shockwave: {shockwaveDmg} dmg, {_angerDuration}s)");
        }

        private void ExitAnger()
        {
            _isAngerActive = false;
            _angerCooldownTimer = _angerCooldown;
            _controller.EmotionSpeedMultiplier = 1.0f;

            Debug.Log("[EmotionStateMachine] Exit ANGER (30s cooldown)");
        }

        private IEnumerator AngerInvincibilityRoutine()
        {
            // 标记无敌
            var col = GetComponent<Collider2D>();
            // 可以通过tag/layer实现无敌

            yield return new WaitForSeconds(_attributes.AngerInvincibleDur);

            // 无敌结束
        }

        private void ExecuteAngerBehavior()
        {
            _angerTimer -= Time.deltaTime;
            if (_angerTimer <= 0f)
            {
                // 愤怒结束，回到评估
                return;
            }

            // 暴走：主动冲向最近敌人攻击
            GameObject nearest = FindNearestEnemy(50f); // 大范围搜索
            if (nearest != null)
            {
                // 攻击力×2，攻击范围+50%
                _controller.SetMoveTarget(nearest.transform.position);

                // 尝试攻击
                float attackRange = 3f * (1f + _angerRangeBonus);
                float dist = Vector2.Distance(transform.position, nearest.transform.position);

                if (dist <= attackRange)
                {
                    // 对敌人造成伤害
                    float damage = _attributes.AngerShockwaveDmg * _angerAttackMultiplier * Mathf.Max(_attributes.BabyAttackPower, 1f);
                    var damageable = nearest.GetComponent<Player.IDamageable>();
                    damageable?.TakeDamage(damage, false, gameObject);

                    Debug.DrawLine(transform.position, nearest.transform.position, Color.red, 0.1f);
                }
            }
            else
            {
                // 无敌人时随机移动
                _controller.SetMoveTarget(_controller.GetPosition() + _controller.GetWanderDirection() * 100f);
            }
        }

        private void ReleaseShockwave(float damage)
        {
            Vector2 origin = transform.position;

            // 对范围内敌人造成伤害
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, _angerShockwaveRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    var damageable = hit.GetComponent<Player.IDamageable>();
                    damageable?.TakeDamage(damage, false, gameObject);
                }
            }

            // 视觉反馈
            OnAngerShockwave?.Invoke(origin, _angerShockwaveRadius, damage);

            Debug.Log($"[EmotionStateMachine] Shockwave released! Radius: {_angerShockwaveRadius}, Damage: {damage}");
        }

        #endregion

        #region ─── TIRED (疲惫) ──────────────────────────────

        private void EnterTired()
        {
            // 原地不动（移速=0）
            _controller.StopMoving();

            // 恢复速度×2（有G02卡牌则×3）
            float regenMult = _tiredRegenMultiplier;
            // TODO: 检测G02卡牌 → regenMult = 3f

            _attributes.ModifyStat("babyRegenPerSec", _attributes.BabyRegenPerSec * (regenMult - 1f));

            Debug.Log($"[EmotionStateMachine] Enter TIRED (regen ×{regenMult})");
        }

        private void ExitTired()
        {
            // 恢复普通恢复速度
            _controller.ResumeMoving();

            Debug.Log("[EmotionStateMachine] Exit TIRED");
        }

        private void ExecuteTiredBehavior()
        {
            // 原地不动，等待精神力恢复
            // 直到精神力>30%退出（由EvaluateState自动处理）

            // 不执行任何移动
        }

        #endregion

        #region ─── AWAKENING (觉醒) ⭐终极状态 ────────────────

        private void EnterAwakening()
        {
            _isAwakeningActive = true;
            _awakeningTimer = _awakeningDuration;
            _awakeningKillCount = 0;

            // 消耗充能
            _attributes.ConsumeAwakenCharge();

            // 变身效果
            _controller.EmotionSpeedMultiplier = _awakeningSpeedMultiplier; // 移速×1.5

            // 无敌（觉醒期间）
            // 3倍伤害（由攻击逻辑读取此状态）
            // 全屏光照显形所有敌人（由光照系统读取此状态）

            OnAwakeningStarted?.Invoke();
            EventBus.Instance?.FireBabyEmotionChanged("AWAKENING_START");

            Debug.Log($"[EmotionStateMachine] ★ AWAKENING STARTED! Duration: {_awakeningDuration}s ★");
        }

        private void ExitAwakening()
        {
            _isAwakeningActive = false;
            _awakeningCooldownTimer = _awakeningCooldown;
            _controller.EmotionSpeedMultiplier = 1.0f;

            OnAwakeningEnded?.Invoke();
            EventBus.Instance?.FireBabyEmotionChanged("AWAKENING_END");

            Debug.Log($"[EmotionStateMachine] ★ AWAKENING ENDED ({_awakeningKillCount} kills) ★");
        }

        private void ExecuteAwakeningBehavior()
        {
            _awakeningTimer -= Time.deltaTime;
            if (_awakeningTimer <= 0f)
            {
                // 觉醒时间到
                return; // EvaluateState会切换
            }

            // 无视恐惧/疲惫

            // 主动猎杀最强敌人
            GameObject strongest = FindStrongestEnemy(100f);
            if (strongest != null)
            {
                _controller.SetMoveTarget(strongest.transform.position);

                float dist = Vector2.Distance(transform.position, strongest.transform.position);
                if (dist <= 5f)
                {
                    // 攻击
                    float damage = _attributes.BabyAttackPower * _awakeningDamageMultiplier * 50f;
                    var damageable = strongest.GetComponent<Player.IDamageable>();
                    damageable?.TakeDamage(damage, true, gameObject);
                }
            }
            else
            {
                // 无敌人，向天使移动
                Transform angel = FindAngel();
                if (angel != null)
                {
                    _controller.SetMoveTarget(angel.position);
                }
            }
        }

        /// <summary>
        /// 觉醒击杀回调 —— 每杀5个敌人延长觉醒1秒
        /// </summary>
        public void OnAwakeningKill()
        {
            if (!_isAwakeningActive) return;

            _awakeningKillCount++;

            if (_awakeningKillCount % 5 == 0)
            {
                _awakeningTimer += _awakeningKillExtension;
                Debug.Log($"[EmotionStateMachine] Awakening extended! +{_awakeningKillExtension}s (total kills: {_awakeningKillCount})");
            }
        }

        /// <summary>
        /// 更新觉醒时长（随等级变化：30-54秒）
        /// </summary>
        public void UpdateAwakeningDuration()
        {
            int level = GameManager.Instance?.CurrentLevel ?? 1;
            float t = Mathf.Clamp01((level - 1) / 24f); // 1-25级映射到0-1
            _awakeningDuration = Mathf.Lerp(_awakeningMinDuration, _awakeningMaxDuration, t);
        }

        #endregion

        #region ─── State Behavior Dispatcher ──────────────────

        /// <summary>
        /// 每帧执行当前状态的行为
        /// </summary>
        private void ExecuteStateBehavior()
        {
            switch (_currentState)
            {
                case EmotionState.CURIOUS:
                    ExecuteCuriousBehavior();
                    break;
                case EmotionState.FEAR:
                    ExecuteFearBehavior();
                    break;
                case EmotionState.ANGER:
                    ExecuteAngerBehavior();
                    break;
                case EmotionState.TIRED:
                    ExecuteTiredBehavior();
                    break;
                case EmotionState.AWAKENING:
                    ExecuteAwakeningBehavior();
                    break;
            }
        }

        #endregion

        #region ─── Cooldown Management ───────────────────────

        private void UpdateCooldowns()
        {
            float dt = Time.deltaTime;

            if (_fearCooldownTimer > 0f) _fearCooldownTimer -= dt;
            if (_angerCooldownTimer > 0f) _angerCooldownTimer -= dt;
            if (_awakeningCooldownTimer > 0f) _awakeningCooldownTimer -= dt;
        }

        #endregion

        #region ─── Hit Tracking (for ANGER) ──────────────────

        /// <summary>
        /// 记录受伤（由BabyAI调用）
        /// </summary>
        public void RecordHit(float damage)
        {
            _recentHits.Add(new HitRecord { Time = Time.time, Damage = damage });

            // 清理3秒外的记录
            _recentHits.RemoveAll(h => Time.time - h.Time > _angerHitWindow);
        }

        /// <summary>
        /// 获取3秒内受伤次数
        /// </summary>
        public int GetRecentHitCount()
        {
            _recentHits.RemoveAll(h => Time.time - h.Time > _angerHitWindow);
            return _recentHits.Count;
        }

        #endregion

        #region ─── Utility ───────────────────────────────────

        private Transform FindAngel()
        {
            var angelObj = GameObject.FindGameObjectWithTag("Angel");
            if (angelObj == null) angelObj = GameObject.FindGameObjectWithTag("Player");
            if (angelObj == null) angelObj = GameObject.Find("Angel");
            return angelObj != null ? angelObj.transform : null;
        }

        private int CountNearbyEnemies(float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            int count = 0;
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                    count++;
            }
            return count;
        }

        private GameObject FindNearestEnemy(float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            GameObject nearest = null;
            float minDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    float dist = Vector2.Distance(transform.position, hit.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = hit.gameObject;
                    }
                }
            }
            return nearest;
        }

        private GameObject FindStrongestEnemy(float radius)
        {
            // 寻找范围内"最强"敌人（优先血量最高/精英/Boss）
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            GameObject strongest = null;
            float maxPriority = float.MinValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                float priority = 1f;
                // Boss优先
                if (hit.CompareTag("Boss")) priority = 100f;
                // 精英优先
                else if (hit.name.Contains("Elite")) priority = 50f;

                // 距离衰减
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                priority -= dist * 0.1f;

                if (priority > maxPriority)
                {
                    maxPriority = priority;
                    strongest = hit.gameObject;
                }
            }

            return strongest;
        }

        #endregion

        #region ─── Inner Types ───────────────────────────────

        [Serializable]
        private struct HitRecord
        {
            public float Time;
            public float Damage;
        }

        #endregion

        #region ─── Gizmos ────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 状态颜色
            switch (_currentState)
            {
                case EmotionState.CURIOUS:   Gizmos.color = Color.green;  break;
                case EmotionState.FEAR:      Gizmos.color = Color.yellow; break;
                case EmotionState.ANGER:     Gizmos.color = Color.red;    break;
                case EmotionState.TIRED:     Gizmos.color = Color.gray;   break;
                case EmotionState.AWAKENING: Gizmos.color = Color.magenta; break;
            }

            Gizmos.DrawWireSphere(transform.position, 1.5f);

            // 恐惧检测范围
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _fearDetectionRadius);

            // 愤怒冲击波范围
            if (_isAngerActive)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, _angerShockwaveRadius);
            }

            // 觉醒光环
            if (_isAwakeningActive)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.4f);
                Gizmos.DrawWireSphere(transform.position, 3f);
            }
        }

        #endregion
    }
}
