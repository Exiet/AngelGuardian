using System;
using System.Collections;
using UnityEngine;

namespace AngelGuardian.Dungeon
{
    /// <summary>
    /// 门控制器 —— 管理门的完整生命周期
    /// 状态机: Open / Closed / Destroyed
    /// 支持自动开关、手动开关、Boss破坏、关门伤害、耐久度系统
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        #region Door State

        public enum DoorState
        {
            Open,       // 门打开 —— 可通过
            Closed,     // 门关闭 —— 阻挡通行
            Destroyed   // 门被摧毁 —— 永久可通过
        }

        public enum DoorOrientation
        {
            Horizontal, // 水平门(横跨走廊)
            Vertical    // 垂直门(竖跨走廊)
        }

        #endregion

        #region Configuration

        [Header("Door Properties")]
        [SerializeField]
        private DoorState initialState = DoorState.Closed;

        [SerializeField]
        private DoorOrientation orientation = DoorOrientation.Horizontal;

        [SerializeField, Range(100, 1000)]
        private float maxDurability = 500f;

        [Header("Animation")]
        [SerializeField, Range(0.2f, 1.5f)]
        private float openCloseDuration = 0.5f;      // 开关动画时长(秒)

        [SerializeField]
        private AnimationCurve openCloseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Damage")]
        [SerializeField, Range(0.1f, 0.5f)]
        private float closeDamageMultiplier = 0.3f;  // 关门伤害 = 攻击力 × 30%

        [SerializeField, Range(10, 200)]
        private float baseCloseDamage = 25f;          // 基础关门伤害

        [Header("Auto Detection")]
        [SerializeField, Range(1f, 10f)]
        private float autoOpenRange = 4f;             // 自动开门检测范围

        [SerializeField, Range(0.1f, 1f)]
        private float detectionInterval = 0.2f;       // 检测间隔(秒)

        [Header("Boss Interaction")]
        [SerializeField]
        private bool canBeDestroyedByBoss = true;

        [SerializeField, Range(50, 500)]
        private float bossDestroyDamage = 200f;       // Boss单次破坏伤害

        #endregion

        #region Runtime State

        private DoorState currentState;
        private float currentDurability;
        private bool isAnimating;
        private Coroutine autoDetectionCoroutine;
        private Coroutine animationCoroutine;

        // 门视觉组件引用
        private Transform doorVisual;
        private Vector3 closedLocalPosition;
        private Vector3 openLocalPosition;
        private Collider2D doorCollider;
        private SpriteRenderer doorRenderer;

        // 动画进度
        private float animationProgress; // 0 = 完全关闭, 1 = 完全打开

        #endregion

        #region Events

        public event Action<DoorState, DoorState> OnStateChanged;     // (oldState, newState)
        public event Action<float> OnDurabilityChanged;               // (currentDurability)
        public event Action<float> OnDamageDealt;                     // (damageAmount)
        public event Action OnDestroyedByBoss;
        public event Action OnAnimationComplete;

        #endregion

        #region Properties

        public DoorState CurrentState => currentState;
        public float CurrentDurability => currentDurability;
        public float DurabilityPercent => currentDurability / maxDurability;
        public bool IsAnimating => isAnimating;
        public bool IsPassable => currentState == DoorState.Open || currentState == DoorState.Destroyed;
        public DoorOrientation Orientation => orientation;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取视觉组件
            doorVisual = transform.Find("Visual");
            if (doorVisual == null)
                doorVisual = transform;

            doorCollider = GetComponent<Collider2D>();
            doorRenderer = GetComponent<SpriteRenderer>();

            // 保存位置
            closedLocalPosition = doorVisual != null ? doorVisual.localPosition : transform.localPosition;

            // 计算打开位置(水平门向上移动，垂直门向左移动)
            float openOffset = orientation == DoorOrientation.Horizontal ? 3f : -3f;
            openLocalPosition = closedLocalPosition + new Vector3(
                orientation == DoorOrientation.Vertical ? openOffset : 0,
                orientation == DoorOrientation.Horizontal ? openOffset : 0,
                0
            );
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (autoDetectionCoroutine != null)
                StopCoroutine(autoDetectionCoroutine);
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化门状态
        /// </summary>
        public void Initialize()
        {
            currentDurability = maxDurability;
            animationProgress = initialState == DoorState.Open ? 1f : 0f;
            currentState = initialState;

            UpdateVisualImmediate();

            // 启动自动检测
            if (autoDetectionCoroutine != null)
                StopCoroutine(autoDetectionCoroutine);
            autoDetectionCoroutine = StartCoroutine(AutoDetectionRoutine());

            // 更新碰撞器
            UpdateCollider();
        }

