using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Core.CrossCompatibility.Inbound.ModReferences;

namespace NoxusBoss.Core.CrossCompatibility.Inbound.Infernum;

public class InfernumCompatibilitySystem : ModSystem
{
    public static bool InfernumModeIsActive
    {
        get
        {
            if (InfernumMod is null)
                return false;

            return (bool)InfernumMod.Call("GetInfernumActive");
        }
    }

    public static bool BossCardModCallsExist => InfernumMod is not null && InfernumMod.Version >= new Version(2, 0, 0);

    public static bool BossBarModCallsExist => InfernumMod is not null && InfernumMod.Version >= new Version(2, 0, 0);

    public override void PostSetupContent()
    {
        LoadBossIntroCardSuport();
        LoadBossBarSuport();
    }

    internal void LoadBossIntroCardSuport()
    {
        if (!BossCardModCallsExist)
            return;

        // Collect all bosses that should adhere to Infernum's boss card mod calls.
        IEnumerable<ModNPC> modNPCsWithIntroSupport = Mod.LoadInterfacesFromContent<ModNPC, IInfernumBossBarSupport>();

        // Use the mod call for boss intro cards.
        foreach (ModNPC? modNPC in modNPCsWithIntroSupport)
        {
            IInfernumBossIntroCardSupport? introCardSupport = modNPC as IInfernumBossIntroCardSupport;
            if (introCardSupport is not null)
                PrepareIntroCard(introCardSupport);
        }
    }

    internal void LoadBossBarSuport()
    {
        if (!BossBarModCallsExist || Main.netMode == NetmodeID.Server)
            return;

        // Collect all bosses that should adhere to Infernum's boss card mod calls.
        IEnumerable<ModNPC> modNPCsWithBarSupport = Mod.LoadInterfacesFromContent<ModNPC, IInfernumBossBarSupport>();

        // Use the mod call for boss intro cards.
        foreach (ModNPC modNPC in modNPCsWithBarSupport)
        {
            if (modNPC is not IInfernumBossBarSupport bossBarSupport)
                continue;

            Texture2D iconTexture = ModContent.Request<Texture2D>(modNPC.BossHeadTexture, AssetRequestMode.ImmediateLoad).Value;
            InfernumMod.Call("RegisterBossBarPhaseInfo", modNPC.Type, bossBarSupport.PhaseThresholdLifeRatios.ToList(), iconTexture);
        }
    }

    public static void PrepareIntroCard(IInfernumBossIntroCardSupport bossIntroCard)
    {
        // Initialize the base instance for the intro card. Alternative effects may be added separately.
        Func<bool> isActiveDelegate = bossIntroCard.ShouldDisplayIntroCard;
        Func<float, float, Color> textColorSelectionDelegate = bossIntroCard.GetIntroCardTextColor;
        object instance = InfernumMod.Call("InitializeIntroScreen", bossIntroCard.IntroCardTitleName, bossIntroCard.IntroCardAnimationDuration, bossIntroCard.ShouldIntroCardTextBeCentered, isActiveDelegate, textColorSelectionDelegate);
        InfernumMod.Call("IntroScreenSetupLetterDisplayCompletionRatio", instance, new Func<int, float>(animationTimer => Saturate(animationTimer / (float)bossIntroCard.IntroCardAnimationDuration * 1.36f)));

        // Check for optional data and then apply things as needed via optional mod calls.

        // On-completion effects.
        Action onCompletionDelegate = bossIntroCard.OnIntroCardCompletion;
        InfernumMod.Call("IntroScreenSetupCompletionEffects", instance, onCompletionDelegate);

        // Letter addition sound.
        Func<SoundStyle> chooseLetterSoundDelegate = bossIntroCard.ChooseIntroCardLetterSound;
        InfernumMod.Call("IntroScreenSetupLetterAdditionSound", instance, chooseLetterSoundDelegate);

        // Main sound.
        Func<SoundStyle> chooseMainSoundDelegate = bossIntroCard.ChooseIntroCardMainSound;
        Func<int, int, float, float, bool> why = (_, _2, _3, _4) => true;
        InfernumMod.Call("IntroScreenSetupMainSound", instance, why, chooseMainSoundDelegate);

        // Letter shader draw application.
        if (bossIntroCard.LetterDrawShaderEffect is not null)
            InfernumMod.Call("IntroScreenSetupLetterShader", instance, bossIntroCard.LetterDrawShaderEffect, (object)bossIntroCard.PrepareLetterDrawShader);

        // Text scale.
        InfernumMod.Call("IntroScreenSetupTextScale", instance, bossIntroCard.IntroCardScale);

        // Register the intro card.
        InfernumMod.Call("RegisterIntroScreen", instance);
    }
}
