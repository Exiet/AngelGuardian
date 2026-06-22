using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-019 腐烂僵尸 AI
    /// 行为模式：近战中毒攻击，死亡时爆炸扩散中毒
    /// </summary>
    public class InfectionAI : EnemyBase
    {
        [Header("=== E-019 腐烂僵尸 ===")]
        [SerializeField] private float poisonDamage = 5f;               // 中毒每跳伤害
        [SerializeField] private float poisonDuration = 4f;             // 中毒持续时间
        [SerializeField] private float poisonTickInterval = 1f;        // 中毒伤害间隔
        [SerializeField] private float deathExplosionRadius = 4f;      // 死亡爆炸范围
        [SerializeField] private float deathExplosionDamage = 30f;     // 死亡爆炸伤害
        [SerializeField] private float deathPoisonDuration = 6f;       // 死亡爆炸中毒时间
        [SerializeField] private GameObject poisonCloudEffect;
        [SerializeField] private GameObject deathExplosionEffect;
        [SerializeField] private AudioClip explosionSFX;
        [SerializeField] private Color poisonTintColor = new Color(0.4f, 1f, 0.3f, 1f);

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-019";
            enemyName = "腐烂僵尸";
            type = "Zombie";
            threatTarget = ThreatTarget.Any;
            moveSpeed = 2f; // 缓慢移动
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            if (distance <= attackRange)
            {
                // 在攻击范围内 - 近战中毒攻击
                StopMoving();
                ChangeState(EnemyState.Attacking);
                TryPoisonAttack();
            }
            else if (distance <= detectRange)
            {
                // 在检测范围内 - 追逐
                ChangeState(EnemyState.Chasing);
                MoveTowards(currentTarget.position, 0.8f);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }

        /// <summary>
        /// 近战中毒攻击
        /// </summary>
        private void TryPoisonAttack()
        {
            if (attackTimer > 0f || currentTarget == null) return;

            attackTimer = attackInterval;
            ChangeState(EnemyState.Attacking);

            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                // 基础物理伤害
                damageable.TakeDamage(attackPower * difficultyAttackModifier, DamageType.Physical);

                // 施加中毒
                ApplyPoison(currentTarget.gameObject);
            }

            if (animator != null)
                animator.SetTrigger("Attack");
        }

        /// <summary>
        /// 施加中毒效果
        /// </summary>
        private void ApplyPoison(GameObject target)
        {
            if (target == null) return;

            // 检查是否已有中毒效果
            var existingPoison = target.GetComponent<PoisonEffect>();
            if (existingPoison != null)
            {
                // 刷新中毒时间
                existingPoison.Refresh(poisonDuration, poisonDamage, poisonTickInterval);
            }
            else
            {
                // 添加新的中毒效果
                PoisonEffect poison = target.AddComponent<PoisonEffect>();
                poison.Initialize(poisonDuration, poisonDamage, poisonTickInterval, poisonTintColor, poisonCloudEffect);
            }

            Debug.Log($"[{enemyName}] 使 {target.name} 中毒! {poisonDuration}秒, 每跳{poisonDamage}伤害");
        }

        /// <summary>
        /// 重写死亡 - 爆炸扩散中毒
        /// </summary>
        public override void Die()
        {
            if (currentState == EnemyState.Dead) return;

            // 死亡爆炸
            DeathExplosion();

            base.Die();
        }

        /// <summary>
        /// 死亡爆炸 - 范围伤害+扩散中毒
        /// </summary>
        private void DeathExplosion()
        {
            // 爆炸特效
            if (deathExplosionEffect != null)
            {
                GameObject effect = Instantiate(deathExplosionEffect, transform.position, Quaternion.identity);
                Destroy(effect, 3f);
            }

            // 音效
            if (explosionSFX != null)
            {
                AudioManager.Instance?.PlaySFX(explosionSFX, transform.position);
            }

            // 范围伤害+中毒
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, deathExplosionRadius);

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null && !(damageable is EnemyBase))
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    float falloff = 1f - (distance / deathExplosionRadius);
                    float finalDamage = deathExplosionDamage * Mathf.Max(0.3f, falloff);

                    damageable.TakeDamage(finalDamage, DamageType.Poison);

                    // 爆炸中毒（更长时间）
                    if (hit.CompareTag("Angel") || hit.CompareTag("Baby"))
                    {
                        ApplyDeathPoison(hit.gameObject);
                    }
                }
            }

            Debug.Log($"[{enemyName}] 死亡爆炸! 范围{deathExplosionRadius}, 最大伤害{deathExplosionDamage}");
        }

        /// <summary>
        /// 死亡爆炸中毒（比普通中毒更久）
        /// </summary>
        private void ApplyDeathPoison(GameObject target)
        {
            if (target == null) return;

            var existingPoison = target.GetComponent<PoisonEffect>();
            if (existingPoison != null)
            {
                existingPoison.Refresh(deathPoisonDuration, poisonDamage * 1.5f, poisonTickInterval);
            }
            else
            {
                PoisonEffect poison = target.AddComponent<PoisonEffect>();
                poison.Initialize(deathPoisonDuration, poisonDamage * 1.5f, poisonTickInterval, poisonTintColor, poisonCloudEffect);
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 绘制死亡爆炸范围
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, deathExplosionRadius);

            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"Poison: {poisonDamage}/tick × {poisonDuration}s\nDeath Explosion: {deathExplosionDamage}");
        }
