using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

[Autoload(Side = ModSide.Client)]
public class NamelessDeityPlayerDeathVisualsPlayer : ModPlayer
{
    public bool WasKilledByNamelessDeity
    {
        get;
        private set;
    }

    public ManagedRenderTarget PlayerAtTimeOfDeath
    {
        get;
        private set;
    }

    public int DeathTimerOverride
    {
        get;
        set;
    }

    public bool TakePlayerScreenshot
    {
        get;
        set;
    }

    public static int DeathTimerMax => 240;

    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        RenderTargetManager.RenderTargetUpdateLoopEvent += ManagePlayerTarget;
        On_LegacyPlayerRenderer.DrawPlayerFull += DrawPlayerFull;
    }

    private static void ManagePlayerTarget()
    {
        List<NamelessDeityPlayerDeathVisualsPlayer> playersInNeedOfScreenshot = [];
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (!p.active)
                continue;

            var modPlayer = p.GetModPlayer<NamelessDeityPlayerDeathVisualsPlayer>();
            if (!modPlayer.TakePlayerScreenshot)
                continue;

            playersInNeedOfScreenshot.Add(modPlayer);
        }

        // Don't go any further if no players are in need of screenshots.
        if (playersInNeedOfScreenshot.Count == 0)
            return;

        // Go through all players that need a screenshot and take on, drawing the results to their render target.
        var gd = Main.instance.GraphicsDevice;
        foreach (var player in playersInNeedOfScreenshot)
        {
            // Go to the screenshot target.
            player.PlayerAtTimeOfDeath ??= new(false, (width, height) =>
            {
                return new(Main.instance.GraphicsDevice, 350, 350);
            });
            var screenshotTarget = player.PlayerAtTimeOfDeath;
            gd.SetRenderTarget(screenshotTarget);
            gd.Clear(Color.Transparent);

            // Get the player in position for screenshot drawing.
            Vector2 oldPlayerPosition = player.Player.position;
            player.Player.Center = player.PlayerAtTimeOfDeath.Size() * 0.5f + Main.Camera.UnscaledPosition;

            // Emit white light at the player's position.
            Lighting.AddLight(player.Player.Center, Vector3.One);

            // Draw the player to the screenshot target.
            Main.spriteBatch.Begin();
            Main.PlayerRenderer.DrawPlayer(Main.Camera, player.Player, player.Player.position, 0f, player.Player.fullRotationOrigin);
            Main.spriteBatch.End();

            // Reset the player's position.
            player.Player.position = oldPlayerPosition;

            player.TakePlayerScreenshot = false;
        }

        // Return to the back-buffer.
        gd.SetRenderTarget(null);
    }

    private void DrawPlayerFull(On_LegacyPlayerRenderer.orig_DrawPlayerFull orig, LegacyPlayerRenderer self, Camera camera, Player player)
    {
        if (!player.active || Main.gameMenu)
        {
            orig(self, camera, player);
            return;
        }

        // Draw the dissipation effect.
        var modPlayer = player.GetModPlayer<NamelessDeityPlayerDeathVisualsPlayer>();
        if (!modPlayer.TakePlayerScreenshot && modPlayer.WasKilledByNamelessDeity && !player.ShouldNotDraw && player.dead)
        {
            // Prepare the sprite batch for shaders.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            float deathAnimationCompletion = InverseLerp(0f, DeathTimerMax * 0.64f, modPlayer.DeathTimerOverride);
            Texture2D screenshot = modPlayer.PlayerAtTimeOfDeath;

            // Prepare the psychedelic dissipation shader.
            float fadeOut = InverseLerp(0.49f, 0.71f, deathAnimationCompletion);
            float dissipationIntensity = InverseLerp(0.15f, 0.3f, deathAnimationCompletion) * Utils.Remap(deathAnimationCompletion, 0.2f, 0.64f, 0.001f, 0.1577f);
            float scale = Utils.Remap(deathAnimationCompletion, 0.3f, 0.6f, 1f, 1.4f);
            Color screenshotColor = Color.Lerp(Color.White, Color.Gold with { A = 0 }, InverseLerp(0.05f, 0.12f, deathAnimationCompletion)) * (1f - fadeOut);

            var afterimageShader = ShaderManager.GetShader("NoxusBoss.NamelessDeityPsychedelicAfterimageShader");
            afterimageShader.TrySetParameter("uScreenResolution", screenshot.Size());
            afterimageShader.TrySetParameter("warpSpeed", dissipationIntensity);
            afterimageShader.SetTexture(TurbulentNoise, 1);
            afterimageShader.Apply();
            Main.spriteBatch.Draw(screenshot, player.Center - Main.screenPosition, null, screenshotColor, 0f, screenshot.Size() * 0.5f, scale, 0, 0f);

            // Return to regular drawing.
            Main.spriteBatch.End();
            return;
        }

        orig(self, camera, player);
    }

    public override void UpdateDead()
    {
        // Disable the natural death vfx where the player's body parts fly around if Nameless killed them.
        // Instead, the player will dissolve into holy light.
        if (WasKilledByNamelessDeity)
        {
            Player.headPosition = Vector2.Zero;
            Player.bodyPosition = Vector2.Zero;
            Player.legPosition = Vector2.Zero;
            Player.headVelocity = Vector2.Zero;
            Player.bodyVelocity = Vector2.Zero;
            Player.legVelocity = Vector2.Zero;
            Player.headRotation = 0f;
            Player.bodyRotation = 0f;
            Player.legRotation = 0f;

            // Release death particles.
            float deathAnimationCompletion = InverseLerp(0f, DeathTimerMax * 0.45f, DeathTimerOverride);
            float particleAppearInterpolant = InverseLerp(0.02f, 0.1f, deathAnimationCompletion);
            float deathFadeOut = InverseLerp(0.49f, 0.71f, deathAnimationCompletion);
            if (Main.rand.NextFloat() > deathFadeOut)
            {
                for (int i = 0; i < particleAppearInterpolant * (1f - deathFadeOut) * 4f; i++)
                {
                    Dust light = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Square(-25f, 25f), 264);
                    light.velocity = -Vector2.UnitY * Main.rand.NextFloat(1.2f, 2.5f);
                    light.color = Color.Lerp(Color.Wheat, Color.Gold, Sin01(Main.GlobalTimeWrappedHourly * 10f + i * 0.2f)) * particleAppearInterpolant * (1f - deathFadeOut);
                    light.scale = Main.rand.NextFloat(0.1f, 1.8f) * Lerp(1f, 0.1f, deathAnimationCompletion);
                    light.noGravity = true;
                }
            }
        }
    }

    public override void PreUpdate()
    {
        if (!Player.dead)
            DeathTimerOverride = 0;
        else if (WasKilledByNamelessDeity)
        {
            DeathTimerOverride = Utils.Clamp(DeathTimerOverride + 1, 0, DeathTimerMax);
            if (DeathTimerOverride < DeathTimerMax)
                Player.respawnTimer = 8;
        }
    }

    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource)
    {
        // Determine if the death resulted from Nameless. Technically there are a handful of niche cases can cause this where it wasn't actually Nameless who killed the player, such as
        // if two players enabled PVP while Nameless is killing both of them, but honestly who cares.
        WasKilledByNamelessDeity = NamelessDeityBoss.Myself is not null;

        // Use custom death text and sounds if Nameless killed the player.
        if (WasKilledByNamelessDeity)
        {
            MagicBurstParticle burst = new MagicBurstParticle(Player.Center, Vector2.Zero, Color.Wheat, 24, 1f);
            burst.Spawn();
            RadialScreenShoveSystem.Start(Player.Center - Vector2.UnitY * 400f, 36);

            // Create burst effects.
            ScreenShakeSystem.StartShake(9.6f);

            TakePlayerScreenshot = true;

            string deathKey = $"NamelessDeity{Main.rand.Next(1, 19)}";

            // Poke fun at the player for installing Dragon Lens without permission. This does not happen for me, though.
            if (!BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>() && ModLoader.TryGetMod("DragonLens", out _) && !NamelessDeityFormPresetRegistry.UsingLucillePreset)
                deathKey = "NamelessDeityCheatEasterEgg";

            damageSource = PlayerDeathReason.ByCustomReason(Language.GetText($"Mods.NoxusBoss.PlayerDeathMessages.{deathKey}").Format(Player.name));
            playSound = false;
            genGore = false;

            WorldSaveSystem.NamelessDeityDeathCount++;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        return true;
    }
}
