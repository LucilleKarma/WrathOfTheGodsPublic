using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Dusts;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;
using NoxusBoss.Core.Graphics.Automators;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Core.World.Subworlds.EternalGardenUpdateSystem;

namespace NoxusBoss.Content.Projectiles.Visuals;

public class GodRayVisual : ModProjectile, IDrawsWithShader
{
    public static int Width => 1440;

    public static int Height => 3700;

    public static Color MainColor => NamelessDeityFormPresetRegistry.UsingLucillePreset ? new Color(255, 28, 105) : new Color(0, 140, 170);

    public static Color ColorAccent => NamelessDeityFormPresetRegistry.UsingLucillePreset ? new Color(255, 197, 175) : new Color(199, 199, 255);

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 7400;

    public override void SetDefaults()
    {
        // Using the Height constant here has a habit of causing vanilla's out-of-world projectile deletion to kill this, due to how large it is.
        Projectile.width = Width;
        Projectile.height = 1;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 900000;
        Projectile.Opacity = 0f;
        Projectile.tileCollide = false;
        Projectile.netImportant = true;
    }

    public override void SendExtraAI(BinaryWriter writer) => writer.Write(Projectile.width);

    public override void ReceiveExtraAI(BinaryReader reader) => Projectile.Resize(reader.ReadInt32(), Projectile.height);

    public override void AI()
    {
        // Fade in or out depending on if Nameless is present.
        bool fadeIn = NamelessDeityBoss.Myself is null || ModContent.GetInstance<EndCreditsScene>().IsActive;
        Projectile.Opacity = Saturate(Projectile.Opacity + fadeIn.ToDirectionInt() * 0.0379f);

        if (Projectile.Opacity <= 0f && !fadeIn)
            Projectile.Kill();

        // Emit light.
        Vector2 rayDirection = Vector2.UnitY.RotatedBy(Projectile.rotation);
        DelegateMethods.v3_1 = (NamelessDeityFormPresetRegistry.UsingLucillePreset ? Color.LightCoral : new Color(0, 120, 255)).ToVector3() * 1.4f;
        Utils.PlotTileLine(Projectile.Bottom - rayDirection * 2500f, Projectile.Bottom + rayDirection, Projectile.width, DelegateMethods.CastLightOpen_StopForSolids);

        // Decide rotation.
        if (Projectile.velocity != Vector2.Zero)
            Projectile.rotation = Projectile.velocity.ToRotation();

        int dustCount = NamelessDeityFormPresetRegistry.UsingLucillePreset ? 7 : 6;
        for (int i = 0; i < dustCount; i++)
        {
            // Emit small light dust in the ray.
            Vector2 lightSpawnPosition = Vector2.Zero;

            // Try to keep the light outside of tiles.
            for (int j = 0; j < 15; j++)
            {
                lightSpawnPosition = Projectile.Bottom - rayDirection * Main.rand.NextFloat(160f, 1500f);
                lightSpawnPosition += rayDirection.RotatedBy(PiOver2) * Main.rand.NextFloatDirection() * Projectile.width * 0.3f;
                if (!WorldGen.SolidTile(lightSpawnPosition.ToTileCoordinates()))
                    break;
            }

            Dust flower = Dust.NewDustPerfect(lightSpawnPosition, ModContent.DustType<FlowerPieceDust>());
            flower.color = Color.Lerp(MainColor, ColorAccent, Main.rand.NextFloat(0.5f));
            flower.color = Color.Lerp(flower.color, Color.Wheat, 0.6f);
            flower.scale = Main.rand.NextFloat(0.37f, 0.8f);
            flower.velocity = Main.rand.NextVector2Circular(1.5f, 0.8f) / flower.scale + Vector2.UnitY * 0.6f;

            Dust glow = Dust.NewDustPerfect(lightSpawnPosition, ModContent.DustType<EntropicSnowDust>());
            glow.scale = Main.rand.NextFloat(0.67f);
            glow.color = Color.Lerp(Color.White, MainColor.HueShift(-0.1f), Main.rand.NextFloat(0.6f)) * 0.8f;
            glow.velocity = Main.rand.NextVector2Circular(0.1f, 0.5f) + Vector2.UnitY * 0.9f;
            glow.velocity.Y *= Main.rand.NextFromList(-1f, 1f);
            glow.customData = 0;
        }
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        if (TimeSpentInLight >= 149)
            Main.gamePaused = false;

        // Collect the shader and draw data for later.
        ManagedShader godRayShader = ShaderManager.GetShader("NoxusBoss.GodRayShader");
        Vector2 textureArea = new Vector2(Projectile.width, Height) / WhitePixel.Size();

        // Apply the god ray shader.
        Vector3[] palette = new Vector3[]
        {
            MainColor.ToVector3(),
            ColorAccent.ToVector3()
        };
        Vector2 sourcePosition = Projectile.Bottom - Main.screenPosition - Vector2.UnitY * Height * 1.2f;
        godRayShader.TrySetParameter("gradient", palette);
        godRayShader.TrySetParameter("gradientCount", palette.Length);
        godRayShader.TrySetParameter("sourcePosition", Vector2.Transform(sourcePosition, Main.GameViewMatrix.TransformationMatrix));
        godRayShader.TrySetParameter("sceneArea", textureArea);
        godRayShader.SetTexture(ViscousNoise, 1, SamplerState.LinearWrap);
        godRayShader.Apply();

        // Draw a large white rectangle based on the hitbox of the ray.
        // The shader will transform the rectangle into the ray.
        float brightnessFadeIn = InverseLerp(15f, NamelessDeitySummonDelayInCenter * 0.67f, TimeSpentInLight);
        float brightnessFadeOut = InverseLerp(NamelessDeitySummonDelayInCenter - 4f, NamelessDeitySummonDelayInCenter - 16f, TimeSpentInLight);
        float brightnessInterpolant = brightnessFadeIn * brightnessFadeOut;
        float brightness = Lerp(0.25f, 0.81f, InverseLerp(0f, 0.7f, brightnessInterpolant));
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, Projectile.GetAlpha(MainColor) * brightness, Projectile.rotation, WhitePixel.Size() * new Vector2(0.5f, 1f), textureArea, 0, 0f);

