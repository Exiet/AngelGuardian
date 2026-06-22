using UnityEngine;
using System.Collections.Generic;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-017 地狱三头犬 AI
    /// 行为模式：三个头独立攻击（中间头攻击天使，左右头攻击婴儿）
    /// </summary>
    public class MultiTargetAI : EnemyBase
    {
        [Header("=== E-017 地狱三头犬 ===")]
        [SerializeField] private HeadConfig centerHead;
        [SerializeField] private HeadConfig leftHead;
        [SerializeField] private HeadConfig rightHead;
        [SerializeField] private float headAttackRadius = 3f;
        [SerializeField] private float bodyMoveSpeed = 2.5f;
        [SerializeField] private float headRotationSpeed = 5f;

        [System.Serializable]
        public class HeadConfig
        {
            [Tooltip("头部攻击点Transform")]
            public Transform headTransform;

            [Tooltip("该头攻击的目标类型")]
            public ThreatTarget targetType = ThreatTarget.Any;

            [Tooltip("该头的攻击力")]
            public float attackPower = 15f;

            [Tooltip("该头的攻击间隔")]
            public float attackInterval = 2f;

            [Tooltip("该头的攻击范围")]
            public float attackRange = 2f;

            [Tooltip("该头的攻击伤害类型")]
            public DamageType damageType = DamageType.Physical;

            [HideInInspector] public float attackTimer = 0f;
            [HideInInspector] public Transform currentTarget;
            [HideInInspector] public bool isAttacking = false;
        }

        private enum BodyState
        {
            Positioning,    // 走位
            MultiAttack,    // 多头攻击
            Roaming         // 游荡
        }

        private BodyState bodyState = BodyState.Roaming;
        private List<HeadConfig> allHeads = new List<HeadConfig>();
        private Vector2 bodyTargetPosition;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-017";
            enemyName = "地狱三头犬";
            type = "Cerberus";
            threatTarget = ThreatTarget.Any;
            maxHP = 800f;
            attackPower = 25f;

            // 初始化头部配置
            allHeads.Add(centerHead);
            allHeads.Add(leftHead);
            allHeads.Add(rightHead);

            // 设置各头的目标类型
            if (centerHead != null) centerHead.targetType = ThreatTarget.Angel;
            if (leftHead != null) leftHead.targetType = ThreatTarget.Baby;
            if (rightHead != null) rightHead.targetType = ThreatTarget.Baby;
        }

        protected override void Start()
        {
            base.Start();
            bodyTargetPosition = transform.position;
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // 更新所有头部的计时器
            foreach (var head in allHeads)
            {
                if (head == null) continue;
                if (head.attackTimer > 0f) head.attackTimer -= Time.deltaTime;
            }

            // 每个头独立索敌
            UpdateHeadTargets();

            // 身体AI
            UpdateBodyAI();

            // 每个头独立攻击
            UpdateHeadAttacks();
        }

        /// <summary>
        /// 每个头独立索敌
        /// </summary>
        private void UpdateHeadTargets()
        {
            foreach (var head in allHeads)
            {
                if (head?.headTransform == null) continue;

                head.currentTarget = FindTargetForHead(head);
            }

            // 主目标（身体朝向）使用中间头的目标
            currentTarget = centerHead?.currentTarget;
        }

        /// <summary>
        /// 为指定头部寻找目标
        /// </summary>
        private Transform FindTargetForHead(HeadConfig head)
        {
            Vector2 searchCenter = head.headTransform != null ? (Vector2)head.headTransform.position : (Vector2)transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(searchCenter, detectRange);

            Transform bestTarget = null;
            float bestPriority = float.MaxValue;

            foreach (var hit in hits)
            {
                Transform candidate = hit.transform;
                float distance = Vector2.Distance(searchCenter, candidate.position);

                // 根据头部目标类型过滤
                bool isValidTarget = false;
                switch (head.targetType)
                {
                    case ThreatTarget.Angel:
                        isValidTarget = candidate.CompareTag("Angel");
                        break;
                    case ThreatTarget.Baby:
                        isValidTarget = candidate.CompareTag("Baby");
                        break;
                    case ThreatTarget.Terrain:
                        isValidTarget = candidate.CompareTag("Terrain") || candidate.CompareTag("Wall");
                        break;
                    case ThreatTarget.Any:
                        isValidTarget = candidate.CompareTag("Angel") || candidate.CompareTag("Baby");
                        break;
                }

                if (isValidTarget && distance < bestPriority)
                {
                    bestPriority = distance;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// 身体AI - 在多头攻击范围之间移动
        /// </summary>
        private void UpdateBodyAI()
        {
            // 计算最优身体位置
            Vector2 optimalPosition = CalculateOptimalBodyPosition();

            float distanceToOptimal = Vector2.Distance(transform.position, optimalPosition);

            if (distanceToOptimal > 1f)
            {
                bodyState = BodyState.Positioning;
                bodyTargetPosition = optimalPosition;
                ChangeState(EnemyState.Chasing);
                MoveTowards(bodyTargetPosition, 0.6f);
            }
            else
            {
                bodyState = BodyState.MultiAttack;
                ChangeState(EnemyState.Attacking);
                StopMoving();
            }
        }

        /// <summary>
        /// 计算最优身体位置（最大化攻击覆盖）
        /// </summary>
        private Vector2 CalculateOptimalBodyPosition()
        {
            Vector2 sumPositions = Vector2.zero;
            int validTargets = 0;

            foreach (var head in allHeads)
            {
                if (head?.currentTarget != null)
                {
                    sumPositions += (Vector2)head.currentTarget.position;
                    validTargets++;
                }
            }

            if (validTargets > 0)
            {
                // 身体放在所有目标的中心位置
                return sumPositions / validTargets;
            }

            return transform.position;
        }

        /// <summary>
        /// 每个头独立攻击
        /// </summary>
        private void UpdateHeadAttacks()
        {
            foreach (var head in allHeads)
            {
                if (head?.headTransform == null || head.currentTarget == null) continue;

                float distance = Vector2.Distance(head.headTransform.position, head.currentTarget.position);

                // 旋转头部朝向目标
                RotateHeadTowards(head);

                if (distance <= head.attackRange && head.attackTimer <= 0f)
                {
                    PerformHeadAttack(head);
                }
            }
        }

        /// <summary>
        /// 旋转头部朝向目标
        /// </summary>
        private void RotateHeadTowards(HeadConfig head)
        {
            if (head.headTransform == null || head.currentTarget == null) return;

            Vector2 direction = (head.currentTarget.position - head.headTransform.position).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

            head.headTransform.rotation = Quaternion.Slerp(
                head.headTransform.rotation,
                targetRotation,
                headRotationSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// 执行头部攻击
        /// </summary>
        private void PerformHeadAttack(HeadConfig head)
        {
            head.attackTimer = head.attackInterval;
            head.isAttacking = true;

            var damageable = head.currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                float damage = head.attackPower * difficultyAttackModifier;
                damageable.TakeDamage(damage, head.damageType);

                string headName = head == centerHead ? "中间头" : (head == leftHead ? "左头" : "右头");
                Debug.Log($"[{enemyName}] {headName} 攻击 {head.currentTarget.name}! 伤害: {damage}");
            }

            if (animator != null)
            {
                string triggerName = head == centerHead ? "CenterAttack" : (head == leftHead ? "LeftAttack" : "RightAttack");
                animator.SetTrigger(triggerName);
            }

            // 短暂延迟后重置
            Invoke(nameof(ResetHeadAttack), 0.3f);
        }

        private void ResetHeadAttack()
        {
            foreach (var head in allHeads)
            {
                if (head != null) head.isAttacking = false;
            }
        }

        /// <summary>
        /// 三头犬的受伤处理 - 可能影响头部功能
        /// </summary>
        public override void TakeDamage(float damage, DamageType type)
        {
            base.TakeDamage(damage, type);

            // HP低于50%时，一侧头攻击力减弱
            if (HPPercentage <= 0.5f && HPPercentage > 0.25f)
            {
                if (leftHead != null) leftHead.attackPower = 10f;
                Debug.Log($"[{enemyName}] 左头受伤，攻击力下降!");
            }

            // HP低于25%时，只剩中间头正常运作
            if (HPPercentage <= 0.25f)
            {
                if (leftHead != null) leftHead.attackInterval = 5f;
                if (rightHead != null) rightHead.attackInterval = 5f;
                Debug.Log($"[{enemyName}] 左右头严重受伤，攻击间隔延长!");
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 绘制各头攻击范围
            foreach (var head in allHeads)
            {
                if (head?.headTransform == null) continue;

                Color headColor = head == centerHead ? Color.red :
                                 head == leftHead ? Color.blue : Color.green;
                Gizmos.color = new Color(headColor.r, headColor.g, headColor.b, 0.3f);
                Gizmos.DrawWireSphere(head.headTransform.position, head.attackRange);

                // 头部朝向
                Gizmos.color = headColor;
                Gizmos.DrawRay(head.headTransform.position, head.headTransform.right * head.attackRange);

                // 目标连线
                if (head.currentTarget != null)
                {
                    Gizmos.DrawLine(head.headTransform.position, head.currentTarget.position);
                }
            }

            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"Center: {centerHead?.currentTarget?.name ?? "None"}\n" +
                $"Left: {leftHead?.currentTarget?.name ?? "None"}\n" +
                $"Right: {rightHead?.currentTarget?.name ?? "None"}");
        }
#endif
    }
}
