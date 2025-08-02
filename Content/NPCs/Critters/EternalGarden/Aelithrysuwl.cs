using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.Biomes;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.Automators;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Utilities;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Critters.EternalGarden;

public class Aelithrysuwl : ModNPC, IDrawsWithShader
{
    public enum AelithrysuwlAIType
    {
        // Flying towards the Good Apple tree.
        FlyToTree,
        PerchToTree,

        // For when the Aelithrysuwl is on top of the Good Apple tree.
        LookAround,
        SitInTree,
        VibeInPlace,
        CockHeadToSide,
        Sing,

        FlyToGround,
        SitOnGround,

        // For when Nameless appears.
        RespectfullyFlyAway
    }

    #region Fields and Properties

    public int HootTime
    {
        get;
        set;
    }

    public int SingExhaustionCountdown
    {
        get;
        set;
    }

    public AelithrysuwlAIType PreviousSitState
    {
        get;
        set;
    }

    public Vector2 GroundPosition
    {
        get;
        set;
    }

    public Vector2 PerchPosition
    {
        get => new(NPC.ai[2], NPC.ai[3]);
        set
        {
            NPC.ai[2] = value.X;
            NPC.ai[3] = value.Y;
        }
    }

    public Vector2 Scale
    {
        get
        {
            float convertedStretchInterpolant = Convert01To010(StretchInterpolant);
            Vector2 scale = Vector2.One * NPC.scale;
            scale.X *= 1f - convertedStretchInterpolant * StretchFactor * 0.11f;
            scale.Y *= 1f + convertedStretchInterpolant * StretchFactor * 0.2f;

            return scale;
        }
    }

    public AelithrysuwlAIType CurrentState
    {
        get => (AelithrysuwlAIType)NPC.ai[0];
        set => NPC.ai[0] = (int)value;
    }

    public List<NPC> Others => Main.npc.Where(n => n.active && n.type == NPC.type && n.whoAmI != NPC.whoAmI).ToList();

    public List<NPC> NearbySingingAelithrysuwls => Others.Where(n => n.As<Aelithrysuwl>().CurrentState == AelithrysuwlAIType.Sing && n.WithinRange(NPC.Center, 450f)).ToList();

    public ref float AITimer => ref NPC.ai[1];

    public ref float FrameY => ref NPC.localAI[0];

    public ref float StretchInterpolant => ref NPC.localAI[1];

    public ref float WingFlapTimer => ref NPC.localAI[2];

    public ref float StretchFactor => ref NPC.localAI[3];

    public static int SingBeatTiming => 60;

    public static int WorldCenterSpawnProximityRequirement => 150;

    public static Vector2[] PotentialPerchOffsets => new Vector2[]
    {
        new(-92f, -502f),
        new(-30f, -558f),
        new(12f, -566f),
        new(46f, -562f),
        new(70f, -552f),
        new(102f, -538f),
    };

    public static readonly Color VioletEyeColor = new Color(109, 60, 242);

    public static readonly Color TurquoiseEyeColor = new Color(8, 142, 174);

    public override string Texture => GetAssetPath("Content/NPCs/Critters/EternalGarden", Name);

    #endregion Fields and Properties

    #region Initialization

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 13;

        // Ensure this NPC is registered as a critter.
        NPCID.Sets.CountsAsCritter[Type] = true;

        EmptinessSprayer.NPCsToNotDelete[Type] = true;

        // Garden-specific bestiary critters should be at the highest priority.
        NPCID.Sets.NormalGoldCritterBestiaryPriority.Add(Type);
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 1f;
        NPC.width = 18;
        NPC.height = 34;
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.lifeMax = 1;
        NPC.aiStyle = -1;
        AIType = -1;
        NPC.knockBackResist = 0f;
        NPC.noGravity = true;
        NPC.noTileCollide = false;
        NPC.dontTakeDamage = true;
        NPC.hide = true;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.friendly = true;
        StretchFactor = 1f;

        SpawnModBiomes =
        [
            ModContent.GetInstance<EternalGardenBiome>().Type
        ];

