using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 威胁目标类型
    /// </summary>
    public enum ThreatTarget
    {
        Angel,      // 天使
        Baby,       // 婴儿
        Terrain,    // 地形（墙壁/门）
        Any         // 任意目标
    }

    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType
    {
        Physical,   // 物理
        Magic,      // 魔法
        Fire,       // 火焰
        Ice,        // 冰霜
        Lightning,  // 闪电
        Poison,     // 毒素
        Holy,       // 神圣
        True        // 真实伤害
    }

    /// <summary>
    /// 敌人状态枚举
    /// </summary>
    public enum EnemyState
    {
        Idle,       // 待机
        Chasing,    // 追逐
        Attacking,  // 攻击
        Dead        // 死亡
    }

    /// <summary>
    /// 敌人基类 - 所有敌人类型的核心基础
    /// 提供属性管理、状态机、伤害系统、死亡系统、移动系统、索敌系统和对象池支持
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class EnemyBase : MonoBehaviour
    {
        #region 基础属性

        [Header("=== 基础信息 ===")]
        [SerializeField] protected string enemyId;
        [SerializeField] protected string enemyName;
        [SerializeField] protected string type;
        [SerializeField] protected ThreatTarget threatTarget = ThreatTarget.Any;
        [SerializeField] protected int enemyLevel = 1;

        public string EnemyId => enemyId;
        public string EnemyName => enemyName;
        public string Type => type;
        public ThreatTarget ThreatTargetType => threatTarget;
        public int EnemyLevel => enemyLevel;

        #endregion

        #region 战斗属性

        [Header("=== 战斗属性 ===")]
        [SerializeField] protected float maxHP = 100f;
        [SerializeField] protected float attackPower = 10f;
        [SerializeField] protected float defense = 0f;
        [SerializeField] protected float magicResist = 0f;
        [SerializeField] protected float moveSpeed = 3f;
        [SerializeField] protected float attackRange = 1.5f;
        [SerializeField] protected float detectRange = 8f;
        [SerializeField] protected float attackInterval = 1.5f;

        protected float currentHP;
        protected float attackTimer;

        public float MaxHP => maxHP;
        public float CurrentHP => currentHP;
        public float AttackPower => attackPower;
        public float Defense => defense;
        public float MagicResist => magicResist;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public float DetectRange => detectRange;
        public float AttackInterval => attackInterval;
        public float HPPercentage => maxHP > 0f ? currentHP / maxHP : 0f;

        #endregion

        #region 状态管理

        [Header("=== 状态 ===")]
        [SerializeField] protected EnemyState currentState = EnemyState.Idle;
        protected EnemyState previousState = EnemyState.Idle;

        public EnemyState CurrentState => currentState;

        #endregion

        #region 组件引用

        [Header("=== 组件引用 ===")]
        [SerializeField] protected Rigidbody2D rb;
        [SerializeField] protected Collider2D enemyCollider;
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;
        [SerializeField] protected Transform healthBarRoot;

        protected Transform currentTarget;
        protected Vector2 moveDirection;
        protected bool isFacingRight = true;

        #endregion

        #region 事件

        // === 事件 ===
        /// <summary>敌人被击杀事件 (enemyId, enemyName, killPosition)</summary>
        public static event Action<string, string, Vector3> OnEnemyKilled;

        /// <summary>触发敌人被击杀事件（供外部类调用）</summary>
        public static void TriggerEnemyKilled(string enemyId, string enemyName, Vector3 position)
        {
            EnemyBase.TriggerEnemyKilled(enemyId, enemyName, position);
        }

        /// <summary>敌人受到伤害事件 (enemyId, damage, currentHP, maxHP)</summary>
        public event Action<string, float, float, float> OnEnemyDamaged;

        /// <summary>敌人状态变更事件 (enemyId, oldState, newState)</summary>
        public event Action<string, EnemyState, EnemyState> OnStateChanged;

        /// <summary>敌人进入检测范围事件</summary>
        public event Action<string> OnTargetDetected;

        /// <summary>敌人丢失目标事件</summary>
        public event Action<string> OnTargetLost;

        #endregion

        #region 对象池

        [Header("=== 对象池 ===")]
        [SerializeField] protected bool useObjectPool = true;
        protected bool isPooled = false;

        public bool IsPooled => isPooled;

        #endregion

        #region 掉落配置

        [Header("=== 掉落 ===")]
        [SerializeField] protected int experienceDrop = 10;
        [SerializeField] protected int goldDrop = 5;
        [SerializeField] protected float dropChance = 1f;
        [SerializeField] protected GameObject[] specialDropPrefabs;

        #endregion

        #region 难度倍率（由Spawner设置）

        protected float difficultyHPModifier = 1f;
        protected float difficultyAttackModifier = 1f;
        protected float difficultySpeedModifier = 1f;

        public float DifficultyHPModifier
        {
            get => difficultyHPModifier;
            set
            {
                difficultyHPModifier = value;
                maxHP *= difficultyHPModifier;
                currentHP = maxHP;
            }
        }

        public float DifficultyAttackModifier
        {
            get => difficultyAttackModifier;
            set
            {
                difficultyAttackModifier = value;
                attackPower *= difficultyAttackModifier;
            }
        }

        public float DifficultySpeedModifier
        {
            get => difficultySpeedModifier;
            set
            {
                difficultySpeedModifier = value;
                moveSpeed *= difficultySpeedModifier;
            }
        }

        #endregion

        #region Unity生命周期

        protected virtual void Awake()
        {
            // 自动获取组件引用
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (enemyCollider == null) enemyCollider = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            // 配置Rigidbody2D
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            currentHP = maxHP;
        }

        protected virtual void Start()
        {
            attackTimer = 0f;
        }

        protected virtual void Update()
        {
            // 死亡状态不做任何处理
            if (currentState == EnemyState.Dead) return;

            // 更新攻击计时器
            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
            }

            // 执行AI逻辑
            UpdateAI();

            // 更新动画
            // UpdateAnimator(); // TODO: implement
        }

        protected virtual void FixedUpdate()
        {
            if (currentState == EnemyState.Dead) return;

            // 执行移动
            UpdateMovement();
        }

        #endregion

        #region AI系统（子类重写）

        /// <summary>
        /// AI更新逻辑 - 每个敌人类型必须重写
        /// </summary>
        protected abstract void UpdateAI();

        #endregion

        #region 移动系统

        /// <summary>
        /// 物理移动更新
        /// </summary>
        protected virtual void UpdateMovement()
        {
            if (rb == null || currentState == EnemyState.Dead) return;

            if (currentState == EnemyState.Chasing && currentTarget != null)
            {
                Vector2 direction = ((Vector2)currentTarget.position - rb.position).normalized;
                moveDirection = direction;

                float speed = moveSpeed * difficultySpeedModifier;
                rb.velocity = direction * speed;

                UpdateFacing(direction.x);
            }
            else if (currentState == EnemyState.Idle)
            {
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.fixedDeltaTime * 5f);
            }
            else if (currentState == EnemyState.Attacking)
            {
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.fixedDeltaTime * 10f);
            }
        }

        /// <summary>
        /// 向指定方向移动
        /// </summary>
        protected virtual void MoveTowards(Vector2 targetPosition, float speedMultiplier = 1f)
        {
            if (rb == null) return;

            Vector2 direction = (targetPosition - rb.position).normalized;
            moveDirection = direction;
            rb.velocity = direction * moveSpeed * difficultySpeedModifier * speedMultiplier;
            UpdateFacing(direction.x);
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        protected virtual void StopMoving()
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            moveDirection = Vector2.zero;
        }

        /// <summary>
        /// 朝向更新
        /// </summary>
        protected virtual void UpdateFacing(float directionX)
        {
            if (Mathf.Abs(directionX) < 0.01f) return;

            bool shouldFaceRight = directionX > 0;
            if (shouldFaceRight != isFacingRight)
            {
                isFacingRight = shouldFaceRight;
                Vector3 scale = transform.localScale;
                scale.x = isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }

        #endregion

        #region 索敌系统

        /// <summary>
        /// 检测范围内有效目标
        /// 根据threatTarget优先级选择目标
        /// </summary>
        protected virtual Transform FindTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange);
            Transform bestTarget = null;
            float bestPriority = float.MaxValue;

            foreach (var hit in hits)
            {
                Transform candidate = hit.transform;
                float priority = CalculateThreatPriority(candidate);

                if (priority >= 0 && priority < bestPriority)
                {
                    // 验证目标是否存活
                    if (IsTargetAlive(candidate))
                    {
                        bestPriority = priority;
                        bestTarget = candidate;
                    }
                }
            }

            if (bestTarget != null && currentTarget == null)
            {
                OnTargetDetected?.Invoke(enemyId);
            }
            else if (bestTarget == null && currentTarget != null)
            {
                OnTargetLost?.Invoke(enemyId);
            }

            return bestTarget;
        }

        /// <summary>
        /// 计算目标威胁优先级（数值越小优先级越高）
        /// </summary>
        protected virtual float CalculateThreatPriority(Transform candidate)
        {
            float basePriority = Vector2.Distance(transform.position, candidate.position);

            // 根据threatTarget调整优先级
            string tag = candidate.tag;

            switch (threatTarget)
            {
                case ThreatTarget.Angel:
                    if (tag == "Angel") return basePriority;
                    if (tag == "Baby") return basePriority + 100f;
                    if (tag == "Terrain") return basePriority + 200f;
                    return -1f; // 非目标

                case ThreatTarget.Baby:
                    if (tag == "Baby") return basePriority;
                    if (tag == "Angel") return basePriority + 100f;
                    if (tag == "Terrain") return basePriority + 200f;
                    return -1f;

                case ThreatTarget.Terrain:
                    if (tag == "Terrain" || tag == "Wall" || tag == "Door") return basePriority * 0.5f; // 仇恨×2（距离权重减半）
                    if (tag == "Angel" || tag == "Baby") return basePriority + 100f;
                    return -1f;

                case ThreatTarget.Any:
                default:
                    if (tag == "Angel" || tag == "Baby" || tag == "Terrain" || tag == "Wall" || tag == "Door")
                        return basePriority;
                    return -1f;
            }
        }

        /// <summary>
        /// 检查目标是否存活
        /// </summary>
        protected virtual bool IsTargetAlive(Transform target)
        {
            if (target == null) return false;

            // 尝试获取生命值组件
            var healthComponent = target.GetComponentInParent<IDamageable>();
            if (healthComponent != null)
            {
                return !healthComponent.IsDead;
            }

            // 对于地形目标，始终认为存活
            if (target.CompareTag("Terrain") || target.CompareTag("Wall") || target.CompareTag("Door"))
                return true;

            // 默认认为存活
            return target.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 目标是否在攻击范围内
        /// </summary>
        protected virtual bool IsTargetInAttackRange()
        {
            if (currentTarget == null) return false;
            return Vector2.Distance(transform.position, currentTarget.position) <= attackRange;
        }

        /// <summary>
        /// 目标是否在检测范围内
        /// </summary>
        protected virtual bool IsTargetInDetectRange()
        {
            if (currentTarget == null) return false;
            return Vector2.Distance(transform.position, currentTarget.position) <= detectRange;
        }

        #endregion

        #region 攻击系统

        /// <summary>
        /// 攻击 - 虚拟方法，子类可重写
        /// </summary>
        public virtual void Attack()
        {
            if (attackTimer > 0f) return;
            if (currentTarget == null) return;

            attackTimer = attackInterval;

            // 对目标造成伤害
            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackPower * difficultyAttackModifier, DamageType.Physical);
            }

            // 播放攻击动画
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
        }

        /// <summary>
        /// 尝试攻击 - 检查条件后执行攻击
        /// </summary>
        protected virtual void TryAttack()
        {
            if (attackTimer > 0f) return;
            if (currentTarget == null) return;
            if (!IsTargetInAttackRange()) return;

            ChangeState(EnemyState.Attacking);
            Attack();
        }

        #endregion

        #region 受伤系统

        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="type">伤害类型</param>
        public virtual void TakeDamage(float damage, DamageType type)
        {
            if (currentState == EnemyState.Dead) return;

            float finalDamage = CalculateDamage(damage, type);
            currentHP -= finalDamage;
            currentHP = Mathf.Max(0f, currentHP);

            OnEnemyDamaged?.Invoke(enemyId, finalDamage, currentHP, maxHP);

            // 播放受击动画
            if (animator != null)
            {
                animator.SetTrigger("Hit");
            }

            // 受击闪烁效果
            StartCoroutine(HitFlashCoroutine());

            if (currentHP <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// 计算最终伤害（考虑防御和伤害类型）
        /// </summary>
        protected virtual float CalculateDamage(float damage, DamageType type)
        {
            float finalDamage = damage;

            switch (type)
            {
                case DamageType.Physical:
                    finalDamage = damage * (1f - defense / (defense + 100f));
                    break;
                case DamageType.Magic:
                case DamageType.Fire:
                case DamageType.Ice:
                case DamageType.Lightning:
                    finalDamage = damage * (1f - magicResist / (magicResist + 100f));
                    break;
                case DamageType.Poison:
                    finalDamage = damage; // 毒素无视部分防御
                    break;
                case DamageType.Holy:
                    finalDamage = damage * 1.5f; // 神圣伤害对敌人有加成
                    break;
                case DamageType.True:
                    finalDamage = damage; // 真实伤害无视所有防御
                    break;
            }

            return Mathf.Max(1f, finalDamage);
        }

        /// <summary>
        /// 受击闪烁协程
        /// </summary>
        protected virtual IEnumerator HitFlashCoroutine()
        {
            if (spriteRenderer == null) yield break;

            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;

            yield return new WaitForSeconds(0.1f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        #endregion

        #region 死亡系统

        /// <summary>
        /// 死亡处理
        /// </summary>
        public virtual void Die()
        {
            if (currentState == EnemyState.Dead) return;

            ChangeState(EnemyState.Dead);

            // 禁用碰撞
            if (enemyCollider != null)
            {
                enemyCollider.enabled = false;
            }

            // 停止移动
            StopMoving();

            // 播放死亡动画
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            // 掉落处理
            HandleDrops();

            // 发送击杀事件
            EnemyBase.TriggerEnemyKilled(enemyId, enemyName, transform.position);

            // 对象池回收或销毁
            if (useObjectPool)
            {
                StartCoroutine(DespawnAfterDelay(1.5f));
            }
            else
            {
                Destroy(gameObject, 1.5f);
            }
        }

        /// <summary>
        /// 处理掉落
        /// </summary>
        protected virtual void HandleDrops()
        {
            if (UnityEngine.Random.value > dropChance) return;

            // 掉落经验
            if (experienceDrop > 0)
            {
                DropExperience(experienceDrop);
            }

            // 掉落金币
            if (goldDrop > 0)
            {
                DropGold(goldDrop);
            }

            // 掉落特殊物品
            if (specialDropPrefabs != null && specialDropPrefabs.Length > 0)
            {
                foreach (var prefab in specialDropPrefabs)
                {
                    if (prefab != null && UnityEngine.Random.value <= 0.1f)
                    {
                        Instantiate(prefab, transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.5f), Quaternion.identity);
                    }
                }
            }
        }

        /// <summary>
        /// 掉落经验
        /// </summary>
        protected virtual void DropExperience(int amount)
        {
            // 通过事件或直接调用经验系统
            ExperienceManager.Instance?.AddExperience(amount, transform.position);
        }

        /// <summary>
        /// 掉落金币
        /// </summary>
        protected virtual void DropGold(int amount)
        {
            GoldManager.Instance?.AddGold(amount, transform.position);
        }

        /// <summary>
        /// 延迟回收协程
        /// </summary>
        protected virtual IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            OnDespawn();
        }

        #endregion

        #region 状态管理

        /// <summary>
        /// 切换状态
        /// </summary>
        protected virtual void ChangeState(EnemyState newState)
        {
            if (currentState == newState) return;

            previousState = currentState;
            currentState = newState;

            OnStateChanged?.Invoke(enemyId, previousState, newState);

            // 状态进入处理
            OnEnterState(newState);
        }

        /// <summary>
        /// 进入状态时的处理
        /// </summary>
        protected virtual void OnEnterState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                    StopMoving();
                    break;
                case EnemyState.Chasing:
                    break;
                case EnemyState.Attacking:
                    break;
                case EnemyState.Dead:
                    StopMoving();
                    break;
            }
        }

        #endregion

        #region 对象池支持

        /// <summary>
        /// 从对象池生成时调用
        /// </summary>
        public virtual void OnSpawn()
        {
            isPooled = true;
            currentHP = maxHP;
            attackTimer = 0f;
            currentTarget = null;
            moveDirection = Vector2.zero;

            if (enemyCollider != null) enemyCollider.enabled = true;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            ChangeState(EnemyState.Idle);
            gameObject.SetActive(true);

            // 重置动画
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        /// <summary>
        /// 回收到对象池时调用
        /// </summary>
        public virtual void OnDespawn()
        {
            StopMoving();
            currentTarget = null;
            ChangeState(EnemyState.Idle);

            if (enemyCollider != null) enemyCollider.enabled = true;
            currentHP = maxHP;

            gameObject.SetActive(false);
        }

        #endregion

        #region 编辑器辅助

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // 绘制检测范围
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, detectRange);

            // 绘制攻击范围
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 绘制当前目标连线
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, currentTarget.position);
            }

            // 显示状态
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f,
                $"{enemyName}\nState: {currentState}\nHP: {currentHP}/{maxHP}");
        }
#endif

        #endregion
    }
}
