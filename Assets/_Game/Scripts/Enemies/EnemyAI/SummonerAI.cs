using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-008 Boss召唤师 AI
    /// 行为模式：保持距离 → 召唤小怪 → 射击，小怪全灭后本体减伤移除
    /// </summary>
    public class SummonerAI : BossBase
    {
        [Header("=== E-008 召唤师 ===")]
        [SerializeField] private float preferredDistance = 8f;
        [SerializeField] private int maxSummonedMinions = 4;
        [SerializeField] private float summonCooldown = 10f;
        [SerializeField] private GameObject[] minionPrefabs;
        [SerializeField] private Transform[] summonSpawnPoints;
        [SerializeField] private float summonAnimationDuration = 1f;
        [SerializeField] private GameObject summonEffect;
        [SerializeField] private GameObject shieldEffect;          // 减伤护盾特效
        [SerializeField] private float shieldDamageReduction = 0.7f; // 本体减伤70%
        [SerializeField] private float shotDamage = 25f;
        [SerializeField] private GameObject shotProjectilePrefab;
        [SerializeField] private float shotCooldown = 2f;

        private List<EnemyBase> activeMinions = new List<EnemyBase>();
        private float summonTimer = 0f;
        private float shotTimer = 0f;
        private bool isShieldActive = true;
        private bool isSummoning = false;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-008";
            enemyName = "Boss召唤师";
            type = "Summoner";
            threatTarget = ThreatTarget.Any;
            bossTitle = "暗影召唤师";
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // 更新计时器
            if (summonTimer > 0f) summonTimer -= Time.deltaTime;
            if (shotTimer > 0f) shotTimer -= Time.deltaTime;

            // 清理已死亡的小怪
            CleanupMinions();

            // 检查护盾状态
            UpdateShieldState();

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // 保持距离
            if (distance < preferredDistance - 2f)
            {
                // 太近，后退
                Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
                MoveTowards((Vector2)transform.position + retreatDirection * 3f, 0.7f);
                ChangeState(EnemyState.Chasing);
            }
            else if (distance > preferredDistance + 2f)
            {
                // 太远，靠近
                MoveTowards(currentTarget.position, 0.6f);
                ChangeState(EnemyState.Chasing);
            }
            else
            {
                // 理想距离
                StopMoving();
                ChangeState(EnemyState.Attacking);

                // 优先召唤
                if (activeMinions.Count < maxSummonedMinions && summonTimer <= 0f && !isSummoning)
                {
                    StartCoroutine(SummonMinions());
                }
                // 其次射击
                else if (shotTimer <= 0f)
                {
                    ShootAtTarget();
                }
            }
        }

        /// <summary>
        /// 召唤小怪
        /// </summary>
        private IEnumerator SummonMinions()
        {
            isSummoning = true;
            summonTimer = summonCooldown;

            if (animator != null)
                animator.SetTrigger("Summon");

            // 召唤动画
            yield return new WaitForSeconds(summonAnimationDuration * 0.5f);

            int minionsToSummon = Mathf.Min(2, maxSummonedMinions - activeMinions.Count);
            for (int i = 0; i < minionsToSummon; i++)
            {
                if (minionPrefabs == null || minionPrefabs.Length == 0) break;

                Vector2 spawnPos;
                if (summonSpawnPoints != null && i < summonSpawnPoints.Length && summonSpawnPoints[i] != null)
                {
                    spawnPos = summonSpawnPoints[i].position;
                }
                else
                {
                    spawnPos = (Vector2)transform.position + Random.insideUnitCircle * 2f;
                }

                // 召唤特效
                if (summonEffect != null)
                {
                    Instantiate(summonEffect, spawnPos, Quaternion.identity);
                }

                // 随机选择小怪类型
                GameObject minionPrefab = minionPrefabs[Random.Range(0, minionPrefabs.Length)];
                GameObject minionObj = Instantiate(minionPrefab, spawnPos, Quaternion.identity);

                EnemyBase minion = minionObj.GetComponent<EnemyBase>();
                if (minion != null)
                {
                    activeMinions.Add(minion);
                }

                yield return new WaitForSeconds(0.3f);
            }

            isSummoning = false;
            Debug.Log($"[{bossTitle}] 召唤 {minionsToSummon} 只小怪 (总计: {activeMinions.Count})");
        }

        /// <summary>
        /// 射击
        /// </summary>
        private void ShootAtTarget()
        {
            if (currentTarget == null) return;

            shotTimer = shotCooldown;
            attackTimer = attackInterval;

            if (animator != null)
                animator.SetTrigger("Attack");

            if (shotProjectilePrefab != null)
            {
                Vector2 direction = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
                GameObject projectile = Instantiate(shotProjectilePrefab, transform.position, Quaternion.identity);
                var rb2d = projectile.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                {
                    rb2d.velocity = direction * 8f;
                }
                var proj = projectile.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.SetDamage(shotDamage * difficultyAttackModifier, DamageType.Magic);
                }
            }
            else
            {
                var damageable = currentTarget.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(shotDamage * difficultyAttackModifier, DamageType.Magic);
                }
            }
        }

        /// <summary>
        /// 更新护盾状态 - 小怪全灭后减伤移除
        /// </summary>
        private void UpdateShieldState()
        {
            bool shouldHaveShield = activeMinions.Count > 0;

            if (shouldHaveShield != isShieldActive)
            {
                isShieldActive = shouldHaveShield;

                if (isShieldActive)
                {
                    // 开启护盾
                    defense *= (1f + shieldDamageReduction);
                    if (shieldEffect != null)
                        shieldEffect.SetActive(true);
                    Debug.Log($"[{bossTitle}] 小怪存活，本体减伤 {shieldDamageReduction * 100}%");
                }
                else
                {
                    // 移除护盾
                    defense /= (1f + shieldDamageReduction);
                    if (shieldEffect != null)
                        shieldEffect.SetActive(false);
                    Debug.Log($"[{bossTitle}] 小怪全灭，本体减伤移除!");
                }
            }
        }

        /// <summary>
        /// 清理已死亡的小怪
        /// </summary>
        private void CleanupMinions()
        {
            activeMinions.RemoveAll(m => m == null || m.CurrentState == EnemyState.Dead);
        }

        protected override void HandleDrops()
        {
            // Boss特殊掉落
            experienceDrop = 500;
            goldDrop = 200;
            base.HandleDrops();
        }
    }
}
