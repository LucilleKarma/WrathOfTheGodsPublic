using Luminance.Core.Cutscenes;

using NoxusBoss.Core.World.GameScenes.Stargazing;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class DuskCutsceneTimePacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write(BecomeDuskScene.TimeAtStartOfAnimation);
        packet.Write(BecomeDuskScene.CutsceneStarterPlayerIndex);
        packet.Write((byte)(Main.dayTime ? 1 : 0));
        packet.Write((int)Main.time);
        packet.Write((byte)(ModContent.GetInstance<BecomeDuskScene>().IsActive ? 1 : 0));
    }

    public override void Read(BinaryReader reader)
    {
        BecomeDuskScene.TimeAtStartOfAnimation = reader.ReadInt32();
        BecomeDuskScene.CutsceneStarterPlayerIndex = reader.ReadInt32();
        Main.dayTime = reader.ReadByte() == 1;
        Main.time = reader.ReadInt32();

        bool active = reader.ReadByte() == 1;
        if (active)
            CutsceneManager.QueueCutscene(ModContent.GetInstance<BecomeDuskScene>());
    }
}
