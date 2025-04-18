using Microsoft.Xna.Framework;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Fixes;
using NoxusBoss.Core.Graphics;
using NoxusBoss.Core.Graphics.UI;
using NoxusBoss.Core.Graphics.UI.Books;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;
using SubworldLibrary;
using Terraria;
using Terraria.GameContent;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;
using Terraria.WorldBuilding;
using static NoxusBoss.Core.World.WorldSaving.WorldSaveSystem;

namespace NoxusBoss.Core.World.Subworlds;

// This has been renamed from EternalGarden to EternalGardenNew to ensure that players with old subworlds regenerate a new one, since the tree tile dimensions changed.
public class EternalGardenNew : Subworld
{
    public class EternalGardenPass : GenPass
    {
        public EternalGardenPass() : base("Terrain", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            // Set the progress text.
            progress.Message = "Forming the Eternal Garden.";

            // Define the position of the world lines.
            Main.worldSurface = Main.maxTilesY - 8;
            Main.rockLayer = Main.maxTilesY - 9;

            // Generate the garden.
            EternalGardenWorldGen.Generate();
        }
    }

    public static float TextOpacity
    {
        get;
        set;
    }

    public static TagCompound ClientWorldDataTag
    {
        get;
        internal set;
    }

    public override int Width => 1200;

    public override int Height => 350;

    // This is mainly so that map data is saved across attempts.
    public override bool ShouldSave => true;

    public override List<GenPass> Tasks => new List<GenPass>()
    {
        new EternalGardenPass()
    };

    public override void Load()
    {
        // Autoload the music box for this subworld.
        string musicPath = "Assets/Sounds/Music/EternalGarden";
        MusicBoxAutoloader.Create(Mod, GetAssetPath("Content/Items/Placeable/MusicBoxes", "EternalGardenMusicBox"), musicPath, out _, out _);

        On_WorldGen.KillTile += DisallowGrassToDirtConversion;

        CellPhoneInfoModificationSystem.PlayerXPositionReplacementTextEvent += (string originalText) =>
        {
            if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
                return "???";

            return null;
        };
        CellPhoneInfoModificationSystem.PlayerYPositionReplacementTextEvent += (string originalText) =>
        {
            if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
                return "???";

            return null;
        };
    }

