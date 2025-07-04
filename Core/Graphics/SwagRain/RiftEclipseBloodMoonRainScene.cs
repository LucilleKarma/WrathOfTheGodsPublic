using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Core.Autoloaders;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.SwagRain;

public class RiftEclipseBloodMoonRainScene : ModSceneEffect
{
    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/Hemolacria");

    public override SceneEffectPriority Priority => SceneEffectPriority.Environment;

    public override void Load()
    {
        string contentPath = GetAssetPath("Content/Items/Placeable/MusicBoxes", "HemolacriaMusicBox");
        string musicPath = "Assets/Sounds/Music/Hemolacria";
        MusicBoxAutoloader.Create(Mod, contentPath, musicPath, out _, out _, AvatarRift.DrawMusicBoxWithBackRift);
    }

    public override bool IsSceneEffectActive(Player player) => RiftEclipseBloodMoonRainSystem.EffectActive && player.Center.Y <= Main.worldSurface * 16f;

    public override float GetWeight(Player player) => 0.67f;
}
