using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;


namespace HanAirDropS2;
public class HanAirDropGlow
{

    private ILogger<HanAirDropGlow> _logger;
    private ISwiftlyCore Core;

    public HanAirDropGlow(ISwiftlyCore core, ILogger<HanAirDropGlow> logger)
    {
        Core = core;
        _logger = logger;
    }
    //设置发光 
    public void SetGlow(CBaseEntity entity, int ColorR, int ColorG, int ColorB, int ColorA)
    {

        CBaseModelEntity modelGlow = Core.EntitySystem.CreateEntity<CBaseModelEntity>();
        CBaseModelEntity modelRelay = Core.EntitySystem.CreateEntity<CBaseModelEntity>();

        if (modelGlow == null || modelRelay == null)
            return;

        string modelName = entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;

        modelRelay.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags = 256u;
        modelRelay.RenderMode = RenderMode_t.kRenderNone;
        modelRelay.DispatchSpawn();

        modelGlow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u;
        modelGlow.DispatchSpawn();

        modelGlow.Glow.GlowColorOverride = new SwiftlyS2.Shared.Natives.Color(ColorR, ColorG, ColorB, ColorA);
        modelGlow.Glow.GlowRange = 5000;
        modelGlow.Glow.GlowTeam = -1;
        modelGlow.Glow.GlowType = 3;
        modelGlow.Glow.GlowRangeMin = 100;

        modelRelay.AcceptInput("FollowEntity", "!activator", entity, modelRelay);
        modelGlow.AcceptInput("FollowEntity", "!activator", modelRelay, modelGlow);

    }

    public static bool TryParseColor(string colorStr, out SwiftlyS2.Shared.Natives.Color color, SwiftlyS2.Shared.Natives.Color defaultColor)
    {
        // 默认返回预设颜色
        color = defaultColor;

        // 1. 检查空值
        if (string.IsNullOrWhiteSpace(colorStr))
            return false;

        // 2. 分割字符串
        var parts = colorStr.Split(',');

        // 3. 检查最小长度
        if (parts.Length < 4)
            return false;

        // 严格按照ARGB顺序解析
        if (!TryParseColorComponent(parts[0], out int r) ||  // Red
            !TryParseColorComponent(parts[1], out int g) ||  // Green
            !TryParseColorComponent(parts[2], out int b) ||  // Blue
            !TryParseColorComponent(parts[3], out int a))    // Alpha

        {
            return false;
        }

        color = new SwiftlyS2.Shared.Natives.Color(r, g, b, a);
        return true;
    }

    // 辅助方法：解析单个颜色分量（0-255）
    public static bool TryParseColorComponent(string str, out int value)
    {
        value = 0;
        if (!int.TryParse(str, out int tmp) || tmp < 0 || tmp > 255)
            return false;

        value = tmp;
        return true;
    }

}
