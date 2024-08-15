global using Luminance.Core.Graphics;
global using static System.MathF;
global using static Luminance.Common.Utilities.Utilities;
global using static Luminance.Core.Graphics.ScreenShakeSystem;
global using static Microsoft.Xna.Framework.MathHelper;
global using static NoxusBoss.Assets.CommonSoundsRegistry;
global using static NoxusBoss.Assets.MiscTexturesRegistry;
global using static NoxusBoss.Common.Utilities.Utilities;
using Luminance.Core.ModCalls;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss
{
    public class NoxusBoss : Mod
    {
        // If this is enabled, various development-specific tools are operational, such as the automatic shader compiler and the keyboard shader debug drawer.
        // If it's disabled, they do not run at all, and where possible don't even load in the first place.
        // This means that this isn't something that other mods can just turn by flipping this property to true, since it will be too late for
        // automatic loading to happen.
        // The reason for this is mainly performance. No use having a bunch of resources in the background on the 1/100 chance a single person actually cares.
#if DEBUG
        public static bool DebugFeaturesEnabled
        {
            get;
            private set;
        } = true;
#endif

#if !DEBUG
        public static bool DebugFeaturesEnabled
        {
            get;
            private set;
        } = false; // this needs to be set to false
#endif

        public static Mod Instance
        {
            get;
            private set;
        }

        public override void Load()
        {
            Instance = this;

            if (Main.netMode != NetmodeID.Server)
                LoadParticlesForCalamity();
        }

        private static void LoadParticlesForCalamity()
        {
            Main.QueueMainThreadAction(() =>
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
                    cal?.Call("LoadParticleInstances", Instance);
            });
        }

        // Defer mod-call interpretation to a separate class.
        public override object Call(params object[] args) => ModCallManager.ProcessAllModCalls(this, args);
    }
}
