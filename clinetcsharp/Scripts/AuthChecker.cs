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
            }
        }
    }
}
