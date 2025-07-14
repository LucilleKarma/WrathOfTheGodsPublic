using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.Projectiles;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.SpecificEffectManagers;

public class PermafrostDoorUnlockSystem : ModSystem
{
    internal static Dictionary<Point, float> DoorBrightnesses = [];

    public override void PostUpdateNPCs()
    {
        foreach (Point p in DoorBrightnesses.Keys)
        {
            if (DoorBrightnesses[p] > 0f)
            {
                DoorBrightnesses[p] += 0.014f;
                ScreenShakeSystem.StartShakeAtPoint(p.ToWorldCoordinates(), InverseLerpBump(0f, 1f, 1.25f, 1.75f, DoorBrightnesses[p]) * 1.2f);
            }

            if (DoorBrightnesses[p] >= 2.25f)
            {
                PermafrostKeepWorldGen.DoorHasBeenUnlocked = true;
                ModContent.GetInstance<PermafrostKeepEvent>().SafeSetStage(2);
                DoorBrightnesses[p] = 0f;

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            Vector2 goreSpawnPosition = p.ToWorldCoordinates(8f, 0f) + new Vector2(Main.rand.NextFloatDirection() * 6f, Main.rand.NextFloat(PermafrostDoor.Height * 16f));
                            Gore.NewGore(new EntitySource_WorldEvent(), goreSpawnPosition, Main.rand.NextVector2CircularEdge(10f, 3f) - Vector2.UnitY * 4f, ModContent.Find<ModGore>(Mod.Name, $"PermafrostDoor{i}").Type, Main.rand.NextFloat(0.5f, 1f));
                        }
                    }

                    for (int i = 0; i < 16; i++)
                    {
                        ParticleOrchestrator.RequestParticleSpawn(true, ParticleOrchestraType.StardustPunch, new ParticleOrchestraSettings()
                        {
                            PositionInWorld = p.ToWorldCoordinates() + Main.rand.NextVector2Circular(50f, 50f),
                            MovementVector = Main.rand.NextVector2Circular(6f, 6f)
                        });
                    }

                    ScreenShakeSystem.StartShakeAtPoint(p.ToWorldCoordinates(), 10f);
                }

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < PermafrostDoor.Height; i++)
                    {
                        Point doorPoint = new Point(p.X, p.Y + i);
                        Tile door = Main.tile[doorPoint];

                        door.Get<TileWallWireStateData>().HasTile = false;

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendTileSquare(-1, doorPoint.X, doorPoint.Y);
                    }

                    PermafrostTileProtectionVisualSystem.CreateImpact(p.ToWorldCoordinates(), 1000f);
                }
            }
        }
    }

    public override void OnWorldLoad() => DoorBrightnesses.Clear();

    public override void OnWorldUnload() => DoorBrightnesses.Clear();

    /// <summary>
    /// Starts an unlock animation for a given door at a given point.
    /// </summary>
    /// <param name="p">The tile position of the door.</param>
    public static void Start(Point p)
    {
        Tile door = Main.tile[p];
        p.X -= door.TileFrameX / 16;
        p.Y -= door.TileFrameY / 16;

        if (DoorBrightnesses.TryGetValue(p, out float b) && b > 0f)
            return;

        DoorBrightnesses[p] = 0.001f;

        Vector2 keyholePosition = p.ToWorldCoordinates(8f, 25f);
        NewProjectileBetter(new EntitySource_WorldEvent(), keyholePosition, Vector2.Zero, ModContent.ProjectileType<PermafrostDoorConvergingEnergy>(), 0, 0f);

        SoundEngine.PlaySound(GennedAssets.Sounds.Environment.PermafrostKeepDoorBreak, keyholePosition);
    }
}
