using NoxusBoss.Core.Autoloaders.SolynBooks;
using NoxusBoss.Core.GlobalInstances;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items;

public partial class SolynBooksSystem : ModSystem
{
    private static void LoadDubiousBrochureObtainment()
    {
        GlobalTileEventHandlers.KillTileEvent += DropBrochure;
    }

    private static void DropBrochure(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        Tile t = Framing.GetTileSafely(i, j);
        if (t.TileFrameX % 36 == 0 && t.TileFrameY % 36 == 0 && t.TileType == TileID.ShadowOrbs && Main.rand.NextBool(3))
        {
            int brochureID = WorldGen.crimson ? SolynBookAutoloader.Books["DubiousBrochureCrimson"].Type : SolynBookAutoloader.Books["DubiousBrochureCorruption"].Type;
            Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 32, 32, brochureID);
        }
    }
}
