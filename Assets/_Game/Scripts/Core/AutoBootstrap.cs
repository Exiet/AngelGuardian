using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// 全自动启动器 —— 零操作，Play即运行。
    /// 
    /// [RuntimeInitializeOnLoadMethod] 确保在任何场景加载后自动执行。
    /// 你只需要：打开任意场景 → 点击 Play
    /// </summary>
    public static class AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            // 执行共享的启动逻辑
            AutoBootstrapHelper.Bootstrap();

            // 延迟启动游戏
            var gm = GameManager.Instance;
            gm.StartCoroutine(DelayedStart(gm));
        }

        private static System.Collections.IEnumerator DelayedStart(GameManager gm)
        {
            yield return null;
            yield return null;
            gm.StartGame();
            Debug.Log("[AutoBootstrap] ▶ 游戏自动开始！");
        }
    }
}
