using Microsoft.Xna.Framework;
using NoxusBoss.Core.Graphics.Shaders.Screen;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.BackgroundManagement;

public class BackgroundManagerSkyScene : ModSceneEffect
{
    public override bool IsSceneEffectActive(Player player) => BackgroundManager.AnyActive;

    public override void Load()
    {
        Filters.Scene["NoxusBoss:CustomSkies"] = new Filter(new GenericScreenShaderData("FilterMiniTower").UseColor(Color.Transparent).UseOpacity(0f), EffectPriority.VeryHigh);
        SkyManager.Instance["NoxusBoss:CustomSkies"] = new BackgroundManagerSky();
    }

    public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals("NoxusBoss:CustomSkies", isActive);
}

public class BackgroundManager : ModSystem
{
    /// <summary>
    /// Whether any backgrounds are currently active.
    /// </summary>
    public static bool AnyActive => ModContent.GetContent<Background>().Any(b => b.IsActive);

    public override void SetStaticDefaults()
    {
        foreach (Background background in ModContent.GetContent<Background>())
            background.SetStaticDefaults();
    }

    /// <summary>
    /// Renders all currently active backgrounds.
    /// </summary>
    public static void Render(float minDepth, float maxDepth)
    {
        if (Main.gameMenu)
            return;

        IOrderedEnumerable<Background> orderedBackgrounds = ModContent.GetContent<Background>().Where(b => b.IsActive).OrderBy(b => b.Priority);
        Vector2 backgroundSize = ViewportSize;
        foreach (Background background in orderedBackgrounds)
            background.Render(backgroundSize, minDepth, maxDepth);
    }

    public override void PostUpdateEverything()
    {
        foreach (Background background in ModContent.GetContent<Background>())
        {
            background.Update();
            background.ShouldBeActive = false;
        }
    }

    public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
    {
        IOrderedEnumerable<Background> orderedBackgrounds = ModContent.GetContent<Background>().Where(b => b.IsActive).OrderBy(b => b.Priority);
        foreach (Background background in orderedBackgrounds)
            background.ModifyLightColors(ref backgroundColor, ref tileColor);
    }
}
