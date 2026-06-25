using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// 全自动启动器（备用方案）
    /// 主启动逻辑已移至 GameManager.Awake()，确保一定执行。
    /// </summary>
    public static class AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            // GameManager 的 Awake 已经调用了 AutoBootstrapHelper.Bootstrap()
            // 这里不需要重复操作
            Debug.Log("[AutoBootstrap] RuntimeInitializeOnLoad 触发");
        }
    }
}
