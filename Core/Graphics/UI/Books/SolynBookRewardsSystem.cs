using NoxusBoss.Core.Autoloaders.SolynBooks;
using NoxusBoss.Core.Data;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.UI.Books;

public class SolynBookRewardsSystem : ModSystem
{
    /// <summary>
    /// The set of all mappings from book type to reward.
    /// </summary>
    public static Dictionary<AutoloadableSolynBook, List<SolynReward>> RewardMappings
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// The set of all rewards that are earned based on percentage of books found.
    /// </summary>
    public static List<SolynProgressionRatioReward> ProgressionRewardMappings
    {
        get;
        private set;
    } = [];

    public override void PostSetupContent()
    {
        Dictionary<string, List<SolynReward>>? data = LocalDataManager.Read<List<SolynReward>>("Core/Graphics/UI/Books/SolynBookRewards.json");
        List<SolynProgressionRatioReward>? progressionData = LocalDataManager.Read<SolynProgressionRatioReward>("Core/Graphics/UI/Books/SolynProgressionRatioBookRewards.json")?.Values?.ToList();
        if (data is null || progressionData is null)
        {
            Mod.Logger.Warn("Could not load Solyn reward JSON mappings.");
            return;
        }

        ProgressionRewardMappings = progressionData;
        foreach (string bookName in data.Keys)
        {
            if (SolynBookAutoloader.Books.TryGetValue(bookName, out AutoloadableSolynBook? book))
                RewardMappings[book] = data[bookName];
        }
    }

    /// <summary>
    /// Adds a new reward for a given book.
    /// </summary>
    public static void AddRewardForBook(AutoloadableSolynBook book, SolynReward reward)
    {
        if (!RewardMappings.ContainsKey(book))
            RewardMappings[book] = new List<SolynReward>();

        RewardMappings[book].Add(reward);
    }
}
