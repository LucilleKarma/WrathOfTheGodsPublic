using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;

using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.SolynDialogue;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;

using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    /// <summary>
    /// Whether Mars has been hit by Solyn and the player's tag team beam during the teaching mechanic state.
    /// </summary>
    public bool TeachPlayerAboutTeamAttack_HitByBeam
    {
        get => NPC.ai[0] == 1f;
        set => NPC.ai[0] = value.ToInt();
    }

    /// <summary>
    /// How long Mars spends flying off during his teaching mechanic state.
    /// </summary>
    public static int TeachPlayerAboutTeamAttack_MarsFlyOffTime => GetAIInt("TeachPlayerAboutTeamAttack_MarsFlyOffTime");

    /// <summary>
    /// The force factor which dictates how much Mars is pushed back by the initial tag team beam during the teaching mechanic state.
    /// </summary>
    public static float TeachPlayerAboutTeamAttack_LaserPushForce => GetAIFloat("TeachPlayerAboutTeamAttack_LaserPushForce");

    [AutomatedMethodInvoke]
    public void LoadState_TeachPlayerAboutTeamAttack()
    {
        StateMachine.RegisterTransition(MarsAIType.TeachPlayerAboutTeamAttack, MarsAIType.ResetCycle, false, () =>
        {
            return TeachPlayerAboutTeamAttack_HitByBeam && AITimer >= 90;
        });
        StateMachine.RegisterStateBehavior(MarsAIType.TeachPlayerAboutTeamAttack, DoBehavior_TeachPlayerAboutTeamAttack);

        TeamBeamHitEffectEvent += OnHitByTeamBeam_TeachPlayerAboutTeamAttack;
        GameSceneSlowdownSystem.RegisterConditionalEffect(true, DoBehavior_TeachPlayerAboutTeamAttack_CalculateSlowdown, DoBehavior_TeachPlayerAboutTeamAttack_CalculateSlowdown, () =>
        {
            if (Myself is null)
                return false;

            if (Myself.As<MarsBody>().StateMachine.StateStack.Count <= 0)
                return false;

            if (Myself.As<MarsBody>().CurrentState != MarsAIType.TeachPlayerAboutTeamAttack)
                return false;

            return true;
        });
    }

    private static float DoBehavior_TeachPlayerAboutTeamAttack_CalculateSlowdown()
    {
        if (Myself is null)
            return 0f;

        if (Myself.As<MarsBody>().TeachPlayerAboutTeamAttack_HitByBeam)
            return 0f;

        if (Myself.As<MarsBody>().AITimer >= TeachPlayerAboutTeamAttack_MarsFlyOffTime)
            return InverseLerp(980f, 540f, Myself.Distance(Main.player[Myself.target].Center));

        return 0f;
    }

    private void OnHitByTeamBeam_TeachPlayerAboutTeamAttack(Projectile beam)
    {
        if (CurrentState != MarsAIType.TeachPlayerAboutTeamAttack)
            return;

        if (!TeachPlayerAboutTeamAttack_HitByBeam)
        {
            TeachPlayerAboutTeamAttack_HitByBeam = true;
            AITimer = 0;
        }

        Player caster = Main.player[beam.owner];
        float antiSpacePush = InverseLerp(900f, 2700f, NPC.Center.Y);
        NPC.velocity += caster.SafeDirectionTo(NPC.Center) * beam.localNPCHitCooldown * TeachPlayerAboutTeamAttack_LaserPushForce * antiSpacePush;
        NPC.netUpdate = true;
    }

    /// <summary>
    /// Performs Mars' spawn animation state, making him appear with attached wires before autonomously activating.
    /// </summary>
    public void DoBehavior_TeachPlayerAboutTeamAttack()
    {
        SolynAction = DoBehavior_TeachPlayerAboutTeamAttack_Solyn;

        float idealRotation = (NPC.position.X - NPC.oldPosition.X) * 0.008f;

        // Only take damage if the player is using a beam
        NPC.dontTakeDamage = Target.ownedProjectileCounts[ModContent.ProjectileType<SolynTagTeamBeam>()] <= 0;

        if (TeachPlayerAboutTeamAttack_HitByBeam)
        {
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center) * 5f, 0.156f);

            idealRotation = Sin01(NPC.velocity.X * 0.22f + AITimer / 9f) * NPC.velocity.X.NonZeroSign() * InverseLerp(5f, 10f, NPC.velocity.Length()) * 0.9f;
            RailgunCannonAngle = idealRotation * 3f;
            EnergyCannonAngle = idealRotation * 3f;
        }
        else if (AITimer <= TeachPlayerAboutTeamAttack_MarsFlyOffTime)
        {
            NPC.velocity.X -= NPC.HorizontalDirectionTo(Target.Center) * 1.2f;
            NPC.velocity.Y -= 0.2f;
            RailgunCannonAngle = RailgunCannonAngle.AngleLerp(leftElbowPosition.AngleTo(LeftHandPosition) - PiOver2, 0.3f);
            EnergyCannonAngle = EnergyCannonAngle.AngleLerp(rightElbowPosition.AngleTo(RightHandPosition), 0.3f);
        }
        else
        {
            int relativeTimer = AITimer - TeachPlayerAboutTeamAttack_MarsFlyOffTime;
            NPC.Center = Vector2.Lerp(NPC.Center, Target.Center, (1f - GameSceneSlowdownSystem.SlowdownInterpolant) * 0.02f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center) * 34f, 0.095f);

            RailgunCannonAngle = RailgunCannonAngle.AngleLerp(NPC.velocity.ToRotation(), 0.3f);
            EnergyCannonAngle = EnergyCannonAngle.AngleLerp(NPC.velocity.ToRotation(), 0.3f);
        }

        // Zoom in based on how slowed the game is.
        CameraPanSystem.ZoomIn(SmoothStep(0f, 0.37f, GameSceneSlowdownSystem.SlowdownInterpolant));

        // Don't give the player rippers during the game mechanic tutorial, please.
        foreach (Player player in Main.ActivePlayers)
            CalamityCompatibility.ResetRippers(player);

        if (NPC.velocity.X > 0f)
            MoveArmsTowards(new(72f, 120f), new(400f, 10f));
        else
            MoveArmsTowards(new(-400f, 10f), new(-72f, 120f));

        EnergyCannonChainsawActive = true;
        NPC.rotation = NPC.rotation.AngleLerp(idealRotation, 0.4f);
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for the team attack teaching state.
    /// </summary>
    public void DoBehavior_TeachPlayerAboutTeamAttack_Solyn(BattleSolyn solyn)
    {
        if (solyn.IsMultiplayerClone)
        {
            return;
        }

        NPC solynNPC = solyn.NPC;
        Vector2 lookDestination = solyn.Player.Center;
        Vector2 hoverDestination = solyn.Player.Center + new Vector2(solyn.Player.direction * -30f, -50f);

        solynNPC.Center = Vector2.Lerp(solynNPC.Center, hoverDestination, 0.1f);
        solynNPC.SmoothFlyNear(hoverDestination, 0.27f, 0.6f);

        solyn.UseStarFlyEffects();
        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);

        if (AITimer == TeachPlayerAboutTeamAttack_MarsFlyOffTime + 75)
        {
            SolynWorldDialogueManager.CreateNew(Language.GetTextValue("Mods.NoxusBoss.Dialog.SolynUseMouseButtons"), -solynNPC.spriteDirection, solynNPC.Top - Vector2.UnitY * 32f, 120, true);

            SolynAndPlayerCanDoTeamAttack = true;
            NPC.netUpdate = true;
        }

        if (AITimer % 900 == 899)
            SolynWorldDialogueManager.CreateNew(Language.GetTextValue("Mods.NoxusBoss.Dialog.SolynUseMouseButtons"), -solynNPC.spriteDirection, solynNPC.Top - Vector2.UnitY * 32f, 120, true);

        HandleSolynPlayerTeamAttack(solyn);
    }

    /// <summary>
    /// Whether Solyn and a given player are charging up energy.
    /// </summary>
    public static bool SolynEnergyBeamIsCharging(Player player)
    {
        return player.ownedProjectileCounts[ModContent.ProjectileType<SolynTagTeamBeam>()] >= 1 || player.ownedProjectileCounts[ModContent.ProjectileType<SolynTagTeamChargeUp>()] >= 1;
    }

    /// <summary>
    /// Handles team attack interactions with the player and Solyn.
    /// </summary>
    public void HandleSolynPlayerTeamAttack(BattleSolyn solyn)
    {
        if (solyn.IsMultiplayerClone)
        {
            return;
        }

        if (!SolynAndPlayerCanDoTeamAttack)
        {
            ResetSolynPlayerTeamAttackTimers();
            return;
        }

        Player player = solyn.Player;
        int beamID = ModContent.ProjectileType<SolynTagTeamBeam>();
        if (SolynEnergyBeamIsCharging(player))
        {
            Vector2 hoverDestination = player.Center + (TagTeamBeamDirection + PiOver2).ToRotationVector2() * 67f;
            solyn.NPC.spriteDirection = Cos(TagTeamBeamDirection).NonZeroSign();
            solyn.NPC.SmoothFlyNear(hoverDestination, 0.4f, 0.6f);
            solyn.Frame = 44f;

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, TagTeamBeamDirection - PiOver2);
            player.direction = solyn.NPC.spriteDirection;

            float idealRotation = TagTeamBeamDirection;
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.owner != player.whoAmI || projectile.type != beamID)
                    continue;

                idealRotation = projectile.velocity.ToRotation();
                break;
            }

            if (solyn.NPC.spriteDirection == -1)
                idealRotation += Pi;

            solyn.NPC.rotation = idealRotation.AngleLerp(0f, 0.45f);

            int attackTimer = SolynPlayerTeamAttackTimer;
            if (attackTimer <= 0 || attackTimer >= 19)
            {
                player.eyeHelper.BlinkBecausePlayerGotHurt();
                solyn.Frame = 45f;
            }
        }

        // Let the beam simply exist until it dies if one is present, rather than having another one be charged up
        if (player.ownedProjectileCounts[beamID] >= 1)
        {
            ResetSolynPlayerTeamAttackTimers();
            return;
        }

        if (Main.myPlayer == player.whoAmI)
        {
            // Handle beam charge up effects.
            if (Main.mouseRight && Main.mouseLeft)
            {
                SolynPlayerTeamAttackTimer++;

                //Tell server and other clients that we are charging beam
                if (SolynPlayerTeamAttackTimer == 1)
                {
                    PacketManager.SendPacket<MarsBeamChargePacket>();
                }

                // Charge up the beam with a custom visual.
                if (SolynPlayerTeamAttackTimer == 2)
                {
                    TagTeamBeamDirection = player.AngleTo(Main.MouseWorld);
                    NewProjectileBetter(player.GetSource_FromThis(), player.Center, TagTeamBeamDirection.ToRotationVector2(), ModContent.ProjectileType<SolynTagTeamChargeUp>(), 0, 0f, solyn.MultiplayerIndex, solyn.NPC.whoAmI);
                }

                // Fire the laser when ready.
                if (SolynPlayerTeamAttackTimer >= SolynTagTeamChargeUp.Lifetime)
                {
                    ResetSolynPlayerTeamAttackTimers();

                    // This looks stupid. And I gotta say, it is.
                    // But due to a non-defensive attempt by the PetsOverhaulCalamityAddon mod to access item data from projectile sources, this is necessary for compatibility with that mod in this fight.
                    // They make the mistaken assumption that damage to NPCs is all from player items and not potentially from miscellaneous/neutral sources for some reason, and I'm accomodating this via this
                    // scuffed solution (and making a PR to their repo shortly after).
                    // It doesn't really affect things otherwise it seems, so all is good.
                    EntitySource_ItemUse source = new EntitySource_ItemUse(player, new Item());
                    Projectile.NewProjectile(source, player.Center, TagTeamBeamDirection.ToRotationVector2(), beamID, TagTeamBeamBaseDamage, 0f, solyn.MultiplayerIndex, solyn.NPC.whoAmI);
                }

                player.channel = true;
                player.itemAnimation = 0;
                player.itemTime = 0;
            }
            else
            {
                ResetSolynPlayerTeamAttackTimers();
            }

            return;
        }

        if (SolynPlayerTeamAttackTimer > 0)
        {
            SolynPlayerTeamAttackTimer++;
        }
    }
}
