using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoMod.Cil;

using MonoStereo;
using MonoStereo.Filters;

using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound.MonoStereo;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.Utilities;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;

public class GameSceneSlowdownSystem : ModSystem
{
    // Needs to be separate to not attempt to load when the parent ModSystem is
    // loaded.
    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    private static class MonoStereoSupport
    {
        /// <summary>
        /// The filter responsible for pitching down music based on slowdown.
        /// </summary>
        public static PitchShiftFilter PitchFilter
        {
            get;
            internal set;
        }
    }
    
    private static bool musicBeingAffected;

    private static readonly List<SlowdownEffectCondition> slowdownEffects = [];

    /// <summary>
    /// How much the game should be slowed down, as a 0-1 interpolant.
    /// </summary>
    public static float SlowdownInterpolant
    {
        get;
        private set;
    }

    /// <summary>
    /// How much the game, with the exception of players, Mars, and Solyn should be greyscaled, as a 0-1 interpolant.
    /// </summary>
    public static float GreyscaleInterpolant
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target that dictates what shouldn't be greyscaled.
    /// </summary>
    public static ManagedRenderTarget GreyscaleExclusionTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// Represents a slowdown effect condition and its associated functions which calculate how much the greyscale/slowdown effect should apply.
    /// </summary>
    /// <param name="SlowdownInterpolantFunction">The function which dictates how far the slowdown effect should go.</param>
    /// <param name="GreyscaleInterpolantFunction">The function which dictates how far the greyscale effect should go.</param>
    /// <param name="IsActiveFunction">Whether the slowdown effect should apply or not.</param>
    public record SlowdownEffectCondition(bool AffectsMusic, Func<float> SlowdownInterpolantFunction, Func<float> GreyscaleInterpolantFunction, Func<bool> IsActiveFunction);

    public override void OnModLoad()
    {
        On_Projectile.UpdatePosition += SlowDownProjectiles;
        On_NPC.UpdateCollision += SlowDownNPCs_TileCollision;
        IL_NPC.UpdateNPC_Inner += SlowDownNPCs_NoTileCollision;
        On_Player.CheckDrowning += SlowDownPlayers_SlowDownVelocity;
        On_Player.SetItemAnimation += SlowDownItemUsage;

        AudioReversingSystem.FreezingConditionEvent += FreezeMusicDuringGreyscale;

        if (Main.netMode != NetmodeID.Server)
        {
            GreyscaleExclusionTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
            RenderTargetManager.RenderTargetUpdateLoopEvent += UpdateTarget;
        }
    }

    public override void OnWorldLoad() => GreyscaleInterpolant = 0f;

    public override void OnWorldUnload() => GreyscaleInterpolant = 0f;

    public override void PostSetupContent()
    {
        if (Main.netMode != NetmodeID.Server && MonoStereoSystem.Enabled)
            MonoStereoInitialize();
    }

    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    private static void MonoStereoInitialize()
    {
        MonoStereoSupport.PitchFilter = new(1f);
        AudioManager.MusicMixer?.AddFilter(MonoStereoSupport.PitchFilter);
    }

    private bool FreezeMusicDuringGreyscale() => GreyscaleInterpolant >= 0.8f;

