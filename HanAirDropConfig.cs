namespace HanAirDropS2;
public class HanAirDropConfig
{
    public bool AirDropEnble { get; set; } = true; //是否开启 Whether air drop is enabled  
    public int AirDropMode { get; set; } = 0; //掉落模式 0 随时间生成 1 玩家死亡生成 2 两种均开启 Drop mode: 0 - Spawn over time, 1 - Spawn on player death, 2 - Both enabled
    public int AirDropPosMode { get; set; } = 0; //掉落模式为 随时间时 掉落位置 配置 0 ct t 复活点随机 1 仅ct 复活点 2仅 t复活点  When drop mode is "over time," position config: 0 - Random CT/T spawn, 1 - Only CT spawn, 2 - Only T spawn  
    public float DeathDropPercent { get; set; } = 0.1f; //死亡掉落的几率  Probability of drop on death  
    public float AirDropTimer { get; set; } = 60.0f; //随时间生成的 间隔秒 Time interval (seconds) for timed spawns  
    public float AirDropKillTimer { get; set; } = 20.0f; //存在的时间 多少秒后自动删除消失 Lifetime (seconds) before auto-removal  
    public string AirDropName { get; set; } = "AirdropA,AirdropB,AirdropC"; //可以掉落的空投名称  Name of the droppable air supply  
    public int PlayerPickEachRound { get; set; } = 0; //玩家每回合可以拾取空投的次数限制 0 无限  Player pickup limit per round (0 = unlimited)  
    public int AirDropSpawnMode { get; set; } = 0; //空投生成模式 0 固定模式 1 根据玩家数量动态生成模式 Spawn mode: 0 - Fixed amount, 1 - Dynamic based on player count  
    public int AirDropCount { get; set; } = 3; //固定模式 一次生成多少个 Fixed mode: number of drops per spawn  
    public int AirDropDynamicCount { get; set; } = 1; //动态模式 根据玩家数量 生成 几个 假设填写 1 Dynamic mode: base multiplier (e.g., if 1)  
    public int AirDropPlayerCount { get; set; } = 1; //动态模式 每几个玩家 假设 AirDropPlayerCount  填写 2 AirDropDynamicCount 填写 1 则 服务器 玩家 / 2 * 1 = 生成数量 少于2 生成 1 Dynamic mode: divisor (e.g., if AirDropPlayerCount=2 and AirDropDynamicCount=1, spawn count = (player count / 2) * 1; min 1 if <2)  
    public string PrecacheSoundEvent { get; set; } = "soundevents/vo/announcer/game_sounds_cs2_classic.vsndevts,soundevents/vo/agents/game_sounds_balkan_epic.vsndevts,soundevents/game_sounds_ui.vsndevts"; //预缓存soundevent Precached sound event  
    public string BlockPickUpSoundEvent { get; set; } = "vsnd_files_track_01"; //无法拾取播放音效 
    public string AdminCommand { get; set; } = "sw_createbox"; //自定义管理员召唤空投的命令 Custom admin command to summon an airdrop
    public int Openrandomspawn { get; set; } = 0; //打开deathmatch 生成点 用于随机生成
    public string AdminCommandFlags { get; set; } = string.Empty; //管理员召唤随机空投所需要的Flags
    public string AdminSelectBoxCommand { get; set; } = "sw_selectbox"; //召唤自选空投箱命令
    public string AdminSelectBoxCommandFlags { get; set; } = string.Empty; //召唤自选空投箱的权限
    public int AdminSelectBoxCount { get; set; } = 10; //限制恶意生成数量过多炸服
    public float AdminSelectBoxColdCown { get; set; } = 1.0f;

}