using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    [Tool]
    public partial class EditorScreenshotTool : EditorScript
    {
        public override void _Run()
        {
            var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
            if (sceneRoot == null)
            {
                GD.Print("Error: No scene open");
                return;
            }

            var screenshotDir = "res://screenshots";
            var dir = DirAccess.Open("res://");
            if (!dir.DirExists("screenshots"))
                dir.MakeDir("screenshots");

            var datetime = Time.GetDatetimeDictFromSystem();
            var timestamp = $"{datetime["year"]:D4}{datetime["month"]:D2}{datetime["day"]:D2}_{datetime["hour"]:D2}{datetime["minute"]:D2}{datetime["second"]:D2}";
            var filename = $"screenshot_{timestamp}.png";
            var filepath = $"{screenshotDir}/{filename}";

            var sceneInfo = GatherSceneInfo(sceneRoot);
            var infoFilepath = $"{screenshotDir}/{timestamp}_info.txt";
            SaveInfoFile(infoFilepath, sceneInfo);

            GD.Print("Scene screenshot prepared");
            GD.Print("Scene name: " + sceneRoot.Name);
            GD.Print("Debug info saved to: " + infoFilepath);
            GD.Print("");
            GD.Print("Please manually capture screenshot and save to: " + filepath);
        }

        private Dictionary GatherSceneInfo(Node root)
        {
            var info = new Dictionary
            {
                ["scene_name"] = root.Name,
                ["node_count"] = CountNodes(root),
                ["player_info"] = new Dictionary(),
                ["camera_info"] = new Dictionary(),
                ["grid_info"] = new Dictionary()
            };

            var players = FindNodesByGroup(root, "player");
            if (players.Count > 0)
            {
                var player = players[0];
                info["player_info"] = new Dictionary
                {
                    ["position"] = player.Get("position"),
                    ["grid_pos"] = player.Get("grid_pos"),
                    ["character_name"] = player.Get("character_name")
                };
            }

            var cameras = FindNodesByGroup(root, "camera");
            if (cameras.Count > 0)
            {
                var camera = cameras[0];
                info["camera_info"] = new Dictionary
                {
                    ["zoom"] = camera.Get("zoom"),
                    ["position"] = camera.Get("position")
                };
            }

            var grids = FindNodesByGroup(root, "grid_manager");
            if (grids.Count > 0)
            {
                var grid = grids[0];
                info["grid_info"] = new Dictionary
                {
                    ["grid_size"] = grid.Get("grid_size")
                };
            }

            return info;
        }

        private int CountNodes(Node node)
        {
            int count = 1;
            foreach (Node child in node.GetChildren())
                count += CountNodes(child);
            return count;
        }

        private Array<Node> FindNodesByGroup(Node root, string groupName)
        {
            var result = new Array<Node>();
            if (root.IsInGroup(groupName))
                result.Add(root);
            foreach (Node child in root.GetChildren())
                result.AddRange(FindNodesByGroup(child, groupName));
            return result;
        }

        private void SaveInfoFile(string filepath, Dictionary info)
        {
            var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Write);
            if (file == null) return;

            file.StoreLine("Scene Analysis Data");
            file.StoreLine("Generated: " + Time.GetDatetimeStringFromSystem());
            file.StoreLine("");
            file.StoreLine("Scene name: " + info["scene_name"]);
            file.StoreLine("Total nodes: " + info["node_count"]);
            file.Close();
        }
    }
}
