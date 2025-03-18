using System.Reflection;
using CalamityMod;
using CalamityMod.TileEntities;
using CalamityMod.UI.DraedonSummoning;
using CalamityMod.World;
using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Draedon;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SolynEvents;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityMod.UI.DraedonSummoning.CodebreakerUI;

namespace NoxusBoss.Core.Graphics.UI.Codebreaker;

[JITWhenModsEnabled(CalamityCompatibility.ModName)]
[ExtendsFromMod(CalamityCompatibility.ModName)]
public class CodebreakerUIRewriter : ModSystem
{
    /// <summary>
    /// The IL edit responsible for the modifying Draedon summon conditions to include <see cref="QuestDraedon"/>.
    /// </summary>
    public static ILHook DraedonSummonConditionHook
    {
        get;
        private set;
    }

    /// <summary>
    /// The detour responsible for the modifying the Draedon summon UI to include <see cref="QuestDraedon"/>.
    /// </summary>
    public static Hook DraedonSummonUIHook
    {
        get;
        private set;
    }

    /// <summary>
    /// The palette that contains the exo mech colors.
    /// </summary>
    public static readonly Palette ExoPalette = new Palette(CalamityUtils.ExoPalette);

    public delegate void orig_HandleDraedonSummonButton(TECodebreaker codebreakerTileEntity, Vector2 drawPosition);

    public delegate void hook_HandleDraedonSummonButton(orig_HandleDraedonSummonButton orig, TECodebreaker codebreakerTileEntity, Vector2 drawPosition);

    public override void Load()
    {
        MethodInfo? drawMethod = typeof(CodebreakerUI).GetMethod("Draw", UniversalBindingFlags);
        MethodInfo? handleDraedonSummonButtonMethod = typeof(CodebreakerUI).GetMethod("HandleDraedonSummonButton", UniversalBindingFlags);
        if (drawMethod is null)
        {
            Mod.Logger.Warn("Could not find the Draw method in CodebreakerUI.");
            return;
        }
        if (handleDraedonSummonButtonMethod is null)
        {
            Mod.Logger.Warn("Could not find the HandleDraedonSummonButton method in CodebreakerUI.");
            return;
        }

        // Apply the IL edit that makes the UI consider whether QuestDraedon is present.
        new ManagedILEdit("Tweak Per-frame Adrenaline Yields", Mod, edit =>
        {
            DraedonSummonConditionHook = new(drawMethod, edit.SubscriptionWrapper);
        }, _ =>
        {
            DraedonSummonConditionHook?.Undo();
        }, UpdateCodebreakerSummonRequirements).Apply();

        // Apply the detour that changes the rendering of the Draedon summoning buttons.
        DraedonSummonUIHook = new Hook(handleDraedonSummonButtonMethod, RemakeDraedonSummonUI);
        DraedonSummonUIHook.Apply();
    }

    public override void Unload() => DraedonSummonUIHook?.Undo();

