namespace HanAirDropS2;

public class HanAirDropGlobals
{
    public CancellationTokenSource? MapStartDropTimer { get; set; } = null;
    public Dictionary<ulong, DateTime> AdminCreateBoxCooldown = new();

    public Dictionary<uint, AirBoxData> BoxData = new();

    public string physicsBox = "models/generic/crate_plastic_01/crate_plastic_01_bottom.vmdl";
    //public string physicsBox = "models/de_inferno/inferno_winebar_interior_01/inferno_winebar_crate_01_a.vmdl";

    // 使用字典存储每个箱子的限制
    public int[] PlayerPickUpLimit = new int[65];
    public Dictionary<int, Dictionary<int, int>> PlayerRoundPickUpLimit = new(); // [玩家Slot][箱子Code] = 剩余次数
    public Dictionary<int, Dictionary<int, int>> PlayerSpawnPickUpLimit = new(); // [玩家Slot][箱子Code] = 剩余次数

    public class AirBoxData
    {
        public int Code { get; set; }
        public string[] Items { get; set; } = Array.Empty<string>();
        public int TeamOnly { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DropSound { get; set; } = string.Empty;
        public int RoundPickLimit { get; set; }
        public int SpawnPickLimit { get; set; }
        public string Flags { get; set; } = string.Empty;
        public bool OpenGlow { get; set; }
    }

}