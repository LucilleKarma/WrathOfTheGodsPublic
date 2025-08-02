using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.ID;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

// Layer ordering is as follows:
// 1. Back legs.
// 2. Back arms.
// 3. Bloodied lily.
// 4. Head and neck.
// 5. Dark smoke.
// 6. Front arm props.
// 7. Front arms.
// 8. Blood.
// 9. Spider lily.
// Some of these are in practice represented as a single thing in the form of render targets.
public partial class AvatarOfEmptiness
{
    /// <summary>
    /// The identifier for rendering for the Avatar.
    /// </summary>
    public int RenderTargetIdentifier
    {
        get
        {
            if (NPC.IsABestiaryIconDummy)
                return -32000;

            return NPC.whoAmI + 32000;
        }
    }

    /// <summary>
    /// Whether the Avatar is being drawn as a silhouette.
    /// </summary>
    public bool DrawnAsSilhouette
    {
        get;
        set;
    }

    /// <summary>
    /// The base angle at which the Avatar's hands are being held. This affects the base digit.
    /// </summary>
    public float HandBaseGraspAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The angle at which the Avatar's hands are being held. This affects individual digits. Useful for punch-like effects where fingers should be clenched.
    /// </summary>
    public float HandGraspAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The angle at which the Avatar's index finger digits are drawn. Useful for flick-like effects.
    /// </summary>
    public float? IndexFingerAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The angle at which the Avatar's ring finger digits are drawn. Useful for flick-like effects.
    /// </summary>
    public float? RingFingerAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The angle at which the Avatar's thumb digits are drawn. Useful for flick-like effects.
    /// </summary>
    public float? ThumbAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the Avatar's left front arm.
    /// </summary>
    public float LeftFrontArmOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the Avatar's right front arm.
    /// </summary>
    public float RightFrontArmOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the Avatar's right front arm afterimages.
    /// </summary>
    public float LeftFrontArmAfterimageOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the Avatar's right front arm afterimages.
    /// </summary>
    public float RightFrontArmAfterimageOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The scale of the Avatar's left front arm.
    /// </summary>
    public float LeftFrontArmScale
    {
        get;
        set;
    }

    /// <summary>
    /// The scale of the Avatar's right front arm.
    /// </summary>
    public float RightFrontArmScale
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the Avatar's head.
    /// </summary>
    public float HeadOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The rotational offset of the Avatar's mask.
    /// </summary>
    public float MaskRotation
    {
        get;
        set;
    }

    /// <summary>
    /// The scale factor of the Avatar's head.
    /// </summary>
    public float HeadScaleFactor
    {
        get;
        set;
    }

    /// <summary>
    /// An animation interpolant for the Avatar's head. As this approaches 1, the Avatar's neck segments materialize, giving the illusion of jutting forward in space.
    /// </summary>
    public float NeckAppearInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The scale of the Avatar's spider lily.
    /// </summary>
    public float LilyScale
    {
        get;
        set;
    }

    /// <summary>
    /// The amount by which the Avatar's spider lily glow should be increased.
    /// </summary>
    public float LilyGlowIntensityBoost
    {
        get;
        set;
    }

    /// <summary>
    /// The amount by which the Avatar's spider lily glow should draw as a frozen cyan color.
    /// </summary>
    public float LilyFreezeInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The scale of the Avatar's arm portals.
    /// </summary>
    public float FrontArmPortalScale
    {
        get;
        set;
    }

    /// <summary>
    /// The brightness of the Avatar's body.
    /// </summary>
    public float BodyBrightness
    {
        get;
        set;
    } = 1f;

    /// <summary>
    /// A 0-1 interpolant that represents how strong suck visuals are.
    /// </summary>
    public float SuckOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The frame of the Avatar's mask.
    /// </summary>
    public int MaskFrame
    {
        get;
        set;
    }

    /// <summary>
    /// The scale of the Avatar's shadowy back legs.
    /// </summary>
    public Vector2 LegScale
    {
        get;
        set;
    }

    /// <summary>
    /// Positions the Avatar's left front arm used to be in.
    /// </summary>
    public Vector2[] PastLeftArmPositions
    {
        get;
        set;
    } = new Vector2[15];

    /// <summary>
    /// Positions the Avatar's right front arm used to be in.
    /// </summary>
    public Vector2[] PastRightArmPositions
    {
        get;
        set;
    } = new Vector2[15];

    /// <summary>
    /// The world position of the Avatar's spider lily, taking into account <see cref="NPC.scale"/>.
    /// </summary>
    public Vector2 SpiderLilyPosition => NPC.Center - Vector2.UnitY * NPC.scale * LilyScale * ZPositionScale * 140f;