#endif
    }

    /// <summary>
    /// 中毒效果组件 - 附加到目标上
    /// </summary>
    public class PoisonEffect : MonoBehaviour
    {
        private float duration;
        private float damagePerTick;
        private float tickInterval;
        private float tickTimer;
        private float remainingDuration;
        private Color tintColor;
        private GameObject cloudEffectPrefab;
        private GameObject cloudInstance;
        private SpriteRenderer targetRenderer;
        private Color originalColor;
        private IDamageable damageable;

        public void Initialize(float dur, float dmg, float interval, Color tint, GameObject cloudPrefab)
        {
            duration = dur;
            remainingDuration = dur;
            damagePerTick = dmg;
            tickInterval = interval;
            tickTimer = interval;
            tintColor = tint;
            cloudEffectPrefab = cloudPrefab;

            targetRenderer = GetComponentInChildren<SpriteRenderer>();
            if (targetRenderer != null)
            {
                originalColor = targetRenderer.color;
            }

            damageable = GetComponent<IDamageable>();

            // 中毒特效
            if (cloudEffectPrefab != null)
            {
                cloudInstance = Instantiate(cloudEffectPrefab, transform);
            }
        }

        public void Refresh(float newDuration, float newDamage, float newInterval)
        {
            remainingDuration = Mathf.Max(remainingDuration, newDuration);
            damagePerTick = Mathf.Max(damagePerTick, newDamage);
            tickInterval = Mathf.Min(tickInterval, newInterval);
        }

        private void Update()
        {
            if (remainingDuration <= 0f)
            {
                RemoveEffect();
                return;
            }

            remainingDuration -= Time.deltaTime;

            // 视觉闪烁
            if (targetRenderer != null)
            {
                float flicker = Mathf.PingPong(Time.time * 8f, 0.3f);
                targetRenderer.color = Color.Lerp(originalColor, tintColor, flicker);
            }

            // 伤害跳动
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = tickInterval;
                ApplyPoisonDamage();
            }
        }

        private void ApplyPoisonDamage()
        {
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damagePerTick, DamageType.Poison);
            }
        }

        private void RemoveEffect()
        {
            if (targetRenderer != null)
            {
                targetRenderer.color = originalColor;
            }

            if (cloudInstance != null)
            {
                Destroy(cloudInstance);
            }

            Destroy(this);
        }
    }
}
