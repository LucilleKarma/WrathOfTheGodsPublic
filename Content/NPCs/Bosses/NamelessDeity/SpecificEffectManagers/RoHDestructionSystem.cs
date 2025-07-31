using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix.CalRemixCompatibilitySystem;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class RoHDestructionSystem : ModSystem
{
    public static int AnimationTimer
    {
        get;
        set;
    }

    public static int SlashDelay => 210;

    public static int SlashHitDelay => 9;

    public static float RoHVerticalOffset => Pow(InverseLerp(0f, 45f, AnimationTimer), 0.3f) * -90f - Sin01(TwoPi * AnimationTimer / 36f) * 24f;

    public override void OnModLoad()
    {
        Main.OnPostDraw += DrawAnimation;
    }

    public override void SetupContent()
    {
        // Load Fanny dialog.
        FannyDialog nonEvilText = new FannyDialog("RodOfHarmonyBreak", "FannyIdle").WithDuration(4.5f).WithCondition(_ => AnimationTimer == SlashDelay + SlashHitDelay + 1).WithoutClickability();
        FannyDialog evilText1 = new FannyDialog("RodOfHarmonyBreakEvil1", "EvilFannyIdle").WithDuration(5f).WithEvilness().WithCondition(_ => AnimationTimer == SlashDelay + SlashHitDelay + 36).WithoutClickability();
        FannyDialog evilText2 = new FannyDialog("RodOfHarmonyBreakEvil2", "EvilFannyIdle").WithDuration(5.5f).WithEvilness().WithParentDialog(evilText1, 0.5f);

        nonEvilText.Register();
        evilText1.Register();
        evilText2.Register();
    }

    public override void OnModUnload()
    {
        Main.OnPostDraw -= DrawAnimation;
    }

    private void DrawAnimation(GameTime obj)
    {
        // Make the animation stop if on the game menu.
        if (Main.gameMenu)
            AnimationTimer = 0;

        if (AnimationTimer <= 0)
            return;

        Main.spriteBatch.Begin();

        // Increment the animation timer.
        if (!Main.gamePaused)
            AnimationTimer++;

        // Load the RoH texture.
        Main.instance.LoadItem(ItemID.RodOfHarmony);

        // Calculate draw variables.
        Vector2 unmodifiedCameraPosition = Main.LocalPlayer.TopLeft + new Vector2(Main.LocalPlayer.width * 0.5f, Main.LocalPlayer.height - 21f) - Main.ScreenSize.ToVector2() * 0.5f + Vector2.UnitY * Main.LocalPlayer.gfxOffY;
        Vector2 cameraOffset = -unmodifiedCameraPosition;
        Vector2 rohPosition = Main.LocalPlayer.Top + Vector2.UnitY * RoHVerticalOffset;
        Texture2D rohTexture = TextureAssets.Item[ItemID.RodOfHarmony].Value;
        Rectangle frame = rohTexture.Frame();

        float cartoonFlipInterpolant = InverseLerp(96f, 120f, AnimationTimer);
        Vector2 scale = Vector2.One * InverseLerp(0f, 90f, AnimationTimer) * 1.67f;

        // Make the RoH do a cartoon flip before it's sliced in two.
        scale.X *= Cos(TwoPi * cartoonFlipInterpolant);

        // Make the camera focus on the RoH.
        CalamityCompatibility.ResetStealthBarOpacity(Main.LocalPlayer);
        float cameraPanInterpolant = InverseLerp(84f, 90f, AnimationTimer);
        float cameraZoomInterpolant = InverseLerp(90f, 156f, AnimationTimer);
        CameraPanSystem.PanTowards(rohPosition, cameraPanInterpolant);
        CameraPanSystem.Zoom = cameraZoomInterpolant * 1.1f;

        scale *= Main.GameViewMatrix.Zoom;

        // Draw the texture.
        if (AnimationTimer < SlashDelay + SlashHitDelay)
        {
            // Draw a pulse behind the main texture.
            float pulse = Main.GlobalTimeWrappedHourly * 4f % 1f;
            Main.spriteBatch.Draw(rohTexture, rohPosition + cameraOffset, frame, Color.Pink with { A = 0 } * Pow(1f - pulse, 2f), 0f, rohTexture.Size() * 0.5f, scale * (1f + pulse * 2.2f), 0, 0f);
            Main.spriteBatch.Draw(rohTexture, rohPosition + cameraOffset, frame, Color.DarkViolet with { A = 0 } * Pow(1f - pulse, 1.3f), 0f, rohTexture.Size() * 0.5f, scale * (1f + pulse * 1.1f), 0, 0f);

            Main.spriteBatch.Draw(rohTexture, rohPosition + cameraOffset, frame, Color.White, 0f, rohTexture.Size() * 0.5f, scale, 0, 0f);
        }

        if (AnimationTimer >= SlashDelay + SlashHitDelay + 1f)
            AnimationTimer = 0;

        Main.spriteBatch.End();
    }

    public static bool PerformRodOfHarmonyCheck(Player player)
    {
        // Check if the player has a rod of harmony in their inventory and no legitimate permission slip.
        // If they do, Nameless becomes so disgusted with the player that he completely destroys it and goes on a rant.
        if (!Main.zenithWorld || player.FindItem(ItemID.RodOfHarmony) == -1 || CheatPermissionSlip.PlayerHasLegitimateSlip(player))
            return false;

        return true;
    }

    public static void Start()
    {
        // Start the animation timer.
        AnimationTimer = 1;

        NamelessDeityBoss.CreateTwinkle(Main.LocalPlayer.Center, Vector2.One * 1.9f, Color.Pink, new(Vector2.Zero, () => Main.LocalPlayer.Center));

        // Delete the rod of harmony from the player's inventory.
        int rohCount = Main.LocalPlayer.CountItem(ItemID.RodOfHarmony);
        for (int i = 0; i < rohCount * 50; i++)
            Main.LocalPlayer.ConsumeItem(ItemID.RodOfHarmony);
    }

    public override void PostUpdateWorld()
    {
        // Slice the screen.
        Vector2 rohPosition = Main.LocalPlayer.Top + Vector2.UnitY * RoHVerticalOffset;
        if (AnimationTimer == SlashDelay)
        {
            ScreenShakeSystem.StartShake(4f);

            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.SliceTelegraph with { Volume = 1.05f, MaxInstances = 20 });

            float sliceLength = 3400f;
            Vector2 sliceDirection = 0.57f.ToRotationVector2();
            NewProjectileBetter(new EntitySource_WorldEvent(), rohPosition - sliceDirection * sliceLength * 0.5f + Vector2.UnitY * 10f, sliceDirection, ModContent.ProjectileType<VergilScreenSlice>(), 0, 0f, -1, 12, sliceLength);
        }

        // Create rod break visuals.
        if (AnimationTimer == SlashDelay + SlashHitDelay)
        {
            TotalScreenOverlaySystem.OverlayInterpolant = 1.2f;
            TotalScreenOverlaySystem.OverlayColor = Color.White;
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.ScreenShatter with { Pitch = -0.5f, Volume = 2f });

            for (int i = 0; i < 16; i++)
            {
                ParticleOrchestraType orchestraType = Main.rand.NextBool(3) ? ParticleOrchestraType.PrincessWeapon : ParticleOrchestraType.RainbowRodHit;
                Vector2 sparkleOffset = Main.rand.NextVector2Unit() * Main.rand.NextFloat(24f, 56f);
                Vector2 positionInWorld = rohPosition + sparkleOffset;
                ParticleOrchestrator.RequestParticleSpawn(true, orchestraType, new ParticleOrchestraSettings
                {
                    PositionInWorld = positionInWorld,
                    MovementVector = Main.rand.NextVector2Circular(10f, 10f)
                }, Main.myPlayer);
            }

            NewProjectileBetter(new EntitySource_WorldEvent(), rohPosition, Vector2.Zero, ModContent.ProjectileType<RodOfHarmonyExplosion>(), 0, 0f);

            // Shake the screen.
            ScreenShakeSystem.StartShake(20f, shakeStrengthDissipationIncrement: 0.4f);
        }
    }
}
