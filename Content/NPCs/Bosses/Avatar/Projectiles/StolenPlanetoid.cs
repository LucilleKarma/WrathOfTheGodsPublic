using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.Utilities;
using ReLogic.Content;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using static NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness;
using LazyAssetTexture = NoxusBoss.Assets.LazyAsset<Microsoft.Xna.Framework.Graphics.Texture2D>;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class StolenPlanetoid : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    public enum PlanetoidVariant
    {
        Forest,
        Desert,
        Snow,
        Crimson,
        Marble,
        Ogscule,

        Count
    }

    private SlotId rumbleSound;

    /// <summary>
    /// The disintegration interpolant of the planetoid's trees.
    /// </summary>
    public float TreeDisintegrationInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the planetoid has created impact particles.
    /// </summary>
    public bool HasCreateImpactParticles
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the planetoid has played a rumble sound.
    /// </summary>
    public bool HasPlayedRumbleSound
    {
        get;
        set;
    }

    /// <summary>
    /// The texture variant of this planetoid.
    /// </summary>
    public PlanetoidVariant Variant
    {
        get => (PlanetoidVariant)Projectile.localAI[0];
        set => Projectile.localAI[0] = (int)value;
    }

    /// <summary>
    /// Whether the planetoid has created bloom visuals.
    /// </summary>
    public bool HasCreatedBloom
    {
        get => Projectile.ai[2] == 1f;
        set => Projectile.ai[2] = value.ToInt();
    }

    /// <summary>
    /// The direction of the planetoid's heat highlights.
    /// </summary>
    public Vector2 HighlightDirection
    {
        get;
        set;
    }

    /// <summary>
    /// The planetoid's Z position.
    /// </summary>
    public ref float ZPosition => ref Projectile.ai[0];

    /// <summary>
    /// The amount of time that's passed since the planetoid spawned, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[1];

    /// <summary>
    /// The visual intensity of the planetoid's collision-based heat effects.
    /// </summary>
    public ref float CollisionIntensity => ref Projectile.localAI[1];

    /// <summary>
    /// The disintegration interpolant of the planetoid.
    /// </summary>
    public ref float DisintegrationInterpolant => ref Projectile.localAI[2];

    /// <summary>
    /// How long one should expect the strip textures from the planetoids to be.
    /// </summary>
    public static float PlanetoidTextureWidth => 1612f;

    /// <summary>
    /// A table that maps each possible planetoid variant with a respective texture.
    /// </summary>
    public static readonly Dictionary<PlanetoidVariant, LazyAssetTexture> PlanetoidTextureTable = [];

    /// <summary>
    /// A table that maps each possible planetoid variant's trees with a respective texture.
    /// </summary>
    public static readonly Dictionary<PlanetoidVariant, LazyAssetTexture> PlanetoidTreeTextureTable = [];

    /// <summary>
    /// A table that maps distance values at angle increments for each possible planetoid variant.
    /// </summary>
    public static readonly Dictionary<PlanetoidVariant, int[]> PlanetoidTopographyTable = [];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        // Load planetoid textures.
        if (Main.netMode != NetmodeID.Server)
        {
            string texturePrefix = "NoxusBoss/Assets/Textures/Content/NPCs/Bosses/Avatar/Projectiles";

            PlanetoidTextureTable[PlanetoidVariant.Forest] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_Forest", AssetRequestMode.ImmediateLoad);
            PlanetoidTextureTable[PlanetoidVariant.Desert] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_Desert", AssetRequestMode.ImmediateLoad);
            PlanetoidTextureTable[PlanetoidVariant.Snow] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_Snow", AssetRequestMode.ImmediateLoad);
            PlanetoidTextureTable[PlanetoidVariant.Crimson] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_Crimson", AssetRequestMode.ImmediateLoad);
            PlanetoidTextureTable[PlanetoidVariant.Marble] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_Marble", AssetRequestMode.ImmediateLoad);
            PlanetoidTextureTable[PlanetoidVariant.Ogscule] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_Ogscule", AssetRequestMode.ImmediateLoad);

            PlanetoidTreeTextureTable[PlanetoidVariant.Forest] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_ForestTrees");
            PlanetoidTreeTextureTable[PlanetoidVariant.Desert] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_DesertTrees");
            PlanetoidTreeTextureTable[PlanetoidVariant.Snow] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_SnowTrees");
            PlanetoidTreeTextureTable[PlanetoidVariant.Crimson] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_CrimsonTrees");
            PlanetoidTreeTextureTable[PlanetoidVariant.Ogscule] = LazyAssetTexture.FromPath($"{texturePrefix}/StolenPlanetoid_OgsculeTrees");

            // NOTE -- Yes, this means that the effect won't work in multiplayer. This has been deemed acceptable.
            Color[][] colorsByPlanetoid = Main.RunOnMainThread(() =>
            {
                Color[][] result = new Color[PlanetoidTextureTable.Count][];
                for (int i = 0; i < (int)PlanetoidVariant.Count; i++)
                {
                    Texture2D texture = PlanetoidTextureTable[(PlanetoidVariant)i].Value;
                    result[i] = new Color[texture.Width * texture.Height];
                    texture.GetData(result[i]);
                }

                return result;
            }).GetAwaiter().GetResult();

            GenerateTopographyMaps(colorsByPlanetoid);
        }

        // Draw the planetoid such that it doesn't randomly get cut off when "offscreen".
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
    }

    public static void GenerateTopographyMaps(Color[][] colorsByPlanetoid)
    {
        for (int i = 0; i < (int)PlanetoidVariant.Count; i++)
        {
            Texture2D texture = PlanetoidTextureTable[(PlanetoidVariant)i].Value;
            int totalAngles = texture.Width / 8;

            // Calculate texture colors.
            Color[] colors = colorsByPlanetoid[i];

            // Store a collection of height values.
            int[] topography = new int[totalAngles];

            // Go through every 8 X increment on the texture. This is 8 instead of 16 because the texture is in 1x1 for mod size reasons.
            for (int j = 0; j < totalAngles; j++)
            {
                // Calculate the height of the current pixel.
                int x = j * 8;
                for (int y = texture.Height - 1; y >= 0; y -= 8)
                {
                    int index = y * texture.Width + x;
                    if (colors[index].A <= 0)
                    {
                        topography[j] = y;
                        break;
                    }
                }
            }

            // Offset the topography by the average height so that everything represents an offset from low ground.
            int averageHeight = (int)topography.Average();
            for (int j = 0; j < totalAngles; j++)
                topography[j] -= averageHeight;

            // Store the topography.
            PlanetoidTopographyTable[(PlanetoidVariant)i] = topography;
        }
    }

    public override void SetDefaults()
    {
        int diameter = (int)PlanetoidTextureWidth;
        Projectile.width = diameter;
        Projectile.height = diameter;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.hide = true;
        Projectile.penetrate = -1;

        // Edge-case: If for some diabolical reason the planetoids exist for too long due to missing each other, they need to die naturally.
        // If this doesn't happen, the Avatar will get softlocked forever due to how the world smash attack state works.
        Projectile.timeLeft = SecondsToFrames(10f);
        Projectile.scale = 1.2f;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void OnSpawn(IEntitySource source)
    {
        // Decide a frame to use.
        WeightedRandom<PlanetoidVariant> variantRNG = new WeightedRandom<PlanetoidVariant>(Main.rand);
        variantRNG.Add(PlanetoidVariant.Forest, 1D);
        variantRNG.Add(PlanetoidVariant.Desert, 1D);
        variantRNG.Add(PlanetoidVariant.Snow, 1D);
        variantRNG.Add(PlanetoidVariant.Crimson, 1D);
        variantRNG.Add(PlanetoidVariant.Marble, 1D);
        variantRNG.Add(PlanetoidVariant.Ogscule, 0.0004);

        Variant = variantRNG;
    }

    public float CalculateTopographyOffsetForDirection(Vector2 direction)
    {
        // Return 0 as a fallback if the topography map is undefined.
        if (!PlanetoidTopographyTable.TryGetValue(Variant, out int[]? topography))
            return 0f;

        // Calculate the direction's angle in a -pi to pi range.
        float angle = direction.ToRotation();

        // Normalize the angle to 0-1.
        float normalizedAngle = (angle + Pi) / TwoPi;

        // Use the 0-1 normalized angle as an interpolant to decide which topography index to use.
        int topographyIndex = Utils.Clamp((int)(normalizedAngle * topography.Length), 0, topography.Length - 1);
        return topography[topographyIndex];
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(CollisionIntensity);
        writer.Write(DisintegrationInterpolant);
        writer.Write((int)Variant);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        CollisionIntensity = reader.ReadSingle();
        DisintegrationInterpolant = reader.ReadSingle();
        Variant = (PlanetoidVariant)reader.ReadInt32();
    }

    public override void AI()
    {
        if (Myself is null)
        {
            Projectile.Kill();
            return;
        }

        Time++;

        bool pushBackPlayers = Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing;
        var planetoids = AllProjectilesByID(Type).Where(p => p.whoAmI != Projectile.whoAmI).OrderBy(p => p.DistanceSQ(Projectile.Center));
        if (ZPosition <= 0f && !pushBackPlayers && planetoids.Any())
            HandlePlanetoidCollision(planetoids);
        else
            CollisionIntensity = 0f;

        // Scale in accordance with the Z position.
        Projectile.scale = 1f / (ZPosition + 1f);

        if (pushBackPlayers)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (Projectile.Colliding(Projectile.Hitbox, player.Hitbox) && player.velocity.Length() <= 40f)
                    player.velocity += Projectile.SafeDirectionTo(player.Center) * Projectile.velocity.Length();
            }
        }

        // Spin in place.
        Projectile.rotation += TwoPi / 560f * (1f - CollisionIntensity);
    }

    public void HandlePlanetoidCollision(IEnumerable<Projectile> planetoids)
    {
        var closestPlanetoid = planetoids.First();
        float distanceToPlanetoid = Projectile.Distance(closestPlanetoid.Center);

        // Account for distance discrepancies based on angle.
        distanceToPlanetoid -= CalculateTopographyOffsetForDirection(HighlightDirection.RotatedBy(Projectile.rotation));

        // Calculate interpolants for the burn effects.
        CalculateCollisionVisuals(distanceToPlanetoid);

        // Look towards the nearest planetoid.
        HighlightDirection = Projectile.SafeDirectionTo(closestPlanetoid.Center);

        // Slow down prior to impact.
        if (distanceToPlanetoid <= WorldSmash_SlowDownDistance)
        {
            float slowdownDeceleration = 0.9f;
            if (Myself is not null)
                slowdownDeceleration = CalculatePlanetoidSlowdownDeceleration(Myself.As<AvatarOfEmptiness>().WorldSmash_PlanetoidFlingSpeed);

            Projectile.velocity *= slowdownDeceleration;
        }

        // Calculate proximity effects.
        Vector2 centerPoint = (Projectile.Center + closestPlanetoid.Center) * 0.5f;
        if (CreateProximityVisuals(centerPoint, distanceToPlanetoid))
            closestPlanetoid.Kill();

        // Create a rumble sound.
        if (distanceToPlanetoid <= 4950f && !HasPlayedRumbleSound)
        {
            rumbleSound = SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.PlanetoidRumble with { Volume = 4f, MaxInstances = 8 });
            closestPlanetoid.As<StolenPlanetoid>().HasPlayedRumbleSound = true;
            HasPlayedRumbleSound = true;
        }

        // Update the rumble sound's volume.
        float volumeFade = InverseLerp(1000f, 1550f, distanceToPlanetoid);
        if (SoundEngine.TryGetActiveSound(rumbleSound, out var sound))
        {
            if (Time % 5f == 4f)
                sound.Volume = volumeFade * 3.7f;
            MusicVolumeManipulationSystem.MuffleFactor = Lerp(1f, 0.999f, volumeFade);
        }
    }

    /// <summary>
    /// Calculates visual interpolants and intensities based on the proximity to the nearest planetoid.
    /// </summary>
    /// <param name="distanceToPlanetoid">The distance to the nearest planetoid.</param>
    public void CalculateCollisionVisuals(float distanceToPlanetoid)
    {
        // Do not change these numbers unless you are highly competent at visuals.
        CollisionIntensity = InverseLerp(1300f, 960f, distanceToPlanetoid);
        TreeDisintegrationInterpolant = Pow(InverseLerp(2400f, 1100f, distanceToPlanetoid), 0.4f) * 0.8f;
        DisintegrationInterpolant = InverseLerp(1700f, 850f, distanceToPlanetoid) * 0.84f;
    }

    /// <summary>
    /// Creates particles and projectiles based on the planetoid's proximity to the nearest planetoid.
    /// </summary>
    /// <param name="centerPoint">The center point of the two planetoids.</param>
    /// <param name="distanceToPlanetoid">The distance to the nearest planetoid.</param>
    public bool CreateProximityVisuals(Vector2 centerPoint, float distanceToPlanetoid)
    {
        // Create bloom at the impact point.
        if (distanceToPlanetoid <= 1525f && !HasCreatedBloom)
        {
            // Create bloom.
            StrongBloom bloom = new StrongBloom(centerPoint, Vector2.Zero, Color.Wheat, 12f, 90);
            bloom.Spawn();
            bloom = new(centerPoint, Vector2.Zero, Color.Orange * 0.7f, 25f, 90);
            bloom.Spawn();
            bloom = new(centerPoint, Vector2.Zero, Color.Red * 0.5f, 39.5f, 90);
            bloom.Spawn();

            HasCreatedBloom = true;
            Projectile.netUpdate = true;
        }

        // Create impact particles and molten blobs at the impact point.
        if (distanceToPlanetoid <= 1100f && !HasCreateImpactParticles)
        {
            // Create lava.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float maxLavaAngularOffset = 0.98f;
                for (int i = 0; i < 36; i++)
                {
                    float blobAngularOffsetDevianceInterpolant = Main.rand.NextFloat();
                    float blobAngularOffset = blobAngularOffsetDevianceInterpolant * Main.rand.NextFromList(-maxLavaAngularOffset, maxLavaAngularOffset);
                    if (Main.rand.NextBool())
                        blobAngularOffset *= Main.rand.NextFloat(1f, 2f);

                    float blobSpeed = Lerp(80f, 23f, blobAngularOffsetDevianceInterpolant) * Main.rand.NextFloat(0.7f, 1.2f);
                    Vector2 blobDirection = HighlightDirection.RotatedBy(PiOver2 + blobAngularOffset) * Main.rand.NextFromList(-1f, 1f);
                    NewProjectileBetter(Projectile.GetSource_FromThis(), centerPoint, blobDirection * blobSpeed, ModContent.ProjectileType<MoltenBlob>(), MoltenBlobDamage, 0f);
                }
            }

            // Play a crack sound.
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.PlanetoidCrack with { Volume = 4f, MaxInstances = 8 });

            HasCreateImpactParticles = true;
        }

        // Create glowing molten particles that move towards the other planetoid if the distance if sufficiently low, implying a Roche limit type of effect.
        if (CollisionIntensity > 0f)
        {
            int materialCount = (int)(CollisionIntensity.Squared() * 19f) + 3;
            for (int i = 0; i < materialCount; i++)
            {
                float materialSpeed = Projectile.velocity.Length() + Main.rand.NextFloat(1f, 11f);
                Color materialColor = Color.Lerp(Color.Orange, Color.Wheat, Main.rand.NextFloat(0.8f));
                Vector2 materialSpawnPosition = Projectile.Center + HighlightDirection.RotatedByRandom(PiOver2 - 0.25f) * Projectile.width * 0.33f;
                GlowyShardParticle material = new GlowyShardParticle(materialSpawnPosition, HighlightDirection.RotatedByRandom(0.4f) * materialSpeed, materialColor, Color.Wheat * 0.3f, 1f, 0.2f, 32);
                material.Spawn();
            }
        }

        // Explode once close enough.
        if (distanceToPlanetoid <= WorldSmash_ExplodeDistance)
        {
            ScreenShakeSystem.StartShake(30f, shakeStrengthDissipationIncrement: 0.51f);
            RadialScreenShoveSystem.Start(centerPoint, 30);

            // Create a lens flare.
            float lensFlareRotation = HighlightDirection.ToRotation() + PiOver2;
            LensFlareParticle lensFlare = new LensFlareParticle(centerPoint, Color.Wheat, 28, 1.6f, lensFlareRotation);
            lensFlare.Spawn();
            lensFlare = new(centerPoint, Color.Yellow * 0.7f, 28, 1.6f, lensFlareRotation);
            lensFlare.Spawn();

            // Kill both planetoids.
            Projectile.Kill();

            TotalScreenOverlaySystem.OverlayColor = Color.White;
            TotalScreenOverlaySystem.OverlayInterpolant = 1f;

            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.PlanetoidExplosion with { Volume = 4f, MaxInstances = 8 });
            return true;
        }

        return false;
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        if (ZPosition >= 0.2f)
            behindNPCsAndTiles.Add(index);
        else
            overPlayers.Add(index);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float radius = Projectile.width * 0.25f;
        if (Variant == PlanetoidVariant.Marble)
            radius *= 1.27f;

        return CircularHitboxCollision(Projectile.Center, radius, targetHitbox) && ZPosition <= 0f;
    }

    public override Color? GetAlpha(Color lightColor)
    {
        return Color.Lerp(Color.White, new(68, 6, 9), InverseLerp(0.6f, 4.6f, ZPosition)) * Projectile.Opacity;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.spriteBatch.PrepareForShaders();
        DrawWithShader();
        Main.spriteBatch.ResetToDefault();
        return false;
    }

    public void DrawWithShader()
    {
        // Acquire the planetoid and tree texture.
        Texture2D planetoidTexture = PlanetoidTextureTable[Variant].Value;

        // Calculate the degree by which the planetoid should bulge. This increases as the burn intensity increases.
        float bulgeScale = Cos(Main.GlobalTimeWrappedHourly * 70f) * InverseLerp(0.5f, 0.97f, CollisionIntensity) * 0.0051f + 1f;

        // Calculate draw variables.
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Vector2 planetoidSize = Projectile.Size * bulgeScale;
        Vector2 planetoidScale = planetoidSize / InvisiblePixel.Size();

        // Apply the planetoid shader.
        var planetoidShader = ShaderManager.GetShader("NoxusBoss.StolenPlanetoidShader");
        planetoidShader.TrySetParameter("disintegrateOnlyAtHighlight", true);
        planetoidShader.TrySetParameter("glowHighlightIntensity", CollisionIntensity * 1.25f);
        planetoidShader.TrySetParameter("glowHighlightColor", new Vector3(3f, 3f, 2.6f));
        planetoidShader.TrySetParameter("glowHighlightDirection", HighlightDirection.RotatedBy(-Projectile.rotation));
        planetoidShader.TrySetParameter("disintegrationCompletion", DisintegrationInterpolant);
        planetoidShader.TrySetParameter("pixelationFactor", Vector2.One / planetoidSize * 1.6f);
        planetoidShader.TrySetParameter("heightRatio", planetoidTexture.Height / planetoidSize.Y * 1.4f);
        planetoidShader.SetTexture(planetoidTexture, 1, SamplerState.PointWrap);
        planetoidShader.SetTexture(ViscousNoise, 2, SamplerState.PointWrap);
        planetoidShader.SetTexture(CrackedNoiseA, 3, SamplerState.PointWrap);
        planetoidShader.Apply();

        // Draw the planetoid.
        Main.spriteBatch.Draw(InvisiblePixel, drawPosition, null, Projectile.GetAlpha(Color.White), Projectile.rotation, InvisiblePixel.Size() * 0.5f, planetoidScale * Projectile.scale, 0, 0f);

        // Draw the planetoid's trees.
        DrawTrees(drawPosition, planetoidScale);
    }

    public void DrawTrees(Vector2 drawPosition, Vector2 planetoidScale)
    {
        // Get the tree texture, assuming it exists for the current variant.
        if (!PlanetoidTreeTextureTable.TryGetValue(Variant, out LazyAssetTexture planetoidTreeAsset))
            return;

        Texture2D planetoidTreeTexture = planetoidTreeAsset.Value;
        var planetoidShader = ShaderManager.GetShader("NoxusBoss.StolenPlanetoidShader");
        planetoidShader.TrySetParameter("disintegrateOnlyAtHighlight", true);
        planetoidShader.TrySetParameter("disintegrationColor", Color.OrangeRed);
        planetoidShader.TrySetParameter("disintegrationCompletion", TreeDisintegrationInterpolant);
        planetoidShader.SetTexture(planetoidTreeTexture, 1, SamplerState.PointWrap);
        planetoidShader.SetTexture(ViscousNoise, 2, SamplerState.PointWrap);
        planetoidShader.SetTexture(CrackedNoiseA, 3, SamplerState.PointWrap);
        planetoidShader.Apply();
        Main.spriteBatch.Draw(InvisiblePixel, drawPosition, null, Projectile.GetAlpha(Color.White), Projectile.rotation, InvisiblePixel.Size() * 0.5f, planetoidScale * Projectile.scale, 0, 0f);
    }
}
