using MongoDB.Bson.Serialization.Attributes;

namespace GameServer.Database.Models;

/// <summary>
/// 按职业保存的技能数据
/// </summary>
public class JobSkillData
{
    [BsonElement("learned_skills")]  public List<int> LearnedSkills  { get; set; } = new();
    [BsonElement("equipped_skills")] public List<int> EquippedSkills { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class Role
{
    [BsonElement("role_id")]         public long   RoleId        { get; set; }
    [BsonElement("account_id")]      public long   AccountId     { get; set; }
    [BsonElement("server_id")]       public int    ServerId      { get; set; }
    [BsonElement("role_name")]       public string RoleName      { get; set; } = "";
    [BsonElement("level")]           public int    Level         { get; set; }
    [BsonElement("exp")]             public int    Exp           { get; set; }
    [BsonElement("avatar_id")]       public int    AvatarId      { get; set; }
    [BsonElement("gold")]            public long   Gold          { get; set; }
    [BsonElement("diamond")]         public long   Diamond       { get; set; }
    [BsonElement("total_power")]     public long   TotalPower    { get; set; }
    [BsonElement("vip_level")]       public int    VipLevel      { get; set; }
    [BsonElement("create_time")]     public int    CreateTime    { get; set; }
    [BsonElement("last_login_time")] public int    LastLoginTime { get; set; }
    [BsonElement("job")]             public string Job           { get; set; } = "";
    [BsonElement("title")]           public string Title         { get; set; } = "";
    [BsonElement("status")]          public string Status        { get; set; } = "";
    [BsonElement("current_map")]     public string CurrentMap    { get; set; } = "xinshoucun";
    [BsonElement("grid_x")]          public int    GridX         { get; set; }
    [BsonElement("grid_y")]          public int    GridY         { get; set; }
    [BsonElement("hp")]              public int    Hp            { get; set; }
    [BsonElement("max_hp")]          public int    MaxHp         { get; set; }
    [BsonElement("mp")]              public int    Mp            { get; set; }
    [BsonElement("max_mp")]          public int    MaxMp         { get; set; }
    [BsonElement("agility")]         public int    Agility       { get; set; }
    [BsonElement("patk")]            public int    Patk          { get; set; }
    [BsonElement("matk")]            public int    Matk          { get; set; }
    [BsonElement("pdef")]            public int    Pdef          { get; set; }
    [BsonElement("mdef")]            public int    Mdef          { get; set; }
    [BsonElement("mp_regen")]         public int    MpRegen       { get; set; }
    [BsonElement("ui_panel_pos_x")]  public float  UiPanelPosX   { get; set; }
    [BsonElement("ui_panel_pos_y")]  public float  UiPanelPosY   { get; set; }
    [BsonElement("ui_panel_width")]  public float  UiPanelWidth  { get; set; }
    [BsonElement("ui_panel_height")] public float  UiPanelHeight { get; set; }
    [BsonElement("move_speed_ms")]   public int    MoveSpeedMs   { get; set; }
    [BsonElement("learned_skills")]  public List<int> LearnedSkills  { get; set; } = new();
    [BsonElement("equipped_skills")] public List<int> EquippedSkills { get; set; } = new();
    [BsonElement("job_skills")]      public Dictionary<string, JobSkillData> JobSkills { get; set; } = new();
}
