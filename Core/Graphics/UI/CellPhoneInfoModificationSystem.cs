﻿using Luminance.Core.Hooking;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using NoxusBoss.Content.NPCs.Bosses.Avatar;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;
using NoxusBoss.Core.World.GameScenes.TerminusStairway;
using NoxusBoss.Core.World.Subworlds;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static NoxusBoss.Core.World.TileDisabling.TileDisablingSystem;

namespace NoxusBoss.Core.Graphics.UI;

public class CellPhoneInfoModificationSystem : ModSystem
{
    public enum InfoType
    {
        Time,
        Weather,
        FishingPower,
        MoonPhase,
        TreasureTiles,
        PlayerSpeed,
        PlayerXPosition,
        PlayerYPosition
    }

    public delegate string TextReplacementFunction(string originalText);

    public override void OnModLoad()
    {
        new ManagedILEdit("Obfuscate Position Info", Mod, edit =>
        {
            IL_Main.DrawInfoAccs += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Main.DrawInfoAccs -= edit.SubscriptionWrapper;
        }, ObfuscatePositionInfo).Apply(false);
    }

    private void ObfuscatePositionInfo(ILContext context, ManagedILEdit edit)
    {
        int displayTextIndex = 0;
        ILCursor cursor = new ILCursor(context);

        // Find the text display string.
        for (int i = 0; i < 2; i++)
        {
            if (!cursor.TryGotoNext(MoveType.Before, c => c.MatchLdstr(out _)))
            {
                edit.LogFailure($"The {(i == 0 ? "first" : "second")} ldstr opcode could not be found.");
                return;
            }
        }

        // Store the display string's local index.
        if (!cursor.TryGotoNext(c => c.MatchStloc(out displayTextIndex)))
        {
            edit.LogFailure($"The displayText local index storage could not be found.");
            return;
        }

        // Change text for the watch.
        if (!cursor.TryGotoNext(MoveType.After, c => c.MatchLdsfld<Main>("time")))
        {
            edit.LogFailure($"The Main.time load could not be found.");
            return;
        }

        cursor.Emit(OpCodes.Pop);
        cursor.EmitDelegate(() =>
        {
            if (AvatarUniverseExplorationSystem.InAvatarUniverse)
                return (double)Main.rand.NextFloat(86400f);

            if (!TilesAreUninteractable || EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame || TerminusStairwaySystem.Enabled)
                return Main.time;

            return (double)Main.rand.NextFloat(86400f);
        });

        ApplyReplacementTweak(cursor, edit, InfoType.Weather, "GameUI.PartlyCloudy", displayTextIndex, ChooseWeatherText);
        ApplyReplacementTweak(cursor, edit, InfoType.MoonPhase, "GameUI.FullMoon", displayTextIndex, ChooseMoonPhaseText, 8);
        ApplyReplacementTweak(cursor, edit, InfoType.FishingPower, "GameUI.FishingPower", displayTextIndex, ChooseFishingPowerText);
        ApplyReplacementTweak(cursor, edit, InfoType.TreasureTiles, "GameUI.OreDetected", displayTextIndex, ChooseTreasureTilesText);
        ApplyReplacementTweak(cursor, edit, InfoType.PlayerSpeed, "GameUI.Speed", displayTextIndex, ChooseSpeedText);
        ApplyReplacementTweak(cursor, edit, InfoType.PlayerXPosition, "GameUI.CompassEast", displayTextIndex, ChoosePlayerXPositionText);
        ApplyReplacementTweak(cursor, edit, InfoType.PlayerYPosition, "GameUI.LayerUnderground", displayTextIndex, ChoosePlayerYPositionText);
    }

