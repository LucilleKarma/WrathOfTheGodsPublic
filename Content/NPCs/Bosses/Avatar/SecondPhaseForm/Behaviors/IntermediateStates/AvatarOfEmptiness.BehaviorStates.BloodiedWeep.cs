using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.FormPresets;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    public float BloodyTearsAnimationStartInterpolant
    {
        get;
        set;
    }

    public float BloodyTearsAnimationEndInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar should create blood cry visuals from his face.
    /// </summary>
    public static bool CreateBloodCryVisuals => !AvatarFormPresetRegistry.UsingLucillePreset;

    /// <summary>
    /// How long the Avatar sits and place and cries during his bloodied weep attack.
    /// </summary>
    public static int BloodiedWeep_WeepDuration => GetAIInt("BloodiedWeep_WeepDuration");

    /// <summary>
    /// How long it takes for the Avatar's tears to fully fall during his bloodied weep attack.
    /// </summary>
    public static int BloodiedWeep_WeepStartTime => GetAIInt("BloodiedWeep_WeepStartTime");

    /// <summary>
    /// How long it takes for the Avatar's tears to fully fall at the end of his bloodied weep attack.
    /// </summary>
    public static int BloodiedWeep_WeepEndTime => GetAIInt("BloodiedWeep_WeepEndTime");

    [AutomatedMethodInvoke]
    public void LoadState_BloodiedWeep()
    {
        StateMachine.RegisterTransition(AvatarAIType.BloodiedWeep, null, false, () =>
        {
            return AITimer >= BloodiedWeep_WeepDuration;
        }, () =>
        {
            BloodyTearsAnimationStartInterpolant = 0f;
            BloodyTearsAnimationEndInterpolant = 0f;
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(AvatarAIType.BloodiedWeep, DoBehavior_BloodiedWeep);
    }

    public void DoBehavior_BloodiedWeep()
    {
        // Decide the distortion intensity.
        IdealDistortionIntensity = 0.67f;

        // Make the tears appear.
        BloodyTearsAnimationStartInterpolant = InverseLerp(0f, BloodiedWeep_WeepStartTime, AITimer);

        // Make the tears disappear before the state terminates.
        BloodyTearsAnimationEndInterpolant = InverseLerp(-BloodiedWeep_WeepEndTime - 30f, -30f, AITimer - BloodiedWeep_WeepDuration);

        float animationInterpolant = BloodyTearsAnimationStartInterpolant * (1f - BloodyTearsAnimationEndInterpolant);

        // Slow down.
        NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.UnitY * Cos(TwoPi * AITimer / 240f) * 1.3f, 0.15f);

        // Teleport near the target on the first frame.
        if (AITimer <= 2)
            NPC.Center = Target.Center - Vector2.UnitY * 400f;

        // Play a sound at first.
        if (AITimer == 3)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.BloodCry);

        // Enter the foreground.
        ZPosition = Lerp(ZPosition, 0f, 0.156f);

        // Make the head dangle.
        PerformBasicHeadUpdates(1.3f);
        HeadPosition += Main.rand.NextVector2Circular(6f, 2.5f);

        if (LifeRatio <= Phase3LifeRatio)
            NPC.dontTakeDamage = true;

        // Make the Avatar put his hands near his head.
        Vector2 leftArmDestination = HeadPosition + new Vector2(-600f, 300f) * NPC.scale * RightFrontArmScale;
        Vector2 rightArmDestination = HeadPosition + new Vector2(600f, 300f) * NPC.scale * RightFrontArmScale;
        LeftArmPosition = Vector2.Lerp(LeftArmPosition, leftArmDestination, 0.23f) + Main.rand.NextVector2Circular(6f, 2f) * animationInterpolant;
        RightArmPosition = Vector2.Lerp(RightArmPosition, rightArmDestination, 0.23f) + Main.rand.NextVector2Circular(6f, 2f) * animationInterpolant;

        // Release a bunch of blood particles on the Avatar's face.
        if (CreateBloodCryVisuals)
        {
            Vector2 bloodSpawnPosition = HeadPosition;
            Color bloodColor = Color.Lerp(new(255, 0, 30), Color.Brown, Main.rand.NextFloat(0.4f, 0.8f)) * animationInterpolant * 0.45f;
            LargeMistParticle blood = new LargeMistParticle(bloodSpawnPosition, Main.rand.NextVector2Circular(8f, 6f) + Vector2.UnitY * 2.9f, bloodColor, 0.5f, 0f, 45, 0f, true);
            blood.Spawn();
            for (int i = 0; i < animationInterpolant * 3f; i++)
            {
                bloodColor = Color.Lerp(new(255, 36, 0), new(73, 10, 2), Main.rand.NextFloat(0.15f, 0.7f));
                BloodParticle blood2 = new BloodParticle(bloodSpawnPosition + Main.rand.NextVector2Circular(80f, 50f), Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY * 2f, 30, Main.rand.NextFloat(1.25f), bloodColor);
                blood2.Spawn();
            }
        }

        // Shake the screen.
        ScreenShakeSystem.SetUniversalRumble(1f, TwoPi, null, 0.2f);
    }

    public void DrawBloodyWeepTears(Vector2 screenPos)
    {
        // Don't do anything if the bloody tears effect is not in use.
        if (BloodyTearsAnimationStartInterpolant <= 0f)
            return;

        if (!CreateBloodCryVisuals)
            return;

        // Prepare for shader drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        // Draw eye blood.
        ManagedShader tearsShader = ShaderManager.GetShader("NoxusBoss.AvatarBloodyTearsShader");
        tearsShader.TrySetParameter("topCutoffThresholdLeft", 0f);
        tearsShader.TrySetParameter("topCutoffThresholdRight", 0f);
        DrawBloodyTearsWithHeadOffset(new(-35f, 8f), screenPos, 46f, 1f);
        DrawBloodyTearsWithHeadOffset(new(32f, -14f), screenPos, 48f, 1f);

        // Draw left brain blood.
        tearsShader.TrySetParameter("topCutoffThresholdLeft", 0.16f);
        tearsShader.TrySetParameter("topCutoffThresholdRight", 0f);
        DrawBloodyTearsWithHeadOffset(new(-52f, -74f), screenPos, 60f, 0.81f);

        // Draw right brain blood.
        tearsShader.TrySetParameter("topCutoffThresholdLeft", 0f);
        tearsShader.TrySetParameter("topCutoffThresholdRight", 0.05f);
        DrawBloodyTearsWithHeadOffset(new(14f, -72f), screenPos, 70f, 0.81f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.AnisotropicClamp, DepthStencilState.None, Main.Rasterizer, null, Matrix.Identity);
    }

    public void DrawBloodyTearsWithHeadOffset(Vector2 headOffset, Vector2 screenPos, float width, float opacity)
    {
        // Prepare the blood shader.
        Vector2 bloodSize = new Vector2(width, 380f);
        Vector2 drawPositionRT = screenPos + headOffset + new Vector2(20f, 4f);
        Vector2 drawPositionScreen = drawPositionRT;
        Vector2 origin = WhitePixel.Size() * new Vector2(0.5f, 0f);
        Vector2 bloodScale = bloodSize / WhitePixel.Size();

        Vector2 playerTopLeft = Vector2.One * -999f;
        Vector2 playerTopRight = Vector2.One * -999f;

        // Rearrange coordinates into relative UVs.
        playerTopLeft.X = InverseLerp(drawPositionScreen.X - bloodSize.X * 0.1f, drawPositionScreen.X + bloodSize.X * 0.5f, playerTopLeft.X, false);
        playerTopRight.X = InverseLerp(drawPositionScreen.X - bloodSize.X * 0.1f, drawPositionScreen.X + bloodSize.X * 0.5f, playerTopRight.X, false);
        playerTopLeft.Y = InverseLerp(drawPositionScreen.Y, drawPositionScreen.Y + bloodSize.Y, playerTopLeft.Y + 180f, false) * 0.45f;
        playerTopRight.Y = InverseLerp(drawPositionScreen.Y, drawPositionScreen.Y + bloodSize.Y, playerTopRight.Y + 180f, false) * 0.45f;

        // Check if the player is in the range of blood. If they are, make their hair red.
        if (playerTopLeft.Between(Vector2.Zero, Vector2.One) || playerTopRight.Between(Vector2.Zero, Vector2.One))
        {
            Core.DataStructures.Referenced<float> hairBloodiness = PlayerBloodiedHairSystem.GetPlayerHairBloodiness(Main.LocalPlayer);
            hairBloodiness.Value = Saturate(hairBloodiness.Value + 0.02f);
        }

        ManagedShader tearsShader = ShaderManager.GetShader("NoxusBoss.AvatarBloodyTearsShader");
        tearsShader.TrySetParameter("animationStartInterpolant", BloodyTearsAnimationStartInterpolant);
        tearsShader.TrySetParameter("animationEndInterpolant", BloodyTearsAnimationEndInterpolant);
        tearsShader.TrySetParameter("playerTopLeft", playerTopLeft);
        tearsShader.TrySetParameter("playerTopRight", playerTopRight);
        tearsShader.TrySetParameter("playerWidth", Distance(playerTopLeft.X, playerTopRight.X) * 0.5f);
        tearsShader.SetTexture(ViscousNoise, 1, SamplerState.LinearWrap);
        tearsShader.SetTexture(WavyBlotchNoise, 2, SamplerState.LinearWrap);
        tearsShader.Apply();

        // Draw the blood.
        DrawData targetData = new DrawData(WhitePixel, drawPositionRT, null, new Color(82, 1, 3) * opacity, 0f, origin, bloodScale, 0, 0f);
        targetData.Draw(Main.spriteBatch);
    }
}
