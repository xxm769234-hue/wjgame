using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 网络请求工具：地块建造发包、接收服务端回调（无GameManager依赖版）
/// </summary>
public static class NetRequest
{
    // 临时挂载协程的空物体，全局唯一
    private static MonoBehaviour coroutineRunner;

    // 初始化协程载体
    private static void InitRunner()
    {
        if (coroutineRunner != null) return;
        GameObject runnerObj = new GameObject("NetCoroutineRunner");
        Object.DontDestroyOnLoad(runnerObj);
        coroutineRunner = runnerObj.AddComponent<NetCoroutineHolder>();
    }

    /// <summary>
    /// 发送地块建造请求给服务端
    /// </summary>
    /// <param name="x">网格X</param>
    /// <param name="y">网格Y</param>
    /// <param name="landTypeId">地块ID grass/sea等</param>
    /// <param name="OnSuccess">服务端返回成功回调</param>
    /// <param name="OnFail">服务端返回失败回调</param>
    public static void SendBuildLandRequest(int x, int y, string landTypeId, System.Action OnSuccess, System.Action<string> OnFail)
    {
        InitRunner();
        // ========== 【这里替换成真实后端网络请求代码】 ==========
        // 示例：http/post /buildLand 参数 {x,y,landType}
        // 收到后端返回code=0 执行OnSuccess，code!=0执行OnFail("错误文本")

        // 本地模拟延迟发包（模拟网络等待）
        Debug.Log($"【发包】请求建造地块 X:{x} Y:{y} 地块类型:{landTypeId}");
        coroutineRunner.StartCoroutine(SimulateNetDelay(x, y, landTypeId, OnSuccess, OnFail));
    }

    // 模拟网络延迟协程
    private static IEnumerator SimulateNetDelay(int x, int y, string landTypeId, System.Action OnSuccess, System.Action<string> OnFail)
    {
        yield return new WaitForSeconds(0.3f);
        // 模拟服务端校验（和客户端本地校验逻辑一致，防止作弊）
        LandConfig cfg = ConfigManager.Instance.GetLand(landTypeId);
        bool canBuild = LandManager.Instance.CheckCanPlaceTile(x, y, cfg);
        if (canBuild)
        {
            // 服务端校验通过，回调成功
            OnSuccess?.Invoke();
        }
        else
        {
            // 服务端校验失败，返回错误
            OnFail?.Invoke("服务端校验：无法在此放置地块");
        }
    }
}

// 专门用来承载协程的空脚本，不需要手动挂载
public class NetCoroutineHolder : MonoBehaviour
{

}
