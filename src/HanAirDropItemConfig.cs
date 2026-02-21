
namespace HanAirDropS2;
public class HanAirDropItemConfig
{
    public class Item
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string PickSound { get; set; } = string.Empty;
        public float ItemProbability { get; set; } = 0.5f;
        public bool Enabled { get; set; } = true;
        public string Permissions { get; set; } = string.Empty;
    }
    public List<Item> ItemList { get; set; } = new List<Item>();
}