using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using AngelGuardian.Core;
using AngelGuardian.Dungeon;

namespace AngelGuardian.Player
{
    /// <summary>
    /// 天使与婴儿交互系统 —— 抱起、保护翅膀、门交互
    /// 与EventBus集成，发送交互事件
    /// </summary>
    [RequireComponent(typeof(AngelController))]
    public class BabyInteraction : MonoBehaviour
    {
        #region ─── Inspector ────────────────────────────────

        [Header("Pick Up (抱起)")]
        [SerializeField] private float _pickUpRange = 200f;
        [SerializeField] private float _pickUpCooldown = 15f;

        [Header("Protective Wings (保护翅膀)")]
        [SerializeField] private float _wingsDuration = 2f;
        [SerializeField] private float _wingsCooldown = 30f;
        [SerializeField] private float _wingsBabyPullForce = 15f;
        [SerializeField] private GameObject _wingsVisualPrefab;

        [Header("Door Interaction")]
        [SerializeField] private float _doorInteractRange = 4f;

        #endregion

        #region ─── Components ────────────────────────────────

        private AngelController _controller;
        private AngelAttributes _attributes;
        private GameObject _wingsVisualInstance;

        #endregion

        #region ─── State ─────────────────────────────────────

        // 抱起状态
        private bool _isBabyCarried;
        private GameObject _carriedBaby;
        private float _pickUpCooldownTimer;

        // 翅膀状态
        private bool _isWingsActive;
        private float _wingsCooldownTimer;
        private Coroutine _wingsCoroutine;

        // 输入
        private bool _wingsInputHeld;

        #endregion

        #region ─── Properties ────────────────────────────────

        /// <summary>婴儿是否正被抱起</summary>
        public bool IsBabyCarried => _isBabyCarried;

        /// <summary>当前抱起的婴儿GameObject</summary>
        public GameObject CarriedBaby => _carriedBaby;

        /// <summary>保护翅膀是否激活</summary>
        public bool IsWingsActive => _isWingsActive;

        /// <summary>抱起冷却剩余</summary>
        public float PickUpCooldownRemaining => Mathf.Max(0f, _pickUpCooldownTimer);

        /// <summary>翅膀冷却剩余</summary>
        public float WingsCooldownRemaining => Mathf.Max(0f, _wingsCooldownTimer);

        /// <summary>抱起冷却进度 [0,1]</summary>
        public float PickUpCooldownProgress => Mathf.Clamp01(1f - _pickUpCooldownTimer / _pickUpCooldown);

        /// <summary>翅膀冷却进度 [0,1]</summary>
        public float WingsCooldownProgress => Mathf.Clamp01(1f - _wingsCooldownTimer / _wingsCooldown);

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            _controller = GetComponent<AngelController>();
            _attributes = GetComponent<AngelAttributes>();
        }

        private void Start()
        {
            // 订阅天使冲刺完成事件
            if (_controller != null)
            {
                _controller.OnDashCompleted += OnAngelDashCompleted;
            }

            // 实例化翅膀视觉
            if (_wingsVisualPrefab != null)
            {
                _wingsVisualInstance = Instantiate(_wingsVisualPrefab, transform);
                _wingsVisualInstance.SetActive(false);
            }
        }

