using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-003 远程弓箭手 AI
    /// 行为模式：保持最大射程距离，被近身时跳跃后退，攻击前0.5秒预警
    /// </summary>
    public class RangedAI : EnemyBase
    {
        [Header("=== E-003 远程弓箭手 ===")]
        [SerializeField] private float preferredDistance = 6f;      // 理想距离
        [SerializeField] private float minDistance = 3f;            // 最小安全距离
        [SerializeField] private float jumpBackDistance = 4f;
        [SerializeField] private float jumpBackCooldown = 5f;
        [SerializeField] private float warningDuration = 0.5f;     // 攻击预警时间
        [SerializeField] private GameObject warningIndicatorPrefab;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private Transform projectileSpawnPoint;

        private float jumpBackCooldownTimer = 0f;
        private bool isWarning = false;
        private float warningTimer = 0f;
        private Vector2 warningTargetPosition;
        private GameObject currentWarningIndicator;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-003";
            enemyName = "远程弓箭手";
            type = "Ranged";
            threatTarget = ThreatTarget.Any;
            attackInterval = 2f; // 远程攻击间隔稍长
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            if (jumpBackCooldownTimer > 0f)
                jumpBackCooldownTimer -= Time.deltaTime;

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                CancelWarning();
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // 被近身 → 跳跃后退
            if (distance < minDistance && jumpBackCooldownTimer <= 0f)
            {
                JumpBack();
                return;
            }

            // 太远 → 靠近
            if (distance > preferredDistance + 1f)
            {
                MoveTowards(currentTarget.position, 0.8f);
                ChangeState(EnemyState.Chasing);
                CancelWarning();
            }
            // 太近 → 后退保持距离
            else if (distance < preferredDistance - 1f)
            {
                Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
                Vector2 retreatTarget = (Vector2)transform.position + retreatDirection * 2f;
                MoveTowards(retreatTarget, 0.6f);
                ChangeState(EnemyState.Chasing);
                CancelWarning();
            }
            // 理想距离 → 攻击
            else
            {
                StopMoving();
                ChangeState(EnemyState.Attacking);
                TryRangedAttack();
            }
        }

        /// <summary>
        /// 远程攻击（带预警）
        /// </summary>
        private void TryRangedAttack()
        {
            if (attackTimer > 0f) return;
            if (currentTarget == null) return;

            if (!isWarning)
            {
                // 开始预警
                StartWarning();
            }
            else
            {
                // 更新预警
                warningTimer -= Time.deltaTime;
                if (warningTimer <= 0f)
                {
                    ExecuteRangedAttack();
                }
            }
        }

        /// <summary>
        /// 开始攻击预警
        /// </summary>
        private void StartWarning()
        {
            isWarning = true;
            warningTimer = warningDuration;
            warningTargetPosition = currentTarget.position;

            // 显示预警指示器
            if (warningIndicatorPrefab != null && currentTarget != null)
            {
                currentWarningIndicator = Instantiate(warningIndicatorPrefab, currentTarget.position, Quaternion.identity);
                Destroy(currentWarningIndicator, warningDuration);
            }

            if (animator != null)
                animator.SetTrigger("Aim");
        }

        /// <summary>
        /// 取消预警
        /// </summary>
        private void CancelWarning()
        {
            isWarning = false;
            warningTimer = 0f;

            if (currentWarningIndicator != null)
            {
                Destroy(currentWarningIndicator);
                currentWarningIndicator = null;
            }
        }

        /// <summary>
        /// 执行远程攻击
        /// </summary>
        private void ExecuteRangedAttack()
        {
            attackTimer = attackInterval;
            isWarning = false;

            if (currentTarget == null) return;

            // 发射弹丸
            if (projectilePrefab != null)
            {
                Vector2 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
                Vector2 direction = ((Vector2)currentTarget.position - spawnPos).normalized;

                GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
                var projectileRb = projectile.GetComponent<Rigidbody2D>();
                if (projectileRb != null)
                {
                    projectileRb.velocity = direction * projectileSpeed;
                }

                // 设置弹丸伤害
                var projectileComponent = projectile.GetComponent<Projectile>();
                if (projectileComponent != null)
                {
                    projectileComponent.SetDamage(attackPower * difficultyAttackModifier, DamageType.Physical);
                }
            }
            else
            {
                // 无弹丸预制体时直接造成伤害
                var damageable = currentTarget.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(attackPower * difficultyAttackModifier, DamageType.Physical);
                }
            }

            if (animator != null)
                animator.SetTrigger("Attack");
        }

        /// <summary>
        /// 跳跃后退
        /// </summary>
        private void JumpBack()
        {
            if (currentTarget == null) return;

            jumpBackCooldownTimer = jumpBackCooldown;

            Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
            Vector2 jumpTarget = (Vector2)transform.position + retreatDirection * jumpBackDistance;

            // 使用协程实现跳跃动画
            StartCoroutine(JumpBackCoroutine(jumpTarget));

            if (animator != null)
                animator.SetTrigger("JumpBack");
        }

        private IEnumerator JumpBackCoroutine(Vector2 target)
        {
            Vector2 startPos = transform.position;
            float jumpDuration = 0.4f;
            float elapsed = 0f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpDuration;

                // 抛物线跳跃
                Vector2 pos = Vector2.Lerp(startPos, target, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 2f; // 跳跃高度
                transform.position = pos;

                yield return null;
            }

            transform.position = target;
        }
    }

    /// <summary>
    /// 弹丸组件 - 用于远程攻击弹丸
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private GameObject hitEffect;
        [SerializeField] private LayerMask targetLayers;

        private float damage;
        private DamageType damageType;

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        public void SetDamage(float dmg, DamageType type)
        {
            damage = dmg;
            damageType = type;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, damageType);
            }

            if (hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}
