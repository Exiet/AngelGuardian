using System;
using System.Collections.Generic;
using UnityEngine;
using AngelGuardian.Core;
using AngelGuardian.Data;

namespace AngelGuardian.Player
{
    /// <summary>
    /// 天使战斗系统 —— 管理武器、攻击逻辑、投射物/近战判定、暴击、元素效果、对象池
    /// </summary>
    [RequireComponent(typeof(AngelAttributes))]
    public class AngelCombat : MonoBehaviour
    {
        #region ─── Inspector ────────────────────────────────

        [Header("Projectile")]
        [SerializeField] private GameObject _defaultProjectilePrefab;
        [SerializeField] private Transform _projectileSpawnPoint;
        [SerializeField] private Transform _projectilePoolParent;

        [Header("Melee")]
        [SerializeField] private Transform _meleeOrigin;
        [SerializeField] private float _defaultMeleeRange = 2f;
        [SerializeField] private LayerMask _enemyLayerMask = -1;

        #endregion

        #region ─── Components ────────────────────────────────

        private AngelAttributes _attributes;
        private AngelController _controller;

        #endregion

        #region ─── Weapon Management ─────────────────────────

        /// <summary>当前装备的武器数据列表</summary>
        private List<WeaponData> _equippedWeapons = new List<WeaponData>();

        /// <summary>当前激活的武器索引</summary>
        private int _activeWeaponIndex = 0;

        /// <summary>每个武器的攻击计时器</summary>
        private Dictionary<string, float> _weaponTimers = new Dictionary<string, float>();

        /// <summary>当前激活的武器</summary>
        public WeaponData ActiveWeapon
        {
            get
            {
                if (_equippedWeapons.Count == 0) return null;
                if (_activeWeaponIndex >= _equippedWeapons.Count)
                    _activeWeaponIndex = 0;
                return _equippedWeapons[_activeWeaponIndex];
            }
        }

        /// <summary>已装备武器数量</summary>
        public int EquippedWeaponCount => _equippedWeapons.Count;

        /// <summary>最大武器数量</summary>
        public int MaxWeapons => _attributes != null ? _attributes.MaxWeapons : 6;

        #endregion

        #region ─── Projectile Pool ───────────────────────────

        private GameObjectPool _projectilePool;
        private List<GameObject> _activeProjectiles = new List<GameObject>();

        /// <summary>当前活跃投射物数量</summary>
        public int ActiveProjectileCount => _activeProjectiles.Count;

        /// <summary>最大投射物数量</summary>
        public int MaxProjectiles => _attributes != null ? _attributes.MaxProjectiles : 45;

        #endregion

        #region ─── Attack State ──────────────────────────────

        private bool _isAttacking;
        private float _globalAttackCooldown;

        #endregion

        #region ─── Unity Lifecycle ───────────────────────────

        private void Awake()
        {
            _attributes = GetComponent<AngelAttributes>();
            _controller = GetComponent<AngelController>();
        }

        private void Start()
        {
            // 初始化投射物对象池
            if (_defaultProjectilePrefab != null)
            {
                Transform poolParent = _projectilePoolParent;
                if (poolParent == null)
                {
                    var go = new GameObject("[ProjectilePool]");
                    poolParent = go.transform;
                    poolParent.SetParent(transform);
                }

                _projectilePool = new GameObjectPool(
                    _defaultProjectilePrefab,
                    poolParent,
                    initialCapacity: 20,
                    maxSize: MaxProjectiles,
                    autoExpand: true
                );
            }
        }

        private void Update()
        {
            // 更新鼠标瞄准
            _controller?.UpdateMouseAim();

            // 递减武器计时器
            UpdateWeaponTimers();

            // 清理失效的投射物引用
            CleanupProjectileList();
        }

        #endregion

        #region ─── Weapon Management ─────────────────────────

        /// <summary>
        /// 装备武器
        /// </summary>
        public bool EquipWeapon(WeaponData weapon)
        {
            if (_equippedWeapons.Count >= MaxWeapons)
            {
                Debug.LogWarning($"[AngelCombat] Max weapons ({MaxWeapons}) reached! Cannot equip {weapon.weaponName}");
                return false;
            }

            if (!_equippedWeapons.Contains(weapon))
            {
                _equippedWeapons.Add(weapon);
                _weaponTimers[weapon.weaponId] = 0f;
                Debug.Log($"[AngelCombat] Equipped: {weapon.weaponName} ({weapon.weaponId})");
            }

            return true;
        }

