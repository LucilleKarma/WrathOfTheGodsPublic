using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.Biomes;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Core.DataStructures.ShapeCurves;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Critters.EternalGarden;

public class Vivajuyfylae : ModNPC, IBoid
{
    public enum VivajuyfylaeAIType
    {
        // Default behavior.
        FlyInSwarm,

        // Constellation formations.
        FormLargeStarConstellation,
        FormThumbsUpConstellation,

        // For when Nameless appears.
        RespectfullyFlyAway
    }

    #region Boid Implementation

    public int GroupID => Type;

    public float FlockmateDetectionRange => 350f;

    public Vector2 BoidCenter => NPC.Center;

    public Rectangle BoidArea => NPC.Hitbox;

    public ref Vector2 BoidVelocity => ref NPC.velocity;

    public List<BoidsManager.BoidForceApplicationRule> SimulationRules
    {
        get
        {
            List<BoidsManager.BoidForceApplicationRule> rules =
            [
                BoidsManager.CreateAlignmentRule(0.025f),
                BoidsManager.CreateCohesionRule(NPC.scale * 0.00125f),
                BoidsManager.CreateSeparationRule(250f, NPC.scale * 0.003f),
                BoidsManager.AvoidGroundRule(80f, NPC.scale * 0.19f),
                BoidsManager.StayNearGroundRule(Sin01(NPC.position.X * 0.0167f) * 50f + 380f, NPC.scale * 0.09f),
                BoidsManager.ClampVelocityRule(Pow(NPC.scale, 2f) * 4f),
            ];

            return rules;
        }
    }

    public bool CurrentlyUsingBoidBehavior
    {
        get;
        set;
    }

    #endregion Boid Implementation

    #region Fields and Properties

    public int? ConstellationConnectNPCIndex
    {
        get;
        set;
    }

    public bool StartedConstellationEffect
    {
        get;
        set;
    }

    public bool Disappearing
    {
        get;
        set;
    }

    public Vector2 ConstellationCenter
    {
        get;
        set;
    }

    public VivajuyfylaeAIType CurrentState
    {
        get;
        set;
    }

    public bool ThumbsUpAnimationVariant
    {
        get => NPC.ai[0] == 1f;
        set => NPC.ai[0] = value.ToInt();
    }

    public ref float AITimer => ref NPC.ai[1];

    public ref float ConstellationAnimationTime => ref NPC.ai[2];

    public ref float ConstellationOffsetInterpolant => ref NPC.ai[3];

    public ref float ShineBrightnessFactor => ref NPC.localAI[0];

    public override string Texture => GetAssetPath("Content/NPCs/Critters/EternalGarden", Name);

    #endregion Fields and Properties

    #region Initialization

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 2;

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
        NPC.height = 14;
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
        NPC.scale = Main.rand?.NextFloat(0.23f, 0.85f) ?? 0.5f;
        if (NPC.IsABestiaryIconDummy)
            NPC.scale = 0.8f;

        NPC.friendly = true;
        ShineBrightnessFactor = 1f;

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
        writer.Write((int)CurrentState);
        writer.Write(ConstellationConnectNPCIndex ?? int.MinValue);
        writer.WriteVector2(ConstellationCenter);
        writer.Write((byte)StartedConstellationEffect.ToInt());
        writer.Write((byte)Disappearing.ToInt());
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        CurrentState = (VivajuyfylaeAIType)reader.ReadInt32();

        ConstellationConnectNPCIndex = reader.ReadInt32();
        if (ConstellationConnectNPCIndex == int.MinValue)
            ConstellationConnectNPCIndex = null;

