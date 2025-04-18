using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.Placeable;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Physics.VerletIntergration;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Tiles.SolynRopes;

public class SolynRopeData
{
    private Point end;

    /// <summary>
    /// Whether this rope should rop an item if it's destroyed.
    /// </summary>
    public bool DropsItem
    {
        get;
        set;
    }

    /// <summary>
    /// A general-purpose timer used for wind movement on the baubles attached to this rope.
    /// </summary>
    public float WindTime
    {
        get;
        set;
    }

    /// <summary>
    /// The starting position of the rope.
    /// </summary>
    public readonly Point Start;

    /// <summary>
    /// The end position of the rope.
    /// </summary>
    public Point End
    {
        get => end;
        set
        {
            end = value;
            Vector2 endVector = end.ToVector2();
            ClampToMaxLength(ref endVector);

            VerletRope.Rope[^1].Position = endVector;
            VerletRope.Rope[^1].OldPosition = endVector;
        }
    }

    /// <summary>
    /// The verlet segments associated with this rope.
    /// </summary>
    public readonly VerletSimulatedRope VerletRope;

    /// <summary>
    /// The maximum length of ropes.
    /// </summary>
    public static float MaxLength => 360f;

    /// <summary>
    /// The amount of gravity imposed on this rope.
    /// </summary>
    public static float Gravity => 0.3f;

    public SolynRopeData(Point start, Point end)
    {
        Vector2 startVector = start.ToVector2();
        Vector2 endVector = end.ToVector2();

        VerletRope = new VerletSimulatedRope(startVector, Vector2.Zero, 16, 0f);
        Start = start;
        End = end;

        VerletRope.Rope[0].Position = startVector;
        VerletRope.Rope[0].OldPosition = startVector;
        VerletRope.Rope[0].Locked = true;

        VerletRope.Rope[^1].Position = endVector;
        VerletRope.Rope[^1].OldPosition = endVector;
        VerletRope.Rope[^1].Locked = true;
    }

    private void ClampToMaxLength(ref Vector2 end)
    {
        Vector2 startVector = Start.ToVector2();
        if (!end.WithinRange(startVector, MaxLength))
            end = startVector + (end - startVector).SafeNormalize(Vector2.Zero) * MaxLength;
    }

    /// <summary>
    /// Updates this rope.
    /// </summary>
    public void Update_Standard()
    {
        bool startHasNoTile = !Framing.GetTileSafely(Start.ToVector2().ToTileCoordinates()).HasTile;
        bool endHasNoTile = !Framing.GetTileSafely(End.ToVector2().ToTileCoordinates()).HasTile;
        if (startHasNoTile || endHasNoTile)
        {
            SolynRopeSystem.Remove(this);
            if (Main.netMode != NetmodeID.MultiplayerClient && DropsItem)
                Item.NewItem(new EntitySource_TileBreak(Start.X / 16, Start.Y / 16), Start.ToVector2(), ModContent.ItemType<FancyRopeSet>());
            return;
        }

        WindTime = (WindTime + Abs(Main.windSpeedCurrent) * 0.067f) % (TwoPi * 5000f);
        VerletRope.Update(Start.ToVector2(), Gravity);
    }

    /// <summary>
    /// Updates this rope for a preview instance, ensuring that gravity applies extremely quickly.
    /// </summary>
    public void Update_Preview()
    {
        Vector2 snappedMouseWorld = (Main.MouseWorld / 8f).Floor() * 8f;
        End = snappedMouseWorld.ToPoint();

        for (int i = 0; i < 8; i++)
            VerletRope.Update(Start.ToVector2(), Gravity);
    }

