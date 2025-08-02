using Microsoft.Xna.Framework;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.SpecificEffectManagers;

[Autoload(Side = ModSide.Client)]
public class PlayerBloodiedHairSystem : ModSystem
{
    public static Referenced<float> GetPlayerHairBloodiness(Player player)
    {
        return player.GetValueRef<float>("HairBloodiness");
    }

    public override void OnModLoad()
    {
        On_Player.GetHairColor += ApplyBloodEffect;
    }

    private Color ApplyBloodEffect(On_Player.orig_GetHairColor orig, Player self, bool useLighting)
    {
        Referenced<float> hairBloodiness = GetPlayerHairBloodiness(self);
        hairBloodiness.Value = Saturate(hairBloodiness - 0.004f);

        Color result = orig(self, useLighting);
        return Color.Lerp(result, new(102, 0, 0), InverseLerp(0f, 0.56f, hairBloodiness));
    }
}
