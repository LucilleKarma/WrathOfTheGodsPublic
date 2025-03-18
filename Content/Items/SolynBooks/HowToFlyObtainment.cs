using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using static NoxusBoss.Core.Autoloaders.SolynBooks.SolynBookAutoloader;

namespace NoxusBoss.Content.Items;

public partial class SolynBooksSystem : ModSystem
{
    /// <summary>
    /// A cooldown timer that ensures that "How to Fly" books cannot spawn in rapid succession.
    /// </summary>
    public static int HowToFlySpawnCooldown
    {
        get;
        set;
    }

    /// <summary>
    /// The flight speed in miles per hour needed for the player to obtain the How to Fly book.
    /// </summary>
    public static float HowToFlyRequiredSpeed => 100f;

    private static float PixelsPerFrameToMPH(float speed) => speed * 225f / 44f;

    private static void TryToSpawnHowToFlyBook()
    {
        if (HowToFlySpawnCooldown >= 1)
        {
            HowToFlySpawnCooldown--;
            return;
        }

        foreach (Player player in Main.ActivePlayers)
        {
            if (player.dead)
                continue;

            TryToSpawnHowToFlyBookForPlayer(player);
        }
    }

    private static void TryToSpawnHowToFlyBookForPlayer(Player player)
    {
        // Only spawn the book if the player has wings.
        if (player.wings == 0)
            return;

        if (PixelsPerFrameToMPH(player.velocity.Length()) >= HowToFlyRequiredSpeed)
        {
            Vector2 bookSpawnPosition = player.Center;
            Item.NewItem(new EntitySource_WorldEvent(), bookSpawnPosition, Books["HowToFly"].Type);

            HowToFlySpawnCooldown = MinutesToFrames(300f);
        }
    }
}
