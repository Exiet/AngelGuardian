using UnityEngine;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 可受伤接口 - 所有可受到伤害的对象实现此接口
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float damage, DamageType type);
        Transform transform { get; }
        GameObject gameObject { get; }
    }
}
