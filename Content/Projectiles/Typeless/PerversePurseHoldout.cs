using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Items;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.Meshes;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Projectiles.Typeless;

public class PerversePurseHoldout : ModProjectile
{
    /// <summary>
    /// The owner of this purse.
    /// </summary>
    public Player Owner => Main.player[Projectile.owner];

    /// <summary>
    /// The growth interpolant of the suction effect for this purse.
    /// </summary>
    public float SuctionGrowthInterpolant => InverseLerp(0f, SuctionFadeInTime, Time) * InverseLerp(0f, SuctionFadeOutTime, Projectile.timeLeft);

    /// <summary>
    /// The width of the suction effect.
    /// </summary>
    public float SuctionWidth => SmoothStep(0f, 150f, SuctionGrowthInterpolant);

    /// <summary>
    /// The reach, of the suction effect.
    /// </summary>
    public float SuctionReach => SuctionGrowthInterpolant * 300f;

    /// <summary>
    /// How long this purse should exist for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// The direction in which the rotation effects are moving.
    /// </summary>
    public ref float RotationDirection => ref Projectile.ai[1];

    /// <summary>
    /// The ideal direction of the suction effect of this purse.
    /// </summary>
    public ref float IdealDirection => ref Projectile.ai[2];

    /// <summary>
    /// How long the suction effect spends fading in when the purse starts being used.
    /// </summary>
    public static int SuctionFadeInTime => SecondsToFrames(0.425f);

    /// <summary>
    /// How long the suction effect spends fading out when the purse stops being used.
    /// </summary>
    public static int SuctionFadeOutTime => SecondsToFrames(0.38f);

    /// <summary>
    /// The speed at which the suction direction is updated.
    /// </summary>
    public static float AimSpeedInterpolant => 0.08f;

    /// <summary>
    /// The variable name that dictates how close a given NPC is to a purse's suction.
    /// </summary>
    public const string SuctionDistanceVariableName = "PerversePurseSuctionDistance";

    /// <summary>
    /// The variable name that dictates which purse is sucking a town NPC in.
    /// </summary>
    public const string SuctionPurseIndexName = "PerversePurseSuctionIndex";

    public override string Texture => GetAssetPath("Content/Items", ModContent.GetInstance<PerversePurse>().Name);

    public override void SetStaticDefaults()
    {
        GlobalNPCEventHandlers.PreDrawEvent += DrawTownNPCSuctionVisualWrapper;
    }

    private bool DrawTownNPCSuctionVisualWrapper(NPC npc)
    {
        if (npc.townNPC && npc.type != ModContent.NPCType<Solyn>() && npc.type != ModContent.NPCType<BattleSolyn>())
        {
            float suctionDistance = GlobalNPCEventHandlers.GetValueRef<float>(npc, SuctionDistanceVariableName);
            Referenced<int> suctionPurseIndex = GlobalNPCEventHandlers.GetValueRef<int>(npc, SuctionPurseIndexName);
            bool validPurse = suctionPurseIndex >= 0 && Main.projectile[suctionPurseIndex].active && Main.projectile[suctionPurseIndex].type == Type;

            if (validPurse && suctionDistance > 0f)
            {
                DrawTownNPCSuctionVisual(npc, Main.projectile[suctionPurseIndex], suctionDistance);
                return false;
            }
            else
                suctionPurseIndex.Value = -1;
        }

        return true;
    }

    private static void DrawTownNPCSuctionVisual(NPC npc, Projectile purse, float suctionDistance)
    {
        // Ordinarily a Main.instance.LoadNPC call would be necessary to use this, but since the town NPC was definitely already drawing before
        // it got sucked in, it isn't required here.
        Texture2D texture = TextureAssets.Npc[npc.type].Value;

        Vector2 drawPosition = npc.Center + Vector2.UnitY * npc.gfxOffY - Main.screenPosition - Vector2.UnitY * 4f;
        Color color = npc.GetAlpha(Lighting.GetColor(npc.Center.ToTileCoordinates()));
        float scale = npc.scale * InverseLerp(25f, 160f, suctionDistance);
        float angleToPurse = npc.AngleTo(purse.Center) + PiOver2;
        float rotation = npc.rotation.AngleLerp(angleToPurse, InverseLerp(175f, 50f, suctionDistance) * 0.6f);

        Main.EntitySpriteDraw(texture, drawPosition, npc.frame, color, rotation, npc.frame.Size() * 0.5f, scale, npc.spriteDirection.ToSpriteDirection());
    }

