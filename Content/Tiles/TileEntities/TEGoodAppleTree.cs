using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.Projectiles.Typeless;

using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Tiles.TileEntities;

public class TEGoodAppleTree : ModTileEntity, IClientSideTileEntityUpdater
{
    /// <summary>
    /// Represents an apple as defined on a good apple tree. Is optionally rendered based on whether it's fallen from the tree or not.
    /// </summary>
    /// 
    /// <remarks>
    /// These apples are purely visual props, and are unrelated to item collection interactions. For the entity that the player can interact with, refer to <see cref="FallenGoodApple"/>.
    /// </remarks>
    public class Apple
    {
        /// <summary>
        /// Whether the apple is active on the tree. If it isn't, it will not render on it.
        /// </summary>
        public bool Active;

        /// <summary>
        /// The current rotation of this apple.
        /// </summary>
        public float Rotation;

        /// <summary>
        /// The facing direction of the apple as a value of -1 or 1.
        /// </summary>
        public readonly int FacingDirection;

        /// <summary>
        /// The standard offset of the apple when on the tree.
        /// </summary>
        public readonly Vector2 StandardOffset;

        /// <summary>
        /// The amount by which a given apple is darkened due its positioning on the tree. This should be used to mimic light based on position (aka apples beneath a bunch of leaves should be darker).
        /// </summary>
        public readonly float Darkening;

        /// <summary>
        /// The scale of the apple.
        /// </summary>
        public readonly float Scale;

        public Apple(Vector2 standardOffset, float darkening)
        {
            StandardOffset = standardOffset;
            Darkening = darkening;
            FacingDirection = Main.rand.NextFromList(-1, 1);
            Scale = Main.rand.NextFloat(0.6f, 1.01f);
            Active = true;
        }

        /// <summary>
        /// Updates the state of this apple.
        /// </summary>
        public void Update(int time)
        {
            float windSwayInterpolant = Cos01(StandardOffset.X * 2.6f + time * 0.0716f);
            float windSwayRotation = Lerp(-0.14f, 0.11f, windSwayInterpolant);
            Rotation = windSwayRotation * FacingDirection;
        }

        /// <summary>
        /// Renders this apple on the tree.
        /// </summary>
        /// <param name="startingPoint">The starting point upon which the apple should be rendered relative to.</param>
        public void Render(Vector2 startingPoint)
        {
            if (!Active)
                return;

            Texture2D appleTexture = GennedAssets.Textures.Items.GoodApple.Value;
            Color color = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.2f + StandardOffset.X * 0.07f) % 1f, 0.92f, 0.92f);
            color = Color.Lerp(color, Color.Black, Darkening);

            Vector2 origin = new Vector2(24f, 2f);
            if (FacingDirection == -1)
                origin.X = 6f;

            Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawPosition = startingPoint + new Vector2(120f, 150f) - Main.screenPosition + drawOffset;
            SpriteEffects direction = FacingDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.spriteBatch.Draw(appleTexture, drawPosition + StandardOffset, null, color, Rotation, origin, Scale, direction, 0f);
        }
    }

    /// <summary>
    /// A general purpose timer that dictates the updating of the apples.
    /// </summary>
    public int Time
    {
        get;
        set;
    }

    /// <summary>
    /// The set of all apples on the tree.
    /// </summary>
    public Apple[] ApplesOnTree = GenerateStandardAppleConfiguration();

    private static Apple[] GenerateStandardAppleConfiguration()
    {
        return new Apple[]
        {
            new Apple(new(-272f, -302f), 0.21f),
            new Apple(new(-244f, -284f), 0.4f),
            new Apple(new(-164f, -248f), 0.32f),
            new Apple(new(-70f, -478f), 0f),
            new Apple(new(-46f, -248f), 0f),
            new Apple(new(-30f, -456f), 0.4f),
            new Apple(new(-14f, -474f), 0.25f),
            new Apple(new(2f, -480f), 0f),
            new Apple(new(6f, -260f), 0.1f),
            new Apple(new(24f, -460f), 0.2f),
            new Apple(new(146f, -244f), 0.4f),
            new Apple(new(166f, -308f), 0.4f),
            new Apple(new(188f, -184f), 0.27f),
            new Apple(new(212f, -320f), 0.6f),
        };
    }

    /// <summary>
    /// Drops all apples from the tree, making the props disappear and creating obtainable entities in their place.
    /// </summary>
    public void DropApples()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        foreach (Apple apple in ApplesOnTree)
        {
            apple.Active = false;

            Vector2 fruitWorldPosition = Position.ToWorldCoordinates() + new Vector2(120f, 170f) + apple.StandardOffset;
            NewProjectileBetter(new EntitySource_WorldEvent(), fruitWorldPosition, Vector2.UnitY.RotatedBy(apple.Rotation) * Main.rand.NextFloat(2.5f, 3.3f), ModContent.ProjectileType<FallenGoodApple>(), 0, 0f);
        }

        NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }

    /// <summary>
    /// Causes all apple props on the tree to re-appear.
    /// </summary>
    public void RegenerateApples()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        foreach (Apple apple in ApplesOnTree)
            apple.Active = true;

        NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }

    public void ClientSideUpdate()
    {
        Time++;
        foreach (Apple apple in ApplesOnTree)
            apple.Update(Time);
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(ApplesOnTree.Length);
        for (int i = 0; i < ApplesOnTree.Length; i++)
        {
            writer.Write(ApplesOnTree[i].Active);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            ApplesOnTree[i].Active = reader.ReadBoolean();
        }
    }

    public override void SaveData(TagCompound tag)
    {
        List<bool> appleActivityStates = ApplesOnTree.Select(a => a.Active).ToList();
        tag["AppleActivityStates"] = appleActivityStates;
    }

    public override void LoadData(TagCompound tag)
    {
        IList<bool> appleActivityStates = tag.GetList<bool>("AppleActivityStates");
        for (int i = 0; i < appleActivityStates.Count; i++)
        {
            if (i < ApplesOnTree.Length)
                ApplesOnTree[i].Active = appleActivityStates[i];
        }
    }

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<GoodAppleTree>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
    }

    public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        // If in multiplayer, tell the server to place the tile entity and DO NOT place it yourself. That would mismatch IDs.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
            return -1;
        }
        return Place(i, j);
    }

    // Sync the tile entity the moment it is place on the server.
    // This is done to cause it to register among all clients.
    public override void OnNetPlace() => NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
}
