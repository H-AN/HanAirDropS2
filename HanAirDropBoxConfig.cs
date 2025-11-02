

using System;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using static HanAirDropS2.HanAirDropItemConfig;

namespace HanAirDropS2;
public class HanAirDropBoxConfig
{
    private ILogger<HanAirDropCreateBox> logger;
    private ISwiftlyCore Core;

    private static readonly Random _random = new Random();

    private HanAirDropItemConfig _airItemCFG = null!;

    public class Box
    {
        public string Name { get; set; }
        public string ModelPath { get; set; }
        public string DropSound { get; set; }
        public string Items { get; set; }
        public int TeamOnly { get; set; }
        public int RoundPickLimit { get; set; }
        public int SpawnPickLimit { get; set; }
        public float Probability { get; set; }
        public bool Enabled { get; set; }
        public int Code { get; set; }
        public string Flags { get; set; }
        public bool OpenGlow { get; set; }
        public string GlowColor { get; set; }
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