        /// <summary>
        /// 从数据初始化门
        /// </summary>
        public void InitializeFromData(Vector2Int position, DoorOrientation orient, float durability = -1)
        {
            transform.position = new Vector3(position.x, position.y, 0);
            orientation = orient;
            if (durability > 0) maxDurability = durability;
            Initialize();
        }

        #endregion

        #region State Transitions

        /// <summary>
        /// 打开门
        /// </summary>
        public void Open(bool instant = false)
        {
            if (currentState == DoorState.Destroyed) return;
            if (currentState == DoorState.Open) return;
            if (isAnimating && !instant) return;

            DoorState oldState = currentState;
            currentState = DoorState.Open;

            if (instant)
            {
                animationProgress = 1f;
                UpdateVisualImmediate();
                UpdateCollider();
                OnStateChanged?.Invoke(oldState, currentState);
            }
            else
            {
                if (animationCoroutine != null)
                    StopCoroutine(animationCoroutine);
                animationCoroutine = StartCoroutine(AnimateDoor(animationProgress, 1f, oldState));
            }
        }

        /// <summary>
        /// 关闭门
        /// </summary>
        public void Close(bool instant = false)
        {
            if (currentState == DoorState.Destroyed) return;
            if (currentState == DoorState.Closed) return;
            if (isAnimating && !instant) return;

            DoorState oldState = currentState;
            currentState = DoorState.Closed;

            if (instant)
            {
                animationProgress = 0f;
                UpdateVisualImmediate();
                UpdateCollider();
                OnStateChanged?.Invoke(oldState, currentState);
            }
            else
            {
                if (animationCoroutine != null)
                    StopCoroutine(animationCoroutine);
                animationCoroutine = StartCoroutine(AnimateDoor(animationProgress, 0f, oldState));
            }
        }

        /// <summary>
        /// 切换门状态
        /// </summary>
        public void Toggle(bool instant = false)
        {
            if (currentState == DoorState.Open)
                Close(instant);
            else if (currentState == DoorState.Closed)
                Open(instant);
        }

        /// <summary>
        /// 摧毁门(永久)
        /// </summary>
        public void DestroyDoor(bool byBoss = false)
        {
            if (currentState == DoorState.Destroyed) return;

            DoorState oldState = currentState;
            currentState = DoorState.Destroyed;
            currentDurability = 0;

            // 立即更新
            UpdateCollider();
            UpdateDestroyedVisual();

            OnStateChanged?.Invoke(oldState, currentState);
            OnDurabilityChanged?.Invoke(0);

            if (byBoss)
                OnDestroyedByBoss?.Invoke();

            // 停止自动检测
            if (autoDetectionCoroutine != null)
            {
                StopCoroutine(autoDetectionCoroutine);
                autoDetectionCoroutine = null;
            }
        }

        #endregion

        #region Damage & Durability

