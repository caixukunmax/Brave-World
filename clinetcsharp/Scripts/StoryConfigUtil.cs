using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 剧情配置静态数据 — 从 Luban 导出的 JSON 加载（客户端专用）
    /// </summary>
    public static class StoryConfigUtil
    {
        public static readonly Dictionary<int, StoryChapterJsonRow> Chapters = new();
        public static readonly Dictionary<int, List<StoryDialogueJsonRow>> DialoguesByChapter = new();

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            LoadChapters();
            LoadDialogues();

            GD.Print($"[StoryConfigUtil] Loaded {Chapters.Count} chapters, {DialoguesByChapter.Values.Sum(list => list.Count)} dialogues");
        }

        private static void LoadChapters()
        {
            try
            {
                var file = FileAccess.Open("res://data/story_chapter_config.json", FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr("[StoryConfigUtil] story_chapter_config.json not found");
                    return;
                }

                string json = file.GetAsText();
                file.Close();

                var rows = JsonSerializer.Deserialize<List<StoryChapterJsonRow>>(json);
                if (rows == null) return;

                foreach (var row in rows)
                {
                    if (row.id == 0)
                        continue;
                    Chapters[row.id] = row;
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[StoryConfigUtil] Failed to load chapters: {ex.Message}");
            }
        }

        private static void LoadDialogues()
        {
            try
            {
                var file = FileAccess.Open("res://data/story_dialogue_config.json", FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr("[StoryConfigUtil] story_dialogue_config.json not found");
                    return;
                }

                string json = file.GetAsText();
                file.Close();

                var rows = JsonSerializer.Deserialize<List<StoryDialogueJsonRow>>(json);
                if (rows == null) return;

                foreach (var row in rows)
                {
                    if (row.id == 0)
                        continue;
                    if (!DialoguesByChapter.TryGetValue(row.chapter_id, out var list))
                    {
                        list = new List<StoryDialogueJsonRow>();
                        DialoguesByChapter[row.chapter_id] = list;
                    }
                    list.Add(row);
                }

                foreach (var list in DialoguesByChapter.Values)
                    list.Sort((a, b) => a.sequence.CompareTo(b.sequence));
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[StoryConfigUtil] Failed to load dialogues: {ex.Message}");
            }
        }

        public static string GetChapterTitle(int chapterId)
        {
            return Chapters.TryGetValue(chapterId, out var chapter) ? chapter.title ?? "" : "";
        }

        public static IReadOnlyList<StoryDialogueJsonRow> GetDialogues(int chapterId)
        {
            return DialoguesByChapter.TryGetValue(chapterId, out var list) ? list : System.Array.Empty<StoryDialogueJsonRow>();
        }

        public class StoryChapterJsonRow
        {
            public int id { get; set; }
            public string title { get; set; } = "";
        }

        public class StoryDialogueJsonRow
        {
            public int id { get; set; }
            public int chapter_id { get; set; }
            public int sequence { get; set; }
            public string speaker { get; set; } = "";
            public string content { get; set; } = "";
        }
    }
}
