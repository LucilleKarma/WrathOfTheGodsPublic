using NoxusBoss.Core.Data;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;

public class NamelessDeityFormPresetRegistry : ModSystem
{
    private static readonly Dictionary<string, NamelessDeityFormPreset> formPresets = [];

    // This preset makes Nameless horizontally stretched at all times.
    public static bool UsingAmmyanPreset => Main.LocalPlayer.name.Equals("Ammyan", StringComparison.OrdinalIgnoreCase);

    // This preset makes Nameless wear a cute hat at all times.
    public static bool UsingBlastPreset => Main.LocalPlayer.name.Equals("Blast", StringComparison.OrdinalIgnoreCase);

    // This present makes the aesthetic of the background grayscale.
    public static bool UsingHealthyPreset => Main.LocalPlayer.name.Equals("Healthy", StringComparison.OrdinalIgnoreCase);

    // This preset removes Nameless' censor, and changes Nameless' overall aesthetic to be more cool and spiritual.
    public static bool UsingLucillePreset => Main.LocalPlayer.name.Equals("Lucille", StringComparison.OrdinalIgnoreCase);

    // This present makes Nameless' censor comically sized.
    public static bool UsingFluffyPreset => Main.LocalPlayer.name.Equals("Fluffy", StringComparison.OrdinalIgnoreCase);

    // This present uses a couple of special preferences and makes Nameless go on a diatribe from Jerma.
    public static bool UsingLynelPreset => Main.LocalPlayer.name.Equals("Lynel", StringComparison.OrdinalIgnoreCase);

    // This preset makes Nameless and the background blue-tinted.
    public static bool UsingMoonburnPreset => Main.LocalPlayer.name.Equals("Moonburn", StringComparison.OrdinalIgnoreCase);

    // This preset makes Nameless spin at a comical rate at all times.
    public static bool UsingSmhPreset => Main.LocalPlayer.name.Equals("smh", StringComparison.OrdinalIgnoreCase);

    // This preset makes all of Nameless' forms cycle once per frame, as long as photosensitivity mode is disabled.
    public static bool UsingYuHPreset => Main.LocalPlayer.name.Equals("GinYuH", StringComparison.OrdinalIgnoreCase);

    public static NamelessDeityFormPreset? SelectFirstAvailablePreset() => formPresets.FirstOrDefault(p =>
    {
        return p.Value.IsActive;
    }).Value;

    public override void PostSetupContent()
    {
        string dataPath = "Content/NPCs/Bosses/NamelessDeity/FormPresets/NamelessDeityPresets.json";
        Dictionary<string, NamelessDeityLoadablePresetData> data = LocalDataManager.Read<NamelessDeityLoadablePresetData>(dataPath);

        foreach (KeyValuePair<string, NamelessDeityLoadablePresetData> kv in data)
        {
            formPresets.Add(kv.Key, new NamelessDeityFormPreset()
            {
                Data = kv.Value
            });
        }

        formPresets["Healthy"].ShaderOverlayEffect = NamelessDeityBoss.ApplyHealthyGrayscaleEffect;
        formPresets["Moonburn"].ShaderOverlayEffect = NamelessDeityBoss.ApplyMoonburnBlueEffect;
        formPresets["Myra"].ShaderOverlayEffect = NamelessDeityBoss.ApplyMyraGoldEffect;
    }
}
