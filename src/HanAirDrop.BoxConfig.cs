namespace HanAirDropS2;
public class HanAirDropBoxConfig
{
    public class Box
    {
        public string Name { get; set; } = string.Empty;
        public string ModelPath { get; set; } = string.Empty;
        public string DropSound { get; set; } = string.Empty;
        public string Items { get; set; } = string.Empty;
        public int TeamOnly { get; set; } = 0;
        public int RoundPickLimit { get; set; } = 0;
        public int SpawnPickLimit { get; set; } = 0;
        public float Probability { get; set; } = 0.5f;
        public bool Enabled { get; set; } = true;
        public int Code { get; set; } = 1;
        public string Flags { get; set; } = string.Empty;
        public bool OpenGlow { get; set; } = true;
        public string GlowColor { get; set; } = string.Empty;
    }
    public List<Box> BoxList { get; set; } = new List<Box>();
}