        /// <summary>
        /// 对门造成伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="isBoss">是否来自Boss</param>
        /// <returns>是否摧毁了门</returns>
        public bool TakeDamage(float damage, bool isBoss = false)
        {
            if (currentState == DoorState.Destroyed) return false;

            // Boss特殊处理
            if (isBoss && canBeDestroyedByBoss)
            {
                damage = Mathf.Max(damage, bossDestroyDamage);
            }

            currentDurability -= damage;
            OnDurabilityChanged?.Invoke(currentDurability);

            if (currentDurability <= 0)
            {
                currentDurability = 0;
                DestroyDoor(isBoss);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 修复门
        /// </summary>
        public void Repair(float amount)
        {
            if (currentState == DoorState.Destroyed)
            {
                // 从摧毁状态修复
                currentState = DoorState.Closed;
                animationProgress = 0f;
                UpdateVisualImmediate();

                // 重新启动自动检测
                if (autoDetectionCoroutine != null)
                    StopCoroutine(autoDetectionCoroutine);
                autoDetectionCoroutine = StartCoroutine(AutoDetectionRoutine());
            }

            currentDurability = Mathf.Min(currentDurability + amount, maxDurability);
            OnDurabilityChanged?.Invoke(currentDurability);
        }

        /// <summary>
        /// 关门伤害 —— 对门附近的单位造成伤害
        /// </summary>
        /// <param name="attackerAttackPower">攻击者的攻击力</param>
        /// <returns>实际造成的伤害</returns>
        public float DealCloseDamage(float attackerAttackPower = 0)
        {
            float damage = baseCloseDamage + attackerAttackPower * closeDamageMultiplier;
            OnDamageDealt?.Invoke(damage);

            // 对门附近的所有单位造成伤害(由外部系统处理碰撞检测)
            return damage;
        }

        #endregion

        #region Auto Detection

        /// <summary>
        /// 自动检测天使/婴儿靠近，自动开关门
        /// </summary>
        private IEnumerator AutoDetectionRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(detectionInterval);

            while (currentState != DoorState.Destroyed)
            {
                yield return wait;

                if (isAnimating) continue;

                bool shouldOpen = DetectNearbyAlly();

                if (shouldOpen && currentState == DoorState.Closed)
                {
                    Open();
                }
                else if (!shouldOpen && currentState == DoorState.Open)
                {
                    // 延迟关门 —— 角色离开后再关
                    yield return new WaitForSeconds(1.5f);
                    if (!DetectNearbyAlly() && currentState == DoorState.Open)
                    {
                        Close();
                    }
                }
            }
        }

        /// <summary>
        /// 检测附近是否有友方单位
        /// </summary>
        private bool DetectNearbyAlly()
        {
            // 检测范围内的天使和婴儿
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                autoOpenRange
            );

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Angel") || hit.CompareTag("Baby") || hit.CompareTag("Player"))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Animation

        /// <summary>
        /// 门开关动画协程
        /// </summary>
        private IEnumerator AnimateDoor(float fromProgress, float toProgress, DoorState oldState)
        {
            isAnimating = true;
            float elapsed = 0f;
            float duration = openCloseDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = openCloseCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
                animationProgress = Mathf.Lerp(fromProgress, toProgress, t);

                UpdateVisualAnimated();
                yield return null;
            }

            animationProgress = toProgress;
            UpdateVisualAnimated();
            UpdateCollider();

            isAnimating = false;
            OnAnimationComplete?.Invoke();
            OnStateChanged?.Invoke(oldState, currentState);
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// 立即更新门视觉(无动画)
        /// </summary>
        private void UpdateVisualImmediate()
        {
            if (currentState == DoorState.Destroyed)
            {
                UpdateDestroyedVisual();
                return;
            }

            if (doorVisual != null)
            {
                doorVisual.localPosition = Vector3.Lerp(closedLocalPosition, openLocalPosition, animationProgress);
            }

            if (doorRenderer != null)
            {
                Color c = doorRenderer.color;
                c.a = currentState == DoorState.Open ? 0.3f : 1f;
                doorRenderer.color = c;
            }
        }

        /// <summary>
        /// 动画更新门视觉
        /// </summary>
        private void UpdateVisualAnimated()
        {
            if (doorVisual != null)
            {
                doorVisual.localPosition = Vector3.Lerp(closedLocalPosition, openLocalPosition, animationProgress);
            }

            if (doorRenderer != null)
            {
                Color c = doorRenderer.color;
                c.a = Mathf.Lerp(1f, 0.3f, animationProgress);
                doorRenderer.color = c;
            }
        }

        /// <summary>
        /// 更新摧毁状态视觉
        /// </summary>
        private void UpdateDestroyedVisual()
        {
            if (doorRenderer != null)
            {
                // 变暗、半透明
                Color c = doorRenderer.color;
                c.a = 0.1f;
                doorRenderer.color = c;

                // 可选：播放碎裂动画
            }

            if (doorVisual != null)
            {
                // 略微位移表示被摧毁
                doorVisual.localPosition = closedLocalPosition + new Vector3(
                    UnityEngine.Random.Range(-0.3f, 0.3f),
                    UnityEngine.Random.Range(-0.3f, 0.3f),
                    0
                );
            }
        }

        #endregion

        #region Collider Management

        /// <summary>
        /// 根据状态更新碰撞器
        /// </summary>
        private void UpdateCollider()
        {
            if (doorCollider != null)
            {
                doorCollider.enabled = !IsPassable;
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// 手动触发(玩家点击)
        /// </summary>
        public void OnPlayerInteract()
        {
            if (currentState == DoorState.Destroyed) return;
            if (isAnimating) return;

            Toggle();
        }

        /// <summary>
        /// 获取关门伤害值
        /// </summary>
        public float GetCloseDamage(float attackerPower = 0)
        {
            return baseCloseDamage + attackerPower * closeDamageMultiplier;
        }

        /// <summary>
        /// 设置门的耐久度
        /// </summary>
        public void SetDurability(float value)
        {
            maxDurability = value;
            currentDurability = Mathf.Min(currentDurability, maxDurability);
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            // 自动检测范围
            Gizmos.color = new Color(0, 1, 0, 0.15f);
            Gizmos.DrawWireSphere(transform.position, autoOpenRange);

            // 门状态颜色
            switch (currentState)
            {
                case DoorState.Open:
                    Gizmos.color = Color.green;
                    break;
                case DoorState.Closed:
                    Gizmos.color = Color.red;
                    break;
                case DoorState.Destroyed:
                    Gizmos.color = Color.gray;
                    break;
            }
            Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 3f, 0));
        }

        #endregion
    }
}
