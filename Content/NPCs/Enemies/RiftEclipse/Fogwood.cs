using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.SoundSystems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using WorldConditions = Terraria.WorldBuilding.Conditions;

namespace NoxusBoss.Content.NPCs.Enemies.RiftEclipse;

public class Fogwood : ModNPC
{
    public enum FogwoodAttackType
    {
        CreepTowardsTarget,
        ScreamViolently,
        CrushTarget,
        RootSkewer
    }

    #region Fields and Properties

    private LoopedSoundInstance whistleLoopSound;

    public Player Target => Main.player[NPC.target];

    public Vector2 WhistleSourcePosition
    {
        get;
        set;
    }

    public FogwoodAttackType CurrentState
    {
        get => (FogwoodAttackType)NPC.ai[0];
        set => NPC.ai[0] = (int)value;
    }

    public ref float AITimer => ref NPC.ai[1];

    public ref float TreeTopOffsetAngle => ref NPC.localAI[0];

    public ref float BranchMaxOffsetAngle => ref NPC.localAI[1];

    public ref float EvilInterpolant => ref NPC.localAI[2];

    public static LazyAsset<Texture2D> MyTexture
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D> MyTextureEvil
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D> BodyTexture
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D> BodyTextureEvil
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D>[] BranchTextures
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D>[] BranchTexturesEvil
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D>[] TopsTextures
    {
        get;
        private set;
    }

    public static LazyAsset<Texture2D>[] TopsTexturesEvil
    {
        get;
        private set;
    }

    // Determines whether tree tops and such should be accurate to the world environment or not.
    // When set to false, a predefined variant is used.
    public static bool UseSingleVariant => true;

    public override string Texture => GetAssetPath("Content/NPCs/Enemies/RiftEclipse", Name);

    #endregion Fields and Properties