    /// <summary>
    /// Determines what text should be displayed regarding weather.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChooseWeatherText(string originalText)
    {
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
        if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.CryonicDimension)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.CryogenicMaelstromText");
        if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.VisceralDimension)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.VisceralTyphoonText");
        if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.FogDimension || AvatarUniverseExplorationSystem.InAvatarUniverse)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.DyingWorldFogText");
        if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.DarkDimension)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.EmptinessWeatherText");
        if (AvatarOfEmptiness.Myself is not null && !AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().BattleIsDone)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.CrimsonDerechoText");

        return originalText;
    }

    /// <summary>
    /// Determines what text should be displayed regarding fishing power.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChooseFishingPowerText(string originalText)
    {
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");

        return originalText;
    }

    /// <summary>
    /// Determines what text should be displayed regarding the moon phase.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChooseMoonPhaseText(string originalText)
    {
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
        if (TilesAreUninteractable && !TerminusStairwaySystem.Enabled)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.MoonNotFoundText");

        return originalText;
    }

    /// <summary>
    /// Determines what text should be displayed regarding treasure tiles, such as chests and ores.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChooseTreasureTilesText(string originalText)
    {
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
        if (TilesAreUninteractable)
            return Language.GetTextValue("GameUI.NoTreasureNearby");

        return originalText;
    }

    /// <summary>
    /// Determines what text should be displayed regarding player speed.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChooseSpeedText(string originalText)
    {
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
        if (TilesAreUninteractable && AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState == AvatarOfEmptiness.AvatarAIType.TravelThroughVortex)
        {
            float shownSpeed = AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().TravelThroughVortex_ShownPlayerSpeed;
            return Language.GetTextValue("GameUI.Speed", Math.Round(shownSpeed));
        }

        return originalText;
    }

    /// <summary>
    /// Determines what text should be displayed regarding player X position.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChoosePlayerXPositionText(string originalText)
    {
        if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
            return "???";
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
        if (TilesAreUninteractable && AvatarOfEmptiness.Myself is not null)
            return AvatarOfEmptiness.CompassTextInMyUniverse;
        if (AvatarUniverseExplorationSystem.InAvatarUniverse)
        {
            ulong parsecs = Utils.RandomNextSeed((ulong)WorldGen._genRandSeed) / 100;
            return Language.GetText($"Mods.NoxusBoss.CellPhoneInfoOverrides.ParsecText").Format($"{parsecs:n0}");
        }

        return originalText;
    }

    /// <summary>
    /// Determines what text should be displayed regarding player Y position.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    private static string ChoosePlayerYPositionText(string originalText)
    {
        if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
            return "???";
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
            return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
        if (TilesAreUninteractable && AvatarOfEmptiness.Myself is not null)
            return AvatarOfEmptiness.DepthTextInMyUniverse;
        if (AvatarUniverseExplorationSystem.InAvatarUniverse)
        {
            ulong parsecs = Utils.RandomNextSeed((ulong)WorldGen._genRandSeed + 472589) / 100;
            return Language.GetText($"Mods.NoxusBoss.CellPhoneInfoOverrides.ParsecText").Format($"{parsecs:n0}");
        }

        return originalText;
    }

    private static void ApplyReplacementTweak(ILCursor cursor, ManagedILEdit edit, InfoType infoType, string searchString, int displayTextIndex, TextReplacementFunction replacementFunction, int loopCount = 1)
    {
        if (!cursor.TryGotoNext(c => c.MatchLdstr(searchString)))
        {
            edit.LogFailure($"The '{searchString}' string load could not be found!");
            return;
        }

        for (int i = 1; i <= loopCount; i++)
        {
            if (!cursor.TryGotoNext(MoveType.After, c => c.MatchStloc(displayTextIndex)))
            {
                edit.LogFailure($"{infoType} search #{loopCount} for the displayText local variable could not be found.");
                return;
            }

            cursor.Emit(OpCodes.Ldloc, displayTextIndex);
            cursor.EmitDelegate(replacementFunction);
            cursor.Emit(OpCodes.Stloc, displayTextIndex);
        }
    }
}