    /// <summary>
    /// The hitbox of the Avatar's spider lily, taking into account <see cref="NPC.scale"/>.
    /// </summary>
    public Rectangle SpiderLilyHitbox => Utils.CenteredRectangle(SpiderLilyPosition, new Vector2(500f, 500f) * NPC.scale);

    /// <summary>
    /// Whether the Avatar should draw behind tiles in its layering due to his current <see cref="ZPosition"/>.
    /// </summary>
    public bool ShouldDrawBehindTiles => ZPosition >= 0.2f;

    /// <summary>
    /// Whether the Avatar should draw over everything in its layering due to his current <see cref="ZPosition"/>.
    /// </summary>
    public bool ShouldDrawOverEverything => ZPosition <= -0.1f;

    /// <summary>
    /// The scale of the Avatar's front arms.
    /// </summary>
    public float ArmScale => NPC.scale * 0.67f;

    /// <summary>
    /// The scale of the Avatar's head and mask.
    /// </summary>
    public float HeadScale => NPC.scale * HeadScaleFactor * 0.8f;

    /// <summary>
    /// The visual handler for the Avatar's liquid visuals.
    /// </summary>
    public AvatarRiftLiquidInfo LiquidDrawContents
    {
        get;
        private set;
    }

    /// <summary>
    /// Loads all of the Avatar's textures.
    /// </summary>
    public void LoadTargets()
    {
        // Don't attempt to load targets server-side.
        if (Main.netMode == NetmodeID.Server)
            return;

        LoadTargets_Body();
        LoadTargets_ShadowyParts();
        LoadTargets_Silhouette();
        LoadTargets_Final();
    }

    public override void BossHeadSlot(ref int index)
    {
        // Make the head icon disappear if the Avatar is invisible.
        if (NPC.Opacity <= 0.45f)
            index = -1;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (NPC.IsABestiaryIconDummy)
        {
            RiftRotationTimer = Main.GlobalTimeWrappedHourly * 0.03f;

            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Scale = 0.15f,
                PortraitScale = 0.2f,
                Position = -Vector2.UnitY * 85f
            };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;

            NeckAppearInterpolant = 1f;
            LeftFrontArmScale = 1f;
            RightFrontArmScale = 1f;
            LeftFrontArmOpacity = 1f;
            RightFrontArmOpacity = 1f;
            LilyScale = 1f;
            LegScale = Vector2.One;
            HeadOpacity = 1f;
            HeadScaleFactor = 1f;
            MaskFrame = 23;

            LeftArmPosition = NPC.Center + new Vector2(-600f, 300f);
            RightArmPosition = NPC.Center + new Vector2(600f, 300f);

            float wave = Cos(Main.GlobalTimeWrappedHourly * 1.8f);
            HeadPosition = NPC.Center + Vector2.UnitY.RotatedBy(wave * 0.06f) * 750f;
        }

        ProcessTargets();
        RenderSuckEffect(screenPos);
        RenderFromFinalTarget(screenPos);
        DrawLilyGlow(screenPos);
        UniversalAnnihilation_DrawGleam(screenPos);
        DrawBloodyWeepTears(HeadPosition - screenPos);

