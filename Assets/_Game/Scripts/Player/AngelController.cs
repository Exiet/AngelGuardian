using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using AngelGuardian.Core;

namespace AngelGuardian.Player
{
    /// <summary>
    /// 天使玩家控制器 —— 处理移动、冲刺、瞄准输入
    /// 支持PC（WASD+鼠标）和移动端（虚拟摇杆）
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(AngelAttributes))]
    public class AngelController : MonoBehaviour
    {
        #region ─── Inspector ────────────────────────────────

        [Header("Movement")]
        [SerializeField] private float _baseMoveSpeed = 200f;

        [Header("Dash")]
        [SerializeField] private float _dashDistance = 200f;
        [SerializeField] private float _dashDuration = 0.15f;
        [SerializeField] private float _dashCooldown = 15f;
        [SerializeField] private float _doubleTapWindow = 0.3f;

        [Header("Map Bounds")]
        [SerializeField] private float _mapSize = 3000f;

        #endregion

        #region ─── Components ────────────────────────────────

        private Rigidbody2D _rb;
        private AngelAttributes _attributes;
        private AngelCombat _combat;

        #endregion

        #region ─── Input State ───────────────────────────────

        // Movement
        private Vector2 _moveInput;
        private Vector2 _lastMoveDirection = Vector2.right;

        // Aim
        private Vector2 _aimInput;          // 来自虚拟摇杆的瞄准输入
        private Vector2 _mouseWorldPos;      // 鼠标世界坐标
        private bool _useMouseAim = true;    // PC端使用鼠标瞄准

        // Dash
        private bool _isDashing;
        private float _dashCooldownTimer;
        private float _lastDashTime = -999f;

        // Double-tap detection
        private KeyCode _lastKeyPressed;
        private float _lastKeyTime;
        private Vector2 _lastTapDirection;

        // Dash input
        private bool _dashRequested;

        #endregion

        #region ─── Properties ────────────────────────────────

        /// <summary>当前有效移动速度（含属性加成）</summary>
        public float EffectiveMoveSpeed
        {
            get
            {
                float attrSpeed = _attributes != null ? _attributes.MoveSpeed : _baseMoveSpeed;
                return attrSpeed;
            }
        }

        /// <summary>当前移动方向（归一化）</summary>
        public Vector2 MoveDirection => _moveInput.normalized;

        /// <summary>上一帧的移动方向</summary>
        public Vector2 LastMoveDirection => _lastMoveDirection;

        /// <summary>是否正在冲刺</summary>
        public bool IsDashing => _isDashing;

        /// <summary>冲刺冷却剩余时间</summary>
        public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);

        /// <summary>冲刺冷却进度 [0, 1]，1表示可用</summary>
        public float DashCooldownProgress => Mathf.Clamp01(1f - (_dashCooldownTimer / _dashCooldown));

        /// <summary>瞄准方向（世界空间）</summary>
        public Vector2 AimDirection
        {
            get
            {
                if (_useMouseAim)
                {
                    Vector2 dir = _mouseWorldPos - (Vector2)transform.position;
                    return dir.magnitude > 0.1f ? dir.normalized : _lastMoveDirection;
                }
                else
                {
                    return _aimInput.magnitude > 0.1f ? _aimInput.normalized : _lastMoveDirection;
                }
            }
        }

        /// <summary>瞄准目标点（世界空间）</summary>
        public Vector2 AimTarget
        {
            get
            {
                if (_useMouseAim)
                    return _mouseWorldPos;
                else
                    return (Vector2)transform.position + _aimInput.normalized * 5f;
            }
        }

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _attributes = GetComponent<AngelAttributes>();
            _combat = GetComponent<AngelCombat>();

            _rb.gravityScale = 0f;
            _rb.drag = 8f; // 较高的阻尼使移动更灵敏
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void Start()
        {
            // 从 GameConfig 读取地图大小
            if (GameManager.Instance?.Config != null)
            {
                _mapSize = GameManager.Instance.Config.mapSize;
            }
        }

        private void Update()
        {
            // 更新冲刺冷却
            if (_dashCooldownTimer > 0f)
                _dashCooldownTimer -= Time.deltaTime;

            // 检测双击冲刺
            DetectDoubleTapDash();

            // 处理冲刺请求
            if (_dashRequested && _dashCooldownTimer <= 0f && !_isDashing)
            {
                StartCoroutine(DashRoutine());
            }
            _dashRequested = false;
        }

        private void FixedUpdate()
        {
            if (_isDashing) return;

            // 基于 Rigidbody2D 的移动
            Vector2 targetVelocity = _moveInput * EffectiveMoveSpeed * Time.fixedDeltaTime;

            // 使用 MovePosition 实现精确移动
            Vector2 newPosition = _rb.position + targetVelocity;

            // 边界限制
            float halfMap = _mapSize * 0.5f;
            newPosition.x = Mathf.Clamp(newPosition.x, -halfMap, halfMap);
            newPosition.y = Mathf.Clamp(newPosition.y, -halfMap, halfMap);

            _rb.MovePosition(newPosition);

            // 更新上一帧移动方向
            if (_moveInput.magnitude > 0.1f)
                _lastMoveDirection = _moveInput.normalized;
        }

        #endregion

        #region ─── Input System Callbacks ────────────────────

        /// <summary>
        /// 移动输入（WASD 或左虚拟摇杆）
        /// </summary>
        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        /// <summary>
        /// 瞄准输入（右虚拟摇杆，移动端）
        /// </summary>
        public void OnAim(InputValue value)
        {
            Vector2 input = value.Get<Vector2>();
            if (input.magnitude > 0.1f)
            {
                _aimInput = input;
                _useMouseAim = false;
            }
        }

        /// <summary>
        /// 鼠标位置更新（PC端）
        /// </summary>
        public void OnMousePosition(InputValue value)
        {
            // 鼠标输入在 Input System 中通常通过 Pointer position 获取
            // 这里通过 Camera.ScreenToWorldPoint 转换
        }

        /// <summary>
        /// 冲刺输入（Shift键或双击）
        /// </summary>
        public void OnDash(InputValue value)
        {
            if (value.isPressed)
            {
                _dashRequested = true;
            }
        }

        /// <summary>
        /// 每帧从 Camera 更新鼠标世界坐标（在 Update 中由外部或自身调用）
        /// </summary>
        public void UpdateMouseAim()
        {
            if (Camera.main == null) return;

            Vector3 mouseScreen = Mouse.current?.position.ReadValue() ?? Vector3.zero;
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -Camera.main.transform.position.z));
            _mouseWorldPos = new Vector2(mouseWorld.x, mouseWorld.y);

            // 如果有鼠标移动，切换到鼠标瞄准模式
            if (Mouse.current?.delta.ReadValue().magnitude > 0.1f)
            {
                _useMouseAim = true;
            }
        }

        /// <summary>
        /// 设置虚拟摇杆的瞄准输入（供移动端UI调用）
        /// </summary>
        public void SetVirtualAimInput(Vector2 aimInput)
        {
            _aimInput = aimInput;
            if (aimInput.magnitude > 0.1f)
                _useMouseAim = false;
        }

        /// <summary>
        /// 设置虚拟摇杆的移动输入（供移动端UI调用）
        /// </summary>
        public void SetVirtualMoveInput(Vector2 moveInput)
        {
            _moveInput = moveInput;
        }

        #endregion

        #region ─── Dash System ───────────────────────────────

        /// <summary>
        /// 检测双击方向键触发冲刺
        /// </summary>
        private void DetectDoubleTapDash()
        {
            if (_moveInput.magnitude < 0.5f) return;

            // 确定当前按下的主方向键
            KeyCode currentKey = GetDominantKeyFromDirection(_moveInput);

            if (currentKey != KeyCode.None && currentKey == _lastKeyPressed)
            {
                float timeSinceLastTap = Time.time - _lastKeyTime;
                if (timeSinceLastTap <= _doubleTapWindow && timeSinceLastTap > 0.05f)
                {
                    _lastTapDirection = _moveInput.normalized;
                    _dashRequested = true;
                    _lastKeyTime = 0f; // 防止连续触发
                }
            }

            _lastKeyPressed = currentKey;
            _lastKeyTime = Time.time;
        }

        private KeyCode GetDominantKeyFromDirection(Vector2 dir)
        {
            float absX = Mathf.Abs(dir.x);
            float absY = Mathf.Abs(dir.y);

            if (absX > absY)
                return dir.x > 0 ? KeyCode.D : KeyCode.A;
            else
                return dir.y > 0 ? KeyCode.W : KeyCode.S;
        }

        /// <summary>
        /// 冲刺协程
        /// </summary>
        private IEnumerator DashRoutine()
        {
            _isDashing = true;
            _dashCooldownTimer = _dashCooldown;
            _lastDashTime = Time.time;

            // 确定冲刺方向
            Vector2 dashDir;
            if (_moveInput.magnitude > 0.1f)
                dashDir = _moveInput.normalized;
            else if (_lastTapDirection.magnitude > 0.1f)
                dashDir = _lastTapDirection;
            else
                dashDir = _lastMoveDirection;

            // 冲刺距离
            float dashSpeed = _dashDistance / _dashDuration;
            float elapsed = 0f;

            // 保存冲刺前的碰撞状态（可选：冲刺期间忽略某些碰撞）
            Vector2 startPos = _rb.position;

            while (elapsed < _dashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _dashDuration;

                // 使用 ease-out 曲线
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                Vector2 newPos = startPos + dashDir * _dashDistance * easedT;

                // 边界限制
                float halfMap = _mapSize * 0.5f;
                newPos.x = Mathf.Clamp(newPos.x, -halfMap, halfMap);
                newPos.y = Mathf.Clamp(newPos.y, -halfMap, halfMap);

                _rb.MovePosition(newPos);

                yield return null;
            }

            _isDashing = false;

            // 触发冲刺结束事件（供 BabyInteraction 检测抱起婴儿）
            OnDashCompleted?.Invoke(dashDir);
        }

        /// <summary>冲刺完成事件 (冲刺方向)</summary>
        public event Action<Vector2> OnDashCompleted;

        #endregion

        #region ─── Auto-Aim ──────────────────────────────────

        /// <summary>
        /// 松手时自动瞄准最近敌人。
        /// 当没有主动瞄准输入时，自动锁定范围内最近的敌人。
        /// </summary>
        /// <param name="searchRadius">搜索半径</param>
        /// <returns>最近的敌人位置，无敌人时返回默认瞄准方向</returns>
        public Vector2 GetAutoAimTarget(float searchRadius = 500f)
        {
            GameObject nearest = FindNearestEnemy(searchRadius);
            if (nearest != null)
                return nearest.transform.position;
            return (Vector2)transform.position + _lastMoveDirection * 5f;
        }

        /// <summary>
        /// 查找范围内最近的敌人
        /// </summary>
        public GameObject FindNearestEnemy(float radius)
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

        #endregion

        #region ─── Public API ────────────────────────────────

        /// <summary>
        /// 获取当前世界坐标
        /// </summary>
        public Vector2 GetPosition() => _rb.position;

        /// <summary>
        /// 传送天使到指定位置（用于关卡切换等）
        /// </summary>
        public void Teleport(Vector2 position)
        {
            _rb.position = position;
        }

        /// <summary>
        /// 设置地图边界大小
        /// </summary>
        public void SetMapSize(float size)
        {
            _mapSize = size;
        }

        #endregion

        #region ─── Gizmos ────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 显示冲刺冷却状态
            if (_dashCooldownTimer <= 0f)
                Gizmos.color = Color.cyan;
            else
                Gizmos.color = Color.gray;

            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // 瞄准方向
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, (Vector3)AimDirection * 2f);

            // 移动方向
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, (Vector3)_lastMoveDirection * 1.5f);
        }

        #endregion
    }
}