        if (Main.netMode != NetmodeID.Server)
            NPCNameFontSystem.RegisterFontForNPCID(Type, DisplayName.Value, FontRegistry.Instance.DivineLanguageTextText);
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.UIInfoProvider = new CritterUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[Type]);
        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}")
        });
    }

    #endregion Initialization

    #region Syncing

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((int)PreviousSitState);
        writer.Write(HootTime);
        writer.WriteVector2(GroundPosition);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        PreviousSitState = (AelithrysuwlAIType)reader.ReadInt32();
        HootTime = reader.ReadInt32();
        GroundPosition = reader.ReadVector2();
    }

    #endregion Syncing

    #region AI
    public override void AI()
    {
        // Reset things every frame.
        NPC.noGravity = false;
        NPC.gfxOffY = 0f;

        if (SingExhaustionCountdown > 0)
            SingExhaustionCountdown--;

        // Fly away if Nameless is present or the end credits are ongoing.
        bool shouldFlyAway = ModContent.GetInstance<EndCreditsScene>().IsActive || NamelessDeityBoss.Myself is not null;
        if (shouldFlyAway && CurrentState != AelithrysuwlAIType.RespectfullyFlyAway)
        {
            SelectNextState();
            NPC.velocity.Y = -6f;
            CurrentState = AelithrysuwlAIType.RespectfullyFlyAway;
        }

        switch (CurrentState)
        {
            case AelithrysuwlAIType.FlyToTree:
                DoBehavior_FlyToTree();
                break;
            case AelithrysuwlAIType.PerchToTree:
                DoBehavior_PerchToTree();
                break;
            case AelithrysuwlAIType.SitInTree:
                DoBehavior_SitInTree();
                break;
            case AelithrysuwlAIType.LookAround:
                DoBehavior_LookAround();
                break;
            case AelithrysuwlAIType.VibeInPlace:
                DoBehavior_VibeInPlace();
                break;
            case AelithrysuwlAIType.CockHeadToSide:
                DoBehavior_CockHeadToSide();
                break;
            case AelithrysuwlAIType.Sing:
                DoBehavior_Sing();
                break;
            case AelithrysuwlAIType.FlyToGround:
                DoBehavior_FlyToGround();
                break;
            case AelithrysuwlAIType.SitOnGround:
                DoBehavior_SitOnGround();
                break;
            case AelithrysuwlAIType.RespectfullyFlyAway:
                DoBehavior_RespectfullyFlyAway();
                break;
        }

        NPC.noGravity = PreviousSitState == AelithrysuwlAIType.SitInTree || FrameY >= 8f;
        if (!NPC.noGravity && FrameY <= 7f)
            NPC.gfxOffY = 6f;

        // Fly back into the tree if on the ground and a player is a uncomfortably close.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        if (PreviousSitState == AelithrysuwlAIType.SitOnGround && NPC.WithinRange(closestPlayer.Center, 80f) && CurrentState != AelithrysuwlAIType.SitOnGround)
        {
            SelectNextState();
            NPC.velocity.Y = -5.4f;
            CurrentState = AelithrysuwlAIType.FlyToTree;
        }

        AITimer++;
    }

    public void DoBehavior_FlyToTree()
    {
        // Fly towards the tree.
        Vector2 treeDestination = new Vector2(Main.maxTilesX * 8f, Main.maxTilesY * 16f - 1600f);
        Vector2 idealVelocity = NPC.SafeDirectionTo(treeDestination) * 5f;
        NPC.SimpleFlyMovement(idealVelocity, 0.2f);

        // Fly up and down when there's still a distance amount of distance to the tree.
        float distanceToTree = Distance(NPC.Center.X, treeDestination.X);
        float verticalMovementInterpolant = InverseLerp(240f, 480f, distanceToTree);
        NPC.velocity.Y += Cos(AITimer / 21.5f) * verticalMovementInterpolant * 0.14f;

        // Update the current direction.
        NPC.spriteDirection = NPC.velocity.X.NonZeroSign();

        // Avoid horizontal obstacles.
        if (!Collision.CanHit(NPC.TopLeft, NPC.width, NPC.height, NPC.TopLeft + Vector2.UnitX * NPC.spriteDirection * 176f, NPC.width, NPC.height))
            NPC.velocity.Y -= 0.95f;

        // Stay above the ground.
        if (!Collision.CanHit(NPC.TopLeft, NPC.width, NPC.height, NPC.TopLeft + Vector2.UnitY * 400f, NPC.width, NPC.height))
            NPC.velocity.Y -= 0.3f;

        // Flap wings.
        WingFlapTimer++;
        float wingFlapInterpolant = WingFlapTimer / 28f % 1f;
        FrameY = Round(Lerp(8f, 12f, wingFlapInterpolant));

        // Find a locaation to perch when close to the tree.
        if (verticalMovementInterpolant <= 0f)
            SelectNextState();
    }

    public void DoBehavior_PerchToTree()
    {
        // Pick a position to perch on the first frame and again periodically afterwards, taking care to avoid others.
        // This also happens if incredibly close to the perch position, to ensure that this Aelithyruswl doesn't sit on top of another one that reached the same perch recently.
        bool closeToPerching = NPC.WithinRange(PerchPosition, 150f);
        if (AITimer % 120f == 1f || closeToPerching)
        {
            // Find all other Aelithyruswls.
            List<NPC> others = Others;

            int checkIndexOffset = closeToPerching ? NPC.whoAmI * 11 : Main.rand.Next(PotentialPerchOffsets.Length);
            Vector2 treePosition = new Vector2(Main.maxTilesX * 8f, Main.maxTilesY * 16f - 1600f);
            for (int i = 0; i < PotentialPerchOffsets.Length; i++)
            {
                // If another Aelithyruswl is within 20 pixels of this perch position, ignore it.
                Vector2 perchOffset = PotentialPerchOffsets[(i + checkIndexOffset) % PotentialPerchOffsets.Length];
                if (others.Any(n => n.WithinRange(treePosition + perchOffset, 20f)))
                    continue;

                // Otherwise, use this perch position.
                PerchPosition = treePosition + perchOffset;
                break;
            }

            NPC.netUpdate = true;
        }

        // Fly towards the perch position.
        Vector2 idealVelocity = NPC.SafeDirectionTo(PerchPosition) * 4f;
        NPC.SimpleFlyMovement(idealVelocity, 0.2f);

        // Update the current direction.
        NPC.spriteDirection = NPC.velocity.X.NonZeroSign();

        // Flap wings.
        WingFlapTimer++;
        float wingFlapInterpolant = WingFlapTimer / 34f % 1f;
        FrameY = Round(Lerp(8f, 12f, wingFlapInterpolant));

        // If incredibly close to the perch position, stop and snap there.
        if (NPC.WithinRange(PerchPosition, 8f) && Distance(NPC.Center.X, PerchPosition.X) <= 6f)
        {
            SelectNextState();
            NPC.Center = PerchPosition;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    public void DoBehavior_SitInTree()
    {
        // Sit in place.
        NPC.velocity = Vector2.Zero;

        // Reset the wing flap timer.
        WingFlapTimer = 0f;

        // Randomly choose another state.
        if (AITimer >= 60f && Main.rand.NextBool(60) && StretchInterpolant <= 0.2f)
        {
            SelectNextState();
            CurrentState = Main.rand.NextBool() ? AelithrysuwlAIType.VibeInPlace : AelithrysuwlAIType.LookAround;
        }

        // Look around or cock the head to the side if the player is nearby.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        if (closestPlayer.WithinRange(NPC.Center, 450f))
        {
            SelectNextState();
            CurrentState = Main.rand.NextBool(3) || Distance(closestPlayer.Center.X, NPC.Center.X) <= 60f ? AelithrysuwlAIType.CockHeadToSide : AelithrysuwlAIType.LookAround;
        }

        // Ensure the stretch interpolant completes smoothly.
        NPC.rotation = NPC.rotation.AngleTowards(0f, 0.04f);
        if (StretchInterpolant > 0f)
        {
            StretchInterpolant += 0.04f;
            if (StretchInterpolant >= 1f)
                StretchInterpolant = 0f;
        }

        // If not in the middle of returning the stretch interpolant to normal, check if this Aelithrysuwl should sing.
        // This becomes increasingly likely if others nearby are currently singing.
        else if (SingExhaustionCountdown <= 0)
        {
            int totalNearbySingingAelithrysuwls = NearbySingingAelithrysuwls.Count;
            float singChance = Pow(4f, totalNearbySingingAelithrysuwls) * 0.0016f;
            if (singChance > 0.1f)
                singChance = 0.1f;
            if (Main.rand.NextFloat() <= singChance)
            {
                SelectNextState();
                CurrentState = AelithrysuwlAIType.Sing;
                if (totalNearbySingingAelithrysuwls >= 1)
                    AITimer = (int)NearbySingingAelithrysuwls.Average(n => n.As<Aelithrysuwl>().AITimer) % SingBeatTiming;
            }
        }

        // Randomly leave the tree to sit on the ground.
        if (AITimer >= 100f && Main.rand.NextBool(150))
        {
            SelectNextState();
            CurrentState = AelithrysuwlAIType.FlyToGround;
        }

        // Decide frames.
        FrameY = 0f;
    }

    public void DoBehavior_LookAround()
    {
        // Sit in place.
        NPC.velocity = Vector2.Zero;

        // Look around.
        bool lookLeft = Main.LocalPlayer.Center.X < NPC.Center.X;
        float lookAroundInterpolant = InverseLerpBump(0f, 14f, 60f, 75f, AITimer);
        FrameY = Round(Lerp(lookLeft ? 3f : 1f, lookLeft ? 4f : 2f, lookAroundInterpolant));

        // Use the default direction so that the above things work.
        NPC.spriteDirection = 1;

        // Decide frames.
        if (AITimer >= 75f)
        {
            SelectNextState();

            // Cock the head to the side if the player is nearby.
            if (Main.LocalPlayer.WithinRange(NPC.Center, 232f))
            {
                SelectNextState();
                CurrentState = AelithrysuwlAIType.CockHeadToSide;
            }
        }
    }

    public void DoBehavior_VibeInPlace()
    {
        // Sit in place.
        NPC.velocity = Vector2.Zero;

        // Vibe.
        int vibeRate = 25;
        StretchFactor = 0.5f;
        StretchInterpolant = Sin01(TwoPi * AITimer / vibeRate);
        NPC.rotation = StretchInterpolant * 0.14f - 0.07f;

        if (AITimer >= vibeRate * 4)
            SelectNextState();
    }

    public void DoBehavior_CockHeadToSide()
    {
        // Sit in place.
        NPC.velocity = Vector2.Zero;

        // Cock the head to the side.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        NPC.spriteDirection = -(closestPlayer.Center.X - NPC.Center.X).NonZeroSign();
        FrameY = 5f;

        if (AITimer >= 38f)
        {
            if (Distance(closestPlayer.Center.X, NPC.Center.X) <= 60f)
            {
                AITimer = 0f;
                NPC.netUpdate = true;
            }
            else
                SelectNextState();
        }
    }

    public void DoBehavior_Sing()
    {
        int hootRate = 60;

        // Sit in place.
        NPC.velocity = Vector2.Zero;

        // Attempt to loosely use the same attack timer as other Aelithrysuwls so that the sing timings match up loosely enough.
        float oldAITime = AITimer;
        int timingOffset = (NPC.whoAmI + 156) * 251 % SingBeatTiming;

        // Hoot every so often.
        if (AITimer % hootRate == 1 && HootTime <= 0)
        {
            HootTime = 1;
            NPC.netUpdate = true;
        }

        // Apply hoot effects.
        if (HootTime >= 1)
        {
            // Vibe in place.
            float hootCompletion = InverseLerp(-16f, -1f, HootTime - hootRate);
            StretchFactor = 1.15f;
            StretchInterpolant = hootCompletion;
            NPC.rotation = (StretchInterpolant * 0.28f - 0.14f) * InverseLerpBump(0f, 0.15f, 0.8f, 1f, StretchInterpolant);

            // Play the hoot sound and create a cute little note particle.
            if (HootTime == hootRate - 16)
            {
                // Calculate the pitch of the hoot relative to this specific Aelithrysuwl.
                // Higher pitches receive more vibrant, redshifted colors while lower pitches receive more dark blues and violets.
                float pitchInterpolant = timingOffset / 60f;
                float pitch = Lerp(-0.07f, 0.3f, pitchInterpolant);
                Color noteColor = Color.Lerp(Color.Violet, Color.Red, pitchInterpolant * 0.93f);
                SoundEngine.PlaySound(GennedAssets.Sounds.GardenCritters.AelithrysuwlHoot with { Pitch = pitch, PitchVariance = 0.05f }, NPC.Center);

                Vector2 noteSpawnPosition = NPC.Center - Vector2.UnitY.RotatedBy(NPC.rotation) * Scale * 20f + Main.rand.NextVector2Circular(5f, 5f);
                Vector2 noteVelocity = -Vector2.UnitY.RotatedByRandom(0.51f) * Main.rand.NextFloat(4f, 11f);
                CartoonMusicNoteParticle note = new CartoonMusicNoteParticle(noteSpawnPosition, noteVelocity, noteColor, Color.White, 60, 0f, 0.2f, Main.rand.NextBool());
                note.Spawn();
            }

            HootTime++;
            if (HootTime >= hootRate)
            {
                HootTime = 0;
                NPC.netUpdate = true;
            }
        }
        else
            NPC.rotation = NPC.rotation.AngleTowards(0f, 0.04f);

        // Randomly go back to sitting once the singing has gone on for long enough, as though this Aelithrysuwl has gotten tired.
        if (AITimer >= 420f && Main.rand.NextBool(20) && (StretchInterpolant <= 0 || StretchInterpolant >= 1f))
        {
            SingExhaustionCountdown = 600;
            SelectNextState();
        }
    }

    public void DoBehavior_FlyToGround()
    {
        // Choose a place to go to on the ground on the first frame.
        if (AITimer == 1f)
        {
            GroundPosition = new Vector2(Main.maxTilesX * 8f + Main.rand.NextFloat(150f, 900f) * Main.rand.NextFromList(-1f, 1f), Main.maxTilesY * 16f - 16f);
            while (WorldGen.SolidTile(GroundPosition.ToTileCoordinates()))
                GroundPosition -= Vector2.UnitY * 16f;
            NPC.netUpdate = true;
        }

        // Fly towards the ground position.
        Vector2 positionToFlyTo = GroundPosition - Vector2.UnitY * Utils.Remap(NPC.Distance(GroundPosition), 360f, 75f, 275f, 0f);
        Vector2 idealVelocity = NPC.SafeDirectionTo(positionToFlyTo) * 3.6f;
        NPC.SimpleFlyMovement(idealVelocity, 0.15f);

        // Update the current direction.
        NPC.spriteDirection = NPC.velocity.X.NonZeroSign();

        // Flap wings.
        WingFlapTimer++;
        float wingFlapInterpolant = WingFlapTimer / 34f % 1f;
        FrameY = Round(Lerp(8f, 12f, wingFlapInterpolant));

        if (NPC.WithinRange(GroundPosition, 20f))
            SelectNextState();
    }

    public void DoBehavior_SitOnGround()
    {
        // Sit on the ground, adhering to gravity.
        NPC.velocity.X *= 0.6f;
        FrameY = 0f;

        // Randomly choose another state.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        if (AITimer >= 60f && Main.rand.NextBool(60) && StretchInterpolant <= 0.2f)
        {
            SelectNextState();
            CurrentState = Main.rand.NextBool() || !closestPlayer.WithinRange(NPC.Center, 450f) ? AelithrysuwlAIType.VibeInPlace : AelithrysuwlAIType.LookAround;
        }

        // Look at the around or cock the head to the side if the player is nearby.
        if (closestPlayer.WithinRange(NPC.Center, 270f))
        {
            SelectNextState();
            CurrentState = Main.rand.NextBool(3) ? AelithrysuwlAIType.CockHeadToSide : AelithrysuwlAIType.LookAround;
        }

        // Randomly leave the ground to return to the tree. This can also happen if a player gets too close.
        if ((AITimer >= 300f && Main.rand.NextBool(360)) || NPC.WithinRange(closestPlayer.Center, 75f))
        {
            SelectNextState();
            NPC.velocity.Y = -6f;
            CurrentState = AelithrysuwlAIType.FlyToTree;
        }
    }

    public void DoBehavior_RespectfullyFlyAway()
    {
        // Fly away from the closest player and vanish after enough time has passed.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        NPC.velocity.X = Lerp(NPC.velocity.X, NPC.SafeDirectionTo(closestPlayer.Center).X * -15f, 0.15f);
        NPC.velocity.Y = Clamp(NPC.velocity.Y - 0.4f, -39f, 1f);

        // Update the current direction.
        NPC.spriteDirection = NPC.velocity.X.NonZeroSign();

        // Flap wings.
        WingFlapTimer++;
        float wingFlapInterpolant = WingFlapTimer / 28f % 1f;
        FrameY = Round(Lerp(8f, 12f, wingFlapInterpolant));

        if (!NPC.WithinRange(closestPlayer.Center, 1400f))
            NPC.active = false;
    }

    public void SelectNextState()
    {
        switch (CurrentState)
        {
            case AelithrysuwlAIType.FlyToTree:
                CurrentState = AelithrysuwlAIType.PerchToTree;
                break;
            case AelithrysuwlAIType.PerchToTree:
                CurrentState = AelithrysuwlAIType.SitInTree;
                break;
            case AelithrysuwlAIType.FlyToGround:
                CurrentState = AelithrysuwlAIType.SitOnGround;
                break;

            case AelithrysuwlAIType.LookAround:
            case AelithrysuwlAIType.VibeInPlace:
            case AelithrysuwlAIType.CockHeadToSide:
            case AelithrysuwlAIType.Sing:
                CurrentState = PreviousSitState;
                break;
        }

        if (CurrentState is AelithrysuwlAIType.SitInTree or AelithrysuwlAIType.SitOnGround)
            PreviousSitState = CurrentState;
        AITimer = 0f;
        NPC.netUpdate = true;
    }

    #endregion AI

    #region Drawing

    public override void FindFrame(int frameHeight)
    {
        NPC.frame.Y = (int)FrameY * frameHeight;
    }

    public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Color.White, 0.7f) * NPC.Opacity;

    public override void DrawBehind(int index)
    {
        Main.instance.DrawCacheNPCsBehindNonSolidTiles.Add(index);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        // Draw a soft backglow.
        Vector2 drawOffset = Vector2.UnitY * NPC.gfxOffY;
        Vector2 drawPosition = NPC.Bottom - screenPos + drawOffset;
        Main.spriteBatch.Draw(BloomCircleSmall, NPC.Center - screenPos + drawOffset, null, Color.Violet with { A = 0 } * NPC.Opacity * 0.23f, 0f, BloomCircleSmall.Size() * 0.5f, NPC.scale * 0.5f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, NPC.Center - screenPos + drawOffset, null, Color.DeepSkyBlue with { A = 0 } * NPC.Opacity * 0.36f, 0f, BloomCircleSmall.Size() * 0.5f, NPC.scale * 0.4f, 0, 0f);

        // Perform standard drawing.
        Vector2 origin = NPC.frame.Size() * new Vector2(0.5f, 1f);
        Texture2D texture = TextureAssets.Npc[Type].Value;
        SpriteEffects direction = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        Main.spriteBatch.Draw(texture, drawPosition, NPC.frame, NPC.GetAlpha(drawColor), NPC.rotation, origin, Scale, direction, 0f);

        // Draw the eyes separately if this is a bestiary dummy.
        if (NPC.IsABestiaryIconDummy)
        {
            // Prepare for shader drawing.
            spriteBatch.PrepareForShaders(ui: true);

            DrawWithShaderWrapper(spriteBatch, screenPos);

            // Return to natural drawing.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        return false;
    }

    public void DrawWithShaderWrapper(SpriteBatch spriteBatch, Vector2 screenPos)
    {
        // Prepare the eye shader.
        Texture2D eyeTexture = GennedAssets.Textures.EternalGarden.AelithrysuwlEyes.Value;
        ManagedShader eyeShader = ShaderManager.GetShader("NoxusBoss.AelithrysuwlEyeShader");
        eyeShader.TrySetParameter("baseTextureSize", eyeTexture.Size());
        eyeShader.TrySetParameter("drawAreaRectangle", NPC.frame);
        eyeShader.TrySetParameter("eyeColor1", VioletEyeColor);
        eyeShader.TrySetParameter("eyeColor2", TurquoiseEyeColor);
        eyeShader.TrySetParameter("huePhaseShift", (float)NPC.whoAmI);
        eyeShader.Apply();

        // Draw the eyes with a special, all-seeing eyes shader.
        Vector2 origin = NPC.frame.Size() * new Vector2(0.5f, 1f);
        Vector2 drawOffset = Vector2.UnitY * NPC.gfxOffY;
        Vector2 drawPosition = NPC.Bottom - screenPos + drawOffset;
        SpriteEffects direction = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        spriteBatch.Draw(eyeTexture, drawPosition, NPC.frame, NPC.GetAlpha(Color.White), NPC.rotation, origin, Scale, direction, 0f);
    }

    public void DrawWithShader(SpriteBatch spriteBatch) => DrawWithShaderWrapper(spriteBatch, Main.screenPosition);
    #endregion Drawing

    #region Spawn Behavior

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (spawnInfo.Player.name.Equals("Lucille", StringComparison.OrdinalIgnoreCase))
            return 0f;

        float proximityToTree = Distance(spawnInfo.PlayerFloorX, Main.maxTilesX * 0.5f);
        bool closeEnoughToTree = proximityToTree <= WorldCenterSpawnProximityRequirement;

        if (spawnInfo.Player.InModBiome<EternalGardenBiome>() && closeEnoughToTree && NamelessDeityBoss.Myself is null && NPC.CountNPCS(Type) < 4)
            return 0.12f;

        return 0f;
    }

    #endregion Spawn Behaviors
}
