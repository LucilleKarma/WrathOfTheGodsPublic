using Luminance.Common.Easings;
using Luminance.Core.Hooking;
using MonoMod.Cil;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

[Autoload(Side = ModSide.Client)]
public class NamelessDeityDeathVisualsSystem : ModSystem
{
    public int DeathTimerOverride
    {
        get;
        set;
    }

    public override void OnModLoad()
    {
        new ManagedILEdit("Change Death Animation Text During Nameless' Fight", Mod, edit =>
        {
            IL_Main.DrawInterface_35_YouDied += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Main.DrawInterface_35_YouDied -= edit.SubscriptionWrapper;
        }, ChangeNamelessDeityText).Apply();
    }

    private static void ChangeNamelessDeityText(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        // Make the text offset higher up if Nameless killed the player so that the player can better see the death vfx.
        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStloc(out _)))
        {
            edit.LogFailure("Could not find the text draw offset local variable storage.");
            return;
        }

        cursor.EmitDelegate<Func<float, float>>(textOffset =>
        {
            if (Main.LocalPlayer.GetModPlayer<NamelessDeityPlayerDeathVisualsPlayer>().WasKilledByNamelessDeity)
                textOffset -= 120f;

            return textOffset;
        });

        // Replace the "You were slain..." text with something special.
        if (!cursor.TryGotoNext(i => i.MatchLdsfld<Lang>("inter")))
        {
            edit.LogFailure("Could not find Lang.inter load.");
            return;
        }
        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStloc(out _)))
        {
            edit.LogFailure("Could not find the death text local variable storage.");
            return;
        }

        cursor.EmitDelegate<Func<string, string>>(originalText =>
        {
            if (Main.LocalPlayer.GetModPlayer<NamelessDeityPlayerDeathVisualsPlayer>().WasKilledByNamelessDeity)
                return Language.GetTextValue("Mods.NoxusBoss.Dialog.NamelessDeityPlayerDeathText");

            return originalText;
        });

        // Replace the number text.
        if (!cursor.TryGotoNext(i => i.MatchLdstr("Game.RespawnInSuffix")))
        {
            edit.LogFailure("Could not find the game respawn text key load.");
            return;
        }
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt(typeof(Language), "GetTextValue")))
        {
            edit.LogFailure("Could not find the Language.GetTextValue load.");
            return;
        }
        cursor.EmitDelegate<Func<string, string>>(originalText =>
        {
            NamelessDeityPlayerDeathVisualsPlayer modPlayer = Main.LocalPlayer.GetModPlayer<NamelessDeityPlayerDeathVisualsPlayer>();
            if (modPlayer.WasKilledByNamelessDeity)
            {
                float deathTimerInterpolant = modPlayer.DeathTimerOverride / (float)NamelessDeityPlayerDeathVisualsPlayer.DeathTimerMax;
                ulong start = 5;
                ulong end = int.MaxValue * 2uL;
                float smoothInterpolant = EasingCurves.MakePoly(20f).Evaluate(EasingType.InOut, deathTimerInterpolant);
                long textValue = (long)Lerp(start, end, smoothInterpolant);
                if (textValue >= int.MaxValue)
                    textValue -= int.MaxValue * 2L + 2;

                return textValue.ToString();
            }

            return originalText;
        });
    }
}
