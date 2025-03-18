using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.GameScenes.Stargazing;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles.SolynCampsite;

public class SolynTelescopeTile : ModTile
{
    internal static LazyAsset<Texture2D> GoldOpticalTubeBrokenTexture;

    internal static LazyAsset<Texture2D> GoldOpticalTubeTexture;

    internal static LazyAsset<Texture2D> PlatinumOpticalTubeBrokenTexture;

    internal static LazyAsset<Texture2D> PlatinumOpticalTubeTexture;

    internal static LazyAsset<Texture2D> PlatinumBaseTexture;

    internal static Dictionary<int, int> RepairRequirements => new Dictionary<int, int>()
    {
        [GoldVariant ? ItemID.GoldBar : ItemID.PlatinumBar] = 10,
        [ItemID.Wood] = 15,
    };

    /// <summary>
    /// Whether players can repair Solyn's telescope or not.
    /// </summary>
    public static bool AnyoneCanRepairTelescope => DialogueManager.FindByRelativePrefix("StargazeQuest").SeenBefore("Player1");

    /// <summary>
    /// Whether telescopes are gold or not.
    /// </summary>
    public static bool GoldVariant => WorldGen.SavedOreTiers.Gold == TileID.Gold;

    /// <summary>
    /// The tiled width of this telescope.
    /// </summary>
    public const int Width = 2;

    /// <summary>
    /// The tiled height of this telescope.
    /// </summary>
    public const int Height = 3;

    public override string Texture => GetAssetPath("Content/Tiles/SolynCampsite", Name);

    public override void SetStaticDefaults()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            GoldOpticalTubeBrokenTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/SolynCampsite/SolynTelescopeOpticalTubeGoldBroken");
            GoldOpticalTubeTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/SolynCampsite/SolynTelescopeOpticalTubeGold");
            PlatinumOpticalTubeBrokenTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/SolynCampsite/SolynTelescopeOpticalTubePlatinumBroken");
            PlatinumOpticalTubeTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/SolynCampsite/SolynTelescopeOpticalTubePlatinum");
            PlatinumBaseTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/SolynCampsite/SolynTelescopeTilePlatinum");
        }

        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = true;
        TileID.Sets.HasOutlines[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(Width / 2, Height - 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;

        // Set the respective tile entity as a secondary element to incorporate when placing this tile.
        ModTileEntity tileEntity = ModContent.GetInstance<TESolynTelescope>();
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(tileEntity.Hook_AfterPlacement, -1, 0, true);

        TileObjectData.addTile(Type);
        AddMapEntry(new Color(255, 255, 255));

        GlobalTileEventHandlers.IsTileUnbreakableEvent += MakeTilesBelowUnbreakable;
        On_WorldGen.BlockBelowMakesSandFall += DisallowSandfallBeneathTelescope;

        HitSound = SoundID.Tink;
    }

    private bool DisallowSandfallBeneathTelescope(On_WorldGen.orig_BlockBelowMakesSandFall orig, int i, int j)
    {
        Tile t = Framing.GetTileSafely(i, j - 1);
        if (t.TileType == Type && t.HasTile)
            return false;

        return orig(i, j);
    }

    private bool MakeTilesBelowUnbreakable(int x, int y, int type)
    {
        if (Type == type)
            return true;

        Tile above = Framing.GetTileSafely(x, y - 1);
        if (above.TileType == Type && above.HasTile)
            return true;

        return false;
    }

    public override bool CanExplode(int i, int j) => false;

    public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

    public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        Tile tile = Main.tile[i, j];
        int left = i - tile.TileFrameX % (Width * 18) / 18;
        int top = j - tile.TileFrameY % (Height * 18) / 18;

        // Kill the hosted tile entity directly and immediately.
        TESolynTelescope? telescope = FindTileEntity<TESolynTelescope>(i, j, Width, Height);
        telescope?.Kill(left, top);
    }

    public override bool RightClick(int i, int j)
    {
        TESolynTelescope? telescope = FindTileEntity<TESolynTelescope>(i, j, Width, Height);
        if (telescope is not null && !telescope.IsRepaired)
            return HandleUsageInteraction_Broken(telescope);

        return HandleUsageInteraction_Repaired();
    }

    private static bool HandleUsageInteraction_Broken(TESolynTelescope telescope)
    {
        bool playerCanRepairTelescope = AnyoneCanRepairTelescope && RepairRequirements.All(kv => Main.LocalPlayer.CountItem(kv.Key) >= kv.Value);
        if (playerCanRepairTelescope)
        {
            // Eat the player's items.
            foreach (int itemID in RepairRequirements.Keys)
            {
                for (int i = 0; i < RepairRequirements[itemID]; i++)
                    Main.LocalPlayer.ConsumeItem(itemID);
            }

            telescope.IsRepaired = true;
            ModContent.GetInstance<StargazingEvent>().SafeSetStage(1);

            if (Main.netMode != NetmodeID.SinglePlayer)
                PacketManager.SendPacket<SolynTelescopeTileEntityPacket>(telescope.ID);

            SoundEngine.PlaySound(GennedAssets.Sounds.Item.SolynTelescopeFix with { Volume = 0.65f });

            return true;
        }

        if (AnyoneCanRepairTelescope)
        {
            string text = Language.GetTextValue("Mods.NoxusBoss.Dialog.TelescopeRepairFailText");
            Rectangle textSpawnArea = Main.LocalPlayer.Hitbox;
            CombatText.NewText(textSpawnArea, Color.Red, text);
        }

        return false;
    }

    private static bool HandleUsageInteraction_Repaired()
    {
        bool canUseTelescope = !Main.dayTime || Main.time <= 3600D || Main.time >= Main.dayLength - 3600D;
        if (canUseTelescope)
        {
            StargazingScene.IsActive = true;
            return true;
        }

        string text = Language.GetTextValue("Mods.NoxusBoss.Dialog.TelescopeDaytimeText");
        Rectangle textSpawnArea = Main.LocalPlayer.Hitbox;
        CombatText.NewText(textSpawnArea, Color.Orange, text);
        return false;
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile t = Main.tile[i, j];
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;

        Texture2D baseTexture = TextureAssets.Tile[Type].Value;
        if (!GoldVariant)
            baseTexture = PlatinumBaseTexture.Value;

        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        drawOffset.Y += TileObjectData.GetTileData(t).DrawYOffset;

        Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + drawOffset;
        Color lightColor = Lighting.GetColor(i, j);

        Main.spriteBatch.Draw(baseTexture, drawPosition, new(frameX, frameY, 16, 16), lightColor, 0f, Vector2.Zero, 1f, 0, 0f);
        return false;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch) => DrawTube(i, j);

    public static void DrawTube(int i, int j)
    {
        Tile t = Main.tile[i, j];
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;

        if (frameX != 18 || frameY != 36)
            return;

        TESolynTelescope? telescope = FindTileEntity<TESolynTelescope>(i, j, Width, Height);
        bool repaired = telescope is not null && telescope.IsRepaired;
        Texture2D opticalTube;

        if (GoldVariant)
            opticalTube = (repaired ? GoldOpticalTubeTexture : GoldOpticalTubeBrokenTexture).Value;
        else
            opticalTube = (repaired ? PlatinumOpticalTubeTexture : PlatinumOpticalTubeBrokenTexture).Value;

        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y - 16f) + drawOffset;
        Color lightColor = Lighting.GetColor(i, j);

        Main.spriteBatch.Draw(opticalTube, drawPosition, null, lightColor, Pi / 6f, opticalTube.Size() * 0.5f, 1f, 0, 0f);
    }

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => !Main.dayTime;
}