    public override void SetDefaults()
    {
        Projectile.width = 28;
        Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = SuctionFadeOutTime;
        Projectile.scale = 0.64f;
        Projectile.penetrate = -1;
    }

    public override void AI()
    {
        Item heldItem = Owner.HeldMouseItem();

        // Die if necessary.
        if (Owner.dead || !Owner.active || Owner.noItems || Owner.CCed || heldItem is null)
            return;

        if (Owner.channel)
            Projectile.timeLeft = SuctionFadeOutTime;

        // Stick to the owner.
        AdjustPlayerValues();

        // Aim towards the mouse.
        if (Main.myPlayer == Projectile.owner)
            AimTowardsMouse();

        if (Main.rand.NextBool())
            CreateSuctionParticle();

        // Suck in town NPCs.
        if (heldItem.ModItem is PerversePurse purse && purse.TotalStoredVictims < PerversePurse.MaxVictims)
            SuckInTownNPCs(purse);

        Time++;
    }

    /// <summary>
    /// Updates player values for the owner, making them hold this purse.
    /// </summary>
    public void AdjustPlayerValues()
    {
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = 2;
        Owner.itemAnimation = 2;
        Owner.itemRotation = (Projectile.direction * Projectile.velocity).ToRotation();

        // Update the player's arm directions to make it look as though they're holding the purse.
        float frontArmRotation = Projectile.rotation + Owner.direction * -1.1f;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
    }

    /// <summary>
    /// Makes the purse's suction effect aim toawrds the mouse gradually.
    /// </summary>
    public void AimTowardsMouse()
    {
        IdealDirection = Projectile.AngleTo(Main.MouseWorld);
        IdealDirection = IdealDirection.AngleLerp(Projectile.velocity.ToRotation(), InverseLerp(30f, 10f, Projectile.Distance(Main.MouseWorld)));

        Projectile.velocity = Projectile.velocity.ToRotation().AngleLerp(IdealDirection, AimSpeedInterpolant).ToRotationVector2();
        RotationDirection = (IdealDirection - Projectile.velocity.ToRotation()).NonZeroSign();

        if (Projectile.velocity != Projectile.oldVelocity)
            Projectile.netUpdate = true;
        Projectile.spriteDirection = (Projectile.velocity.X < 0f).ToDirectionInt();

        // Update the visual direction and rotation of the purse and player.
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.spriteDirection == 1)
            Projectile.rotation += Pi;
        Owner.ChangeDir(-Projectile.spriteDirection);

