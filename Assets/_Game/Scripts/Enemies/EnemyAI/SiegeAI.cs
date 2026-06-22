using UnityEngine;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-010 城墙破坏者 & E-014 石头巨人 AI
    /// E-010: 优先攻击墙壁/门（仇恨×2），攻城专精
    /// E-014: 攻城+Boss级精英，更高伤害和防御
    /// </summary>
    public class SiegeAI : EnemyBase
    {
        [Header("=== 攻城 AI ===")]
        [SerializeField] private SiegeType siegeType = SiegeType.WallDestroyer;
        [SerializeField] private float structureDamageMultiplier = 2f;     // 对建筑伤害×2
        [SerializeField] private float siegeAttackRange = 3f;
        [SerializeField] private float siegeAttackDamage = 40f;
        [SerializeField] private float siegeAttackCooldown = 2f;
        [SerializeField] private GameObject siegeAttackEffect;
        [SerializeField] private AudioClip siegeAttackSFX;

        // E-014 Boss级
        [SerializeField] private float groundSlamRadius = 5f;
        [SerializeField] private float groundSlamDamage = 30f;
        [SerializeField] private float groundSlamCooldown = 8f;
        [SerializeField] private GameObject groundSlamEffect;

        public enum SiegeType
        {
            WallDestroyer,  // E-010 城墙破坏者
            StoneGiant      // E-014 石头巨人
        }

        private float siegeAttackTimer = 0f;
        private float groundSlamTimer = 0f;
        private bool isSiegeMode = false;
        private Transform structureTarget;

        protected override void Awake()
        {
            base.Awake();

            if (siegeType == SiegeType.StoneGiant)
            {
                enemyId = "E-014";
                enemyName = "石头巨人";
                type = "StoneGiant";
                threatTarget = ThreatTarget.Terrain;
                maxHP = 500f;
                attackPower = 50f;
                defense = 30f;
            }
            else
            {
                enemyId = "E-010";
                enemyName = "城墙破坏者";
                type = "WallDestroyer";
                threatTarget = ThreatTarget.Terrain;
            }
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            if (siegeAttackTimer > 0f) siegeAttackTimer -= Time.deltaTime;
            if (groundSlamTimer > 0f) groundSlamTimer -= Time.deltaTime;

            // 优先寻找建筑目标
            structureTarget = FindStructureTarget();

            if (structureTarget != null)
            {
                // 攻城模式
                isSiegeMode = true;
                currentTarget = structureTarget;
                EngageStructure();
            }
            else
            {
                // 普通战斗模式
                isSiegeMode = false;
                currentTarget = FindTarget();

                if (currentTarget == null)
                {
                    ChangeState(EnemyState.Idle);
                    return;
                }

                float distance = Vector2.Distance(transform.position, currentTarget.position);

                if (distance <= attackRange)
                {
                    TryAttack();
                }
                else
                {
                    ChangeState(EnemyState.Chasing);
                }
            }

            // E-014 地面践踏
            if (siegeType == SiegeType.StoneGiant && groundSlamTimer <= 0f)
            {
                TryGroundSlam();
            }
        }

        /// <summary>
        /// 寻找建筑目标（墙壁/门）
        /// </summary>
        private Transform FindStructureTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange);

            Transform closestStructure = null;
            float closestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Terrain") || hit.CompareTag("Wall") || hit.CompareTag("Door"))
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestStructure = hit.transform;
                    }
                }
            }

            return closestStructure;
        }

        /// <summary>
        /// 攻城行为
        /// </summary>
        private void EngageStructure()
        {
            if (structureTarget == null) return;

            float distance = Vector2.Distance(transform.position, structureTarget.position);

            if (distance <= siegeAttackRange)
            {
                // 在攻城攻击范围内
                StopMoving();
                ChangeState(EnemyState.Attacking);

                if (siegeAttackTimer <= 0f)
                {
                    SiegeAttack();
                }
            }
            else
            {
                // 移向建筑
                ChangeState(EnemyState.Chasing);
                MoveTowards(structureTarget.position, 0.7f);
            }
        }

        /// <summary>
        /// 攻城攻击 - 对建筑伤害×2
        /// </summary>
        private void SiegeAttack()
        {
            if (structureTarget == null) return;

            siegeAttackTimer = siegeAttackCooldown;

            var damageable = structureTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                float damage = siegeAttackDamage * difficultyAttackModifier * structureDamageMultiplier;
                damageable.TakeDamage(damage, DamageType.Physical);
                Debug.Log($"[{enemyName}] 攻城攻击! 伤害: {damage} (×{structureDamageMultiplier} 对建筑加成)");
            }

            // 攻城特效
            if (siegeAttackEffect != null)
            {
                Instantiate(siegeAttackEffect, structureTarget.position, Quaternion.identity);
            }

            if (siegeAttackSFX != null)
            {
                AudioManager.Instance?.PlaySFX(siegeAttackSFX, transform.position);
            }

            if (animator != null)
                animator.SetTrigger("SiegeAttack");

            // 屏幕震动
            BossBase.ScreenShakeTrigger?.Invoke(0.15f, 0.3f);
        }

        /// <summary>
        /// E-014 地面践踏
        /// </summary>
        private void TryGroundSlam()
        {
            // 附近有多个敌人时使用
            int nearbyTargets = Physics2D.OverlapCircleAll(transform.position, groundSlamRadius).Length;

            if (nearbyTargets >= 2)
            {
                groundSlamTimer = groundSlamCooldown;
                GroundSlam();
            }
        }

        private void GroundSlam()
        {
            if (animator != null)
                animator.SetTrigger("GroundSlam");

            // 范围伤害
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, groundSlamRadius);

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null && !(damageable is EnemyBase))
                {
                    damageable.TakeDamage(groundSlamDamage * difficultyAttackModifier, DamageType.Physical);
                }
            }

            // 特效
            if (groundSlamEffect != null)
            {
                Instantiate(groundSlamEffect, transform.position, Quaternion.identity);
            }

            // 全屏震动
            BossBase.ScreenShakeTrigger?.Invoke(0.5f, 0.8f);

            Debug.Log($"[{enemyName}] 地面践踏! 半径{groundSlamRadius}, 伤害{groundSlamDamage}");
        }

        /// <summary>
        /// 重写威胁优先级 - 建筑仇恨×2（距离权重减半）
        /// </summary>
        protected override float CalculateThreatPriority(Transform candidate)
        {
            if (candidate.CompareTag("Terrain") || candidate.CompareTag("Wall") || candidate.CompareTag("Door"))
            {
                // 建筑优先级极高
                return Vector2.Distance(transform.position, candidate.position) * 0.5f; // 距离权重减半 = 仇恨×2
            }

            return base.CalculateThreatPriority(candidate);
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 攻城攻击范围
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, siegeAttackRange);

            if (siegeType == SiegeType.StoneGiant)
            {
                Gizmos.color = new Color(0.8f, 0.3f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, groundSlamRadius);
            }
        }
#endif
    }
}