        ConstellationCenter = reader.ReadVector2();
        StartedConstellationEffect = reader.ReadByte() != 0;
        Disappearing = reader.ReadByte() != 0;
    }

    #endregion Syncing

    #region AI
    public override void AI()
    {
        // Reset things every frame.
        CurrentlyUsingBoidBehavior = false;
        ShineBrightnessFactor = 1f;

        // Fly away if Nameless is present.
        bool shouldLeave = NamelessDeityBoss.Myself is not null;
        if (shouldLeave && CurrentState != VivajuyfylaeAIType.RespectfullyFlyAway)
        {
            SelectNextState();
            NPC.velocity.Y = -6f;
            CurrentState = VivajuyfylaeAIType.RespectfullyFlyAway;
        }

        Main.BestiaryTracker.Sights.RegisterWasNearby(NPC);

        switch (CurrentState)
        {
            case VivajuyfylaeAIType.FlyInSwarm:
                DoBehavior_FlyInSwarm();
                break;
            case VivajuyfylaeAIType.FormLargeStarConstellation:
                DoBehavior_FormLargeStarConstellation();
                break;
            case VivajuyfylaeAIType.FormThumbsUpConstellation:
                DoBehavior_FormThumbsUpConstellation();
                break;
            case VivajuyfylaeAIType.RespectfullyFlyAway:
                DoBehavior_RespectfullyFlyAway();
                break;
        }

        // Disappear if necessary.
        if (Disappearing)
        {
            NPC.Opacity = Saturate(NPC.Opacity - 0.04f);
            NPC.scale *= 0.986f;
            if (NPC.Opacity <= 0f)
                NPC.active = false;
        }

        // Use rotation and directioning.
        if (Abs(NPC.velocity.X) >= 0.3f)
            NPC.spriteDirection = NPC.velocity.X.NonZeroSign();
        NPC.rotation = Clamp(NPC.velocity.X * 0.046f, -0.4f, 0.4f);

        // Ensure that the velocity update step varies based on scale.
        NPC.Center += NPC.velocity * (Pow(NPC.scale, 1.75f) * 1.8f - 1f);

        AITimer++;
    }

    public List<NPC> GetNearbyInRange(float checkArea, bool includeSelf = false)
    {
        List<NPC> nearbyInstances = [];
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC n = Main.npc[i];
            if (n.active && n.type == Type && (n.whoAmI != NPC.whoAmI || includeSelf) && n.WithinRange(NPC.Center, checkArea) && n.As<Vivajuyfylae>().CurrentState == VivajuyfylaeAIType.FlyInSwarm)
                nearbyInstances.Add(n);
        }

        return nearbyInstances;
    }

    public Vector2 GetCenterOfMassInRange(float checkArea)
    {
        int totalInstancesInRange = 0;
        Vector2 centerOfMass = Vector2.Zero;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC n = Main.npc[i];
            if (n.active && n.type == Type && n.whoAmI != NPC.whoAmI && n.WithinRange(NPC.Center, checkArea) && n.As<Vivajuyfylae>().CurrentState == VivajuyfylaeAIType.FlyInSwarm)
            {
                centerOfMass += n.Center;
                totalInstancesInRange++;
            }
        }

        return centerOfMass / totalInstancesInRange;
    }

    public Entity ClosestEntity()
    {
        int aelithrysuwlsID = ModContent.NPCType<Aelithrysuwl>();
        float bestAelithrysuwlDistance = 99999999f;
        NPC? closestAelithrysuwl = null;
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC n = Main.npc[i];
            if (n.type == aelithrysuwlsID && n.active && n.WithinRange(NPC.Center, bestAelithrysuwlDistance))
            {
                closestAelithrysuwl = n;
                bestAelithrysuwlDistance = n.Distance(NPC.Center);
            }
        }

        if (closestAelithrysuwl is null)
            return closestPlayer;

        return closestPlayer.Distance(NPC.Center) > bestAelithrysuwlDistance ? closestAelithrysuwl : closestPlayer;
    }

    public void DoBehavior_FlyInSwarm()
    {
        // The BoidsManager has already handled movement at this point.
        CurrentlyUsingBoidBehavior = true;

        // Reset the constellation connection index.
        ConstellationConnectNPCIndex = null;

        // Move around independently a tiny bit.
        NPC.velocity += (AITimer / 90f + NPC.whoAmI).ToRotationVector2() * new Vector2(0.06f, 0.01f);

        // Get out of the way of players and Aelithrysuwls.
        Entity closestEntity = ClosestEntity();
        if (NPC.WithinRange(closestEntity.Center, 150f))
            NPC.velocity -= (closestEntity.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 0.6f;

        // Slowly err towards the player if far away.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        float playerErrInterpolant = InverseLerp(960f, 1380f, NPC.Distance(closestPlayer.Center));
        NPC.velocity += NPC.SafeDirectionTo(closestPlayer.Center) * playerErrInterpolant * 0.5f;

        // Occasionally start a constellation formation. This does not happen if this is a thumbs up variant, as that animation is based on a timer.
        if (!ThumbsUpAnimationVariant && Main.rand.NextBool(195000))
        {
            // Check for nearby Vivajuflae. If there aren't enough to do an animation, don't try to do it.
            float checkArea = 950f;
            List<NPC> nearbyInstances = GetNearbyInRange(checkArea, true);
            if (nearbyInstances.Count <= 30)
                return;

            if (nearbyInstances.Count % 2 == 1)
                nearbyInstances.RemoveAt(0);

            // Determine the center of mass for the nearby Vivajuflae.
            Vector2 centerOfMass = GetCenterOfMassInRange(checkArea);

            // Pick a constellation attack.
            SelectNextState();
            ConstellationAnimationTime = Main.rand.Next(120, 180);
            StartedConstellationEffect = true;

            // Make other Vivajuflae get the picture.
            SetUpNearbyForConstellation(checkArea, centerOfMass);
        }

        // Start the thumbs up animation if after enough time has passed if that behavior is in effect.
        if (ThumbsUpAnimationVariant && AITimer >= 480f)
        {
            float checkArea = 6000f;

            // Determine the center of mass for the nearby Vivajuflae.
            Vector2 centerOfMass = GetCenterOfMassInRange(checkArea);

            // Pick a constellation attack.
            SelectNextState();
            CurrentState = VivajuyfylaeAIType.FormThumbsUpConstellation;
            ConstellationAnimationTime = 150;
            StartedConstellationEffect = true;

            // Make other Vivajuflae get the picture.
            SetUpNearbyForConstellation(checkArea, centerOfMass);
        }
    }

    public void SetUpNearbyForConstellation(float checkArea, Vector2 centerOfMass)
    {
        ConstellationCenter = centerOfMass - Vector2.UnitY * 100f;

        // Make other Vivajuflae get the picture.
        List<NPC> nearbyInstancesOrdered = [.. GetNearbyInRange(checkArea, true).OrderBy(i => i.Distance(centerOfMass))];
        for (int i = 0; i < nearbyInstancesOrdered.Count; i++)
        {
            nearbyInstancesOrdered[i].As<Vivajuyfylae>().SelectNextState();
            nearbyInstancesOrdered[i].As<Vivajuyfylae>().CurrentState = CurrentState;
            nearbyInstancesOrdered[i].As<Vivajuyfylae>().ConstellationCenter = ConstellationCenter;
            nearbyInstancesOrdered[i].As<Vivajuyfylae>().ConstellationAnimationTime = ConstellationAnimationTime;
            nearbyInstancesOrdered[i].As<Vivajuyfylae>().ConstellationOffsetInterpolant = i / (float)(nearbyInstancesOrdered.Count - 1f);
            nearbyInstancesOrdered[i].As<Vivajuyfylae>().ConstellationConnectNPCIndex = nearbyInstancesOrdered[(i + 1) % nearbyInstancesOrdered.Count].whoAmI;
        }
    }

    public void DoBehavior_FormConstellation(Vector2 hoverOffset)
    {
        // Calculate useful local variables.
        float animationCompletion = InverseLerp(0f, ConstellationAnimationTime, AITimer);
        float outwardExpandFactor = InverseLerpBump(0.41f, 0.44f, 0.85f, 1f, animationCompletion) + 0.001f;
        float brightnessIntensity = InverseLerpBump(0.06f, 0.3f, 0.7f, 1f, animationCompletion);
        float inwardExpansionSpeed = InverseLerp(0f, 0.4f, animationCompletion) * 0.2f + InverseLerp(0.9f, 1f, animationCompletion) * 0.2f + 0.015f;

        // Fly outward.
        NPC.SmoothFlyNear(ConstellationCenter + hoverOffset * outwardExpandFactor, inwardExpansionSpeed, 0.78f);

        // Play a twinkle sound once sufficiently far out.
        if (StartedConstellationEffect && AITimer == (int)(ConstellationAnimationTime * 0.45f))
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.Twinkle with { MaxInstances = 5, PitchVariance = 0.16f, Pitch = -0.26f }, NPC.Center);

        // Modify the abdomen brightness.
        ShineBrightnessFactor = InverseLerpBump(0.44f, 0.45f, 0.85f, 1f, animationCompletion) * 1.64f + 1f;

        if (animationCompletion >= 1f)
        {
            SelectNextState();
            StartedConstellationEffect = false;
            NPC.velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 22f) * new Vector2(1f, 0.45f);
        }
    }

    public void DoBehavior_FormLargeStarConstellation()
    {
        Vector2 hoverOffset = StarPatternedStarburst.StarPolarEquation(5, TwoPi * ConstellationOffsetInterpolant) * 230f;
        DoBehavior_FormConstellation(hoverOffset);
    }

    public void DoBehavior_FormThumbsUpConstellation()
    {
        if (!ShapeCurveManager.TryFind("ThumbsUp", out ShapeCurve? curve) || curve is null)
        {
            NPC.active = false;
            return;
        }

        Vector2 hoverOffset = curve.ShapePoints[(int)(curve.ShapePoints.Count * ConstellationOffsetInterpolant) % curve.ShapePoints.Count] * 250f;
        DoBehavior_FormConstellation(hoverOffset);
    }

    public void DoBehavior_RespectfullyFlyAway()
    {
        // Fly away from the closest player and vanish after enough time has passed.
        Player closestPlayer = Main.player[Player.FindClosest(NPC.Center, 1, 1)];
        NPC.velocity.X = Lerp(NPC.velocity.X, NPC.SafeDirectionTo(closestPlayer.Center).X * -6f, 0.15f);
        NPC.velocity.Y = Clamp(NPC.velocity.Y - 0.5f, -39f, 1f);

        // Update the current direction.
        NPC.spriteDirection = NPC.velocity.X.NonZeroSign();

        if (!NPC.WithinRange(closestPlayer.Center, 1400f))
            NPC.active = false;
    }

    public void SelectNextState()
    {
        if (CurrentState == VivajuyfylaeAIType.FormThumbsUpConstellation)
            Disappearing = true;

        CurrentState = CurrentState switch
        {
            VivajuyfylaeAIType.FlyInSwarm => Main.rand.NextFromList(VivajuyfylaeAIType.FormLargeStarConstellation, VivajuyfylaeAIType.FormLargeStarConstellation),
            _ => VivajuyfylaeAIType.FlyInSwarm,
        };
        AITimer = 0f;
        NPC.netUpdate = true;
    }

    #endregion AI

    #region Drawing

    public override void FindFrame(int frameHeight)
    {
        NPC.frameCounter++;
        NPC.frame.Y = (int)(NPC.frameCounter / 3 % Main.npcFrameCount[Type]) * frameHeight;
    }

    public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Color.White, 0.3f) * NPC.Opacity;

    public override void DrawBehind(int index)
    {
        Main.instance.DrawCacheNPCsOverPlayers.Add(index);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        // Draw a soft backglow behind the abdomen to indicate light.
        float abdomenColorInterpolant = Cos01(Main.GlobalTimeWrappedHourly * 1.4f + NPC.whoAmI);
        float abdomenLightBrightness = Lerp(0.6f, 1.05f, abdomenColorInterpolant) * NPC.Opacity;
        Color abdomenColor = Color.Lerp(Color.Orange, Color.IndianRed, abdomenColorInterpolant);
        Color abdomenGlowColor = abdomenColor.HueShift(Lerp(-0.1f, -0.16f, abdomenColorInterpolant));
        Vector2 drawPosition = NPC.Center - screenPos;
        Vector2 abdomenDrawPosition = drawPosition + new Vector2(NPC.spriteDirection * -6f, 2f).RotatedBy(NPC.rotation) * NPC.scale;
        Main.spriteBatch.Draw(BloomCircleSmall, abdomenDrawPosition, null, abdomenColor with { A = 0 } * abdomenLightBrightness * 0.6f, 0f, BloomCircleSmall.Size() * 0.5f, NPC.scale * 0.5f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, abdomenDrawPosition, null, abdomenGlowColor with { A = 0 } * abdomenLightBrightness * 0.43f, 0f, BloomCircleSmall.Size() * 0.5f, NPC.scale * 0.28f, 0, 0f);

        // Draw a constellation to the nearest Vivajufylae if in use.
        if (ConstellationConnectNPCIndex is not null)
        {
            Vector2 constellationStart = NPC.Center;
            Vector2 constellationEnd = Main.npc[ConstellationConnectNPCIndex.Value].Center;
            DrawConstellationLine(constellationStart, constellationEnd);
        }

        // Perform standard drawing.
        Vector2 origin = NPC.frame.Size() * 0.5f;
        Texture2D texture = GennedAssets.Textures.EternalGarden.Vivajuyfylae.Value;
        Texture2D abdomenTexture = GennedAssets.Textures.EternalGarden.VivajuyfylaeAbdomen.Value;
        SpriteEffects direction = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        Main.spriteBatch.Draw(texture, drawPosition, NPC.frame, NPC.GetAlpha(drawColor), NPC.rotation, origin, NPC.scale, direction, 0f);
        Main.spriteBatch.Draw(abdomenTexture, drawPosition, NPC.frame, NPC.GetAlpha(abdomenColor), NPC.rotation, origin, NPC.scale, direction, 0f);

        // Draw a brighter frontglow behind the abdomen to obscure it a bit.
        float brightGlowIntensity = Cos01(Main.GlobalTimeWrappedHourly * 24f + NPC.whoAmI);
        float brightGlowScale = NPC.scale * Lerp(0.9f, 1.15f, brightGlowIntensity) * ShineBrightnessFactor * 0.127f;
        Main.spriteBatch.Draw(BloomCircleSmall, abdomenDrawPosition, null, Color.Wheat with { A = 0 } * abdomenLightBrightness * (0.9f + brightGlowIntensity * 0.2f), 0f, BloomCircleSmall.Size() * 0.5f, brightGlowScale, 0, 0f);

        // Draw a bloom flare at the abdomen to indicate that it's a miniature star.
        float bloomFlareRotation = Main.GlobalTimeWrappedHourly * 0.5f + NPC.whoAmI * 13.589f;
        float bloomFlareScale = NPC.scale * Lerp(0.9f, 1.67f, brightGlowIntensity) * ShineBrightnessFactor * 0.04f;
        Main.spriteBatch.Draw(BloomFlare, abdomenDrawPosition, null, Color.Wheat with { A = 0 } * abdomenLightBrightness * (0.46f + brightGlowIntensity * 0.06f), bloomFlareRotation, BloomFlare.Size() * 0.5f, bloomFlareScale, 0, 0f);

        return false;
    }

    public void DrawConstellationLine(Vector2 start, Vector2 end)
    {
        if (CurrentState == VivajuyfylaeAIType.FormThumbsUpConstellation)
            return;

        float brightness = (ShineBrightnessFactor - 1f) * 0.48f;
        Main.spriteBatch.DrawBloomLine(start, end, (Color.Wheat with { A = 0 }) * brightness, brightness * 4f);
        Main.spriteBatch.DrawBloomLine(start, end, (Color.OrangeRed with { A = 0 }) * brightness * 0.8f, brightness * 8f);
        Main.spriteBatch.DrawBloomLine(start, end, (Color.LightCoral with { A = 0 }) * brightness * 0.5f, brightness * 16f);
    }

    #endregion Drawing

    #region Spawn Behavior

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (spawnInfo.Player.InModBiome<EternalGardenBiome>() && NamelessDeityBoss.Myself is null)
            return 1.5f;

        return 0f;
    }

    #endregion Spawn Behaviors
}