    private static void UpdateCodebreakerSummonRequirements(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt(typeof(CalamityWorld).GetMethod("get_AbleToSummonDraedon")!)))
        {
            edit.LogFailure("The CalamityWorld.AbleToSummonDraedon load could not be found.");
            return;
        }

        cursor.EmitDelegate(() => !NPC.AnyNPCs(ModContent.NPCType<QuestDraedon>()));
        cursor.Emit(OpCodes.And);
    }

    private static void RemakeDraedonSummonUI(orig_HandleDraedonSummonButton orig, TECodebreaker codebreakerTileEntity, Vector2 drawPosition)
    {
        bool marsIconCanAppear = ModContent.GetInstance<MarsCombatEvent>().Stage >= 1 && !ModContent.GetInstance<MarsCombatEvent>().Finished;

        HandleDraedonSummonButton_StandardExoMechs(codebreakerTileEntity, drawPosition);
        if (marsIconCanAppear)
        {
            // Offset the button to a place where there's clear space on the UI.
            // This doesn't happen if this button takes priority, because that means that the base position is free already.
            Vector2 drawOffset = new Vector2(84f, -56f) * GeneralScale;
            HandleDraedonSummonButton_Mars(codebreakerTileEntity, drawPosition + drawOffset);
        }
    }

    public static void HandleDraedonSummonButton_StandardExoMechs(TECodebreaker codebreakerTileEntity, Vector2 drawPosition)
    {
        Texture2D contactButton = ModContent.Request<Texture2D>("CalamityMod/UI/DraedonSummoning/ContactIcon").Value;
        Rectangle clickArea = Utils.CenteredRectangle(drawPosition, contactButton.Size() * VerificationButtonScale);

        // Make the icon spin if the codebreaker contains a blood sample.
        float iconRotation = codebreakerTileEntity.ContainsBloodSample ? Main.GlobalTimeWrappedHourly * 20f : 0f;

        // Check if the mouse is hovering over the contact button area.
        if (MouseScreenArea.Intersects(clickArea))
        {
            // If so, cause the button to inflate a little bit.
            ContactButtonScale = Clamp(ContactButtonScale + 0.035f, 1f, 1.35f);

            // If a click is done, do a check.
            // Prepare the summoning process by defining the countdown and current summon position. The mech will be summoned by the Draedon NPC.
            // Also play a cool sound.
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                CalamityWorld.DraedonSummonCountdown = CalamityWorld.DraedonSummonCountdownMax;
                CalamityWorld.DraedonSummonPosition = codebreakerTileEntity.Center + new Vector2(-8f, -100f);
                if (Main.zenithWorld && codebreakerTileEntity.ContainsBloodSample)
                    CalamityWorld.DraedonMechdusa = true;

                SoundEngine.PlaySound(SummonSound, CalamityWorld.DraedonSummonPosition);

                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    var netMessage = CalamityCompatibility.Calamity.GetPacket();
                    netMessage.Write((byte)CalamityModMessageType.CodebreakerSummonStuff);
                    netMessage.Write(CalamityWorld.DraedonSummonCountdown);
                    netMessage.WriteVector2(CalamityWorld.DraedonSummonPosition);
                    netMessage.Write(CalamityWorld.DraedonMechdusa);
                    netMessage.Send();
                }
            }
        }

        // Otherwise, if not hovering, cause the button to deflate back to its normal size.
        else
            ContactButtonScale = Clamp(ContactButtonScale - 0.05f, 1f, 1.35f);

        // Draw the contact button.
        Main.spriteBatch.Draw(contactButton, drawPosition, null, Color.White, iconRotation, contactButton.Size() * 0.5f, ContactButtonScale * GeneralScale, 0, 0f);

        // And display a text indicator that describes the function of the button.
        // The color of the text cycles through the exo mech crystal palette.
        string contactTextKey = "Contact";
        if (CommonCalamityVariables.DraedonDefeated)
            contactTextKey = "Summon";
        if (codebreakerTileEntity.ContainsBloodSample)
            contactTextKey = "Evoke";
        string contactText = CalamityUtils.GetTextValue("UI." + contactTextKey);

        Color contactTextColor = ExoPalette.SampleColor(Cos01(Main.GlobalTimeWrappedHourly * 0.7f));

        // Center the draw position and draw text.
        drawPosition.X -= FontAssets.MouseText.Value.MeasureString(contactText).X * GeneralScale * 0.5f;
        drawPosition.Y += GeneralScale * 20f;
        Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, contactText, drawPosition.X, drawPosition.Y, contactTextColor, Color.Black, Vector2.Zero, GeneralScale);
    }

    public static void HandleDraedonSummonButton_Mars(TECodebreaker codebreakerTileEntity, Vector2 drawPosition)
    {
        Texture2D contactButton = ModContent.Request<Texture2D>("CalamityMod/UI/DraedonSummoning/ContactIcon").Value;
        Rectangle clickArea = Utils.CenteredRectangle(drawPosition, contactButton.Size() * VerificationButtonScale);

        // Check if the mouse is hovering over the contact button area.
        if (MouseScreenArea.Intersects(clickArea))
        {
            // If so, cause the button to inflate a little bit.
            ContactButtonScale = Clamp(ContactButtonScale + 0.035f, 1f, 1.35f);

            // If a click is done, do a check.
            // Prepare the summoning process by defining the countdown and current summon position. Mars will be summoned by the Draedon NPC.
            // Also play a cool sound.
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                CalamityWorld.DraedonSummonCountdown = CalamityWorld.DraedonSummonCountdownMax;
                CalamityWorld.DraedonSummonPosition = codebreakerTileEntity.Center + new Vector2(-8f, -100f);
                SoundEngine.PlaySound(SummonSound, CalamityWorld.DraedonSummonPosition);

                MarsCombatEvent.MarsBeingSummoned = true;

                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    var netMessage = CalamityCompatibility.Calamity.GetPacket();
                    netMessage.Write((byte)CalamityModMessageType.CodebreakerSummonStuff);
                    netMessage.Write(CalamityWorld.DraedonSummonCountdown);
                    netMessage.WriteVector2(CalamityWorld.DraedonSummonPosition);
                    netMessage.Write(CalamityWorld.DraedonMechdusa);
                    netMessage.Send();

                    PacketManager.SendPacket<MarsSummonStatusPacket>();
                }
            }
        }

        // Otherwise, if not hovering, cause the button to deflate back to its normal size.
        else
            ContactButtonScale = Clamp(ContactButtonScale - 0.05f, 1f, 1.35f);

        // Draw the contact button.
        Main.spriteBatch.Draw(contactButton, drawPosition, null, Color.White, 0f, contactButton.Size() * 0.5f, ContactButtonScale * GeneralScale, 0, 0f);

        // And display a text indicator that describes the function of the button.
        // The color of the text pulsates.
        string contactText = Language.GetTextValue("Mods.NoxusBoss.Dialog.SummonMarsButtonText");
        Color contactTextColor = Color.Lerp(Color.Wheat, Color.Cyan, Cos01(Main.GlobalTimeWrappedHourly * 7f) * 0.7f);
        if (!MarsCombatEvent.HasSpokenToDraedonBefore)
        {
            contactText = Language.GetTextValue("Mods.NoxusBoss.Dialog.SummonMarsButtonTextInitial");
            contactTextColor = Color.Lerp(Color.White, Color.Red, Cos01(Main.GlobalTimeWrappedHourly * 16f) * 0.8f);
        }

        // Center the draw position and draw text.
        drawPosition.X -= FontAssets.MouseText.Value.MeasureString(contactText).X * GeneralScale * 0.5f;
        drawPosition.Y += GeneralScale * 20f;
        Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, contactText, drawPosition.X, drawPosition.Y, contactTextColor, Color.Black, Vector2.Zero, GeneralScale);
    }
}
