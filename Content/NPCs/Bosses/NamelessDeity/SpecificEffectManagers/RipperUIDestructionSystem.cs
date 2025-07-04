using System.Reflection;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class RipperUIDestructionSystem : ModSystem
{
    public static float FistOpacity
    {
        get;
        set;
    }

    public static bool IsUIDestroyed
    {
        get;
        set;
    }

    public static int AdrenalineFailSoundCountdown
    {
        get;
        set;
    }

    public static int RageFailSoundCountdown
    {
        get;
        set;
    }

    public static float ScreenCenterMoveInterpolant
    {
        get
        {
            if (NamelessDeityBoss.Myself is null || Main.gameMenu)
                return 0f;

            if (IsUIDestroyed)
                return 1f;

            return Pow(FistOpacity, 2f);
        }
    }

    public static Vector2 RageScreenPosition
    {
        get
        {
            float rageX = GetFromCalamityConfig<float>("RageMeterPosX");
            float rageY = GetFromCalamityConfig<float>("RageMeterPosY");
            Vector2 originalPosition = new Vector2(rageX * Main.screenWidth * 0.01f, rageY * Main.screenHeight * 0.01f).Floor();
            if (IsUIDestroyed && FistOpacity <= 0f)
                return originalPosition;

            return Vector2.Lerp(originalPosition, Main.ScreenSize.ToVector2() * 0.5f + new Vector2(100f, -80f) * Main.UIScale, ScreenCenterMoveInterpolant);
        }
    }

    public static Vector2 AdrenalineScreenPosition
    {
        get
        {
            float adrenalineX = GetFromCalamityConfig<float>("AdrenalineMeterPosX");
            float adrenalineY = GetFromCalamityConfig<float>("AdrenalineMeterPosY");
            Vector2 originalPosition = new Vector2(adrenalineX * Main.screenWidth * 0.01f, adrenalineY * Main.screenHeight * 0.01f).Floor();
            if (IsUIDestroyed && FistOpacity <= 0f)
                return originalPosition;

            return Vector2.Lerp(originalPosition, Main.ScreenSize.ToVector2() * 0.5f + new Vector2(-100f, -80f) * Main.UIScale, ScreenCenterMoveInterpolant);
        }
    }

    public delegate void orig_RipperDrawMethod(SpriteBatch spriteBatch, Player player);

    public delegate void hook_RipperDrawMethod(orig_RipperDrawMethod orig, SpriteBatch spriteBatch, Player player);

    public delegate void orig_SpecificRipperDrawMethod(SpriteBatch spriteBatch, ModPlayer player, Vector2 drawPosition);

    public delegate void hook_SpecificRipperDrawMethod(orig_SpecificRipperDrawMethod orig, SpriteBatch spriteBatch, ModPlayer player, Vector2 drawPosition);

    public override void Load()
    {
        if (!ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal))
            return;

        Type? ripperUIType = cal.Code.GetType("CalamityMod.UI.Rippers.RipperUI");
        if (ripperUIType is null)
        {
            Mod.Logger.Warn("Calamity's 'RipperUI' class could not be found. Ripper IL edits unable to be applied.");
            return;
        }

        MethodInfo? ripperUIDrawMethod = ripperUIType.GetMethod("Draw", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? rageDrawMethod = ripperUIType.GetMethod("DrawRageBar", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo? adrenalineDrawMethod = ripperUIType.GetMethod("DrawAdrenalineBar", BindingFlags.NonPublic | BindingFlags.Static);

        if (ripperUIDrawMethod is null)
        {
            Mod.Logger.Warn("Calamity's RipperUI Draw method could not be found. Ripper IL edits unable to be applied.");
            return;
        }
        if (rageDrawMethod is null)
        {
            Mod.Logger.Warn("Calamity's RipperUI DrawRageBar method could not be found. Ripper IL edits unable to be applied.");
            return;
        }
        if (adrenalineDrawMethod is null)
        {
            Mod.Logger.Warn("Calamity's RipperUI DrawAdrenalineBar method could not be found. Ripper IL edits unable to be applied.");
            return;
        }

        MonoModHooks.Add(ripperUIDrawMethod, (hook_RipperDrawMethod)DisableRipperUI);
        MonoModHooks.Add(rageDrawMethod, (hook_SpecificRipperDrawMethod)ChangeRagePosition);
        MonoModHooks.Add(adrenalineDrawMethod, (hook_SpecificRipperDrawMethod)ChangeAdrenalinePosition);
    }

    public override void OnWorldLoad()
    {
        IsUIDestroyed = false;
        FistOpacity = 0f;
    }

    public override void PostUpdatePlayers()
    {
        // Disable rage and adrenaline effects if the UI is destroyed.
        if (IsUIDestroyed)
        {
            CalamityCompatibility.ResetRippers(Main.LocalPlayer);

            Type? keybindType = ModReferences.Calamity.Code.GetType("CalamityMod.CalamityKeybinds");
            ModKeybind? adrenalineKey = (keybindType?.GetProperty("AdrenalineHotKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? null) as ModKeybind;
            ModKeybind? rageKey = (keybindType?.GetProperty("RageHotKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? null) as ModKeybind;

            if (Main.netMode != NetmodeID.Server && (adrenalineKey?.JustPressed ?? false) && AdrenalineFailSoundCountdown <= 0)
            {
                NamelessDeityKeyboardShader.BrightnessIntensity = 1f;
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.NamelessDeityAdrenalineFail with { Volume = 3f });
                GeneralScreenEffectSystem.ChromaticAberration.Start(Main.LocalPlayer.Center - Vector2.UnitY * 600f, 3f, 45);
                AdrenalineFailSoundCountdown = 180;

                // Create dust.
                for (int i = 0; i < 2; i++)
                    Dust.NewDust(Main.LocalPlayer.position, 120, 120, DustID.Firework_Blue, 0f, 0f, 100, default, 1.5f);
                for (int i = 0; i < 6; i++)
                {
                    float angle = TwoPi * i / 30f;
                    int dustIndex = Dust.NewDust(Main.LocalPlayer.position, 120, 120, DustID.TerraBlade, 0f, 0f, 0, default, 2f);
                    Main.dust[dustIndex].noGravity = true;
                    Main.dust[dustIndex].velocity *= 4f;
                    dustIndex = Dust.NewDust(Main.LocalPlayer.position, 120, 120, DustID.TerraBlade, 0f, 0f, 100, default, 1f);
                    Main.dust[dustIndex].velocity *= 2.25f;
                    Main.dust[dustIndex].noGravity = true;
                    Dust.NewDust(Main.LocalPlayer.Center + angle.ToRotationVector2() * 160f, 0, 0, DustID.TerraBlade, 0f, 0f, 100, default, 1f);
                }
            }
            if (Main.netMode != NetmodeID.Server && (rageKey?.JustPressed ?? false) && RageFailSoundCountdown <= 0)
            {
                NamelessDeityKeyboardShader.BrightnessIntensity = 1f;
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.NamelessDeityRageFail with { Volume = 3f });
                GeneralScreenEffectSystem.ChromaticAberration.Start(Main.LocalPlayer.Center - Vector2.UnitY * 600f, 3f, 45);
                RageFailSoundCountdown = 180;

                // Create dust.
                for (int i = 0; i < 2; i++)
                    Dust.NewDust(Main.LocalPlayer.position, 120, 120, DustID.Rain_BloodMoon, 0f, 0f, 100, default, 1.5f);
                for (int i = 0; i < 6; i++)
                {
                    float angle = TwoPi * i / 30f;
                    int dustIndex = Dust.NewDust(Main.LocalPlayer.position, 120, 120, DustID.Rain_BloodMoon, 0f, 0f, 0, default, 2f);
                    Main.dust[dustIndex].noGravity = true;
                    Main.dust[dustIndex].velocity *= 4f;
                    dustIndex = Dust.NewDust(Main.LocalPlayer.position, 120, 120, DustID.Rain_BloodMoon, 0f, 0f, 100, default, 1f);
                    Main.dust[dustIndex].velocity *= 2.25f;
                    Main.dust[dustIndex].noGravity = true;
                    Dust.NewDust(Main.LocalPlayer.Center + angle.ToRotationVector2() * 160f, 0, 0, DustID.Rain_BloodMoon, 0f, 0f, 100, default, 1f);
                }
            }
        }

        // Decrement failure sound countdowns.
        AdrenalineFailSoundCountdown = Utils.Clamp(AdrenalineFailSoundCountdown - 1, 0, 300);
        RageFailSoundCountdown = Utils.Clamp(RageFailSoundCountdown - 1, 0, 300);

        if (Main.LocalPlayer.dead || NamelessDeityBoss.Myself is null)
        {
            IsUIDestroyed = false;
            FistOpacity = 0f;
        }
    }

    public static void DisableRipperUI(orig_RipperDrawMethod orig, SpriteBatch spriteBatch, Player player)
    {
        if (NamelessDeityBoss.Myself is null)
        {
            orig(spriteBatch, player);
            return;
        }

        // Draw hands behind the bars if they should be visible.
        if (FistOpacity > 0f && CommonCalamityVariables.RevengeanceModeActive)
        {
            // Collect hand draw information.
            float handScale = Main.UIScale * 1.18f;
            float handRotation = 0f;
            Color handColor = Color.White * (IsUIDestroyed ? FistOpacity : InverseLerp(0.7f, 0.98f, FistOpacity));
            Vector2 leftHandDrawPosition = RageScreenPosition.X < AdrenalineScreenPosition.X ? RageScreenPosition : AdrenalineScreenPosition;
            Vector2 rightHandDrawPosition = RageScreenPosition.X > AdrenalineScreenPosition.X ? RageScreenPosition : AdrenalineScreenPosition;
            SpriteEffects perspectiveIsHard = leftHandDrawPosition.X < rightHandDrawPosition.X ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Texture2D handTexture = GennedAssets.Textures.NamelessDeity.NamelessDeityPalm.Value;
            if (IsUIDestroyed)
            {
                handTexture = GennedAssets.Textures.NamelessDeity.NamelessDeityHandFist.Value;
                handRotation -= PiOver2;
                perspectiveIsHard ^= SpriteEffects.FlipHorizontally;
            }

            Main.spriteBatch.Draw(handTexture, leftHandDrawPosition - Vector2.UnitY * handScale * 24f, null, handColor, handRotation, handTexture.Size() * 0.5f, handScale, perspectiveIsHard, 0f);
            Main.spriteBatch.Draw(handTexture, rightHandDrawPosition - Vector2.UnitY * handScale * 24f, null, handColor, handRotation + IsUIDestroyed.ToInt() * Pi, handTexture.Size() * 0.5f, handScale, perspectiveIsHard ^ SpriteEffects.FlipHorizontally, 0f);
        }

        if (!IsUIDestroyed)
            orig(spriteBatch, player);
    }

    public static void ChangeRagePosition(orig_SpecificRipperDrawMethod orig, SpriteBatch spriteBatch, ModPlayer player, Vector2 drawPosition)
    {
        orig(spriteBatch, player, RageScreenPosition);
    }

    public static void ChangeAdrenalinePosition(orig_SpecificRipperDrawMethod orig, SpriteBatch spriteBatch, ModPlayer player, Vector2 drawPosition)
    {
        orig(spriteBatch, player, AdrenalineScreenPosition);
    }

    public static void CreateBarDestructionEffects()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        if (!CommonCalamityVariables.RevengeanceModeActive)
            return;

        SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.FUCKYOUSTUPIDRIPPERS with { Volume = 1.9f });

        Vector2 rageBarPositionWorld = RageScreenPosition + Main.screenPosition + new Vector2(20f, 4f) * Main.UIScale;
        Vector2 adrenalineBarPositionWorld = AdrenalineScreenPosition + Main.screenPosition + new Vector2(-20f, 4f) * Main.UIScale;
        List<Vector2> barPositions =
        [
            rageBarPositionWorld,
            adrenalineBarPositionWorld
        ];

        foreach (Vector2 barPosition in barPositions)
        {
            // Create small glass shards.
            for (int i = 0; i < 145; i++)
            {
                int dustID = Main.rand.NextBool() ? DustID.t_SteampunkMetal : DustID.BlueCrystalShard;

                Vector2 shardVelocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3.6f, 13.6f);
                Dust shard = Dust.NewDustPerfect(barPosition + Main.rand.NextVector2Circular(50f, 18f), dustID, shardVelocity);
                shard.noGravity = Main.rand.NextBool();
                shard.scale = Main.rand.NextFloat(1f, 1.425f);
                shard.color = Color.Wheat;
                shard.velocity.Y -= 5f;
            }
        }

        // Create destruction gores.
        for (int i = 1; i <= 4; i++)
            Gore.NewGore(new EntitySource_WorldEvent(), rageBarPositionWorld + Main.rand.NextVector2Circular(50f, 20f), Main.rand.NextVector2CircularEdge(4f, 4f), ModContent.Find<ModGore>("NoxusBoss", $"RageBar{i}").Type, Main.UIScale * 0.75f);
        for (int i = 1; i <= 3; i++)
            Gore.NewGore(new EntitySource_WorldEvent(), adrenalineBarPositionWorld + Main.rand.NextVector2Circular(50f, 20f), Main.rand.NextVector2CircularEdge(4f, 4f), ModContent.Find<ModGore>("NoxusBoss", $"AdrenalineBar{i}").Type, Main.UIScale * 0.75f);

        // Create some screen impact effects to add to the intensity.
        Vector2 barCenter = (rageBarPositionWorld + adrenalineBarPositionWorld) * 0.5f;

        ScreenShakeSystem.StartShake(15f);
        GeneralScreenEffectSystem.ChromaticAberration.Start(barCenter, 1.6f, 45);

        ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(barCenter, Vector2.Zero, Color.Wheat, 20, 0.1f);
        burst.Spawn();

        ExpandingChromaticBurstParticle burst2 = new ExpandingChromaticBurstParticle(barCenter, Vector2.Zero, Color.Wheat, 16, 0.1f);
        burst2.Spawn();
    }
}
