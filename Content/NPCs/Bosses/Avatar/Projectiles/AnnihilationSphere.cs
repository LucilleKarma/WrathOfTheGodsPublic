using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Luminance.Core.Sounds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Buffs;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Configuration;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class AnnihilationSphere : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    private LoopedSoundInstance annihilationSoundLoop;

    public static ManagedRenderTarget AnnihilationTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// How long this sphere has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            AnnihilationTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
            RenderTargetManager.RenderTargetUpdateLoopEvent += UpdateTarget;
        }
    }

    public override void SetDefaults()
    {
        Projectile.width = 4;
        Projectile.height = 4;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = AvatarOfEmptiness.UniversalAnnihilation_AnnihilationSphereExpandTime;
    }

    public override void AI()
    {
        float completionRatio = InverseLerp(0f, AvatarOfEmptiness.UniversalAnnihilation_AnnihilationSphereExpandTime, Time);
        int diameter = (int)EasingCurves.MakePoly(1.44f).Evaluate(EasingType.In, 4f, AvatarOfEmptiness.UniversalAnnihilation_MaxStarOrbitRadius * 2.9f, completionRatio);
        Projectile.Resize(diameter, diameter);

        // Kill stars within the radius of this sphere.
        int starID = ModContent.ProjectileType<StellarRemnant>();
        foreach (Projectile star in Main.ActiveProjectiles)
        {
            if (star.type != starID)
                continue;

            star.As<StellarRemnant>().OpacityFactor = InverseLerp(0.368f, 0.4f, star.Distance(Projectile.Center) / Projectile.width).Cubed();
            if (star.WithinRange(Projectile.Center, Projectile.width * 0.35f))
                star.Kill();
        }

        if (annihilationSoundLoop is null || annihilationSoundLoop.HasBeenStopped)
            annihilationSoundLoop = LoopedSoundManager.CreateNew(GennedAssets.Sounds.Avatar.UniversalAnnihilationLoop, () => !Projectile.active);
        annihilationSoundLoop.Update(Main.LocalPlayer.Center, sound =>
        {
            sound.Volume = InverseLerp(0f, 300f, Time).Cubed() * InverseLerp(0f, 120f, Projectile.timeLeft) * 0.9f;
        });

        Time++;

        ApplyDPSToPlayersInRadius();
        HandleScreenShader();
    }

    public void ApplyDPSToPlayersInRadius()
    {
        int minDPS = AvatarOfEmptiness.AnnihilationSphereMinPlayerDPS;
        int maxDPS = AvatarOfEmptiness.AnnihilationSphereMaxPlayerDPS;
        foreach (Player player in Main.ActivePlayers)
        {
            float playerOriginDistanceInterpolant = InverseLerp(0.38f, 0f, player.Distance(Projectile.Center) / Projectile.width);
            float lolDieInterpolant = InverseLerp(1f, 0.6f, playerOriginDistanceInterpolant);
            if (playerOriginDistanceInterpolant <= 0f)
                continue;

            player.AddBuff(ModContent.BuffType<AntimatterAnnihilation>(), 4);
            player.GetValueRef<int>(AntimatterAnnihilation.DPSVariableName).Value = (int)Lerp(minDPS, maxDPS, lolDieInterpolant);
        }
    }

    public static void HandleScreenShader()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        ManagedScreenFilter annihilationSphereShader = ShaderManager.GetFilter("NoxusBoss.AnnihilationSphereOverlayShader");
        annihilationSphereShader.TrySetParameter("projection", Matrix.Invert(Main.GameViewMatrix.TransformationMatrix));
        annihilationSphereShader.SetTexture(AnnihilationTarget, 1, SamplerState.LinearWrap);
        annihilationSphereShader.Activate();
    }

    public override bool? CanDamage() => Projectile.timeLeft >= 45;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        return CircularHitboxCollision(Projectile.Center, Projectile.width * 0.38f, targetHitbox);
    }

    private void UpdateTarget()
    {
        IEnumerable<Projectile> annihilationSpheres = AllProjectilesByID(Type);
        if (!annihilationSpheres.Any())
            return;

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        gd.SetRenderTarget(AnnihilationTarget);
        gd.Clear(Color.Transparent);

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

        foreach (Projectile sphere in annihilationSpheres)
        {
            float time = Main.GlobalTimeWrappedHourly + Projectile.identity * 0.271f;
            if (WoTGConfig.Instance.PhotosensitivityMode)
                time *= 0.05f;

            ManagedShader annihilationSphereShader = ShaderManager.GetShader("NoxusBoss.AnnihilationSphereShader");
            annihilationSphereShader.TrySetParameter("time", time);
            annihilationSphereShader.TrySetParameter("aspectRatioCorrectionFactor", Vector2.One);
            annihilationSphereShader.TrySetParameter("sourcePosition", Vector2.One * 0.5f);
            annihilationSphereShader.TrySetParameter("blackRadius", 0.4f);
            annihilationSphereShader.TrySetParameter("distortionStrength", 1f);
            annihilationSphereShader.TrySetParameter("maxLensingAngle", 50.2f);
            annihilationSphereShader.TrySetParameter("accretionDiskFadeColor", Color.DeepSkyBlue);
            annihilationSphereShader.TrySetParameter("brightColor", new Vector4(0.15f, 1f, 3f, 1f) * InverseLerp(0f, 120f, sphere.timeLeft));
            annihilationSphereShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 1, SamplerState.LinearWrap);
            annihilationSphereShader.Apply();

            Vector2 drawPosition = sphere.Center - Main.screenPosition;
            Main.spriteBatch.Draw(WhitePixel, drawPosition, null, sphere.GetAlpha(Color.White), 0f, WhitePixel.Size() * 0.5f, sphere.Size / WhitePixel.Size(), 0, 0f);
        }

        Main.spriteBatch.End();

        gd.SetRenderTarget(null);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.spriteBatch.Draw(AnnihilationTarget, Vector2.Zero, Color.White);
        return false;
    }
}