        return false;
    }

    public void RenderSuckEffect(Vector2 screenPos)
    {
        if (SuckOpacity <= 0f)
            return;

        float distortionFalloff = InverseLerp(2f, 0f, ZPosition) * SuckOpacity * (1f - AvatarRiftSuckVisualsManager.ZoomInInterpolant);
        ManagedScreenFilter spaghettificationShader = ShaderManager.GetFilter("NoxusBoss.AvatarRiftSpaghettificationShader");
        spaghettificationShader.TrySetParameter("distortionRadius", SuckOpacity * 480f);
        spaghettificationShader.TrySetParameter("distortionIntensity", distortionFalloff * 0.8f);
        spaghettificationShader.TrySetParameter("distortionPosition", Vector2.Transform(NPC.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
        spaghettificationShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom.X);
        spaghettificationShader.SetTexture(InvisiblePixel, 1, SamplerState.LinearClamp);
        spaghettificationShader.Activate();

        if (WoTGConfig.Instance.PhotosensitivityMode)
            return;

        float suctionBaseRange = 850f;

        // During Antishadow Onslaught, the suction visual becomes pure black, due to the overlay shader.
        // To account for this and fix massive amounts of screen space been blacked out, the radius of the sunction is reduced a moderate amount.
        if (CurrentState == AvatarAIType.AntishadowOnslaught)
            suctionBaseRange *= 0.6f;

        ManagedScreenFilter suctionShader = ShaderManager.GetFilter("NoxusBoss.SuctionSpiralShader");
        suctionShader.TrySetParameter("suctionCenter", Vector2.Transform(NPC.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
        suctionShader.TrySetParameter("zoomedScreenSize", Main.ScreenSize.ToVector2() / Main.GameViewMatrix.Zoom);
        suctionShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom.X);
        suctionShader.TrySetParameter("suctionOpacity", SuckOpacity * (1f - AvatarRiftSuckVisualsManager.ZoomInInterpolant) * 0.32f);
        suctionShader.TrySetParameter("suctionBaseRange", suctionBaseRange);
        suctionShader.TrySetParameter("suctionFadeOutRange", 600f);
        suctionShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        suctionShader.Activate();

        // Create a sucking effect over everything.
        if (AvatarOfEmptinessSky.Dimension?.AppliesSilhouetteShader ?? false)
            return;

        float suckPulse = 1f - Main.GlobalTimeWrappedHourly * 6.7f % 1f;
        float suckRotation = Main.GlobalTimeWrappedHourly * -1.2f;
        Color suckColor = new Color(255, 198, 255) * InverseLerpBump(0.05f, 0.25f, 0.67f, 1f, suckPulse) * NPC.Opacity * SuckOpacity * 0.23f;
        suckColor.A = 0;

        Main.spriteBatch.Draw(ChromaticBurst, NPC.Center - screenPos, null, suckColor, suckRotation, ChromaticBurst.Size() * 0.5f, Vector2.One * suckPulse * 5f, 0, 0f);
        Main.spriteBatch.Draw(ChromaticBurst, NPC.Center - screenPos, null, suckColor, -suckRotation, ChromaticBurst.Size() * 0.5f, Vector2.One * suckPulse * 3.8f, 0, 0f);
    }

    public void RenderBodyWithPostProcessing(Vector2 screenPos)
    {
        if (!BodyRenderTarget.TryGetTarget(RenderTargetIdentifier, out RenderTarget2D? target) || target is null)
            return;

        float blurOffset = 0.00061f;
        float[] blurWeights = new float[7];
        for (int i = 0; i < blurWeights.Length; i++)
            blurWeights[i] = GaussianDistribution(i - blurWeights.Length * 0.5f, 1.1f) / 7f;

        Dictionary<string, Vector3> colorsSet = LocalDataManager.Read<Vector3>("Content/NPCs/Bosses/Avatar/AvatarColors.json");
        float glowFade = Utils.Remap(ZPosition, 0f, 2.5f, 1f, 0.65f);
        ManagedShader overlayShader = ShaderManager.GetShader("NoxusBoss.AvatarBodyOverlayShader");
        overlayShader.TrySetParameter("headPosition", (HeadPosition - NPC.Center) / target.Size() + Vector2.One * 0.5f);
        overlayShader.TrySetParameter("centerPosition", Vector2.One * 0.5f);
        overlayShader.TrySetParameter("headDirection", Vector2.Lerp(Vector2.UnitX, NPC.SafeDirectionTo(HeadPosition + Vector2.UnitY * 50f).RotatedBy(-PiOver2), HeadScaleFactor));
        overlayShader.TrySetParameter("glowInterpolant", 0.2f);
        overlayShader.TrySetParameter("blurWeights", blurWeights);
        overlayShader.TrySetParameter("blurOffset", blurOffset);
        overlayShader.TrySetParameter("topLeftGlowColor", colorsSet["TopLeftGlowColor"] * glowFade);
        overlayShader.TrySetParameter("bottomLeftGlowColor", colorsSet["BottomLeftGlowColor"] * glowFade);
        overlayShader.TrySetParameter("topRightGlowColor", colorsSet["TopRightGlowColor"] * glowFade);
        overlayShader.TrySetParameter("bottomRightGlowColor", colorsSet["BottomRightGlowColor"] * glowFade);
        overlayShader.Apply();

        // Draw the body.
        Color bodyColor = Color.Lerp(Color.Black, Color.White, BodyBrightness) * NPC.Opacity;
        bodyColor = Color.Lerp(bodyColor, new Color(40, 0, 0) * 0.5f, (1f - ZPositionScale) * 0.85f);

        Vector2 drawPosition = NPC.Center - screenPos;
        Main.spriteBatch.Draw(target, drawPosition, target.Frame(), bodyColor, 0f, target.Size() * 0.5f, NPC.scale / TargetDownscaleFactor * ZPositionScale, 0, 0f);
    }

    public void RenderRift(Vector2 screenPos)
    {
        // Draw the rift behind everything.
        if (!DrawnAsSilhouette || CurrentState == AvatarAIType.SendPlayerToMyUniverse)
        {
            Matrix transform = NPC.IsABestiaryIconDummy ? Main.UIScaleMatrix : Main.GameViewMatrix.TransformationMatrix;
            if (UsingFinalTarget)
                transform = Matrix.Identity;

            AvatarRiftTargetContent.DrawRiftWithShader(NPC, screenPos, transform, false, SuckOpacity, -NPC.rotation, RenderTargetIdentifier);
        }
    }

    public void DrawLilyGlow(Vector2 screenPos)
    {
        if (CurrentState == AvatarAIType.AntishadowOnslaught)
            return;

        // Calculate the glow color.
        Dictionary<string, Vector3> colorsSet = LocalDataManager.Read<Vector3>("Content/NPCs/Bosses/Avatar/AvatarColors.json");
        float glowIncrement = Clamp(0.2f - LilyGlowIntensityBoost * 0.14f, 0.03f, 0.2f);
        Color minRegularGlowColor = new Color(colorsSet["LilyBaseGlowColor_Regular_MinGlow"]);
        Color maxRegularGlowColor = new Color(colorsSet["LilyBaseGlowColor_Regular_MaxGlow"]);
        Color minFreezeGlowColor = new Color(colorsSet["LilyBaseGlowColor_Freezing_MinGlow"]);
        Color maxFreezeGlowColor = new Color(colorsSet["LilyBaseGlowColor_Freezing_MaxGlow"]);
        Color freezeBaseGlowColor = Color.Lerp(minFreezeGlowColor, maxFreezeGlowColor, Saturate(LilyGlowIntensityBoost * 2.3f));
        Color baseGlowColor = Color.Lerp(minRegularGlowColor, maxRegularGlowColor, Saturate(LilyGlowIntensityBoost * 2.3f));
        baseGlowColor = Color.Lerp(baseGlowColor, freezeBaseGlowColor, LilyFreezeInterpolant * 0.8f);
        baseGlowColor.A = 0;

        for (float glowInterpolant = 0.25f; glowInterpolant < 1f; glowInterpolant += glowIncrement)
        {
            Vector2 glowScale = new Vector2(8f, 6f) * NPC.scale * LilyScale * (1f - glowInterpolant) * (0.75f + LilyGlowIntensityBoost);
            Color glowColor = baseGlowColor * NPC.Opacity * glowInterpolant * Clamp(LilyGlowIntensityBoost + 0.3f, 0f, 0.75f);
            Main.spriteBatch.Draw(BloomCircleSmall, SpiderLilyPosition - screenPos, null, glowColor, 0f, BloomCircleSmall.Size() * 0.5f, glowScale, 0, 0f);
        }

        // Draw a lens flare over the spider lily if the intensity boost is incredibly strong.
        if (LilyGlowIntensityBoost >= 1.1f)
        {
            float lensFlareInterpolant = InverseLerp(1.1f, 1.3f, LilyGlowIntensityBoost) * 0.6f;
            Vector2 glowScale = new Vector2(Cos(Main.GlobalTimeWrappedHourly * 42f) * 0.14f + 1.5f, 0.6f) * NPC.scale * LilyScale * 0.85f;
            Color glowColor = Color.Lerp(baseGlowColor, Color.White, 0.6f) * NPC.Opacity * lensFlareInterpolant;
            glowColor.A = 0;

            Main.spriteBatch.Draw(BrightSpires, SpiderLilyPosition - screenPos, null, glowColor, 0f, BrightSpires.Size() * 0.5f, glowScale, 0, 0f);
        }
    }

    public override Color? GetAlpha(Color drawColor)
    {
        return drawColor * NPC.Opacity;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        position = SpiderLilyPosition;
        return ZPosition <= 0.4f;
    }

    public override void DrawBehind(int index)
    {
        if (NPC.hide && NPC.Opacity >= 0.02f)
        {
            if (CurrentState == AvatarAIType.GivePlayerHeadpats)
            {
                Main.instance.DrawCacheNPCsOverPlayers.Add(index);
                return;
            }

            if (CurrentState == AvatarAIType.SendPlayerToMyUniverse)
            {
                Main.instance.DrawCacheNPCProjectiles.Add(index);
                return;
            }

            if (ShouldDrawOverEverything)
                SpecialLayeringSystem.DrawCacheFrontLayer.Add(index);
            else
                SpecialLayeringSystem.DrawCacheAfterBlack.Add(index);
        }
    }
}