        private void Update()
        {
            // 更新冷却
            if (_pickUpCooldownTimer > 0f)
                _pickUpCooldownTimer -= Time.deltaTime;

            if (_wingsCooldownTimer > 0f)
                _wingsCooldownTimer -= Time.deltaTime;

            // 检测门交互
            DetectDoorInteraction();

            // 如果婴儿被抱起，更新婴儿位置
            if (_isBabyCarried && _carriedBaby != null)
            {
                _carriedBaby.transform.position = transform.position;
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.OnDashCompleted -= OnAngelDashCompleted;
        }

        #endregion

        #region ─── Pick Up (抱起婴儿) ─────────────────────────

        /// <summary>
        /// 天使冲刺完成时检测是否可以抱起婴儿
        /// 规则：冲刺经过婴儿200px范围内 → 抱起并行
        /// </summary>
        private void OnAngelDashCompleted(Vector2 dashDirection)
        {
            if (_isBabyCarried) return;       // 已经抱着
            if (_pickUpCooldownTimer > 0f) return; // 冷却中

            GameObject baby = FindBaby();
            if (baby == null) return;

            float dist = Vector2.Distance(transform.position, baby.transform.position);
            if (dist <= _pickUpRange)
            {
                PickUpBaby(baby);
            }
        }

        /// <summary>
        /// 抱起婴儿
        /// </summary>
        public void PickUpBaby(GameObject baby)
        {
            if (baby == null) return;
            if (_isBabyCarried) return;
            if (_pickUpCooldownTimer > 0f) return;

            _isBabyCarried = true;
            _carriedBaby = baby;
            _pickUpCooldownTimer = _pickUpCooldown;

            // 通知婴儿被抱起
            var babyController = baby.GetComponent<Baby.BabyController>();
            if (babyController != null)
            {
                babyController.OnPickedUp(transform);
            }

            // 发送事件
            EventBus.Instance?.FireBabyEmotionChanged("CARRIED");

            Debug.Log($"[BabyInteraction] Baby picked up! Cooldown: {_pickUpCooldown}s");
        }

        /// <summary>
        /// 放下婴儿
        /// </summary>
        public void DropBaby()
        {
            if (!_isBabyCarried || _carriedBaby == null) return;

            var babyController = _carriedBaby.GetComponent<Baby.BabyController>();
            if (babyController != null)
            {
                babyController.OnDropped();
            }

            _isBabyCarried = false;
            _carriedBaby = null;

            Debug.Log("[BabyInteraction] Baby dropped.");
        }

        #endregion

        #region ─── Protective Wings (保护翅膀) ───────────────

        /// <summary>
        /// 翅膀输入（长按空格/暂停键）
        /// </summary>
        public void OnWings(InputValue value)
        {
            bool pressed = value.isPressed;

            if (pressed && !_wingsInputHeld)
            {
                // 按下
                _wingsInputHeld = true;
                if (!_isWingsActive && _wingsCooldownTimer <= 0f)
                {
                    StartWings();
                }
            }
            else if (!pressed)
            {
                // 松开
                _wingsInputHeld = false;
            }
        }

        /// <summary>
        /// 也可以通过长按检测来触发（在Update中处理）
        /// </summary>
        public void OnWingsHold(InputValue value)
        {
            // 备用的长按处理
            float holdDuration = value.Get<float>();
            // Input System 的 Hold interaction 可以在 Input Actions 中配置
        }

        /// <summary>
        /// 激活保护翅膀
        /// </summary>
        private void StartWings()
        {
            if (_isWingsActive) return;
            if (_wingsCooldownTimer > 0f) return;

            _isWingsActive = true;
            _wingsCooldownTimer = _wingsCooldown;

            // 显示翅膀视觉
            if (_wingsVisualInstance != null)
                _wingsVisualInstance.SetActive(true);

            // 启动翅膀协程
            if (_wingsCoroutine != null)
                StopCoroutine(_wingsCoroutine);
            _wingsCoroutine = StartCoroutine(WingsRoutine());

            // 发送事件
            EventBus.Instance?.FireBabyEmotionChanged("WINGS_ACTIVE");

            Debug.Log($"[BabyInteraction] Protective wings activated! Duration: {_wingsDuration}s");
        }

        /// <summary>
        /// 翅膀持续协程：2秒内婴儿强制向天使移动
        /// </summary>
        private IEnumerator WingsRoutine()
        {
            float elapsed = 0f;

            while (elapsed < _wingsDuration)
            {
                elapsed += Time.deltaTime;

                // 强制婴儿向天使移动
                if (!_isBabyCarried)
                {
                    GameObject baby = FindBaby();
                    if (baby != null)
                    {
                        var babyRb = baby.GetComponent<Rigidbody2D>();
                        if (babyRb != null)
                        {
                            Vector2 pullDir = ((Vector2)transform.position - babyRb.position).normalized;
                            babyRb.velocity = Vector2.Lerp(babyRb.velocity, pullDir * _wingsBabyPullForce, 0.3f);
                        }
                    }
                }

                yield return null;
            }

            // 翅膀结束
            _isWingsActive = false;

            if (_wingsVisualInstance != null)
                _wingsVisualInstance.SetActive(false);

            Debug.Log("[BabyInteraction] Protective wings ended.");
        }

        /// <summary>
        /// 强制结束翅膀
        /// </summary>
        public void CancelWings()
        {
            if (!_isWingsActive) return;

            if (_wingsCoroutine != null)
                StopCoroutine(_wingsCoroutine);

            _isWingsActive = false;

            if (_wingsVisualInstance != null)
                _wingsVisualInstance.SetActive(false);
        }

        #endregion

        #region ─── Door Interaction (门交互) ──────────────────

        /// <summary>
        /// 检测并触发门交互（靠近门自动触发）
        /// </summary>
        private void DetectDoorInteraction()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                _doorInteractRange
            );

