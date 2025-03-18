using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Fixes;

/* CONTEXT:
 * The antishadow onslaught's green-screen visual is overlayed over the entire game world scene, with non-green pixels becoming pure black.
 * This includes the map overlay.
 * To address this, a detour is used that disables the map overlay feature conditionally.
 */
public class MapStyleLockingSystem : ModSystem
{
    private static readonly List<SwitchMapStyleConditionSet> conditionSets = [];

    public record SwitchMapStyleConditionSet(int StyleToUse, bool DisableMapEntirely, Func<bool> UsageCondition);

    public override void OnModLoad()
    {
        On_Main.DrawInterface += DisableMapOverlay;
        On_Main.DrawMap += DisableMapOverlay2;
    }

    private void DisableMapOverlay(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime)
    {
        MapDisablingWrapper(() => orig(self, gameTime));
    }

    private void DisableMapOverlay2(On_Main.orig_DrawMap orig, Main self, GameTime gameTime)
    {
        MapDisablingWrapper(() => orig(self, gameTime));
    }

    private static void MapDisablingWrapper(Action action)
    {
        int oldMapStyle = Main.mapStyle;
        bool mapWasEnabled = Main.mapEnabled;
        float oldMapOverlayOpacity = Main.mapOverlayAlpha;
        float oldMapOverlayScale = Main.mapOverlayScale;
        foreach (SwitchMapStyleConditionSet set in conditionSets)
        {
            if (set.UsageCondition())
            {
                Main.mapStyle = set.StyleToUse;
                if (set.DisableMapEntirely)
                {
                    Main.mapEnabled = false;
                    Main.mapFullscreen = false;
                    Main.mapOverlayAlpha = 0f;
                    Main.mapOverlayScale = 0f;
                }
            }
        }

        try
        {
            action();
        }
        finally
        {
            Main.mapStyle = oldMapStyle;
            Main.mapEnabled = mapWasEnabled;
            Main.mapOverlayAlpha = oldMapOverlayOpacity;
            Main.mapOverlayScale = oldMapOverlayScale;
        }
    }

    /// <summary>
    /// Registers a map locking condition set.
    /// </summary>
    /// <param name="styleToUse">The style to lock to when the condition is true.</param>
    /// <param name="disableMapEntirely">Whether the style to should completely disable the map.</param>
    /// <param name="usageCondition">The locking condition.</param>
    public static void RegisterConditionSet(int styleToUse, bool disableMapEntirely, Func<bool> usageCondition) =>
        conditionSets.Add(new(styleToUse, disableMapEntirely, usageCondition));
}
