using UnityEngine;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// Boss血条UI接口
    /// </summary>
    public interface IBossHPBar
    {
        void UpdateHP(float currentHP, float maxHP);
        void UpdateBossName(string bossName);
        void UpdatePhase(string phaseName);
        void UpdateBarColor(Color color);
    }
}
