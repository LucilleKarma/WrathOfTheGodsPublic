using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public class TrappingHolographicForcefield : ModNPC
{
    /// <summary>
    /// Whether the latest impact was a big one.
    /// </summary>
    public bool BigImpactHappening
    {
        get => NPC.ai[1] == 1f;
        set => NPC.ai[1] = value.ToInt();
    }

    /// <summary>
    /// How long this forcefield has existed, in frames.
    /// </summary>
    public ref float Time => ref NPC.ai[0];

    /// <summary>
    /// How long this forcefield spends fading in.
    /// </summary>
    public static int FadeInTime => SecondsToFrames(0.42f);

    /// <summary>
    /// A countdown that dictates visual effects related to impacts to the forcefield.
    /// </summary>
    public ref float ImpactTimerCountdown => ref NPC.localAI[0];

    /// <summary>
    /// The disappearance timer of this forcefield. Used to make the forcefield grow and fade away before disapearing.
    /// </summary>
    public ref float DisappearanceTimer => ref NPC.localAI[2];

    /// <summary>
    /// How long impact effects to the forcefield should last.
    /// </summary>
    public static int ImpactEffectDuration => SecondsToFrames(0.15f);

    /// <summary>
    /// How long this forcefield spends disappearing.
    /// </summary>
    public static int DisappearanceDuration => SecondsToFrames(0.65f);

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults() => this.ExcludeFromBestiary();

    public override void SetDefaults()
    {
        NPC.width = (int)MarsBody.ElectricCageBlasts_ForcefieldSize;
        NPC.height = (int)MarsBody.ElectricCageBlasts_ForcefieldSize;

        // Define HP.
        NPC.lifeMax = MarsBody.GetAIInt("ForcefieldHP");

        // Do not use any default AI states.
        NPC.aiStyle = -1;
        AIType = -1;

        // Use 100% knockback resistance.
        NPC.knockBackResist = 0f;

        // Be immune to lava.
        NPC.lavaImmune = true;

        // Disable tile collision and gravity.
        NPC.noGravity = true;
        NPC.noTileCollide = true;
    }

    public override void AI()
    {
        // This is necessary to ensure that the NPC's hitbox can be moused over.
        NPC.frame.Width = NPC.width;
        NPC.frame.Height = NPC.height;

        // Immediately disappear if Mars is not present.
        if (MarsBody.Myself is null)
        {
            NPC.active = false;
            return;
        }

        if (DisappearanceTimer <= 1f)
            NPC.Center = Vector2.Lerp(NPC.Center, MarsBody.Myself.As<MarsBody>().ElectricCageBlasts_ForcefieldPosition, 0.23f);

        if (ImpactTimerCountdown > 0f)
        {
            ImpactTimerCountdown--;
            if (ImpactTimerCountdown <= 0f)
            {
                BigImpactHappening = false;
                NPC.netUpdate = true;
            }
        }

        float fadeInInterpolant = Pow(InverseLerp(0f, FadeInTime, Time), 1.67f);
        NPC.Opacity = SmoothStep(0f, 0.8f, Pow(fadeInInterpolant, 1.5f));
        NPC.scale = SmoothStep(2.3f, 1f, fadeInInterpolant) - Convert01To010(Pow(fadeInInterpolant, 0.67f)) * 0.7f;

        float impactPulse = Convert01To010(ImpactTimerCountdown / ImpactEffectDuration);
        NPC.Opacity += SmoothStep(0f, 0.08f, impactPulse) * (BigImpactHappening ? -2f : 1f);
        NPC.scale += SmoothStep(0f, 0.024f, impactPulse) * (BigImpactHappening ? -5f : 1f);

        if (DisappearanceTimer >= 1f)
        {
            DisappearanceTimer++;
            float disappearanceInterpolant = DisappearanceTimer / DisappearanceDuration;

            NPC.scale += disappearanceInterpolant.Squared() * 2f;
            NPC.Opacity *= 1f - disappearanceInterpolant;

            if (disappearanceInterpolant >= 1f)
                NPC.active = false;
        }

        Time++;
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (ImpactTimerCountdown <= 0f)
        {
            ImpactTimerCountdown = ImpactEffectDuration;
            BigImpactHappening = true;
        }

        if (NPC.soundDelay <= 0)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Solyn.ForcefieldHit with { Volume = 0.85f, MaxInstances = 0 }, NPC.Center);
            NPC.soundDelay = 3;
        }
    }

    /// <summary>
    /// Draws the back part of this forcefield.
    /// </summary>
    public void DrawBack()
    {
        Texture2D glow = BloomCircleSmall.Value;
        Vector2 drawPosition = NPC.Center - Main.screenPosition;

        for (int i = 0; i < 4; i++)
        {
            float scale = (i * 0.3f + 0.95f) * NPC.scale;
            Main.spriteBatch.Draw(glow, drawPosition, null, NPC.GetAlpha(new Color(20, i * 37 + 19, 230, 0)) with { A = 0 }, 0f, glow.Size() * 0.5f, scale, 0, 0f);
        }
        DrawForcefield(Time / -210f, 1.5f);
    }

    /// <summary>
    /// Draws the front part of this forcefield.
    /// </summary>
    public void DrawFront() => DrawForcefield(Time / 210f, 2f);

    public void DrawForcefield(float time, float spherePatternZoom)
    {
        Main.spriteBatch.PrepareForShaders();

        Vector2 forcefieldScale = NPC.Size * NPC.scale * 1.25f;
        ManagedShader forcefieldShader = ShaderManager.GetShader("NoxusBoss.TrappingHolographicForcefieldShader");
        forcefieldShader.TrySetParameter("sphereSpinTime", time);
        forcefieldShader.TrySetParameter("spherePatternZoom", spherePatternZoom);
        forcefieldShader.TrySetParameter("pinchExponent", 1.7f);
        forcefieldShader.TrySetParameter("pixelationFactor", new Vector2(0.005f));
        forcefieldShader.SetTexture(HexagonalLattice, 1, SamplerState.LinearWrap);
        forcefieldShader.Apply();

        Color forcefieldColor = new Color(70, 174, 255).HueShift(Convert01To010(InverseLerp(0f, FadeInTime, Time)) * -0.08f);
        Vector2 drawPosition = NPC.Center - Main.screenPosition;
        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, NPC.GetAlpha(forcefieldColor) * 0.6f, NPC.rotation, WhitePixel.Size() * 0.5f, forcefieldScale, 0, 0f);

        Main.spriteBatch.ResetToDefault();
    }
}
