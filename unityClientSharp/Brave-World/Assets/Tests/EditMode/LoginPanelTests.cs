using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.UI;

namespace BraveWorld.Tests
{
    /// <summary>登录面板纯逻辑：输入校验、自动选服策略（对齐 Godot LoginScene）。</summary>
    public class LoginPanelTests
    {
        [Test]
        public void ValidateInput_EmptyBlocked()
        {
            Assert.AreEqual("请输入用户名", LoginPanel.ValidateInput("", ""));
            Assert.AreEqual("请输入用户名", LoginPanel.ValidateInput(null, "x"));
            Assert.AreEqual("请输入密码", LoginPanel.ValidateInput("abc", ""));
            Assert.AreEqual("请输入密码", LoginPanel.ValidateInput("abc", null));
            Assert.IsNull(LoginPanel.ValidateInput("abc", "def"));
        }

        [Test]
        public void PickServerId_PrefersLastServer()
        {
            var servers = new List<Server.ServerInfo>
            {
                new Server.ServerInfo { ServerId = 1 },
                new Server.ServerInfo { ServerId = 2 },
            };
            Assert.AreEqual(2u, LoginPanel.PickServerId(servers, 2), "LastServerId 在列表中优先");
            Assert.AreEqual(1u, LoginPanel.PickServerId(servers, 99), "不在列表回退第一个服");
            Assert.AreEqual(0u, LoginPanel.PickServerId(new List<Server.ServerInfo>(), 1), "无服返回 0");
            Assert.AreEqual(0u, LoginPanel.PickServerId(null, 1));
        }
    }
}
