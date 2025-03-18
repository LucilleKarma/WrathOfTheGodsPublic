using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.TentInterior;
using NoxusBoss.Core.Graphics.UI.Books;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles.SolynCampsite;

public class SolynTent : ModTile
{
    internal static int ClickDelay
    {
        get;
        set;
    }

    internal static LazyAsset<Texture2D> FrontTexture;

    /// <summary>
    /// The tiled width of this tent.
    /// </summary>
    public const int Width = 11;

    /// <summary>
    /// The tiled height of this tent.
    /// </summary>
    public const int Height = 7;

    public override string Texture => GetAssetPath("Content/Tiles/SolynCampsite", Name);

    /// <summary>
    /// The name of the reference variable that determines whether a given player has interacted with Solyn's bookshelf before.
    /// </summary>
    public static string InteractedWithBookselfVariableName => "InteractedWithBookshelf";

    public override void SetStaticDefaults()
    {
        if (Main.netMode != NetmodeID.Server)
            FrontTexture = LazyAsset<Texture2D>.FromPath($"{Texture}Front");

        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(Width / 2, Height - 1);
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.newTile.LavaDeath = false;

        // Set the respective tile entity as a secondary element to incorporate when placing this tile.
        ModTileEntity tileEntity = ModContent.GetInstance<TESolynTent>();
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(tileEntity.Hook_AfterPlacement, -1, 0, true);

        TileObjectData.addTile(Type);
        AddMapEntry(new Color(114, 44, 68));

        PlayerDataManager.SaveDataEvent += SaveBookshelfInteraction;
        PlayerDataManager.LoadDataEvent += LoadBookshelfInteraction;
        GlobalTileEventHandlers.IsTileUnbreakableEvent += MakeTilesBelowUnbreakable;
        On_WorldGen.BlockBelowMakesSandFall += DisallowSandfallBeneathTent;
    }

    private bool DisallowSandfallBeneathTent(On_WorldGen.orig_BlockBelowMakesSandFall orig, int i, int j)
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

    private static void SaveBookshelfInteraction(PlayerDataManager p, TagCompound tag)
    {
        if (p.GetValueRef<bool>(InteractedWithBookselfVariableName))
            tag[InteractedWithBookselfVariableName] = true;
    }

    private static void LoadBookshelfInteraction(PlayerDataManager p, TagCompound tag)
    {
        p.GetValueRef<bool>(InteractedWithBookselfVariableName).Value = tag.ContainsKey(InteractedWithBookselfVariableName);
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        Tile tile = Main.tile[i, j];
        int left = i - tile.TileFrameX % (Width * 16) / 16;
        int top = j - tile.TileFrameY % (Height * 16) / 16;

        // Kill the hosted tile entity directly and immediately.
        TESolynTent? tent = FindTileEntity<TESolynTent>(i, j, Width, Height);
        tent?.Kill(left, top);
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        SolynTentInteriorRenderer.CloseToTentTimer = 15;

        Tile t = Framing.GetTileSafely(i, j);
        Point p = new Point(i, j);
        if (t.TileFrameX == 0 && t.TileFrameY == 0 && !SolynTentInteriorRenderer.TentPoints.Contains(p))
            SolynTentInteriorRenderer.TentPoints.Add(p);

        bool isCenterTile = t.TileFrameX == 126 && t.TileFrameY == 90;
        bool playerCloseToSelf = Main.LocalPlayer.WithinRange(new Vector2(i, j).ToWorldCoordinates(8f, 0f), 20f);
        if (isCenterTile && playerCloseToSelf && SolynCampsiteNoteManager.NoteIsInTent)
        {
            Item.NewItem(new EntitySource_WorldEvent(), Main.LocalPlayer.Center, ModContent.ItemType<HandwrittenNote>());
            SolynCampsiteNoteManager.HasReceivedNote = true;

            PacketManager.SendPacket<HandwrittenNotePacket>();
        }
    }

    public static void ClickBookshelf(int i, int j)
    {
        TESolynTent? te = FindTileEntity<TESolynTent>(i, j, Width, Height);
        if (te is null)
            return;

        te.UIEnabled = !te.UIEnabled;
        if (te.UIEnabled)
        {
            Main.playerInventory = true;
            ModContent.GetInstance<SolynBookExchangeUI>().VisibleTileEntity = te;
            SoundEngine.PlaySound(SoundID.MenuOpen);
        }
        else
            SoundEngine.PlaySound(SoundID.MenuClose);
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile t = Main.tile[i, j];
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;

        if (frameX == 0 && frameY == 0 && ClickDelay >= 1)
            ClickDelay--;

        // Draw the main tile texture.
        Texture2D mainTexture = TextureAssets.Tile[Type].Value;
        Texture2D interiorTexture = GennedAssets.Textures.SolynCampsite.SolynTentInterior.Value;
        Texture2D bookshelfOutline = GennedAssets.Textures.SolynCampsite.SolynTentBookshelfOutline.Value;
        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + drawOffset;
        drawPosition.Y += TileObjectData.GetTileData(t).DrawYOffset;
        Color lightColor = Lighting.GetColor(i, j);
        if (frameY <= 16)
            lightColor = Color.White;

        if (SolynSleepTracker.SolynIsAsleep)
            interiorTexture = GennedAssets.Textures.SolynCampsite.SolynTentInterior_SolynInBag.Value;

        Color bookshelfOutlineColor = Color.Transparent;
        Point tentTopLeft = new Point(i - frameX / 18, j - frameY / 18).ToWorldCoordinates().ToPoint();
        Rectangle bookshelfArea = new Rectangle(tentTopLeft.X + 44, tentTopLeft.Y + 34, 48, 66);
        Point mousePoint = (new Vector2(Player.tileTargetX, Player.tileTargetY) * 16f).ToPoint();
        if (bookshelfArea.Contains(mousePoint) && SolynTentInteriorRenderer.OutsideDarkness >= 0.6f)
        {
            bookshelfOutlineColor = Color.Yellow;
            if (Main.mouseRight && frameX == 0 && frameY == 0 && ClickDelay <= 0)
            {
                Main.LocalPlayer.GetValueRef<bool>(InteractedWithBookselfVariableName).Value = true;

                ClickBookshelf(i, j);
                ClickDelay = 5;
            }
        }
        if (!Main.LocalPlayer.GetValueRef<bool>(InteractedWithBookselfVariableName))
            bookshelfOutlineColor = Color.Yellow;

        spriteBatch.Draw(mainTexture, drawPosition, new Rectangle(frameX, frameY, 16, 16), lightColor * (1f - SolynTentInteriorRenderer.OutsideDarkness), 0f, Vector2.Zero, 1f, 0, 0f);
        spriteBatch.Draw(interiorTexture, drawPosition, new Rectangle(frameX, frameY, 16, 16), lightColor * SolynTentInteriorRenderer.OutsideDarkness, 0f, Vector2.Zero, 1f, 0, 0f);
        spriteBatch.Draw(bookshelfOutline, drawPosition, new Rectangle(frameX, frameY, 16, 16), bookshelfOutlineColor * SolynTentInteriorRenderer.OutsideDarkness, 0f, Vector2.Zero, 1f, 0, 0f);
        if (SolynCampsiteNoteManager.NoteIsInTent)
        {
            Texture2D note = GennedAssets.Textures.SolynCampsite.SolynTentNote.Value;
            spriteBatch.Draw(note, drawPosition, new Rectangle(frameX, frameY, 16, 16), lightColor * SolynTentInteriorRenderer.OutsideDarkness, 0f, Vector2.Zero, 1f, 0, 0f);
        }

        return false;
    }
}
