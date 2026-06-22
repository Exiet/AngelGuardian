using System;
using System.Collections.Generic;
using UnityEngine;
using AngelGuardian.Core;

namespace AngelGuardian.Baby
{
    /// <summary>
    /// 婴儿移动控制器 —— 使用Rigidbody2D
    /// 移动速度由当前情感状态和属性共同决定
    /// 简单A*避开障碍物，距离约束强制向天使移动
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BabyAttributes))]
    public class BabyController : MonoBehaviour
    {
        #region ─── Inspector ────────────────────────────────

        [Header("Movement")]
        [SerializeField] private float _baseSpeed = 60f;
        [SerializeField] private float _maxDistanceFromAngel = 250f;
        [SerializeField] private float _forceReturnDistance = 400f;

        [Header("Pathfinding")]
        [SerializeField] private float _pathUpdateInterval = 0.5f;
        [SerializeField] private float _obstacleAvoidanceRadius = 2f;
        [SerializeField] private LayerMask _obstacleLayer;

        [Header("Wander")]
        [SerializeField] private float _wanderDirectionChangeInterval = 2f;
        [SerializeField] private float _wanderRadius = 300f;

        #endregion

        #region ─── Components ────────────────────────────────

        private Rigidbody2D _rb;
        private BabyAttributes _attributes;

        #endregion

        #region ─── State ─────────────────────────────────────

        // 移动目标
        private Vector2 _moveTarget;
        private Vector2 _currentWanderDirection;

        // 计时器
        private float _pathUpdateTimer;
        private float _wanderChangeTimer;

        // 被抱起状态
        private bool _isCarried;
        private Transform _carrierTransform;

        // 路径点列表（简化A*）
        private List<Vector2> _currentPath = new List<Vector2>();
        private int _currentPathIndex;

        // 情感状态的速度倍率
        private float _emotionSpeedMultiplier = 1.0f;

        #endregion

        #region ─── Properties ────────────────────────────────

        /// <summary>当前有效移动速度</summary>
        public float EffectiveSpeed
        {
            get
            {
                float baseSpd = _attributes != null ? _attributes.BabyMoveSpeed : _baseSpeed;
                return baseSpd * _emotionSpeedMultiplier;
            }
        }

        /// <summary>是否被抱起</summary>
        public bool IsCarried => _isCarried;

        /// <summary>当前移动目标</summary>
        public Vector2 MoveTarget
        {
            get => _moveTarget;
            set => _moveTarget = value;
        }

        /// <summary>到天使的距离</summary>
        public float DistanceToAngel
        {
            get
            {
                var angel = FindAngel();
                if (angel == null) return 0f;
                return Vector2.Distance(transform.position, angel.position);
            }
        }

        /// <summary>情感速度倍率（由EmotionStateMachine设置）</summary>
        public float EmotionSpeedMultiplier
        {
            get => _emotionSpeedMultiplier;
            set => _emotionSpeedMultiplier = Mathf.Max(value, 0f);
        }

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _attributes = GetComponent<BabyAttributes>();

            _rb.gravityScale = 0f;
            _rb.drag = 5f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            _currentWanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
        }

        private void Start()
        {
            // 从属性读取最大距离
            if (_attributes != null)
            {
                _maxDistanceFromAngel = _attributes.BabyMaxWanderDist;
            }

            // 从配置读取
            if (GameManager.Instance?.Config != null)
            {
                _maxDistanceFromAngel = GameManager.Instance.Config.mapSize * 0.083f; // ~250/3000
            }
        }

        private void Update()
        {
            if (_isCarried) return;

            // 更新路径
            _pathUpdateTimer += Time.deltaTime;
            if (_pathUpdateTimer >= _pathUpdateInterval)
            {
                _pathUpdateTimer = 0f;
                UpdateMovementTarget();
            }

            // 更新漫游方向
            _wanderChangeTimer += Time.deltaTime;
            if (_wanderChangeTimer >= _wanderDirectionChangeInterval)
            {
                _wanderChangeTimer = 0f;
                _currentWanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
            }
        }

        private void FixedUpdate()
        {
            if (_isCarried) return;

            // 向目标移动
            Vector2 currentPos = _rb.position;
            Vector2 toTarget = _moveTarget - currentPos;
            float distToTarget = toTarget.magnitude;

            if (distToTarget > 0.1f)
            {
                Vector2 desiredVelocity = toTarget.normalized * EffectiveSpeed * Time.fixedDeltaTime;

                // 避障
                Vector2 avoidance = CalculateObstacleAvoidance();
                desiredVelocity += avoidance;

                // 距离约束：距天使>forceReturnDistance时强制向天使移动
                Vector2 forceReturn = CalculateForceReturn();
                if (forceReturn.magnitude > 0.1f)
                {
                    desiredVelocity = Vector2.Lerp(desiredVelocity, forceReturn, 0.7f);
                }

                Vector2 newPos = currentPos + desiredVelocity;
                _rb.MovePosition(newPos);
            }
        }

        #endregion

        #region ─── Movement Target Update ────────────────────

        /// <summary>
        /// 每0.5秒更新移动目标
        /// </summary>
        private void UpdateMovementTarget()
        {
            // 默认目标由AI系统设置，这里只处理强制约束
            // AI系统通过 SetMoveTarget() 设置目标

            // 距离约束检查
            Transform angel = FindAngel();
            if (angel != null)
            {
                float dist = Vector2.Distance(transform.position, angel.position);
                if (dist > _forceReturnDistance)
                {
                    // 强制向天使移动
                    _moveTarget = angel.position;
                }
            }
        }

        /// <summary>
        /// 设置移动目标（由AI系统调用）
        /// </summary>
        public void SetMoveTarget(Vector2 target)
        {
            _moveTarget = target;
        }

        /// <summary>
        /// 设置漫游方向
        /// </summary>
        public void SetWanderDirection(Vector2 direction)
        {
            _currentWanderDirection = direction.normalized;
        }

        /// <summary>
        /// 获取当前漫游方向
        /// </summary>
        public Vector2 GetWanderDirection() => _currentWanderDirection;

        /// <summary>
        /// 立即停止移动
        /// </summary>
        public void StopMoving()
        {
            _rb.velocity = Vector2.zero;
            _moveTarget = _rb.position;
            _emotionSpeedMultiplier = 0f;
        }

        /// <summary>
        /// 恢复移动
        /// </summary>
        public void ResumeMoving()
        {
            _emotionSpeedMultiplier = 1.0f;
        }

        #endregion

        #region ─── Obstacle Avoidance ────────────────────────

        /// <summary>
        /// 简化避障：检测前方障碍物，计算偏转方向
        /// </summary>
        private Vector2 CalculateObstacleAvoidance()
        {
            Vector2 avoidance = Vector2.zero;

            // 前方检测
            Vector2 forward = _rb.velocity.normalized;
            if (forward.magnitude < 0.1f)
                forward = (_moveTarget - _rb.position).normalized;

            RaycastHit2D hit = Physics2D.Raycast(
                _rb.position,
                forward,
                _obstacleAvoidanceRadius,
                _obstacleLayer
            );

            if (hit.collider != null)
            {
                // 计算垂直于障碍物的方向
                Vector2 normal = hit.normal;
                // 选择偏转方向（左或右）
                Vector2 right = new Vector2(normal.y, -normal.x);
                float rightDot = Vector2.Dot(right, forward);

                avoidance = rightDot > 0 ? right * EffectiveSpeed * 0.5f : -right * EffectiveSpeed * 0.5f;
            }

            return avoidance;
        }

        /// <summary>
        /// 距离约束：超出最大距离时计算强制返回力
        /// </summary>
        private Vector2 CalculateForceReturn()
        {
            Transform angel = FindAngel();
            if (angel == null) return Vector2.zero;

            float dist = Vector2.Distance(transform.position, angel.position);
            if (dist > _maxDistanceFromAngel)
            {
                Vector2 toAngel = ((Vector2)angel.position - _rb.position).normalized;
                float urgency = Mathf.Clamp01((dist - _maxDistanceFromAngel) / (_forceReturnDistance - _maxDistanceFromAngel));
                return toAngel * EffectiveSpeed * urgency * 2f;
            }

            return Vector2.zero;
        }

        #endregion

        #region ─── Carry System ──────────────────────────────

        /// <summary>
        /// 被天使抱起
        /// </summary>
        public void OnPickedUp(Transform carrier)
        {
            _isCarried = true;
            _carrierTransform = carrier;
            _rb.velocity = Vector2.zero;
            _rb.isKinematic = true;

            // 禁用碰撞（避免与天使碰撞）
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }

        /// <summary>
        /// 被放下
        /// </summary>
        public void OnDropped()
        {
            _isCarried = false;
            _carrierTransform = null;
            _rb.isKinematic = false;

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;

            // 重置移动目标
            _moveTarget = _rb.position;
        }

        #endregion

        #region ─── Utility ───────────────────────────────────

        /// <summary>
        /// 查找场景中的天使
        /// </summary>
        private Transform FindAngel()
        {
            var angelObj = GameObject.FindGameObjectWithTag("Angel");
            if (angelObj == null)
                angelObj = GameObject.FindGameObjectWithTag("Player");
            if (angelObj == null)
                angelObj = GameObject.Find("Angel");

            return angelObj != null ? angelObj.transform : null;
        }

        /// <summary>
        /// 获取当前位置
        /// </summary>
        public Vector2 GetPosition() => _rb.position;

        /// <summary>
        /// 设置障碍物检测层
        /// </summary>
        public void SetObstacleLayer(LayerMask layer)
        {
            _obstacleLayer = layer;
        }

        #endregion

        #region ─── Gizmos ────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 移动目标
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_moveTarget, 0.5f);
            Gizmos.DrawLine(transform.position, _moveTarget);

            // 最大距离约束
            Transform angel = FindAngel();
            if (angel != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(angel.position, _maxDistanceFromAngel);

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(angel.position, _forceReturnDistance);
            }

            // 避障检测
            Gizmos.color = Color.cyan;
            Vector2 forward = _rb != null ? _rb.velocity.normalized : Vector2.right;
            if (forward.magnitude < 0.1f) forward = Vector2.right;
            Gizmos.DrawRay(transform.position, forward * _obstacleAvoidanceRadius);
        }

        #endregion
    }
}
