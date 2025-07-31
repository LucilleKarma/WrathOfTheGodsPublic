using Luminance.Common.StateMachines;

using Microsoft.Xna.Framework;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Balancing;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;

using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// How long it takes for the Avatar, at most, to bring player to the Avatar's universe.
    /// </summary>
    public static int SendPlayerToMyUniverse_MaximumSuckTime => GetAIInt("SendPlayerToMyUniverse_MaximumSuckTime");

    [AutomatedMethodInvoke]
    public void LoadState_SendPlayerToMyUniverse()
    {
        StateMachine.RegisterTransition(AvatarAIType.SendPlayerToMyUniverse, null, false, () =>
        {
            return AITimer >= SendPlayerToMyUniverse_MaximumSuckTime;
        }, () =>
        {
            if (AvatarOfEmptinessSky.Dimension is null)
                TargetPositionBeforeDimensionShift = Target.Center;
            if (NeedsToSelectNewDimensionAttacksSoon)
            {
                ResetCycle();
                NeedsToSelectNewDimensionAttacksSoon = false;
            }
            ZPosition = 0f;
            NPC.Opacity = 1f;
            NeckAppearInterpolant = 1f;
            LeftFrontArmScale = 1f;
            RightFrontArmScale = 1f;
            LeftFrontArmOpacity = 1f;
            RightFrontArmOpacity = LeftFrontArmOpacity;
            LilyScale = NeckAppearInterpolant.Squared();
            LegScale = Vector2.One * NeckAppearInterpolant;
            HeadOpacity = 1f;

            foreach (Player player in Main.ActivePlayers)
            {
                player.Center = new Vector2(Main.maxTilesX, Main.maxTilesY) * 8f;
                if (player.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName))
                {
                    player.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value = false;
                    PacketManager.SendPacket<PlayerAvatarRiftStatePacket>(player.whoAmI);
                }

                if (Main.netMode == NetmodeID.Server)
                    PacketManager.SendPacket<TeleportPlayerPacket>(player.whoAmI);
            }
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(AvatarAIType.SendPlayerToMyUniverse, DoBehavior_SendPlayerToMyUniverse);

        StatesToNotStartTeleportDuring.Add(AvatarAIType.SendPlayerToMyUniverse);
    }

    public void DoBehavior_SendPlayerToMyUniverse()
    {
        // Reset the crack concentration for later.
        RealityShatter_CrackConcentration = 0f;

        // Reset the distortion.
        IdealDistortionIntensity = 0f;

        // Enter the background.
        HideBar = true;
        NPC.velocity *= 0.9f;
        NPC.dontTakeDamage = true;

        // Enter the portal.
        NeckAppearInterpolant = InverseLerp(25f, 0f, AITimer);
        LeftFrontArmScale = Sqrt(NeckAppearInterpolant);
        RightFrontArmScale = Sqrt(NeckAppearInterpolant);
        LeftFrontArmOpacity = InverseLerp(0f, 0.2f, NeckAppearInterpolant);
        RightFrontArmOpacity = LeftFrontArmOpacity;
        LilyScale = NeckAppearInterpolant.Squared();
        LegScale = Vector2.One * NeckAppearInterpolant;
        HeadOpacity = InverseLerp(0f, 0.4f, NeckAppearInterpolant);
        SuckOpacity = (1f - NeckAppearInterpolant).Cubed();
        PerformStandardLimbUpdates();

        // Disable adrenaline growth, to ensure that the player is not rewarded for fighting against the suck.
        AdrenalineGrowthModificationSystem.AdrenalineYieldFactor = 0f;

        float suckPower = InverseLerp(0f, 0.65f, AITimer / (float)SendPlayerToMyUniverse_MaximumSuckTime) * InverseLerp(0f, 150f, AITimer);
        float suckAcceleration = SmoothStep(0.4f, 4.5f, suckPower);
        float suckDistanceThreshold = 19000f;
        float eventHorizonThreshold = 400f;

        ZPosition = Lerp(ZPosition, suckPower * -0.64f, 0.06f);

        // Move towards the player.
        NPC.Center = NPC.Center.MoveTowards(Target.Center, suckPower * 8f);

        SolynAction = DoBehavior_SendPlayerToMyUniverse_MakeSolynFallIntoRift;

        if (NeckAppearInterpolant > 0f)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (player.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName))
                {
                    player.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value = false;
                    PacketManager.SendPacket<PlayerAvatarRiftStatePacket>(player.whoAmI);
                }
            }

            return;
        }

        // Make players approach the rift based on how far away they are.
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (!Main.player[i].active)
                continue;

            float distanceToPlayer = Main.player[i].Distance(NPC.Center);
            if (distanceToPlayer < suckDistanceThreshold && Main.player[i].grappling[0] == -1)
            {
                // Disable mounts.
                Main.player[i].mount?.Dismount(Main.player[i]);

                float suckDirectionX = (Main.player[i].Center.X < NPC.Center.X).ToDirectionInt();
                float suckDirectionY = (Main.player[i].Center.Y < NPC.Center.Y).ToDirectionInt();
                float eventHorizonInterpolant = InverseLerp(eventHorizonThreshold + 450f, eventHorizonThreshold, Main.player[i].Distance(NPC.Center));
                float distanceTaperOff = 1f - Pow(distanceToPlayer / suckDistanceThreshold, 9f) + eventHorizonInterpolant * 0.9f;

                // Apply suck forces to the player's velocity.
                Main.player[i].velocity.X += suckAcceleration * distanceTaperOff * suckDirectionX;
                Main.player[i].velocity.Y += suckAcceleration * distanceTaperOff * suckDirectionY;
                Main.player[i].velocity = Vector2.Lerp(Main.player[i].velocity, Main.player[i].SafeDirectionTo(NPC.Center) * Main.player[i].velocity.Length(), suckPower * 0.05f);

                // Make the player enter the my universe if hit.
                if (distanceToPlayer <= NPC.width * ZPositionScale * 0.4f && !Main.player[i].GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value)
                {
                    if (Main.myPlayer == i)
                        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftPlayerAbsorb);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Main.player[i].GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value = true;
                        Main.player[i].GetValueRef<float>(AvatarRiftSuckVisualsManager.ZoomInInterpolantName).Value = 0.001f;
                        PacketManager.SendPacket<PlayerAvatarRiftStatePacket>(i);
                    }

                    // Evaluate if all players are in the portal.
                    // If they are, this state can be terminated early.
                    bool allPlayersHaveBeenSuckedUp = Main.player.Where(p => p.active && !p.dead).All(p => p.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName));
                    if (allPlayersHaveBeenSuckedUp)
                    {
                        if (AITimer < SendPlayerToMyUniverse_MaximumSuckTime - 60)
                        {
                            AITimer = SendPlayerToMyUniverse_MaximumSuckTime - 60;
                            NPC.netUpdate = true;
                        }
                    }

                    if (Main.myPlayer == i)
                        GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 1f, 60);
                }

                if (Main.player[i].GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName))
                {
                    Player player = Main.player[i];
                    player.Center = NPC.Center;
                    player.velocity.Y = -0.1f;
                }
            }
        }
    }

    public void DoBehavior_SendPlayerToMyUniverse_MakeSolynFallIntoRift(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;

        if (AITimer == 1)
        {
            solynNPC.Center = solyn.Player.Center;
            solynNPC.netUpdate = true;
        }

        solyn.Frame = 21f;
        solynNPC.velocity = Vector2.Lerp(solynNPC.velocity, solynNPC.SafeDirectionTo(NPC.Center) * 20f, 0.067f);
        solynNPC.scale = InverseLerp(60f, 360f, solynNPC.Distance(NPC.Center));
        solynNPC.spriteDirection = solynNPC.velocity.X.NonZeroSign();
        solynNPC.rotation = solynNPC.AngleTo(NPC.Center) + PiOver2;
        if (solynNPC.spriteDirection == -1)
            solynNPC.rotation += Pi;
    }
}
