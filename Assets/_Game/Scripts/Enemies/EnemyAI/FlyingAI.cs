using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-002 飞行小鬼 AI
    /// 行为模式：绕圈飞行 → 俯冲攻击 → 拉起，可越过低矮墙壁
    /// </summary>
    public class FlyingAI : EnemyBase
    {
        [Header("=== E-002 飞行小鬼 ===")]
        [SerializeField] private float circleRadius = 3f;
        [SerializeField] private float circleSpeed = 2f;
        [SerializeField] private float diveSpeed = 8f;
        [SerializeField] private float pullUpHeight = 5f;
        [SerializeField] private float diveCooldown = 3f;
        [SerializeField] private float divePreparationTime = 0.5f;
        [SerializeField] private float lowWallHeight = 2f;
        [SerializeField] private LayerMask wallLayer;

        private enum FlyState
        {
            Circling,       // 绕圈
            DivePrep,       // 俯冲准备
            Diving,         // 俯冲中
            PullingUp,      // 拉起
            Returning       // 返回绕圈
        }

        private FlyState flyState = FlyState.Circling;
        private float circleAngle = 0f;
        private float diveTimer = 0f;
        private float diveCooldownTimer = 0f;
        private Vector2 circleCenter;
        private Vector2 diveTarget;
        private bool isFlyingOverWall = false;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-002";
            enemyName = "飞行小鬼";
            type = "Flying";
            threatTarget = ThreatTarget.Any;

            // 飞行敌人不受重力
            if (rb != null)
            {
                rb.gravityScale = 0f;
            }
        }

        protected override void Start()
        {
            base.Start();
            circleCenter = transform.position;
            diveCooldownTimer = Random.Range(0f, diveCooldown);
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            currentTarget = FindTarget();

            if (diveCooldownTimer > 0f)
                diveCooldownTimer -= Time.deltaTime;

            if (diveTimer > 0f)
                diveTimer -= Time.deltaTime;

            switch (flyState)
            {
                case FlyState.Circling:
                    UpdateCircling();
                    break;

                case FlyState.DivePrep:
                    UpdateDivePrep();
                    break;

                case FlyState.Diving:
                    UpdateDiving();
                    break;

                case FlyState.PullingUp:
                    UpdatePullingUp();
                    break;

                case FlyState.Returning:
                    UpdateReturning();
                    break;
            }
        }

        /// <summary>
        /// 绕圈飞行
        /// </summary>
        private void UpdateCircling()
        {
            if (currentTarget != null)
            {
                // 绕目标旋转
                circleCenter = (Vector2)currentTarget.position;
            }

            circleAngle += circleSpeed * Time.deltaTime;
            if (circleAngle > 360f) circleAngle -= 360f;

            Vector2 targetPos = circleCenter + new Vector2(
                Mathf.Cos(circleAngle * Mathf.Deg2Rad) * circleRadius,
                Mathf.Sin(circleAngle * Mathf.Deg2Rad) * circleRadius
            );

            // 保持飞行高度
            targetPos.y = Mathf.Max(targetPos.y, circleCenter.y + 2f);

            MoveTowards(targetPos, 0.7f);
            ChangeState(EnemyState.Chasing);

            // 检查是否满足俯冲条件
            if (currentTarget != null && diveCooldownTimer <= 0f)
            {
                float distance = Vector2.Distance(transform.position, currentTarget.position);
                if (distance <= detectRange * 0.8f)
                {
                    StartDive();
                }
            }

            // 检查是否需要越过墙壁
            CheckWallAvoidance();
        }

        /// <summary>
        /// 俯冲准备
        /// </summary>
        private void UpdateDivePrep()
        {
            if (currentTarget == null)
            {
                flyState = FlyState.Circling;
                return;
            }

            // 悬停在目标上方
            Vector2 hoverPos = (Vector2)currentTarget.position + Vector2.up * 4f;
            MoveTowards(hoverPos, 0.5f);

            diveTimer -= Time.deltaTime;
            if (diveTimer <= 0f)
            {
                StartDiveAttack();
            }
        }

        /// <summary>
        /// 俯冲攻击
        /// </summary>
        private void UpdateDiving()
        {
            if (currentTarget == null)
            {
                flyState = FlyState.Circling;
                return;
            }

            // 高速俯冲
            diveTarget = currentTarget.position;
            Vector2 direction = (diveTarget - (Vector2)transform.position).normalized;

            if (rb != null)
            {
                rb.velocity = direction * diveSpeed;
            }
            UpdateFacing(direction.x);

            // 到达目标位置或接近地面时拉起
            float distance = Vector2.Distance(transform.position, diveTarget);
            if (distance <= attackRange)
            {
                // 攻击
                Attack();
                StartPullUp();
            }
        }

        /// <summary>
        /// 拉起
        /// </summary>
        private void UpdatePullingUp()
        {
            Vector2 pullTarget = (Vector2)transform.position + Vector2.up * pullUpHeight;

            if (rb != null)
            {
                Vector2 direction = (pullTarget - (Vector2)transform.position).normalized;
                rb.velocity = direction * diveSpeed * 0.7f;
            }

            // 到达拉起高度后返回绕圈
            if (transform.position.y >= circleCenter.y + pullUpHeight * 0.8f)
            {
                flyState = FlyState.Circling;
                diveCooldownTimer = diveCooldown;
            }
        }

        /// <summary>
        /// 返回绕圈
        /// </summary>
        private void UpdateReturning()
        {
            if (currentTarget != null)
            {
                circleCenter = (Vector2)currentTarget.position;
            }

            Vector2 targetPos = circleCenter + Vector2.up * circleRadius;
            float distance = Vector2.Distance(transform.position, targetPos);

            if (distance < 0.5f)
            {
                flyState = FlyState.Circling;
            }
            else
            {
                MoveTowards(targetPos, 0.8f);
            }
        }

        /// <summary>
        /// 开始俯冲序列
        /// </summary>
        private void StartDive()
        {
            flyState = FlyState.DivePrep;
            diveTimer = divePreparationTime;

            if (animator != null)
                animator.SetTrigger("DivePrep");
        }

        /// <summary>
        /// 执行俯冲攻击
        /// </summary>
        private void StartDiveAttack()
        {
            flyState = FlyState.Diving;
            ChangeState(EnemyState.Attacking);

            if (animator != null)
                animator.SetTrigger("Dive");
        }

        /// <summary>
        /// 开始拉起
        /// </summary>
        private void StartPullUp()
        {
            flyState = FlyState.PullingUp;

            if (animator != null)
                animator.SetTrigger("PullUp");
        }

        /// <summary>
        /// 墙壁规避 - 可越过低矮墙壁
        /// </summary>
        private void CheckWallAvoidance()
        {
            Vector2 forward = rb != null ? rb.velocity.normalized : Vector2.right;
            if (forward.magnitude < 0.1f) return;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, forward, 2f, wallLayer);
            if (hit.collider != null)
            {
                float wallHeight = hit.collider.bounds.size.y;

                if (wallHeight <= lowWallHeight)
                {
                    // 越过低矮墙壁
                    isFlyingOverWall = true;
                    Vector2 flyOverTarget = (Vector2)transform.position + forward * 2f + Vector2.up * (wallHeight + 1f);
                    MoveTowards(flyOverTarget, 1.2f);
                }
                else
                {
                    // 绕过高墙
                    Vector2 avoidDirection = Vector2.Perpendicular(forward) * (Random.value > 0.5f ? 1f : -1f);
                    MoveTowards((Vector2)transform.position + avoidDirection * 3f, 1f);
                }
            }
            else
            {
                isFlyingOverWall = false;
            }
        }

        public override void Attack()
        {
            if (attackTimer > 0f) return;
            if (currentTarget == null) return;

            attackTimer = attackInterval;

            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                // 俯冲攻击有额外伤害加成
                float diveBonus = flyState == FlyState.Diving ? 1.5f : 1f;
                damageable.TakeDamage(attackPower * difficultyAttackModifier * diveBonus, DamageType.Physical);
            }

            if (animator != null)
                animator.SetTrigger("Attack");
        }
    }
}
