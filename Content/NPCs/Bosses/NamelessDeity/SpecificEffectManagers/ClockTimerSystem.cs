using NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class ClockTimerSystem : ModSystem
{
    private float effectiveTimeDirection;

    /// <summary>
    /// A general purpose time incrementer value that respects whether time is stopped or reversed by one of Nameless' clock constellations.
    /// </summary>
    public static float Time
    {
        get;
        private set;
    }

    public override void OnModLoad() => Main.OnPreDraw += IncrementTime;

    public override void OnModUnload() => Main.OnPreDraw -= IncrementTime;

    private void IncrementTime(Microsoft.Xna.Framework.GameTime obj)
    {
        float timeDirection = 1f;
        if (!Main.gameMenu)
        {
            int clockID = ModContent.ProjectileType<ClockConstellation>();
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.type == clockID && projectile.As<ClockConstellation>().TimeIsReversed)
                {
                    timeDirection = -1f;
                    break;
                }
            }
            if (ClockConstellation.TimeIsStopped)
                timeDirection = 0f;
        }

        effectiveTimeDirection = Lerp(effectiveTimeDirection, timeDirection, 0.3f);

        Time = (Time + effectiveTimeDirection * (1f - GameSceneSlowdownSystem.SlowdownInterpolant) / 60f) % 24000f;
    }
}