        // Draw the vignette over the player's screen.
        if (brightnessInterpolant > 0.44f)
            DrawVignette(InverseLerp(0.44f, 1f, brightnessInterpolant));
    }

    public void DrawVignette(float brightnessInterpolant)
    {
        // Draw a pixel over the player's screen and then draw the vignette over it.
        ManagedShader vignetteShader = ShaderManager.GetShader("NoxusBoss.FleshyVignetteShader");
        vignetteShader.TrySetParameter("animationSpeed", 0.05f);
        vignetteShader.TrySetParameter("vignettePower", Lerp(6f, 3.97f, brightnessInterpolant));
        vignetteShader.TrySetParameter("vignetteBrightness", Lerp(3f, 20f, brightnessInterpolant));
        vignetteShader.TrySetParameter("crackBrightness", Sqrt(brightnessInterpolant) * 0.95f);
        vignetteShader.TrySetParameter("aspectRatioCorrectionFactor", new Vector2(Main.screenWidth / (float)Main.screenHeight, 1f));
        vignetteShader.TrySetParameter("primaryColor", Color.White);
        vignetteShader.TrySetParameter("secondaryColor", Color.White);
        vignetteShader.TrySetParameter("radialOffsetTime", InverseLerp(30f, NamelessDeitySummonDelayInCenter, TimeSpentInLight) * 1.2f);
        vignetteShader.SetTexture(CrackedNoiseA, 1);
        vignetteShader.Apply();

        Color vignetteColor = Projectile.GetAlpha(Color.Gray) * brightnessInterpolant * InverseLerp(800f, 308f, Distance(Projectile.Center.X, Main.LocalPlayer.Center.X)) * 0.67f;
        Vector2 screenArea = ViewportSize;
        Vector2 textureArea = screenArea / WhitePixel.Size();
        Main.spriteBatch.Draw(WhitePixel, screenArea * 0.5f, null, vignetteColor, 0f, WhitePixel.Size() * 0.5f, textureArea, 0, 0f);
    }

    // Manual drawing is not necessary.
    public override bool PreDraw(ref Color lightColor) => false;

    // This visual should not move.
    public override bool ShouldUpdatePosition() => false;
}
