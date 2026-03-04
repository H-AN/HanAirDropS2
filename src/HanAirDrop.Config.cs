namespace HanAirDropS2;
public class HanAirDropConfig
{
    public bool AirDropEnble { get; set; } = true; 
    public int AirDropMode { get; set; } = 0;
    public int AirDropPosMode { get; set; } = 0; 
    public float DeathDropPercent { get; set; } = 0.1f;
    public float AirDropTimer { get; set; } = 60.0f; 
    public float AirDropKillTimer { get; set; } = 20.0f; 
    public string AirDropName { get; set; } = "AirdropA,AirdropB,AirdropC"; 
    public int PlayerPickEachRound { get; set; } = 0; 
    public int AirDropSpawnMode { get; set; } = 0;   
    public int AirDropCount { get; set; } = 3; 
    public int AirDropDynamicCount { get; set; } = 1; 
    public int AirDropPlayerCount { get; set; } = 1;   
    public string PrecacheSoundEvent { get; set; } = "soundevents/vo/announcer/game_sounds_cs2_classic.vsndevts,soundevents/vo/agents/game_sounds_balkan_epic.vsndevts,soundevents/game_sounds_ui.vsndevts";   
    public string BlockPickUpSoundEvent { get; set; } = "vsnd_files_track_01"; 
    public string AdminCommand { get; set; } = "sw_createbox"; 
    public int Openrandomspawn { get; set; } = 0; 
    public string AdminCommandFlags { get; set; } = string.Empty; 
    public string AdminSelectBoxCommand { get; set; } = "sw_selectbox"; 
    public string AdminSelectBoxCommandFlags { get; set; } = string.Empty; 
    public int AdminSelectBoxCount { get; set; } = 10; 
    public float AdminSelectBoxColdCown { get; set; } = 1.0f;

}