            foreach (var hit in hits)
            {
                var door = hit.GetComponent<DoorController>();
                if (door != null && door.CurrentState == DoorController.DoorState.Closed)
                {
                    // 自动开门
                    door.Open();
                    Debug.Log($"[BabyInteraction] Auto-opened door at {door.transform.position}");
                }
            }
        }

        /// <summary>
        /// 手动与最近的门交互
        /// </summary>
        public void InteractWithDoor()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                _doorInteractRange
            );

            DoorController nearest = null;
            float minDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var door = hit.GetComponent<DoorController>();
                if (door != null && door.CurrentState != DoorController.DoorState.Destroyed)
                {
                    float dist = Vector2.Distance(transform.position, door.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = door;
                    }
                }
            }

            if (nearest != null)
            {
                nearest.OnPlayerInteract();
                Debug.Log($"[BabyInteraction] Manual door interaction: {nearest.CurrentState}");
            }
        }

        #endregion

        #region ─── Utility ───────────────────────────────────

        /// <summary>
        /// 查找场景中的婴儿
        /// </summary>
        private GameObject FindBaby()
        {
            var babyObj = GameObject.FindGameObjectWithTag("Baby");
            if (babyObj == null)
            {
                // Fallback: 通过名称查找
                babyObj = GameObject.Find("Baby");
            }
            return babyObj;
        }

        /// <summary>
        /// 检测婴儿是否在范围内
        /// </summary>
        public bool IsBabyInRange(float range)
        {
            GameObject baby = FindBaby();
            if (baby == null) return false;
            return Vector2.Distance(transform.position, baby.transform.position) <= range;
        }

        /// <summary>
        /// 获取到婴儿的距离
        /// </summary>
        public float GetDistanceToBaby()
        {
            GameObject baby = FindBaby();
            if (baby == null) return float.MaxValue;
            return Vector2.Distance(transform.position, baby.transform.position);
        }

        #endregion

        #region ─── Input Actions ─────────────────────────────

        /// <summary>
        /// 交互输入（E键/移动端按钮）—— 用于手动门交互
        /// </summary>
        public void OnInteract(InputValue value)
        {
            if (value.isPressed)
            {
                InteractWithDoor();
            }
        }

        #endregion

        #region ─── Gizmos ────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 抱起检测范围
            Gizmos.color = new Color(1f, 0.5f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _pickUpRange);

            // 门交互范围
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _doorInteractRange);

            // 翅膀激活状态
            if (_isWingsActive)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(transform.position, 3f);
            }
        }

        #endregion
    }
}
