using UnityEngine;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-005 护盾卫士 & E-006 精英强化恶魔 AI
    /// E-005: 举盾前进 → 攻击 → 举盾循环，正面减伤50%
    /// E-006: HP越低越强（狂暴AI）
    /// </summary>
    public class TankAI : EnemyBase
    {
        [Header("=== 坦克 AI ===")]
        [SerializeField] private TankType tankType = TankType.ShieldGuard;
        [SerializeField] private float shieldDamageReduction = 0.5f;    // 举盾减伤50%
        [SerializeField] private float shieldDuration = 3f;
        [SerializeField] private float shieldCooldown = 2f;
        [SerializeField] private float shieldMoveSpeedMultiplier = 0.5f; // 举盾移速减半

        // E-006 狂暴相关
        [SerializeField] private float berserkHPMaxThreshold = 0.3f;     // HP<30%达到最大狂暴
        [SerializeField] private float berserkAttackMaxBonus = 1f;       // 攻击力最高+100%
        [SerializeField] private float berserkSpeedMaxBonus = 0.5f;      // 速度最高+50%
        [SerializeField] private float berserkDefenseMaxBonus = 0.3f;    // 防御最高+30%
        [SerializeField] private Color berserkColor = new Color(1f, 0.2f, 0f, 1f);

        public enum TankType
        {
            ShieldGuard,    // E-005 护盾卫士
            BerserkElite    // E-006 精英强化恶魔
        }

        private bool isShieldUp = false;
        private float shieldTimer = 0f;
        private float shieldCooldownTimer = 0f;
        private float baseDefense;
        private Color originalColor;

        protected override void Awake()
        {
            base.Awake();

            if (tankType == TankType.BerserkElite)
            {
                enemyId = "E-006";
                enemyName = "精英强化恶魔";
                type = "BerserkElite";
                threatTarget = ThreatTarget.Any;
            }
            else
            {
                enemyId = "E-005";
                enemyName = "护盾卫士";
                type = "ShieldGuard";
                threatTarget = ThreatTarget.Angel;
            }

            baseDefense = defense;
            if (spriteRenderer != null)
                originalColor = spriteRenderer.color;
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // E-006 狂暴更新
            if (tankType == TankType.BerserkElite)
            {
                UpdateBerserkModifiers();
            }

            // 护盾计时器
            if (shieldTimer > 0f)
                shieldTimer -= Time.deltaTime;
            else if (isShieldUp)
                LowerShield();

            if (shieldCooldownTimer > 0f)
                shieldCooldownTimer -= Time.deltaTime;

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            if (distance <= attackRange)
            {
                // 在攻击范围内
                if (tankType == TankType.ShieldGuard)
                {
                    TryShieldAttackCycle();
                }
                else
                {
                    TryAttack();
                }
            }
            else if (distance <= detectRange)
            {
                // 追逐
                ChangeState(EnemyState.Chasing);

                if (tankType == TankType.ShieldGuard && !isShieldUp && shieldCooldownTimer <= 0f)
                {
                    RaiseShield();
                }
            }
        }

        /// <summary>
        /// E-005 举盾→攻击→举盾循环
        /// </summary>
        private void TryShieldAttackCycle()
        {
            if (isShieldUp && shieldTimer <= 0.5f)
            {
                // 盾牌持续时间将尽，执行攻击
                LowerShield();
                TryAttack();
                shieldCooldownTimer = shieldCooldown;
            }
            else if (!isShieldUp && attackTimer <= 0f && shieldCooldownTimer <= 0f)
            {
                // 攻击后重新举盾
                if (TryAttackShield())
                {
                    RaiseShield();
                }
            }
            else if (!isShieldUp && shieldCooldownTimer > 0f)
            {
                // 冷却中，普通攻击
                TryAttack();
            }
        }

        private bool TryAttackShield()
        {
            if (attackTimer > 0f || currentTarget == null) return false;

            attackTimer = attackInterval;
            ChangeState(EnemyState.Attacking);

            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackPower * difficultyAttackModifier, DamageType.Physical);
            }

            if (animator != null)
                animator.SetTrigger("Attack");

            return true;
        }

        /// <summary>
        /// 举盾
        /// </summary>
        private void RaiseShield()
        {
            isShieldUp = true;
            shieldTimer = shieldDuration;
            defense = baseDefense + baseDefense * shieldDamageReduction;

            if (animator != null)
                animator.SetTrigger("RaiseShield");

            // 视觉反馈
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.7f, 0.7f, 1f, 1f);
        }

        /// <summary>
        /// 放盾
        /// </summary>
        private void LowerShield()
        {
            isShieldUp = false;
            shieldTimer = 0f;
            defense = baseDefense;

            if (animator != null)
                animator.SetTrigger("LowerShield");

            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }

        /// <summary>
        /// E-006 狂暴更新：HP越低越强
        /// </summary>
        private void UpdateBerserkModifiers()
        {
            float hpPercent = HPPercentage;
            float berserkFactor = 1f - Mathf.Clamp01(hpPercent / berserkHPMaxThreshold);

            // 动态调整属性
            float currentBerserkAttack = 1f + berserkAttackMaxBonus * berserkFactor;
            float currentBerserkSpeed = 1f + berserkSpeedMaxBonus * berserkFactor;
            float currentBerserkDefense = 1f + berserkDefenseMaxBonus * berserkFactor;

            // 更新视觉
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(originalColor, berserkColor, berserkFactor);
            }

            // 通过难度倍率系统应用修正
            difficultyAttackModifier = currentBerserkAttack;
            difficultySpeedModifier = currentBerserkSpeed;
            difficultyHPModifier = currentBerserkDefense;

            // 更新动画速度
            if (animator != null)
            {
                animator.speed = 1f + berserkFactor * 0.5f;
            }
        }

        /// <summary>
        /// 重写受伤计算 - E-005举盾时正面减伤
        /// </summary>
        public override void TakeDamage(float damage, DamageType type)
        {
            float finalDamage = CalculateDamage(damage, type);

            // E-005 举盾正面额外减伤
            if (tankType == TankType.ShieldGuard && isShieldUp)
            {
                // 检查是否正面受击（简化：始终减伤，实际应检查攻击方向）
                finalDamage *= (1f - shieldDamageReduction);
                Debug.Log($"[{enemyName}] 举盾减伤 {shieldDamageReduction * 100}%, 最终伤害: {finalDamage}");
            }

            base.TakeDamage(finalDamage, type);
        }

        protected override void UpdateMovement()
        {
            if (currentState == EnemyState.Dead || rb == null) return;

            if (currentState == EnemyState.Chasing && currentTarget != null)
            {
                Vector2 direction = ((Vector2)currentTarget.position - rb.position).normalized;
                float speedMultiplier = isShieldUp ? shieldMoveSpeedMultiplier : 1f;

                if (tankType == TankType.BerserkElite)
                {
                    speedMultiplier *= difficultySpeedModifier;
                }

                rb.linearVelocity = direction * moveSpeed * speedMultiplier;
                UpdateFacing(direction.x);
            }
            else
            {
                base.UpdateMovement();
            }
        }
    }
}