        /// <summary>
        /// 卸下武器
        /// </summary>
        public void UnequipWeapon(string weaponId)
        {
            var weapon = _equippedWeapons.Find(w => w.weaponId == weaponId);
            if (weapon != null)
            {
                _equippedWeapons.Remove(weapon);
                _weaponTimers.Remove(weaponId);
            }
        }

        /// <summary>
        /// 切换激活武器
        /// </summary>
        public void SwitchWeapon(int index)
        {
            if (index >= 0 && index < _equippedWeapons.Count)
            {
                _activeWeaponIndex = index;
                Debug.Log($"[AngelCombat] Switched to: {ActiveWeapon?.weaponName}");
            }
        }

        /// <summary>
        /// 切换到下一个武器
        /// </summary>
        public void CycleWeapon()
        {
            if (_equippedWeapons.Count == 0) return;
            _activeWeaponIndex = (_activeWeaponIndex + 1) % _equippedWeapons.Count;
        }

        /// <summary>
        /// 获取所有已装备武器
        /// </summary>
        public IReadOnlyList<WeaponData> GetEquippedWeapons() => _equippedWeapons;

        #endregion

        #region ─── Attack Logic ──────────────────────────────

        /// <summary>
        /// 执行攻击（由输入系统调用）
        /// </summary>
        public void PerformAttack()
        {
            if (_isAttacking) return;
            if (ActiveWeapon == null) return;

            string weaponId = ActiveWeapon.weaponId;
            if (_weaponTimers.TryGetValue(weaponId, out float timer) && timer > 0f)
                return;

            // 检查弹幕数量硬限制
            if (ActiveWeapon.type == WeaponType.Ranged && ActiveProjectileCount >= MaxProjectiles)
            {
                // 回收最老的投射物
                RecycleOldestProjectile();
                if (ActiveProjectileCount >= MaxProjectiles)
                    return; // 仍超限则不发射
            }

            switch (ActiveWeapon.type)
            {
                case WeaponType.Ranged:
                    FireProjectile();
                    break;
                case WeaponType.Melee:
                    PerformMeleeAttack();
                    break;
                case WeaponType.AOE:
                    PerformAOEAttack();
                    break;
            }

            // 设置攻击间隔
            float interval = CalculateAttackInterval(ActiveWeapon);
            _weaponTimers[weaponId] = interval;

            // 攻击事件
            OnAttackPerformed?.Invoke(ActiveWeapon);
        }

        /// <summary>
        /// 持续攻击（按住时调用）
        /// </summary>
        public void PerformContinuousAttack()
        {
            PerformAttack();
        }

        #endregion

        #region ─── Projectile Firing ─────────────────────────

        /// <summary>
        /// 发射投射物
        /// </summary>
        private void FireProjectile()
        {
            WeaponData weapon = ActiveWeapon;
            if (weapon == null || _projectilePool == null) return;

            Vector2 aimDir = _controller != null ? _controller.AimDirection : Vector2.right;
            Vector2 spawnPos = _projectileSpawnPoint != null
                ? _projectileSpawnPoint.position
                : transform.position + (Vector3)aimDir * 1f;

            int count = _attributes.ProjectileCount + weapon.projectileCount;
            float spreadAngle = 0f;

            // 解析特殊参数中的散布角度
            if (!string.IsNullOrEmpty(weapon.specialParams))
            {
                try
                {
                    var spec = JsonUtility.FromJson<WeaponSpecialParams>(weapon.specialParams);
                    spreadAngle = spec.spread_angle;
                }
                catch { }
            }

            for (int i = 0; i < count; i++)
            {
                // 计算每个投射物的方向
                Vector2 dir = aimDir;
                if (count > 1 && spreadAngle > 0f)
                {
                    float angleStep = spreadAngle / (count - 1);
                    float angle = -spreadAngle / 2f + angleStep * i;
                    dir = Quaternion.Euler(0, 0, angle) * aimDir;
                }

                // 从对象池获取投射物
                GameObject proj = _projectilePool.Get();
                if (proj == null) break;

                proj.transform.position = spawnPos;
                proj.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);

                // 配置投射物
                var projectileComp = proj.GetComponent<AngelProjectile>();
                if (projectileComp == null)
                    projectileComp = proj.AddComponent<AngelProjectile>();

                projectileComp.Initialize(
                    damage: CalculateDamage(weapon),
                    speed: _attributes.ProjectileSpeed > 0 ? _attributes.ProjectileSpeed : weapon.projectileSpeed,
                    direction: dir,
                    pierceCount: _attributes.ProjectilePierce + weapon.pierceCount,
                    size: _attributes.ProjectileSize,
                    isCritical: RollCritical(),
                    frostChance: _attributes.FrostChance,
                    burnChance: _attributes.BurnChance,
                    chainCount: _attributes.ChainCount,
                    aoeRadius: _attributes.AoeRadius,
                    lifetime: 5f,
                    owner: gameObject
                );

                projectileComp.OnDestroyed += HandleProjectileDestroyed;

                _activeProjectiles.Add(proj);
            }
        }

