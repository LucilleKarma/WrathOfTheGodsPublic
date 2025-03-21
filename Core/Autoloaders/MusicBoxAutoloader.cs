﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using static NoxusBoss.Core.Autoloaders.MusicBoxAutoloader.AutoloadableMusicBoxItem;

namespace NoxusBoss.Core.Autoloaders;

public static class MusicBoxAutoloader
{
    [Autoload(false)]
    public class AutoloadableMusicBoxItem : ModItem
    {
        // Using MusicLoader.GetMusicSlot at Load time doesn't work and returns a value of 0. As such, it's necessary to store the path of the music so that the slot ID
        // can be retrieved at a later time in the loading process.
        private readonly string musicPath;

        private readonly string texturePath;

        private readonly string name;

        internal int tileID;

        internal PreDrawTooltipDelegate drawOverrideBehavior;

        public override string Name => name;

        public override string Texture => texturePath;

        // Necessary for autoloaded types since the constructor is important in determining the behavior of the given instance, making it impossible to rely on an a parameterless one for
        // managing said instances.
        protected override bool CloneNewInstances => true;

        public delegate bool PreDrawTooltipDelegate(DrawableTooltipLine line, ref int yOffset);

        public AutoloadableMusicBoxItem(string texturePath, string musicPath)
        {
            string name = Path.GetFileName(texturePath).Replace("_Item", string.Empty);
            this.musicPath = musicPath;
            this.texturePath = texturePath;
            this.name = name;
        }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // Music boxes can't get prefixes in vanilla.
            ItemID.Sets.CanGetPrefixes[Type] = false;

            // Recorded music boxes transform into the basic form in shimmer.
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.MusicBox;

            // Register the music box with the desired music.
            int musicSlotID = MusicLoader.GetMusicSlot(Mod, musicPath);
            MusicLoader.AddMusicBox(Mod, musicSlotID, Type, tileID);
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) =>
            drawOverrideBehavior(line, ref yOffset);

        public override void SetDefaults()
        {
            Item.DefaultToMusicBox(tileID);
        }
    }

    [Autoload(false)]
    public class AutoloadableMusicBoxTile(string texturePath) : ModTile
    {
        internal int itemID;

        internal Func<int, int, int, bool> drawBehavior;

        private readonly string texturePath = texturePath;

        private readonly string name = Path.GetFileName(texturePath).Replace("_Tile", "Tile");

        public override string Name => name;

        public override string Texture => texturePath;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileObsidianKill[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            TileID.Sets.DisableSmartCursor[Type] = true;

            AddMapEntry(new Color(150, 137, 142));
        }

        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = itemID;
        }

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override void KillMultiTile(int i, int j, int frameX, int frameY)
        {
            if (frameX >= 36)
                Item.NewItem(new EntitySource_TileBreak(i, j), new Point(i, j).ToWorldCoordinates(), itemID);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => drawBehavior(Type, i, j);
    }

    public static void Create(Mod mod, string texturePathBase, string musicPath, out int itemID, out int tileID, Func<int, int, int, bool>? tileDrawBehavior = null, PreDrawTooltipDelegate? itemTooltipBehavior = null)
    {
        // Autoload the item.
        AutoloadableMusicBoxItem boxItem = new AutoloadableMusicBoxItem($"{texturePathBase}_Item", musicPath);
        mod.AddContent(boxItem);
        itemID = boxItem.Type;

        // Autoload the tile.
        AutoloadableMusicBoxTile boxTile = new AutoloadableMusicBoxTile($"{texturePathBase}_Tile");
        mod.AddContent(boxTile);
        tileID = boxTile.Type;

        // Load the draw behavior.
        boxItem.drawOverrideBehavior = itemTooltipBehavior ?? new((DrawableTooltipLine line, ref int yOffset) => true);
        boxTile.drawBehavior = tileDrawBehavior ?? ((tileID, x, y) => true);

        // Link the loaded types together by informing each other of their respective IDs.
        boxItem.tileID = tileID;
        boxTile.itemID = itemID;
    }
}
