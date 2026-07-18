using System.Collections;
using NUnit.Framework;
using UnityClientSharp.Net;
using UnityClientSharp.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace BraveWorld.Tests.PlayMode
{
    /// <summary>登录流程 PlayMode 验证：离线输入校验 + Test Direct 在线端到端（需服务器 127.0.0.1:8889）。</summary>
    public class LoginFlowTests
    {
        [UnityTest]
        public IEnumerator Offline_ValidationBlocksEmptyInput()
        {
            // 无 NetworkManager：校验先行于连接检查
            var panel = LoginPanel.Create(null);
            yield return null;

            panel.LoginButton.onClick.Invoke();
            StringAssert.Contains("请输入用户名", panel.StatusText.text);

            panel.UserInput.text = "abc";
            panel.LoginButton.onClick.Invoke();
            StringAssert.Contains("请输入密码", panel.StatusText.text);

            panel.PassInput.text = "def";
            panel.LoginButton.onClick.Invoke();
            StringAssert.Contains("未连接到服务器", panel.StatusText.text);

            Object.Destroy(panel.gameObject);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TestDirect_ReachesMapInfo()
        {
            var netGo = new GameObject("Network");
            var nm = netGo.AddComponent<NetworkManager>();
            var smoke = netGo.AddComponent<NetworkSmokeTest>();
            smoke.RunOnStart = false;
            var panel = LoginPanel.Create(smoke);
            yield return null;

            panel.TestDirectButton.onClick.Invoke();

            // 直通五步（连接→登录→选服→进/建角色→MapInfo），每步 10s 超时
            float deadline = Time.realtimeSinceStartup + 45f;
            while (nm.Tiles.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.Greater(nm.Tiles.Count, 0, "Test Direct 未到达 MapInfoSync（服务器是否在跑？）");
            Assert.IsFalse(string.IsNullOrEmpty(nm.GatewayToken), "选服未拿到 GatewayToken");

            Object.Destroy(panel.gameObject);
            Object.Destroy(netGo);
            yield return null;
        }

        /// <summary>回归：登录页启动即自动连接，已连接状态下点 Test Direct 也必须走完（曾卡在等 Connected 超时）。</summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TestDirect_WhenAlreadyConnected_AlsoWorks()
        {
            var netGo = new GameObject("Network");
            var nm = netGo.AddComponent<NetworkManager>();
            yield return null;

            // 预连接（复现登录页真实场景：面板已自动连上，用户再点 Test Direct）
            bool connected = false;
            nm.Connected += () => connected = true;
            nm.ConnectToServer();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!connected && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(connected, "预连接失败（服务器是否在跑？）");

            var smoke = netGo.AddComponent<NetworkSmokeTest>();
            smoke.RunOnStart = false;
            var panel = LoginPanel.Create(smoke);
            yield return null;

            panel.TestDirectButton.onClick.Invoke();

            deadline = Time.realtimeSinceStartup + 45f;
            while (nm.Tiles.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Greater(nm.Tiles.Count, 0, "已连接状态下 Test Direct 未走完");

            Object.Destroy(panel.gameObject);
            Object.Destroy(netGo);
            yield return null;
        }
    }
}