        private void HandleProjectileDestroyed(GameObject proj)
        {
            _activeProjectiles.Remove(proj);
            _projectilePool?.Release(proj);
        }

        #endregion

        #region ─── Melee Attack ──────────────────────────────

        /// <summary>
        /// 近战攻击判定
        /// </summary>
        private void PerformMeleeAttack()
        {
            WeaponData weapon = ActiveWeapon;
            if (weapon == null) return;

            Vector2 origin = _meleeOrigin != null ? _meleeOrigin.position : (Vector2)transform.position;
            Vector2 dir = _controller != null ? _controller.AimDirection : Vector2.right;

            float range = _defaultMeleeRange;
            // 解析特殊参数
            if (!string.IsNullOrEmpty(weapon.specialParams))
            {
                try
                {
                    var spec = JsonUtility.FromJson<WeaponSpecialParams>(weapon.specialParams);
                    if (spec.bash_radius > 0) range = spec.bash_radius;
                }
                catch { }
            }

            // 范围检测
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, _enemyLayerMask);

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                float damage = CalculateDamage(weapon);
                bool isCrit = RollCritical();

                if (isCrit) damage *= _attributes.CritDamage;

                // 造成伤害
                DealDamageToEnemy(hit.gameObject, damage, isCrit);

                // 元素效果
                ApplyElementalEffects(hit.gameObject);
            }

            // 近战视觉反馈（可扩展）
            Debug.DrawRay(origin, dir * range, Color.red, 0.2f);
        }

        #endregion

        #region ─── AOE Attack ────────────────────────────────

        /// <summary>
        /// AOE攻击
        /// </summary>
        private void PerformAOEAttack()
        {
            WeaponData weapon = ActiveWeapon;
            if (weapon == null) return;

            float radius = _attributes.AoeRadius;
            if (radius <= 0f) radius = 3f; // 默认AOE范围

            // 解析特殊参数获取AOE半径
            if (!string.IsNullOrEmpty(weapon.specialParams))
            {
                try
                {
                    var spec = JsonUtility.FromJson<WeaponSpecialParams>(weapon.specialParams);
                    if (spec.radius > 0) radius = spec.radius;
                }
                catch { }
            }

            Vector2 center = transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, _enemyLayerMask);

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                float damage = CalculateDamage(weapon);
                bool isCrit = RollCritical();
                if (isCrit) damage *= _attributes.CritDamage;

                DealDamageToEnemy(hit.gameObject, damage, isCrit);
                ApplyElementalEffects(hit.gameObject);
            }

            // AOE视觉反馈
            DebugDrawCircle(center, radius, Color.yellow, 0.3f);
        }

        #endregion

        #region ─── Damage Calculation ────────────────────────

        /// <summary>
        /// 计算武器最终伤害
        /// </summary>
        public float CalculateDamage(WeaponData weapon)
        {
            if (weapon == null) return _attributes.MinDPS;

            float baseDamage = weapon.baseDamage + _attributes.WeaponDamage;
            float comboBonus = 1f + _attributes.ComboDmgBonus * _attributes.ComboAccumulator;

            return Mathf.Max(baseDamage * comboBonus, _attributes.MinDPS);
        }

        /// <summary>
        /// 暴击判定（独立概率）
        /// </summary>
        public bool RollCritical()
        {
            float critChance = _attributes.CritRate;
            // 运气值微量提升暴击率
            critChance += _attributes.Luck * 0.001f;
            critChance = Mathf.Clamp01(critChance);

            return UnityEngine.Random.value < critChance;
        }

        /// <summary>
        /// 计算攻击间隔
        /// 公式：基础间隔 × (1 - cooldownReduction) ÷ attackSpeed
        /// </summary>
        public float CalculateAttackInterval(WeaponData weapon)
        {
            if (weapon == null) return 1f;

            float baseInterval = weapon.attackInterval;
            float cdReduction = Mathf.Clamp(_attributes.CooldownReduction, 0f, 0.9f);
            float atkSpeed = Mathf.Max(_attributes.AttackSpeed, 0.1f);

            float interval = baseInterval * (1f - cdReduction) / atkSpeed;

            // 连击冷却减免
            float comboCdReduce = _attributes.ComboCdReduce * _attributes.ComboAccumulator;
            interval *= (1f - Mathf.Clamp(comboCdReduce, 0f, 0.5f));

            return Mathf.Max(interval, 0.05f); // 最小间隔50ms
        }

        #endregion

        #region ─── Elemental Effects ─────────────────────────

        /// <summary>
        /// 对敌人附加元素效果
        /// </summary>
        private void ApplyElementalEffects(GameObject enemy)
        {
            // 冰冻效果
            if (_attributes.FrostChance > 0f && UnityEngine.Random.value < _attributes.FrostChance)
            {
                var freezeable = enemy.GetComponent<IFreezeable>();
                freezeable?.ApplyFreeze(2f, 0.4f); // 冻结2秒，减速40%
            }

            // 燃烧效果
            if (_attributes.BurnChance > 0f && UnityEngine.Random.value < _attributes.BurnChance)
            {
                var burnable = enemy.GetComponent<IBurnable>();
                burnable?.ApplyBurn(CalculateDamage(ActiveWeapon) * 0.3f, 4f); // 4秒内造成30%武器伤害
            }

            // 嘲讽效果
            if (_attributes.TauntChance > 0f && UnityEngine.Random.value < _attributes.TauntChance)
            {
                var tauntable = enemy.GetComponent<ITauntable>();
                tauntable?.ApplyTaunt(gameObject, 3f);
            }
        }

        #endregion

        #region ─── Deal Damage ───────────────────────────────

        /// <summary>
        /// 对敌人造成伤害
        /// </summary>
        private void DealDamageToEnemy(GameObject enemy, float damage, bool isCritical)
        {
            // 通过 SendMessage 或接口调用
            var damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, isCritical, gameObject);
            }
            else
            {
                enemy.SendMessage("TakeDamage", new DamageInfo
                {
                    Amount = damage,
                    IsCritical = isCritical,
                    Source = gameObject
                }, SendMessageOptions.DontRequireReceiver);
            }
        }

        #endregion

        #region ─── Projectile Pool Management ────────────────

        private void UpdateWeaponTimers()
        {
            var keys = new List<string>(_weaponTimers.Keys);
            foreach (var key in keys)
            {
                if (_weaponTimers[key] > 0f)
                    _weaponTimers[key] -= Time.deltaTime;
            }
        }

        private void CleanupProjectileList()
        {
            _activeProjectiles.RemoveAll(p => p == null || !p.activeInHierarchy);
        }

        /// <summary>
        /// 回收最老的投射物（超出限制时）
        /// </summary>
        private void RecycleOldestProjectile()
        {
            if (_activeProjectiles.Count == 0) return;

            var oldest = _activeProjectiles[0];
            if (oldest != null)
            {
                var projComp = oldest.GetComponent<AngelProjectile>();
                projComp?.ForceDestroy();
            }
        }

        /// <summary>
        /// 回收所有活跃投射物
        /// </summary>
        public void RecycleAllProjectiles()
        {
            foreach (var proj in _activeProjectiles.ToArray())
            {
                if (proj != null)
                    _projectilePool?.Release(proj);
            }
            _activeProjectiles.Clear();
        }

        #endregion

        #region ─── Events ────────────────────────────────────

        /// <summary>攻击执行事件</summary>
        public event Action<WeaponData> OnAttackPerformed;

        #endregion

        #region ─── Debug Utility ─────────────────────────────

        private void DebugDrawCircle(Vector2 center, float radius, Color color, float duration)
        {
            int segments = 32;
            float angleStep = 360f / segments;
            Vector2 prevPoint = center + new Vector2(radius, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector2 nextPoint = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                Debug.DrawLine(prevPoint, nextPoint, color, duration);
                prevPoint = nextPoint;
            }
        }

        #endregion
    }

    #region ─── Supporting Types ──────────────────────────

    /// <summary>
    /// 天使投射物组件（挂载到投射物GameObject上）
    /// </summary>
    public class AngelProjectile : MonoBehaviour
    {
        public float Damage { get; private set; }
        public float Speed { get; private set; }
        public Vector2 Direction { get; private set; }
        public int PierceCount { get; private set; }
        public float Size { get; private set; }
        public bool IsCritical { get; private set; }
        public float FrostChance { get; private set; }
        public float BurnChance { get; private set; }
        public int ChainCount { get; private set; }
        public float AoeRadius { get; private set; }
        public float Lifetime { get; private set; }
        public GameObject Owner { get; private set; }

        private float _elapsed;
        private int _pierceRemaining;
        private HashSet<GameObject> _hitEnemies = new HashSet<GameObject>();
        private bool _isDestroyed;

        public event Action<GameObject> OnDestroyed;

        public void Initialize(float damage, float speed, Vector2 direction, int pierceCount,
            float size, bool isCritical, float frostChance, float burnChance,
            int chainCount, float aoeRadius, float lifetime, GameObject owner)
        {
            Damage = damage;
            Speed = speed;
            Direction = direction;
            PierceCount = pierceCount;
            _pierceRemaining = pierceCount;
            Size = size;
            IsCritical = isCritical;
            FrostChance = frostChance;
            BurnChance = burnChance;
            ChainCount = chainCount;
            AoeRadius = aoeRadius;
            Lifetime = lifetime;
            Owner = owner;

            _elapsed = 0f;
            _hitEnemies.Clear();
            _isDestroyed = false;

            transform.localScale = Vector3.one * (size / 6f); // 默认大小6为基准
        }

        private void Update()
        {
            if (_isDestroyed) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= Lifetime)
            {
                ForceDestroy();
                return;
            }

            // 移动
            transform.position += (Vector3)(Direction * Speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDestroyed) return;

            if (other.CompareTag("Enemy") && !_hitEnemies.Contains(other.gameObject))
            {
                _hitEnemies.Add(other.gameObject);

                // 造成伤害
                float finalDamage = Damage;
                var damageable = other.GetComponent<IDamageable>();
                damageable?.TakeDamage(finalDamage, IsCritical, Owner);

                // 元素效果
                ApplyElementalEffects(other.gameObject);

                // 穿透判定
                if (_pierceRemaining > 0)
                {
                    _pierceRemaining--;
                }
                else
                {
                    ForceDestroy();
                }
            }
        }

        private void ApplyElementalEffects(GameObject enemy)
        {
            if (FrostChance > 0f && UnityEngine.Random.value < FrostChance)
            {
                enemy.GetComponent<IFreezeable>()?.ApplyFreeze(2f, 0.4f);
            }
            if (BurnChance > 0f && UnityEngine.Random.value < BurnChance)
            {
                enemy.GetComponent<IBurnable>()?.ApplyBurn(Damage * 0.3f, 4f);
            }
        }

        public void ForceDestroy()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            OnDestroyed?.Invoke(gameObject);
            OnDestroyed = null;
        }
    }

    /// <summary>
    /// 武器特殊参数的反序列化结构
    /// </summary>
    [Serializable]
    public struct WeaponSpecialParams
    {
        public float spread_angle;
        public float bash_radius;
        public float radius;
        public float inner_radius;
        public float inner_damage_mult;
        public float headshot_multiplier;
        public float scope_zoom;
        public float homing_strength;
        public float homing_range;
        public float turn_rate;
        public int chain_count;
        public float chain_range;
        public float chain_damage_falloff;
        public float block_chance;
        public float block_damage_reduction;
        public float knockback;
        public float combo_window;
        public int combo_max;
        public float combo_damage_per_stack;
        public float dash_range;
        public float backstab_multiplier;
        public float stealth_duration;
        public float stealth_cooldown;
        public float poison_damage;
        public float poison_duration;
        public float charge_time;
        public float full_charge_mult;
        public int arrow_rain_count;
        public int burst_count;
        public float burst_interval;
        public float reload_time;
        public float bolt_explosion_radius;
        public float explosion_damage;
        public float damage_falloff_start;
        public float damage_falloff_end;
        public float burn_duration;
        public float burn_damage_per_sec;
        public float freeze_duration;
        public float chill_slow;
        public float chill_duration;
        public float meteor_radius;
        public float fall_delay;
        public float burn_ground_duration;
        public float burn_ground_damage;
        public float mana_cost;
        public float mana_regen_per_kill;
        public float earthquake_radius;
        public float earthquake_cooldown;
        public float counter_damage_mult;
        public float pull_strength;
        public float tick_interval;
        public float tick_damage;
        public int lightning_strikes;
        public float duration;
    }

    /// <summary>
    /// 伤害信息结构
    /// </summary>
    [Serializable]
    public struct DamageInfo
    {
        public float Amount;
        public bool IsCritical;
        public GameObject Source;
    }

    #endregion

    #region ─── Interfaces ────────────────────────────────

    /// <summary>可受伤害接口</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, bool isCritical, GameObject source);
    }

    /// <summary>可冻结接口</summary>
    public interface IFreezeable
    {
        void ApplyFreeze(float duration, float slowAmount);
    }

    /// <summary>可燃烧接口</summary>
    public interface IBurnable
    {
        void ApplyBurn(float damagePerSecond, float duration);
    }

    /// <summary>可嘲讽接口</summary>
    public interface ITauntable
    {
        void ApplyTaunt(GameObject taunter, float duration);
    }

    #endregion
}
