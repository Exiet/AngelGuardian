using UnityEngine;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-004 自爆虫 & E-012 献祭教徒 AI
    /// 行为模式：锁定最近目标直线冲刺，HP<30%加速；
    /// E-012特殊：专门冲向婴儿，无视地形阻挡
    /// </summary>
    public class SuicideAI : EnemyBase
    {
        [Header("=== 自爆/献祭 AI ===")]
        [SerializeField] private SuicideType suicideType = SuicideType.SuicideBug;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float explosionDamage = 50f;
        [SerializeField] private float berserkHPThreshold = 0.3f;
        [SerializeField] private float berserkSpeedMultiplier = 2f;
        [SerializeField] private float explosionDelay = 0.5f;
        [SerializeField] private GameObject explosionEffect;
        [SerializeField] private AudioClip explosionSFX;
        [SerializeField] private bool ignoreTerrain = false;    // E-012专用
        [SerializeField] private bool targetOnlyBabies = false; // E-012专用

        private bool isBerserk = false;
        private bool isExploding = false;

        public enum SuicideType
        {
            SuicideBug,     // E-004 自爆虫
            Cultist         // E-012 献祭教徒
        }

        protected override void Awake()
        {
            base.Awake();

            if (suicideType == SuicideType.Cultist)
            {
                enemyId = "E-012";
                enemyName = "献祭教徒";
                type = "Cultist";
                threatTarget = ThreatTarget.Baby;
                ignoreTerrain = true;
                targetOnlyBabies = true;
            }
            else
            {
                enemyId = "E-004";
                enemyName = "自爆虫";
                type = "SuicideBug";
                threatTarget = ThreatTarget.Any;
            }
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead || isExploding) return;

            // 检查狂暴状态
            if (!isBerserk && HPPercentage <= berserkHPThreshold)
            {
                EnterBerserk();
            }

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // 直线冲刺向目标
            ChangeState(EnemyState.Chasing);

            float speedMultiplier = isBerserk ? berserkSpeedMultiplier : 1f;
            MoveTowards(currentTarget.position, speedMultiplier);

            // 到达爆炸范围
            if (distance <= explosionRadius * 0.5f || distance <= attackRange)
            {
                StartExplosion();
            }
        }

        /// <summary>
        /// 进入狂暴状态（HP<30%加速）
        /// </summary>
        private void EnterBerserk()
        {
            isBerserk = true;

            // 视觉效果：变红
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 0.3f, 0.3f, 1f);
            }

            // 加速粒子效果
            if (animator != null)
                animator.SetTrigger("Berserk");

            Debug.Log($"[{enemyName}] 进入狂暴状态! 速度×{berserkSpeedMultiplier}");
        }

        /// <summary>
        /// 开始自爆
        /// </summary>
        private void StartExplosion()
        {
            if (isExploding) return;
            isExploding = true;

            StopMoving();

            if (animator != null)
                animator.SetTrigger("Explode");

            // 延迟后爆炸
            Invoke(nameof(Explode), explosionDelay);
        }

        /// <summary>
        /// 爆炸
        /// </summary>
        private void Explode()
        {
            // 范围伤害
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null && !(damageable is EnemyBase))
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    float falloff = 1f - (distance / explosionRadius);
                    float finalDamage = explosionDamage * Mathf.Max(0.2f, falloff);
                    damageable.TakeDamage(finalDamage, DamageType.Fire);
                }
            }

            // 爆炸特效
            if (explosionEffect != null)
            {
                Instantiate(explosionEffect, transform.position, Quaternion.identity);
            }

            // 爆炸音效
            if (explosionSFX != null)
            {
                AudioManager.Instance?.PlaySFX(explosionSFX, transform.position);
            }

            // 自毁
            currentHP = 0;
            Die();
        }

        /// <summary>
        /// E-012专用：重写威胁优先级，婴儿优先级最高
        /// </summary>
        protected override float CalculateThreatPriority(Transform candidate)
        {
            if (targetOnlyBabies)
            {
                if (candidate.CompareTag("Baby"))
                    return Vector2.Distance(transform.position, candidate.position);
                return -1f; // 忽略非婴儿目标
            }

            return base.CalculateThreatPriority(candidate);
        }

        /// <summary>
        /// E-012专用：重写移动以无视地形
        /// </summary>
        protected override void UpdateMovement()
        {
            if (!ignoreTerrain)
            {
                base.UpdateMovement();
                return;
            }

            // 无视地形的直线移动
            if (currentState == EnemyState.Dead || rb == null || isExploding) return;

            if (currentState == EnemyState.Chasing && currentTarget != null)
            {
                Vector2 direction = ((Vector2)currentTarget.position - rb.position).normalized;
                float speed = moveSpeed * difficultySpeedModifier * (isBerserk ? berserkSpeedMultiplier : 1f);
                rb.linearVelocity = direction * speed;
                UpdateFacing(direction.x);
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 绘制爆炸范围
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
#endif
    }
}