    #region Initialization

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 3;
        if (Main.netMode != NetmodeID.Server)
        {
            MyTexture = LazyAsset<Texture2D>.FromPath(Texture);
            MyTextureEvil = LazyAsset<Texture2D>.FromPath($"{Texture}Evil");

            BodyTexture = LazyAsset<Texture2D>.FromPath($"{Texture}Segment");
            BodyTextureEvil = LazyAsset<Texture2D>.FromPath($"{Texture}SegmentEvil");

            BranchTextures = new LazyAsset<Texture2D>[6];
            TopsTextures = new LazyAsset<Texture2D>[6];
            BranchTexturesEvil = new LazyAsset<Texture2D>[6];
            TopsTexturesEvil = new LazyAsset<Texture2D>[6];
            for (int i = 0; i < 6; i++)
            {
                BranchTextures[i] = LazyAsset<Texture2D>.FromPath($"{Texture}Branches{i}");
                TopsTextures[i] = LazyAsset<Texture2D>.FromPath($"{Texture}Tops{i}");

                BranchTexturesEvil[i] = LazyAsset<Texture2D>.FromPath($"{Texture}Branches{i}Evil");
                TopsTexturesEvil[i] = LazyAsset<Texture2D>.FromPath($"{Texture}Tops{i}Evil");
            }
        }
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 4f;
        NPC.damage = 40;
        NPC.width = 26;
        NPC.height = Main.rand?.Next(140, 200) ?? 140;
        NPC.defense = 8;
        NPC.lifeMax = 7200;
        NPC.aiStyle = -1;
        AIType = -1;
        NPC.knockBackResist = 0f;
        NPC.noGravity = true;
        NPC.noTileCollide = false;
        NPC.dontTakeDamage = true;
        NPC.HitSound = SoundID.DD2_SkeletonHurt with { Volume = -0.3f };
        NPC.DeathSound = GennedAssets.Sounds.NPCKilled.FogwoodDeath;
        NPC.hide = true;
        WhistleSourcePosition = NPC.Center;
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        int avatarID = ModContent.NPCType<AvatarOfEmptiness>();
        bestiaryEntry.UIInfoProvider = new HighestOfMultipleUICollectionInfoProvider(new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[Type], true),
                                                                                     new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[avatarID], true));
        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}")
        });
    }

    #endregion Initialization

    #region AI
    public override void AI()
    {
        // Reset things every frame.
        NPC.damage = NPC.defDamage;

        switch (CurrentState)
        {
            case FogwoodAttackType.CreepTowardsTarget:
                DoBehavior_CreepTowardsTarget();
                break;
            case FogwoodAttackType.ScreamViolently:
                DoBehavior_ScreamViolently();
                break;
            case FogwoodAttackType.CrushTarget:
                DoBehavior_CrushTarget();
                break;
            case FogwoodAttackType.RootSkewer:
                DoBehavior_RootSkewer();
                break;
        }

        // Search for targets.
        NPC.TargetClosest();

        AITimer++;
    }

    public void DoBehavior_CreepTowardsTarget()
    {
        int teleportDelay = 120;

        // Apply gravity manually.
        NPC.velocity.Y += 0.4f;

        // Update the whistle sound.
        whistleLoopSound ??= LoopedSoundManager.CreateNew(GennedAssets.Sounds.Enemies.FogwoodWhistle with { Volume = 0.1f }, () => !NPC.active || CurrentState != FogwoodAttackType.CreepTowardsTarget);
        whistleLoopSound?.Update(WhistleSourcePosition);
        WhistleSourcePosition = Vector2.Lerp(WhistleSourcePosition, NPC.Top, 0.15f);

        // Animate and scream if the player gets close.
        if (NPC.WithinRange(Target.Center, 160f))
        {
            AITimer = 0f;
            CurrentState = FogwoodAttackType.ScreamViolently;
            NPC.netUpdate = true;
            return;
        }

        // Wait a bit, and then teleport closer to the player.
        if (AITimer >= teleportDelay)
        {
            float distanceToTarget = NPC.Distance(Target.Center);
            float minTeleportDistance = 360f;

            // Wait until later if the target is already quite close.
            if (distanceToTarget <= minTeleportDistance)
            {
                AITimer = 0f;
                NPC.netUpdate = true;
                return;
            }

            // Perform the teleport.
            for (int i = 0; i < 8000; i++)
            {
                Vector2 potentialTeleportPosition = Target.Center + new Vector2(Main.rand.NextFloatDirection() * 700f, Main.rand.NextFloat(-100f, 300f));
                potentialTeleportPosition.X = Round(potentialTeleportPosition.X / 8f) * 8f;

                // Try again if the teleport position if too far/close.
                if (potentialTeleportPosition.Distance(Target.Center) >= distanceToTarget - 90f || potentialTeleportPosition.Distance(Target.Center) < minTeleportDistance)
                    continue;

                // Try again if the teleport position cannot reach the target.
                Vector2 topLeft = potentialTeleportPosition + NPC.Size * new Vector2(-0.5f, -1f);
                if (!Collision.CanHitLine(topLeft, NPC.width, NPC.height, Target.TopLeft, Target.width, Target.height))
                    continue;

                // Try again if the teleport position has no dirt/grass ground.
                bool validGround = true;
                for (int dx = -2; dx < 2; dx++)
                {
                    Tile t = Framing.GetTileSafely((int)(potentialTeleportPosition.X / 16f + dx), (int)(potentialTeleportPosition.Y / 16f) + 1);
                    bool dirtOrGrass = t.TileType == TileID.Dirt || t.TileType == TileID.Grass;
                    if (!dirtOrGrass || !WorldGen.SolidTile(t) || t.Slope != SlopeType.Solid || t.IsHalfBlock)
                    {
                        validGround = false;
                        break;
                    }
                }
                if (!validGround)
                    continue;

                // Try again if stuck inside of ground.
                Tile teleportTile = Framing.GetTileSafely((int)(potentialTeleportPosition.X / 16f), (int)(potentialTeleportPosition.Y / 16f));
                if (WorldGen.SolidTile(teleportTile))
                    continue;

                // Try again if there's a real tree (or other misc tiles) in the area.
                bool treeNearby = false;
                for (int dx = -4; dx < 4; dx++)
                {
                    for (int dy = -3; dy <= 2; dy++)
                    {
                        Tile t = Framing.GetTileSafely((int)(potentialTeleportPosition.X / 16f + dx), (int)(potentialTeleportPosition.Y / 16f + dy));
                        if (t.TileType is TileID.Trees or TileID.VanityTreeSakura or TileID.VanityTreeYellowWillow or TileID.Pumpkins or TileID.LargePiles2 or TileID.Sunflower)
                        {
                            treeNearby = true;
                            break;
                        }
                    }
                }
                if (treeNearby)
                    continue;

                // Perform the teleport.
                AITimer = 0f;
                NPC.Bottom = potentialTeleportPosition + Vector2.UnitY * 14f;
                NPC.netUpdate = true;
            }
        }
    }

    public void DoBehavior_ScreamViolently()
    {
        int screamTime = 90;

        // Apply gravity manually.
        NPC.velocity.Y += 0.4f;

        // Disable contact damage.
        NPC.damage = 0;

        if (AITimer == 1f)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Enemies.FogwoodScream with { Volume = 0.5f }, NPC.Center);
            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 13f);
            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Top, 1f, 30);
            GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Top, 2.2f, 90);

            ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(NPC.Top, Vector2.Zero, Color.White, 20, 0.1f);
            burst.Spawn();
        }

        // Make the tree top head snap.
        TreeTopOffsetAngle = Lerp(TreeTopOffsetAngle, NPC.spriteDirection * 0.4f, 0.29f);
        BranchMaxOffsetAngle = 0.5f;

        // Use the evil form.
        EvilInterpolant = Saturate(EvilInterpolant + 0.05f);

        // Allow name visibility.
        NPC.ShowNameOnHover = true;

        // Begin attacking the player.
        if (AITimer >= screamTime)
        {
            BranchMaxOffsetAngle = 0f;
            AITimer = 0f;
            CurrentState = FogwoodAttackType.CrushTarget;
            NPC.netUpdate = true;
        }
    }

    public void DoBehavior_CrushTarget()
    {
        int energyChargeupTime = 30;
        int jumpDelay = 20;
        int jumpTime = 90;
        int slamCount = 2;
        float gravity = 0.6f;
        ref float hasHitGround = ref NPC.ai[2];
        ref float slamCounter = ref NPC.ai[3];

        // Allow damage.
        NPC.dontTakeDamage = false;

        // Make visual things return to normal.
        BranchMaxOffsetAngle *= 0.9f;
        TreeTopOffsetAngle *= 0.87f;

        // Charge up energy at the roots.
        if (AITimer < energyChargeupTime)
        {
            for (int i = 0; i < 2; i++)
            {
                Dust energy = Dust.NewDustPerfect(NPC.Bottom - Vector2.UnitY * 30f + Main.rand.NextVector2Circular(90f, 25f), 264);
                energy.color = Color.Lerp(Color.Magenta, Color.Violet, Main.rand.NextFloat(0.8f));
                energy.noLight = true;
                energy.noGravity = true;
                energy.velocity = (NPC.Bottom - energy.position) * 0.08f + Main.rand.NextVector2Circular(1f, 1f);
            }
        }

        // Perform jump behaviors.
        if (AITimer == energyChargeupTime + jumpDelay)
        {
            StrongBloom bloom = new StrongBloom(NPC.Bottom, Vector2.Zero, Color.DarkViolet * 0.7f, 1.6f, 45);
            bloom.Spawn();
            PulseRing ring = new PulseRing(NPC.Bottom, Vector2.Zero, Color.BlueViolet, 0f, 3f, 20);
            ring.Spawn();

            NPC.velocity.X = NPC.SafeDirectionTo(Target.Center).X * (NPC.Distance(Target.Center) * 0.008f + 15f);
            NPC.velocity.Y -= Distance(Target.Center.Y, NPC.Center.Y) * 0.02f + 30f;
            NPC.netUpdate = true;
        }
        if (AITimer >= energyChargeupTime + jumpDelay)
        {
            NPC.noTileCollide = AITimer < energyChargeupTime + jumpDelay + 3f;
            if (Distance(NPC.Center.X, Target.Center.X) <= 200f && NPC.Bottom.Y < Target.Top.Y)
            {
                NPC.velocity.X *= 0.94f;
                gravity += 0.27f;
            }
            if (Distance(NPC.Center.X, Target.Center.X) <= 40f && NPC.Bottom.Y < Target.Top.Y)
            {
                NPC.velocity.X *= 0.81f;
                gravity += 0.6f;
            }
        }

        // Go to the next attack when ready/
        if (AITimer >= energyChargeupTime + jumpDelay + jumpTime)
        {
            slamCounter++;
            if (slamCounter >= slamCount)
            {
                CurrentState = FogwoodAttackType.RootSkewer;
                slamCounter = 0f;
            }

            hasHitGround = 0f;
            AITimer = 0f;
            NPC.netUpdate = true;
        }

        // Apply friction on the ground.
        if (NPC.velocity.Y == 0f)
        {
            if (AITimer >= energyChargeupTime + jumpDelay && hasHitGround == 0f)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.NPCKilled.FogwoodDeath with { Pitch = -0.25f }, NPC.Bottom);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Bottom, 12f, shakeStrengthDissipationIncrement: 0.45f);
                Collision.HitTiles(NPC.TopLeft, NPC.velocity, NPC.width, NPC.height + 64);
                hasHitGround = 1f;
            }

            NPC.velocity.X *= 0.5f;
        }

        // Apply gravity manually.
        NPC.velocity.Y += gravity;

        // Rotate.
        NPC.rotation = NPC.velocity.X * -0.02f;
    }

    public void DoBehavior_RootSkewer()
    {
        // Slow down and fall.
        NPC.velocity.X *= 0.8f;
        NPC.velocity.Y += 0.4f;

        // Create roots.
        if (AITimer == 1f)
        {
            SoundEngine.PlaySound(SoundID.DD2_SkyDragonsFuryShot with { Pitch = 0.5f }, NPC.Center);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (float dx = -800f; dx < 800f; dx += 100f)
                {
                    Vector2 rootSpawnPosition = Target.Center + new Vector2(dx, -10f);
                    if (WorldGen.SolidTile(rootSpawnPosition.ToTileCoordinates()))
                        continue;

                    if (WorldUtils.Find(rootSpawnPosition.ToTileCoordinates(), Searches.Chain(new Searches.Down(50), new WorldConditions.IsSolid()), out Point p))
                        rootSpawnPosition = p.ToWorldCoordinates(8f, 16f);

                    NewProjectileBetter(NPC.GetSource_FromAI(), rootSpawnPosition, Vector2.Zero, ModContent.ProjectileType<FogwoodRoot>(), 25, 0f);
                }
            }
        }

        if (AITimer == FogwoodRoot.ExtendDelay)
        {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = 0.5f, Volume = 1.8f }, NPC.Center);
            ScreenShakeSystem.StartShake(7f);
        }

        if (AITimer >= FogwoodRoot.Lifetime)
        {
            CurrentState = FogwoodAttackType.CrushTarget;
            AITimer = 0f;
            NPC.netUpdate = true;
        }
    }

    #endregion AI

    #region Hit Effects

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (NPC.life <= 0)
            WorldGen.TreeGrowFX((int)NPC.TopLeft.X / 16, (int)(int)NPC.TopLeft.Y / 16, NPC.height / 16, GoreID.TreeLeaf_Corruption);
    }

    #endregion Hit Effects

    #region Loot

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        // Drop a bunch of spooky wood.
        npcLoot.Add(new DropOneByOne(ItemID.SpookyWood, new DropOneByOne.Parameters()
        {
            MinimumItemDropsCount = 80,
            MaximumItemDropsCount = 100,
            ChanceNumerator = 1,
            ChanceDenominator = 1,
            MinimumStackPerChunkBase = 2,
            MaximumStackPerChunkBase = 9,
        }));
    }

    #endregion Loot

    #region Drawing

    public override void FindFrame(int frameHeight)
    {
        int frameY = NPC.whoAmI * 1743 % Main.npcFrameCount[Type];
        NPC.frame.Y = frameY * frameHeight;
    }

    public override void DrawBehind(int index)
    {
        Main.instance.DrawCacheNPCsBehindNonSolidTiles.Add(index);
    }

    private static int GetTreeStyle(float x)
    {
        if (UseSingleVariant)
            return 0;

        int treeStyle;
        if (x <= Main.treeX[0])
            treeStyle = WorldGen.TreeTops.GetTreeStyle(0);
        else if (x <= Main.treeX[1])
            treeStyle = WorldGen.TreeTops.GetTreeStyle(1);
        else if (x <= Main.treeX[2])
            treeStyle = WorldGen.TreeTops.GetTreeStyle(2);
        else
            treeStyle = WorldGen.TreeTops.GetTreeStyle(3);

        return treeStyle;
    }

    public void DrawSelf(float opacity, Vector2 screenPos, Texture2D trunkTexture, Texture2D bodyTexture, Texture2D topTexture, Texture2D branchTexture)
    {
        if (NPC.IsABestiaryIconDummy)
        {
            NPC.scale = 0.6f;
            NPC.height = 40;
        }

        // Collect generic draw information.
        float windSwayRotation = (Sin01(Main.GlobalTimeWrappedHourly * 0.2f) * 0.18f - 0.06f) * Main.windSpeedCurrent * 2f;
        Vector2 drawPosition = NPC.Bottom.Floor() - screenPos + new Vector2(NPC.spriteDirection * 6f, -20f) * NPC.scale;
        Vector2 right = Vector2.UnitX.RotatedBy(NPC.rotation) * NPC.scale;
        Vector2 down = Vector2.UnitY.RotatedBy(NPC.rotation) * NPC.scale;
        SpriteEffects direction = NPC.spriteDirection.ToSpriteDirection();
        if (NPC.IsABestiaryIconDummy)
            drawPosition.Y += 30f;

        // Draw the trunk.
        Rectangle trunkFrame = NPC.frame;
        Color trunkColor = NPC.IsABestiaryIconDummy ? Color.White : Lighting.GetColor((int)(drawPosition.X + Main.screenPosition.X) / 16, (int)(drawPosition.Y + Main.screenPosition.Y) / 16);
        Main.spriteBatch.Draw(trunkTexture, drawPosition, trunkFrame, NPC.GetAlpha(trunkColor * opacity), NPC.rotation, trunkFrame.Size() * new Vector2(0.5f, 0f), NPC.scale, direction, 0f);

        // Move the draw position up for successive draw calls.
        drawPosition += right * 7f;
        drawPosition -= down * (trunkFrame.Height - 10f);

        // Draw the body in segments, using this NPC's whoAmI as a seed for an RNG.
        float distanceMoved = 0f;
        UnifiedRandom rng = new UnifiedRandom(NPC.whoAmI * 398);
        while (distanceMoved < NPC.height)
        {
            // Decide information for the given segment.
            bool drawBranch = distanceMoved >= 50f && distanceMoved < NPC.height - 54f && rng.NextBool(2);
            Rectangle bodyFrame = bodyTexture.Frame(2, 5, drawBranch.ToInt(), rng.Next(5));

            // Draw the body.
            Color bodyColor = NPC.IsABestiaryIconDummy ? Color.White : Lighting.GetColor((int)(drawPosition.X + Main.screenPosition.X) / 16, (int)(drawPosition.Y + Main.screenPosition.Y) / 16);
            Main.spriteBatch.Draw(bodyTexture, drawPosition, bodyFrame, NPC.GetAlpha(bodyColor * opacity), NPC.rotation, bodyFrame.Size() * new Vector2(0.5f, 0f), NPC.scale, direction, 0f);

            // Draw branches if necessary.
            if (drawBranch)
            {
                float branchRotation = NPC.rotation + windSwayRotation + Sin01(AITimer / 3f + distanceMoved) * BranchMaxOffsetAngle;
                Rectangle branchFrame = branchTexture.Frame(2, 3, 1, rng.Next(3));
                Vector2 branchOrigin = branchFrame.Size() * new Vector2((NPC.spriteDirection == -1f).ToDirectionInt(), 0.5f);
                Main.spriteBatch.Draw(branchTexture, drawPosition + down * 7f + right * NPC.spriteDirection * 5f, branchFrame, NPC.GetAlpha(bodyColor * opacity), branchRotation, branchOrigin, NPC.scale, direction, 0f);
            }

            // Increment the moved height and move up for the next loop iteration.
            distanceMoved += (bodyFrame.Height - 2f) * NPC.scale;
            drawPosition -= down * (bodyFrame.Height - 2f);

            if (distanceMoved < 0f)
                break;
        }

        // Draw the tree top.
        drawPosition += down * 24f;
        Color topColor = NPC.IsABestiaryIconDummy ? Color.White : Lighting.GetColor((int)(drawPosition.X + Main.screenPosition.X) / 16, (int)(drawPosition.Y + Main.screenPosition.Y) / 16);
        Rectangle topFrame = topTexture.Frame(3, 1, 0, 0);
        Main.spriteBatch.Draw(topTexture, drawPosition, topFrame, NPC.GetAlpha(topColor * opacity), NPC.rotation + windSwayRotation + TreeTopOffsetAngle, topFrame.Size() * new Vector2(0.5f, 1f), NPC.scale, direction, 0f);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        int treeStyle = GetTreeStyle(NPC.Center.X / 16f);
        Texture2D trunkTexture = MyTexture.Value;
        Texture2D bodyTexture = BodyTexture.Value;
        Texture2D topTexture = TopsTextures[treeStyle].Value;
        Texture2D branchTexture = BranchTextures[treeStyle].Value;
        DrawSelf(1f - EvilInterpolant, screenPos, trunkTexture, bodyTexture, topTexture, branchTexture);

        Texture2D trunkTextureEvil = MyTextureEvil.Value;
        Texture2D bodyTextureEvil = BodyTextureEvil.Value;
        Texture2D topTextureEvil = TopsTexturesEvil[treeStyle].Value;
        Texture2D branchTextureEvil = BranchTexturesEvil[treeStyle].Value;
        DrawSelf(EvilInterpolant, screenPos, trunkTextureEvil, bodyTextureEvil, topTextureEvil, branchTextureEvil);

        return false;
    }
    #endregion Drawing
}