    private void SlowDownNPCs_NoTileCollision(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(i => i.MatchCallOrCallvirt<NPC>("UpdateCollision")))
            return;
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Entity>("velocity")))
            return;

        cursor.EmitDelegate((Vector2 velocity) =>
        {
            return velocity * (1f - SlowdownInterpolant);
        });
    }

    private void SlowDownProjectiles(On_Projectile.orig_UpdatePosition orig, Projectile self, Vector2 wetVelocity)
    {
        wetVelocity *= 1f - SlowdownInterpolant;
        Vector2 oldVelocity = self.velocity;

        self.velocity *= 1f - SlowdownInterpolant;
        orig(self, wetVelocity);
        self.velocity = oldVelocity;
    }

    private void SlowDownNPCs_TileCollision(On_NPC.orig_UpdateCollision orig, NPC self)
    {
        // Obscure edge case: Not doing this results in extremely small imprecisions in the velocity, resulting in
        // town NPCs sliding when they walk due to ""falling"" (aka having a tiny amount of downward velocity).
        if (SlowdownInterpolant <= 0.001f)
        {
            orig(self);
            return;
        }

        Vector2 oldVelocity = self.velocity;
        self.velocity *= 1f - SlowdownInterpolant;
        orig(self);
        self.velocity = oldVelocity;
    }

    private void SlowDownPlayers_SlowDownVelocity(On_Player.orig_CheckDrowning orig, Player self)
    {
        orig(self);
        self.velocity *= 1f - SlowdownInterpolant;

        if (SlowdownInterpolant >= 0.1f)
        {
            self.eyeHelper.TimeInState = 0;
            self.mount?.Dismount(self);
        }
    }

    private void SlowDownItemUsage(On_Player.orig_SetItemAnimation orig, Player self, int frames)
    {
        float slowdownFactor = 1f / (1.001f - SlowdownInterpolant);
        if (slowdownFactor > 5f)
            slowdownFactor = 5f;

        frames = (int)Round(frames * slowdownFactor);
        orig(self, frames);
    }

    private static void UpdateTarget()
    {
        if (GreyscaleInterpolant <= 0f || Main.gameMenu)
            return;

        GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
        graphicsDevice.SetRenderTarget(GreyscaleExclusionTarget);
        graphicsDevice.Clear(Color.Transparent);

        List<Player> activePlayers = Main.player.Take(Main.maxPlayers).Where(p => p.active).ToList();
        Main.PlayerRenderer.DrawPlayers(Main.Camera, activePlayers);
        Main.spriteBatch.ResetToDefault(false);

        int solynID = ModContent.NPCType<BattleSolyn>();
        int marsID = ModContent.NPCType<MarsBody>();
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.type == solynID || npc.type == marsID)
                Main.instance.DrawNPC(npc.whoAmI, false);
        }

        int gleamID = ModContent.ProjectileType<SolynTagTeamChargeUp>();
        int beamID = ModContent.ProjectileType<SolynTagTeamBeam>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type == gleamID || projectile.type == beamID)
                Main.instance.DrawProj(projectile.whoAmI);
        }

        Main.spriteBatch.End();
    }

    /// <summary>
    /// Registers a slowdown effect condition and its associated functions which calculate how much the greyscale/slowdown effect should apply.
    /// </summary>
    /// <param name="affectsMusic">Whether the slowdown effect should mess with music pitch.</param>
    /// <param name="slowdownInterpolantFunction">The function which dictates how far the slowdown effect should go.</param>
    /// <param name="greyscaleInterpolantFunction">The function which dictates how far the greyscale effect should go.</param>
    /// <param name="isActiveFunction">Whether the slowdown effect should apply or not.</param>
    public static void RegisterConditionalEffect(bool affectsMusic, Func<float> slowdownInterpolantFunction, Func<float> greyscaleInterpolantFunction, Func<bool> isActiveFunction) =>
        slowdownEffects.Add(new SlowdownEffectCondition(affectsMusic, slowdownInterpolantFunction, greyscaleInterpolantFunction, isActiveFunction));

    public override void PreUpdateEntities()
    {
        musicBeingAffected = false;

        float idealSlowdownInterpolant = 0f;
        float idealGreyscaleInterpolant = 0f;
        foreach (SlowdownEffectCondition effect in slowdownEffects)
        {
            if (!effect.IsActiveFunction())
                continue;

            idealSlowdownInterpolant = MathF.Max(idealSlowdownInterpolant, effect.SlowdownInterpolantFunction());
            idealGreyscaleInterpolant = MathF.Max(idealGreyscaleInterpolant, effect.GreyscaleInterpolantFunction());
            musicBeingAffected |= effect.AffectsMusic;
        }
        SlowdownInterpolant = SlowdownInterpolant.StepTowards(idealSlowdownInterpolant, 0.12f);
        GreyscaleInterpolant = GreyscaleInterpolant.StepTowards(idealGreyscaleInterpolant, 0.12f);

        if (SlowdownInterpolant >= 0.3f)
            Main.LocalPlayer.channel = false;
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.Server) return;

        if (MonoStereoSystem.Enabled)
            UpdatePitchFilter();

        if (GreyscaleInterpolant <= 0f)
            return;

        ManagedScreenFilter desaturationShader = ShaderManager.GetFilter("NoxusBoss.DesaturationOverlayShader");
        desaturationShader.TrySetParameter("greyscaleInterpolant", GreyscaleInterpolant);
        desaturationShader.SetTexture(GreyscaleExclusionTarget, 1, SamplerState.PointClamp);
        desaturationShader.Activate();
    }

    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    private static void UpdatePitchFilter()
    {
        if (musicBeingAffected)
            MonoStereoSupport.PitchFilter.PitchFactor = Lerp(1f, 0.6f, GreyscaleInterpolant);
    }
}
