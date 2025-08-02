using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Autoloaders.SolynBooks;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.Graphics.UI.Books;

public class SolynBookExchangeRegistry : ModSystem
{
    /// <summary>
    /// The set of all books given to Solyn so far in the world.
    /// </summary>
    internal static HashSet<string> RedeemedBooks
    {
        get;
        set;
    } = new HashSet<string>(64);

    /// <summary>
    /// The mapping of all books to their respective redeemer.
    /// </summary>
    internal static Dictionary<string, string> RedeemedBooksCreditRelationship
    {
        get;
        set;
    } = new Dictionary<string, string>(64);

    /// <summary>
    /// The set of all books that can be obtained.
    /// </summary>
    public static List<AutoloadableSolynBook> ObtainableBooks
    {
        get;
        private set;
    } = new List<AutoloadableSolynBook>();

    /// <summary>
    /// The amount of books the player has redeemed.
    /// </summary>
    public static int TotalRedeemedBooks => RedeemedBooks.Count;

    /// <summary>
    /// Whether the player has obtained and given all possible books to Solyn.
    /// </summary>
    public static bool RedeemedAllBooks => TotalRedeemedBooks >= ObtainableBooks.Count;

    public override void OnWorldLoad()
    {
        if (!SubworldSystem.AnyActive())
            RedeemedBooks.Clear();

        List<AutoloadableSolynBook> books = SolynBookAutoloader.Books.Values.OrderBy(b => b.Data.Rarity).ThenBy(b => b.DisplayName.Value).ToList();
        if (WorldGen.crimson)
            books.Remove(SolynBookAutoloader.Books["DubiousBrochureCorruption"]);
        else
            books.Remove(SolynBookAutoloader.Books["DubiousBrochureCrimson"]);

        ObtainableBooks = books;
    }

    public override void OnWorldUnload()
    {
        if (!SubworldSystem.AnyActive())
            RedeemedBooks.Clear();
    }

    /// <summary>
    /// Makes Solyn redeem a given book.
    /// </summary>
    /// <param name="bookName">The name of the book that's being redeemed.</param>
    public static void MakeSolynRedeemBook(string bookName)
    {
        if (RedeemedBooks.Contains(bookName))
            return;

        RedeemedBooks.Add(bookName);
        RedeemedBooksCreditRelationship[bookName] = ModContent.GetInstance<Solyn>().DisplayName.Value;

        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            PacketManager.SendPacket<SolynBookStoragePacket>(bookName);
            PacketManager.SendPacket<SolynBookCreditPacket>(bookName, RedeemedBooksCreditRelationship[bookName]);
        }
    }

    /// <summary>
    /// Redeems a given book for a given player.
    /// </summary>
    /// <param name="player">The player that redeemed the book.</param>
    /// <param name="bookName">The name of the book that's being redeemed.</param>
    public static void RedeemBook(Player player, string bookName)
    {
        if (RedeemedBooks.Contains(bookName))
            return;

        float oldProgressionRatio = RedeemedBooks.Count / (float)ObtainableBooks.Count;
        RedeemedBooks.Add(bookName);
        RedeemedBooksCreditRelationship[bookName] = player.name;

        if (SolynBookAutoloader.Books.TryGetValue(bookName, out AutoloadableSolynBook? book))
            player.GetModPlayer<SolynBookRewardsPlayer>().GenerateRewards(oldProgressionRatio, book);
        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            PacketManager.SendPacket<SolynBookStoragePacket>(bookName);
            PacketManager.SendPacket<SolynBookCreditPacket>(bookName, RedeemedBooksCreditRelationship[bookName]);
        }
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["RedeemedBooks"] = RedeemedBooks.ToList();
        tag["RedeemedBooksCreditRelationshipKeys"] = RedeemedBooksCreditRelationship.Keys.ToList();
        tag["RedeemedBooksCreditRelationshipValues"] = RedeemedBooksCreditRelationship.Values.ToList();
    }

    public override void LoadWorldData(TagCompound tag)
    {
        RedeemedBooks = tag.GetList<string>("RedeemedBooks").ToHashSet();
        IList<string> creditMappingKeys = tag.GetList<string>("RedeemedBooksCreditRelationshipKeys");
        IList<string> creditMappingValues = tag.GetList<string>("RedeemedBooksCreditRelationshipValues");

        RedeemedBooksCreditRelationship = [];
        for (int i = 0; i < creditMappingKeys.Count; i++)
            RedeemedBooksCreditRelationship[creditMappingKeys[i]] = creditMappingValues[i];
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(RedeemedBooks.Count);
        foreach (string book in RedeemedBooks)
            writer.Write(book);

        writer.Write(RedeemedBooksCreditRelationship.Count);
        foreach (KeyValuePair<string, string> kv in RedeemedBooksCreditRelationship)
        {
            writer.Write(kv.Key);
            writer.Write(kv.Value);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        RedeemedBooks.Clear();
        RedeemedBooksCreditRelationship.Clear();

        int bookCount = reader.ReadInt32();
        for (int i = 0; i < bookCount; i++)
            RedeemedBooks.Add(reader.ReadString());

        int redeemedBooksCount = reader.ReadInt32();
        for (int i = 0; i < redeemedBooksCount; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            RedeemedBooksCreditRelationship[key] = value;
        }
    }
}
