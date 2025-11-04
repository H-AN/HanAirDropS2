

using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using static HanAirDropS2.HanAirDropItemConfig;

namespace HanAirDropS2;
public class HanAirDropBoxConfig
{
    private static readonly Random _random = new Random();
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
    
    // 随机选择空投配置
    // 根据AirDropName筛选并随机选择
    public Box? SelectRandomBoxConfig(string allowedNames, ILogger? logger = null)
    {
        var allowedNameList = allowedNames.Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        var availableBoxes = BoxList
            .Where(b => b.Enabled && allowedNameList.Contains(b.Name))
            .ToList();

        if (!availableBoxes.Any())
        {
            logger?.LogWarning("没有可用的空投配置: {AllowedNames}", allowedNames);
            return null;
        }

        float totalProb = availableBoxes.Sum(b => b.Probability);
        float randomPoint = (float)_random.NextDouble() * totalProb;

        float cumulative = 0f;
        foreach (var box in availableBoxes)
        {
            cumulative += box.Probability;
            if (randomPoint <= cumulative)
                return box;
        }

        return availableBoxes.Last();
    }


    // 按概率选择道具
    public Item? SelectByProbability(List<Item> itemConfig)
    {
        if (itemConfig == null || itemConfig.Count == 0)
            return null;

        float totalProb = itemConfig.Sum(i => i.ItemProbability);
        float randomPoint = (float)_random.NextDouble() * totalProb;

        float cumulative = 0f;
        foreach (var item in itemConfig)
        {
            cumulative += item.ItemProbability;
            if (randomPoint <= cumulative)
                return item;
        }

        return itemConfig.LastOrDefault();
    }


}