    private void DisallowGrassToDirtConversion(On_WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effectOnly, bool noItem)
    {
        if (!EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
        {
            orig(i, j, fail, effectOnly, noItem);
            return;
        }

        Tile t = Framing.GetTileSafely(i, j);
        if (t.HasTile && t.TileType == ModContent.TileType<EternalGardenGrass>())
            return;

        orig(i, j, fail, effectOnly, noItem);
    }

    public override bool ChangeAudio()
    {
        // Get rid of the jarring title screen music when moving between subworlds.
        if (Main.gameMenu)
        {
            Main.newMusic = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/ascent");
            return true;
        }

        return false;
    }

    public override void DrawMenu(GameTime gameTime)
    {
        // Make the text appear.
        TextOpacity = Saturate(TextOpacity + 0.093f);

        // Give text about how the player's test begins when entering the garden.
        // When exiting, the regular load details text is displayed.
        var font = FontRegistry.Instance.NamelessDeityText;
        string text = Language.GetTextValue($"Mods.{Mod.Name}.Dialog.NamelessDeityEnterGardenText");
        Color textColor = DialogColorRegistry.NamelessDeityTextColor;
        if (!SubworldSystem.IsActive<EternalGardenNew>())
        {
            font = FontAssets.DeathText.Value;
            text = Main.statusText;
            textColor = Color.Black;
        }

        // Draw a pure-white background. Immediate loading is used for the texture because without it there's a tiny, jarring delay before the white background appears where the regular
        // title screen is revealed momentarily.
        Vector2 pixelScale = Main.ScreenSize.ToVector2() * 1.45f / WhitePixel.Size();
        Main.spriteBatch.Draw(WhitePixel, Main.ScreenSize.ToVector2() * 0.5f, null, Color.White, 0f, WhitePixel.Size() * 0.5f, pixelScale, 0, 0f);

        // Draw the text.
        Vector2 drawPosition = Main.ScreenSize.ToVector2() * 0.5f - font.MeasureString(text) * 0.5f;
        ChatManager.DrawColorCodedString(Main.spriteBatch, font, text, drawPosition, textColor * TextOpacity, 0f, Vector2.Zero, Vector2.One);

        // Disable the intro fix's white effect, this draw hook handles that now.
        EternalGardenIntroBackgroundFix.ShouldDrawWhite = false;
    }

    public static TagCompound SafeWorldDataToTag(string suffix, bool saveInCentralRegistry = true)
    {
        // Re-initialize the save data tag.
        TagCompound savedWorldData = [];

        // Ensure that world save data for the Avatar and Nameless are preserved.
        // Nameless is obvious, the main world should know if he was defeated in the subworld.
        // The Avatar's defeat is required to use the Terminus, so not having him marked as defeated and thus unable to use it to leave the subworld would be a problem.
        if (HasMetNamelessDeity)
            savedWorldData["HasMetNamelessDeity"] = true;

        // Save difficulty data. This is self-explanatory.
        bool revengeanceMode = CommonCalamityVariables.RevengeanceModeActive;
        bool deathMode = CommonCalamityVariables.DeathModeActive;
        if (revengeanceMode)
            savedWorldData["RevengeanceMode"] = revengeanceMode;
        if (deathMode)
            savedWorldData["DeathMode"] = deathMode;
        if (BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>())
            savedWorldData["AvatarDefeated"] = true;
        if (BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>())
            savedWorldData["NamelessDeityDefeated"] = true;
        if (Main.zenithWorld)
            savedWorldData["GFB"] = Main.zenithWorld;
        savedWorldData["WorldVersionText"] = WorldVersionSystem.WorldVersionText;
        savedWorldData["RedeemedBooks"] = SolynBookExchangeRegistry.RedeemedBooks.ToList();

        // Save death data. When the player returns to the subworld this will decide how many Starbearers will appear in the garden.
        savedWorldData["NamelessDeityDeathCount"] = NamelessDeityDeathCount;

        // Save Calamity's boss defeat data.
        CommonCalamityVariables.SaveDefeatStates(savedWorldData);

        // Store the tag.
        if (saveInCentralRegistry)
            SubworldSystem.CopyWorldData($"GardenSavedWorldData_{suffix}", savedWorldData);

        return savedWorldData;
    }

    public static void LoadWorldDataFromTag(string suffix, TagCompound? specialTag = null)
    {
        TagCompound savedWorldData = specialTag ?? SubworldSystem.ReadCopiedWorldData<TagCompound>($"GardenSavedWorldData_{suffix}");

        HasMetNamelessDeity = savedWorldData.ContainsKey("HasMetNamelessDeity");

        if (savedWorldData.ContainsKey("AvatarDefeated"))
            BossDownedSaveSystem.SetDefeatState<AvatarOfEmptiness>(true);
        if (savedWorldData.ContainsKey("NamelessDeityDefeated"))
            BossDownedSaveSystem.SetDefeatState<NamelessDeityBoss>(true);

        CommonCalamityVariables.RevengeanceModeActive = savedWorldData.ContainsKey("RevengeanceMode");
        CommonCalamityVariables.DeathModeActive = savedWorldData.ContainsKey("DeathMode");
        Main.zenithWorld = savedWorldData.ContainsKey("GFB");

        if (savedWorldData.TryGet("WorldVersionText", out string version))
            WorldVersionSystem.WorldVersionText = version;
        SolynBookExchangeRegistry.RedeemedBooks = savedWorldData.GetList<string>("RedeemedBooks").ToHashSet();

        NamelessDeityDeathCount = savedWorldData.GetInt("NamelessDeityDeathCount");

        CommonCalamityVariables.LoadDefeatStates(savedWorldData);
    }

    public override void CopyMainWorldData() => SafeWorldDataToTag("Main");

    public override void ReadCopiedMainWorldData() => LoadWorldDataFromTag("Main");

    public override void CopySubworldData() => SafeWorldDataToTag("Subworld");

    public override void ReadCopiedSubworldData() => LoadWorldDataFromTag("Subworld");
}
