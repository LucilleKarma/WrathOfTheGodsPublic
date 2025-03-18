using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Tiles.SolynCampsite;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Graphics.UI.Books;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.Physics.VerletIntergration;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Tiles.TileEntities;

public class TESolynTent : ModTileEntity, IClientSideTileEntityUpdater
{
    /// <summary>
    /// Whether this tile entity's bookshelf UI is visible.
    /// </summary>
    public bool UIEnabled
    {
        get;
        set;
    }

    /// <summary>
    /// The 0-1 interpolant that determines how visible the UI associated with this tent's bookshelf should be.
    /// </summary>
    public float UIAppearanceInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The general purpose timer for use with wind rotation effects.
    /// </summary>
    public float WindTime
    {
        get;
        set;
    }

    /// <summary>
    /// The rope this entity holds locally.
    /// </summary>
    public VerletSimulatedRope? TreeTieRope
    {
        get;
        private set;
    }

    /// <summary>
    /// The starting point of the rope associated with this tent. If there is no rope, this defaults to <see cref="Vector2.Zero"/>.
    /// </summary>
    public Vector2 RopeStart
    {
        get;
        set;
    }

    /// <summary>
    /// The ending point of the rope associated with this tent. If there is no rope, this defaults to <see cref="Vector2.Zero"/>.
    /// </summary>
    public Vector2 RopeEnd
    {
        get;
        set;
    }

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<SolynTent>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
    }

    public void ClientSideUpdate()
    {
        WindTime = (WindTime + Abs(Main.windSpeedCurrent) * 0.067f) % (TwoPi * 5000f);
        HandleUIInteraction();
        if (TreeTieRope is null)
            return;

        // Ensure that the tree that this rope is tied to is still valid.
        // If it isn't, the rope should detach and fall to the ground, disappearing once players leave and rejoin the world.
        // If it is, the roach should attach to it as normal.
        Tile ropeStartTile = Framing.GetTileSafely(RopeStart.ToTileCoordinates());
        bool attachedToTree = ropeStartTile.TileType == TileID.Trees && ropeStartTile.HasUnactuatedTile && RopeStart != Vector2.Zero;
        if (!attachedToTree)
        {
            TreeTieRope.Rope[0].Locked = false;

            // Uninitialize the rope starting position.
            RopeStart = Vector2.Zero;
            if (Main.netMode != NetmodeID.SinglePlayer)
                PacketManager.SendPacket<SolynTentTileEntityPacket>(ID);
        }
        else
        {
            TreeTieRope.Rope[0].Locked = true;
            TreeTieRope.Rope[0].Position = RopeStart;
        }

        // Lock the rope to the tent.
        TreeTieRope.Rope[^1].Position = RopeEnd + (TreeTieRope.Rope[^1].Position - TreeTieRope.Rope[^2].Position).SafeNormalize(Vector2.Zero) * 112f;
        TreeTieRope.Rope[^1].Locked = true;

        if (!TileDisablingSystem.TilesAreUninteractable)
        {
            foreach (Player player in Main.ActivePlayers)
                MoveRopeBasedOnEntity(player);
            foreach (NPC townNPC in Main.ActiveNPCs)
            {
                if (!townNPC.townNPC)
                    continue;

                MoveRopeBasedOnEntity(townNPC);
            }

            float windForceWave = Cos01(WindTime);
            Vector2 windForce = Vector2.UnitX * windForceWave * InverseLerp(0f, 0.75f, Abs(Main.windSpeedCurrent)) * -5f;
            VerletSimulations.TileCollisionVerletSimulation(TreeTieRope.Rope, TreeTieRope.IdealRopeLength / TreeTieRope.Rope.Count, windForce, 60, 0.5f);
        }
    }

    public void HandleUIInteraction()
    {
        bool uiCanAppear = Main.LocalPlayer.WithinRange(Position.ToWorldCoordinates(), 240f) && Main.playerInventory;
        bool teSelectedByUI = ModContent.GetInstance<SolynBookExchangeUI>().VisibleTileEntity == this;
        bool uiIsVisible = uiCanAppear && teSelectedByUI && UIEnabled;
        UIAppearanceInterpolant = Saturate(UIAppearanceInterpolant + uiIsVisible.ToDirectionInt() * 0.037f);

        if (UIAppearanceInterpolant <= 0f && teSelectedByUI)
        {
            ModContent.GetInstance<SolynBookExchangeUI>().VisibleTileEntity = null;
            UIEnabled = false;
        }
    }

    public void MoveRopeBasedOnEntity(Entity e)
    {
        if (TreeTieRope is null)
            return;

        // Cap the velocity to ensure it doesn't make the rope go flying.
        Vector2 entityVelocity = (e.velocity * 1.1f).ClampLength(0f, 20f);
        if (e is NPC)
            entityVelocity *= 3f;
        else
            entityVelocity *= new Vector2(0.8f, 2f);

        for (int i = 1; i < TreeTieRope.Rope.Count - 1; i++)
        {
            VerletSimulatedSegment segment = TreeTieRope.Rope[i];
            VerletSimulatedSegment next = TreeTieRope.Rope[i + 1];

            // Check to see if the entity is between two verlet segments via line/box collision checks.
            // If they are, add the entity's velocity to the two segments relative to how close they are to each of the two.
            float _ = 0f;
            if (Collision.CheckAABBvLineCollision(e.TopLeft, e.Size, segment.Position, next.Position, 30f, ref _))
            {
                // Weigh the entity's distance between the two segments.
                // If they are close to one point that means the strength of the movement force applied to the opposite segment is weaker, and vice versa.
                float distanceBetweenSegments = segment.Position.Distance(next.Position);
                float distanceToRope = e.Distance(segment.Position);
                float currentMovementOffsetInterpolant = InverseLerp(distanceToRope, distanceBetweenSegments, distanceBetweenSegments * 0.2f);
                float nextMovementOffsetInterpolant = 1f - currentMovementOffsetInterpolant;

                // Move the segments based on the weight values.
                if (!segment.Locked)
                    segment.Velocity += entityVelocity * currentMovementOffsetInterpolant;
                if (!next.Locked)
                    next.Velocity += entityVelocity * nextMovementOffsetInterpolant;
            }
        }
    }

    public void DrawRope()
    {
        if (TreeTieRope is null)
            return;

        static Color ropeColorFunction(float completionRatio) => new Color(63, 37, 18);

        TreeTieRope.DrawProjection(WhitePixel, -Main.screenPosition, false, ropeColorFunction, widthFactor: 2f);

        Vector2[] curveControlPoints = new Vector2[TreeTieRope.Rope.Count];
        Vector2[] curveVelocities = new Vector2[TreeTieRope.Rope.Count];
        for (int i = 0; i < curveControlPoints.Length; i++)
        {
            curveControlPoints[i] = TreeTieRope.Rope[i].Position;
            curveVelocities[i] = TreeTieRope.Rope[i].Velocity;
        }

        DeCasteljauCurve positionCurve = new DeCasteljauCurve(curveControlPoints);
        DeCasteljauCurve velocityCurve = new DeCasteljauCurve(curveVelocities);

        int ornamentCount = 5;
        Texture2D ornamentTexture = GennedAssets.Textures.SolynCampsite.SolynTentRopeOrnament.Value;
        Texture2D pinTexture = GennedAssets.Textures.SolynCampsite.SolynTentOrnamentPin.Value;
        for (int i = 0; i < ornamentCount; i++)
        {
            float sampleInterpolant = Lerp(0.1f, 0.6f, i / (float)(ornamentCount - 1f));
            Vector2 ornamentWorldPosition = positionCurve.Evaluate(sampleInterpolant) + Vector2.UnitY * 4f;
            Vector2 velocity = velocityCurve.Evaluate(sampleInterpolant) * 0.3f;

            // Emit light at the point of the ornament.
            Lighting.AddLight(ornamentWorldPosition, Color.Wheat.ToVector3() * 0.3f);

            int windGridTime = 20;
            Point ornamentTilePosition = ornamentWorldPosition.ToTileCoordinates();
            Main.instance.TilesRenderer.Wind.GetWindTime(ornamentTilePosition.X, ornamentTilePosition.Y, windGridTime, out int windTimeLeft, out int direction, out _);
            float windGridInterpolant = windTimeLeft / (float)windGridTime;
            float windGridRotation = Utils.GetLerpValue(0f, 0.5f, windGridInterpolant, true) * Utils.GetLerpValue(1f, 0.5f, windGridInterpolant, true) * direction * -0.93f;

            float windForceWave = AperiodicSin(WindTime + ornamentWorldPosition.X * 0.025f);
            float windForce = windForceWave * InverseLerp(0f, 0.75f, Abs(Main.windSpeedCurrent)) * 0.4f;
            float ornamentRotation = (velocity.X * 0.03f + windForce) * Sign(Main.windSpeedCurrent) + windGridRotation;
            Vector2 ornamentDrawPosition = ornamentWorldPosition - Main.screenPosition;
            Rectangle ornamentFrame = ornamentTexture.Frame(1, 2, 0, i % 2);
            Main.spriteBatch.Draw(ornamentTexture, ornamentDrawPosition, ornamentFrame, Color.White, ornamentRotation, ornamentFrame.Size() * new Vector2(0.5f, 0f), 0.8f, 0, 0f);

            // Draw golden pins.
            sampleInterpolant = Lerp(0.1f, 0.6f, (i + 0.5f) / (float)(ornamentCount - 1f));
            Vector2 pinWorldPosition = positionCurve.Evaluate(sampleInterpolant);
            Vector2 pinDrawPosition = pinWorldPosition - Main.screenPosition;
            float pinRotation = (positionCurve.Evaluate(sampleInterpolant + 0.001f) - pinWorldPosition).ToRotation();
            Main.spriteBatch.Draw(pinTexture, pinDrawPosition, null, Color.White, pinRotation, pinTexture.Size() * new Vector2(0.5f, 0f), 0.8f, 0, 0f);
        }
    }

    public override void SaveData(TagCompound tag)
    {
        tag["RopeStartX"] = RopeStart.X;
        tag["RopeStartY"] = RopeStart.Y;
        tag["RopeEndX"] = RopeEnd.X;
        tag["RopeEndY"] = RopeEnd.Y;
    }

    private void GenerateRopeIfNecessary()
    {
        if (RopeStart != Vector2.Zero && RopeEnd != Vector2.Zero)
            TreeTieRope = new(RopeStart, Vector2.Zero, 10, RopeStart.Distance(RopeEnd) * 1.4f);
    }

    public override void LoadData(TagCompound tag)
    {
        RopeStart = new(tag.GetFloat("RopeStartX"), tag.GetFloat("RopeStartY"));
        RopeEnd = new(tag.GetFloat("RopeEndX"), tag.GetFloat("RopeEndY"));
        GenerateRopeIfNecessary();
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.WriteVector2(RopeStart);
        writer.WriteVector2(RopeEnd);
    }

    public override void NetReceive(BinaryReader reader)
    {
        RopeStart = reader.ReadVector2();
        RopeEnd = reader.ReadVector2();
    }

    public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        // If in multiplayer, tell the server to place the tile entity and DO NOT place it yourself. That would mismatch IDs.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendTileSquare(Main.myPlayer, i, j, SolynStatueTile.Width, SolynStatueTile.Height);
            NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
            return -1;
        }

        // Check ahead for potential places that the rope could attach to.
        Vector2 ropeStart = Vector2.Zero;
        Vector2 ropeEnd = new Point(i, j).ToWorldCoordinates() + new Vector2(26f, 12f);
        for (int dx = -4; dx >= -20; dx--)
        {
            for (int dy = -12; dy <= 0; dy++)
            {
                Tile tile = Framing.GetTileSafely(i + dx, j + dy);
                bool isTree = tile.TileType == TileID.Trees && tile.HasUnactuatedTile;
                if (isTree)
                {
                    ropeStart = new Point(i + dx, j + dy).ToWorldCoordinates();
                    break;
                }
            }
        }

        if (Main.netMode == NetmodeID.Server)
            PacketManager.SendPacket<SolynTentTileEntityPacket>(ID);

        int newTileEntityID = Place(i, j);
        if (ByID.TryGetValue(newTileEntityID, out TileEntity? te) && te is TESolynTent tent)
        {
            tent.RopeStart = ropeStart;
            tent.RopeEnd = ropeEnd;
            tent.GenerateRopeIfNecessary();
        }

        return newTileEntityID;
    }

    // Sync the tile entity the moment it is place on the server.
    // This is done to cause it to register among all clients.
    public override void OnNetPlace() => NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
}
