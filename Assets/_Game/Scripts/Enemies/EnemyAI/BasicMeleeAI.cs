using UnityEngine;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-001 恶魔小兵 AI
    /// 行为模式：直线冲撞目标，HP低于20%时逃跑
    /// </summary>
    public class BasicMeleeAI : EnemyBase
    {
        [Header("=== E-001 恶魔小兵 ===")]
        [SerializeField] private float fleeHPThreshold = 0.2f;
        [SerializeField] private float fleeSpeedMultiplier = 1.3f;
        [SerializeField] private float chargeSpeedMultiplier = 1.5f;
        [SerializeField] private float chargeDuration = 0.8f;
        [SerializeField] private float chargeCooldown = 4f;

        private float chargeTimer = 0f;
        private float chargeCooldownTimer = 0f;
        private bool isCharging = false;
        private bool isFleeing = false;
        private Vector2 chargeDirection;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-001";
            enemyName = "恶魔小兵";
            type = "BasicMelee";
            threatTarget = ThreatTarget.Any;
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // 更新冷却
            if (chargeCooldownTimer > 0f)
                chargeCooldownTimer -= Time.deltaTime;

            if (chargeTimer > 0f)
                chargeTimer -= Time.deltaTime;
            else if (isCharging)
                isCharging = false;

            // 索敌
            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            // 低血量逃跑
            if (HPPercentage <= fleeHPThreshold)
            {
                isFleeing = true;
                FleeFromTarget();
                return;
            }

            isFleeing = false;

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // 冲锋逻辑
            if (distance <= detectRange && distance > attackRange && chargeCooldownTimer <= 0f)
            {
                StartCharge();
            }

            // 在攻击范围内
            if (distance <= attackRange)
            {
                if (isCharging)
                {
                    // 冲锋命中后停止
                    isCharging = false;
                    chargeTimer = 0f;
                }
                TryAttack();
            }
            else if (!isCharging)
            {
                ChangeState(EnemyState.Chasing);
            }
        }

        /// <summary>
        /// 直线冲锋
        /// </summary>
        private void StartCharge()
        {
            if (currentTarget == null) return;

            isCharging = true;
            chargeTimer = chargeDuration;
            chargeCooldownTimer = chargeCooldown;
            chargeDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;

            ChangeState(EnemyState.Chasing);

            if (animator != null)
                animator.SetTrigger("Charge");
        }

        /// <summary>
        /// 逃跑行为
        /// </summary>
        private void FleeFromTarget()
        {
            if (currentTarget == null) return;

            ChangeState(EnemyState.Chasing);

            Vector2 fleeDirection = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
            moveDirection = fleeDirection;

            if (rb != null)
            {
                rb.linearVelocity = fleeDirection * moveSpeed * difficultySpeedModifier * fleeSpeedMultiplier;
                UpdateFacing(fleeDirection.x);
            }
        }

        protected override void UpdateMovement()
        {
            if (currentState == EnemyState.Dead || rb == null) return;

            if (isFleeing)
            {
                // 逃跑移动在FleeFromTarget中处理
                return;
            }

            if (isCharging && currentTarget != null)
            {
                // 冲锋移动
                rb.linearVelocity = chargeDirection * moveSpeed * difficultySpeedModifier * chargeSpeedMultiplier;
                UpdateFacing(chargeDirection.x);
            }
            else
            {
                base.UpdateMovement();
            }
        }
    }
}
