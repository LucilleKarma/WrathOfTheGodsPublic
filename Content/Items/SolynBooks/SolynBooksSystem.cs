using Microsoft.Xna.Framework;
using NoxusBoss.Core.Graphics.UI.Books;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Items;

public partial class SolynBooksSystem : ModSystem
{
    /// <summary>
    /// Whether books are obtainable at all.
    /// </summary>
    public static bool BooksObtainable => !WorldVersionSystem.PreAvatarUpdateWorld;

    public override void OnModLoad()
    {
        LoadFuturisticTreatise();
    }

    public override void PostSetupContent()
    {
        LoadAppleThesisObtainment();
        LoadAncientWritingsObtainment();
        LoadBibleOfSlimeObtainment();
        LoadBookOfMiraclesObtainment_Wrapper();
        LoadBookOfShaders();
        LoadCactomeObtainment();
        LoadDubiousBrochureObtainment();
        LoadDubiousSpellbookObtainment();
        LoadExoBlueprintsObtainment();
        LoadFishingCatalogueObtainment();
        LoadInstructionManualObtainment();
        LoadInvisibleInkDissertationObtainment();
        LoadOminousStorybookObtainment();
        LoadRageOfTheDeitiesObtainment();
        LoadSeapmapCollectionObtainment();
        LoadSulfuricLeafletObtainmentObtainment_Wrapper();
        LoadTwentyTwomeObtainment();
        LoadWulfrumAssemblyGuideObtainment_Wrapper();

        MapInscrutableTextsTile();
    }

    public override void PostUpdatePlayers()
    {
        if (!BooksObtainable)
            return;

        PerformFanaticalRamblingsCheck_Wrapper();
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        DecrementFanaticalRamblingsSpawnCooldown();
        HaveSolynCollectBooks();
        TryToSpawnAcidicPrayerbook_Wrapper();
        TryToSpawnCloverManual();
        TryToSpawnHowToFlyBook();
        TryToSpawnUnfinishedColoringBook();
    }

    private static void HaveSolynCollectBooks()
    {
        if (SolynCampsiteWorldGen.CampSitePosition == Vector2.Zero)
            return;

        // Only collect books if no player is near Solyn's campsite, so that they don't see a book popping into the UI as they're looking at it.
        Player closestToCampsite = Main.player[Player.FindClosest(SolynCampsiteWorldGen.CampSitePosition, 1, 1)];
        if (closestToCampsite.WithinRange(SolynCampsiteWorldGen.CampSitePosition, 1900f))
            return;

        int findChance = 7200;
        float bookCollectionInterpolant = Saturate(SolynBookExchangeRegistry.RedeemedBooks.Count / (float)SolynBookExchangeRegistry.ObtainableBooks.Count);
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.15f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("FanaticalRamblings");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.2f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("Otherworldly1");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.25f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("BestPriceBestiary");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.3f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("Otherworldly2");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.35f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("DecayingTome");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.45f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("DustyDiary");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.5f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("Otherworldly3");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.55f && ModContent.GetInstance<PermafrostKeepEvent>().Finished)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("FrostyPamphlet");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.6f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("AberrantWritings");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.65f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("HiTechInstructionManual");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.7f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("Otherworldly4");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.75f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("MagicLexicon");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.85f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("TheBookOfShaders");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.85f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("Otherworldly5");
        if (Main.rand.NextBool(findChance) && bookCollectionInterpolant >= 0.9f)
            SolynBookExchangeRegistry.MakeSolynRedeemBook("Otherworldly6");
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["CloverManualSpawnCooldown"] = CloverManualSpawnCooldown;
        tag["FanaticalRamblingsSpawnCooldown"] = FanaticalRamblingsSpawnCooldown;
        tag["HowToFlySpawnCooldown"] = HowToFlySpawnCooldown;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        CloverManualSpawnCooldown = tag.GetInt("CloverManualSpawnCooldown");
        FanaticalRamblingsSpawnCooldown = tag.GetInt("FanaticalRamblingsSpawnCooldown");
        HowToFlySpawnCooldown = tag.GetInt("HowToFlySpawnCooldown");
    }
}