        Projectile.Center = Owner.MountedCenter + Projectile.velocity * 12f;
    }

    /// <summary>
    /// Creates a single suction particle.
    /// </summary>
    public void CreateSuctionParticle()
    {
        bool blue = Main.rand.NextBool();
        Vector2 start = Projectile.Center + Projectile.velocity * SuctionReach * 0.3f;
        Vector2 end = start + Projectile.velocity * SuctionReach * 0.95f;
        float reachInterpolant = Main.rand.NextFloat();

        Vector2 perpendicular = Projectile.velocity.RotatedBy(PiOver2) * SuctionWidth * reachInterpolant * 0.3f;
        Vector2 linearPosition = Vector2.Lerp(start, end, reachInterpolant);
        Vector2 spawnPosition = linearPosition + perpendicular * Main.rand.NextFloatDirection();

        float angularSwerve = blue.ToDirectionInt() * Sin01(TwoPi * reachInterpolant * 2f) * -0.7f;
        Vector2 energyVelocity = -Projectile.velocity.RotatedBy(angularSwerve) * reachInterpolant * 18f;

        Dust energy = Dust.NewDustPerfect(spawnPosition, 261, energyVelocity);
        energy.color = blue ? Color.Blue : Color.Crimson;
        energy.scale *= Lerp(0.6f, 1.2f, reachInterpolant);
        energy.noGravity = true;
    }

    /// <summary>
    /// Sucks in nearby town NPCs, making them enter the purse when sufficiently close.
    /// </summary>
    /// <param name="purse">The purse item instance that should store kidnapped town NPCs.</param>
    public void SuckInTownNPCs(PerversePurse purse)
    {
        int solynID = ModContent.NPCType<Solyn>();
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.townNPC || npc.type == NPCID.TravellingMerchant || npc.type == solynID)
                continue;

            float npcOrthogonality = Vector2.Dot(Projectile.SafeDirectionTo(npc.Center), Projectile.velocity);
            float suctionPower = SuctionGrowthInterpolant * Clamp(npcOrthogonality, 0f, 1f) * InverseLerp(SuctionReach * 2f, SuctionReach, Projectile.Distance(npc.Center));

            Vector2 offsetToPurse = Projectile.Center - npc.Center;
            float distanceToPurse = offsetToPurse.Length();
            Vector2 force = (offsetToPurse.SafeNormalize(Vector2.Zero) / distanceToPurse * 180f).ClampLength(0f, 2.4f) * suctionPower;
            npc.velocity = (npc.velocity + force).ClampLength(0f, 22f);

            GlobalNPCEventHandlers.GetValueRef<int>(npc, SuctionPurseIndexName).Value = Projectile.whoAmI;
            GlobalNPCEventHandlers.GetValueRef<float>(npc, SuctionDistanceVariableName).Value = distanceToPurse;

            if (npc.WithinRange(Projectile.Center, 30f))
                purse.KidnapNPC(npc);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        RenderPurse(lightColor);
        RenderSuctionVisual();

        return false;
    }

    /// <summary>
    /// Renders the purse.
    /// </summary>
    public void RenderPurse(Color lightColor)
    {
        Texture2D purse = TextureAssets.Projectile[Type].Value;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;
        Main.EntitySpriteDraw(purse, drawPosition, null, Projectile.GetAlpha(lightColor), Projectile.rotation, purse.Size() * 0.5f, Projectile.scale, Projectile.spriteDirection.ToSpriteDirection());
    }

    /// <summary>
    /// Renders the suction visual of this purse.
    /// </summary>
    public void RenderSuctionVisual()
    {
        GraphicsDevice gd = Main.instance.GraphicsDevice;

        Vector2 suctionCenter = Projectile.Center + Vector2.UnitY.RotatedBy(Projectile.rotation) * 2f + Vector2.UnitY * Projectile.gfxOffY;

        float rotationSwerve = Projectile.velocity.AngleBetween(IdealDirection.ToRotationVector2()) * RotationDirection * 1.3f;
        if (float.IsNaN(rotationSwerve))
            rotationSwerve = 0f;

        Matrix rotation = Matrix.CreateRotationZ(Projectile.velocity.ToRotation() + PiOver2);
        Matrix scale = Matrix.CreateTranslation(0f, 0.5f, 0f) * Matrix.CreateScale(SuctionWidth, -SuctionReach, 1f) * rotation * Matrix.CreateTranslation(0f, -0.5f, 0f);
        Matrix world = Matrix.CreateTranslation(suctionCenter.X - Main.screenPosition.X, suctionCenter.Y - Main.screenPosition.Y, 0f);
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -SuctionWidth, SuctionWidth);

        gd.RasterizerState = RasterizerState.CullNone;

        ManagedShader suctionShader = ShaderManager.GetShader("NoxusBoss.PerversePurseSuctionShader");
        suctionShader.TrySetParameter("uWorldViewProjection", scale * world * Main.GameViewMatrix.TransformationMatrix * projection);
        suctionShader.TrySetParameter("localTime", Time / 60f);
        suctionShader.TrySetParameter("vanishInterpolant", (1f - SuctionGrowthInterpolant) * 0.5f);
        suctionShader.TrySetParameter("rotationSwerve", rotationSwerve);
        suctionShader.TrySetParameter("pixelationDetail", Vector2.One * 105f);
        suctionShader.TrySetParameter("minorSuctionColor", new Vector3(0.7f, 0.04f, 0.18f));
        suctionShader.TrySetParameter("majorSuctionColor", new Vector3(0.27f, 0f, 0.5f));
        suctionShader.SetTexture(PerlinNoise, 1, SamplerState.PointWrap);
        suctionShader.Apply();

        gd.SetVertexBuffer(MeshRegistry.CylinderVertices);
        gd.Indices = MeshRegistry.CylinderIndices;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, MeshRegistry.CylinderVertices.VertexCount, 0, MeshRegistry.CylinderIndices.IndexCount / 3);

        gd.SetVertexBuffer(null);
        gd.Indices = null;
    }

    public override bool? CanDamage() => false;
}
