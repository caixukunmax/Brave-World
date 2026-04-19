using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 认证检查器 - 检查登录状态并重定向
    /// </summary>
    public partial class AuthChecker : Node
    {
        public override void _Ready()
        {
            // 如果没有登录（没有 account_token），强制跳转到登录界面
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || string.IsNullOrEmpty(nm.AccountToken))
            {
                GD.Print("[AuthChecker] 未登录，跳转到登录界面");
                GetTree().ChangeSceneToFile("res://scenes/login_scene.tscn");
                return;
            }

            // 恢复综合面板位置
            var panel = GetNodeOrNull<IntegratedPanel>("../IntegratedPanelCanvas/IntegratedPanel");
            if (panel != null && nm.CachedRoleInfo != null)
            {
                var info = nm.CachedRoleInfo;
                panel.RestorePosition(info.UiPanelPosX, info.UiPanelPosY, info.UiPanelWidth, info.UiPanelHeight);
            }
        }
    }
}
