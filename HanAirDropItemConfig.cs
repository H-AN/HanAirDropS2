using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace HanAirDropS2;
public class HanAirDropItemConfig
{
    public class Item
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string PickSound { get; set; }
        public float ItemProbability { get; set; }
        public bool Enabled { get; set; }
        public string Permissions { get; set; }
    }


    public List<Item> ItemList { get; set; } = new List<Item>();
    /*
    {
        new Item
        { 
            Name = "AK-47",
            Command = "sw_9lGNxrYEnUNQmyCi",
            PickSound = "",
            ItemProbability = 0.5f,
            Enabled = true,
            Permissions = ""
        },
        new Item
        {
            Name = "M4A1",
            Command = "sw_WTLFKthNbj4w4Rmi",
            PickSound = "",
            ItemProbability = 0.5f,
            Enabled = true,
            Permissions = ""
        },
        new Item
        {
            Name = "Nevgev",
            Command = "sw_I3M3NEJqvE7ifF4v",
            PickSound = "",
            ItemProbability = 0.5f,
            Enabled = true,
            Permissions = "admin.vip"
        }
    };
    */


}