    /// <summary>
    /// Renders this rope.
    /// </summary>
    public void Render(bool emitLight, Color colorModifier)
    {
        Color ropeColorFunction(float completionRatio) => new Color(63, 37, 18).MultiplyRGBA(colorModifier);
        VerletRope.DrawProjectionButItActuallyWorks(WhitePixel, -Main.screenPosition, false, ropeColorFunction, widthFactor: 2f);

        Vector2[] curveControlPoints = new Vector2[VerletRope.Rope.Count];
        Vector2[] curveVelocities = new Vector2[VerletRope.Rope.Count];
        for (int i = 0; i < curveControlPoints.Length; i++)
        {
            curveControlPoints[i] = VerletRope.Rope[i].Position;
            curveVelocities[i] = VerletRope.Rope[i].Velocity;
        }

        DeCasteljauCurve positionCurve = new DeCasteljauCurve(curveControlPoints);
        DeCasteljauCurve velocityCurve = new DeCasteljauCurve(curveVelocities);

        int ornamentCount = (int)(VerletRope.RopeLength / 99f) + 2;
        Texture2D ornamentTexture = GennedAssets.Textures.SolynCampsite.SolynTentRopeOrnament.Value;
        Texture2D pinTexture = GennedAssets.Textures.SolynCampsite.SolynTentOrnamentPin.Value;
        for (int i = 0; i < ornamentCount; i++)
        {
            float sampleInterpolant = Lerp(0.1f, 0.8f, i / (float)(ornamentCount - 1f));
            Vector2 ornamentWorldPosition = positionCurve.Evaluate(sampleInterpolant);
            Vector2 velocity = velocityCurve.Evaluate(sampleInterpolant) * 0.3f;

            // Emit light at the point of the ornament.
            if (emitLight)
                Lighting.AddLight(ornamentWorldPosition, Color.Wheat.MultiplyRGBA(colorModifier).ToVector3() * 0.3f);

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
            Main.spriteBatch.Draw(ornamentTexture, ornamentDrawPosition, ornamentFrame, colorModifier, ornamentRotation, ornamentFrame.Size() * new Vector2(0.5f, 0f), 0.8f, 0, 0f);

            // Draw golden pins.
            sampleInterpolant = Lerp(0.1f, 0.8f, (i + 0.5f) / (float)(ornamentCount - 1f));
            Vector2 pinWorldPosition = positionCurve.Evaluate(sampleInterpolant);
            Vector2 pinDrawPosition = pinWorldPosition - Main.screenPosition;
            float pinRotation = (positionCurve.Evaluate(sampleInterpolant + 0.001f) - pinWorldPosition).ToRotation();
            if (Cos(pinRotation) < 0f)
                pinRotation += Pi;

            Main.spriteBatch.Draw(pinTexture, pinDrawPosition, null, colorModifier, pinRotation, pinTexture.Size() * new Vector2(0.5f, 0f), 0.8f, 0, 0f);
        }
    }

    /// <summary>
    /// Serializes this rope data as a tag compound for world saving.
    /// </summary>
    public TagCompound Serialize()
    {
        return new TagCompound()
        {
            ["DropsItem"] = DropsItem,
            ["Start"] = Start,
            ["End"] = End,
            ["RopePositions"] = VerletRope.Rope.Select(p => p.Position.ToPoint()).ToList()
        };
    }

    /// <summary>
    /// Deserializes a tag compound containing data for a rope back into said rope.
    /// </summary>
    public static SolynRopeData Deserialize(TagCompound tag)
    {
        SolynRopeData rope = new SolynRopeData(tag.Get<Point>("Start"), tag.Get<Point>("End"))
        {
            DropsItem = tag.TryGet("DropsItem", out bool dropsItem) && dropsItem
        };
        Vector2[] ropePositions = tag.Get<Point[]>("RopePositions").Select(p => p.ToVector2()).ToArray();

        rope.VerletRope.Rope = new List<VerletSimulatedSegment>();
        for (int i = 0; i < ropePositions.Length; i++)
        {
            bool locked = i == 0 || i == ropePositions.Length - 1;
            rope.VerletRope.Rope.Add(new VerletSimulatedSegment(ropePositions[i], Vector2.Zero, locked));
        }

        return rope;
    }
}
