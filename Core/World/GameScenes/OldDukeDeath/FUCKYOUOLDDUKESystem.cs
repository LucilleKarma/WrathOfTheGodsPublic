using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.CrossCompatibility.Inbound.Infernum;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.OldDukeDeath;

public class FUCKYOUOLDDUKESystem : ModSystem
{
    /// <summary>
    /// The NPC ID of Old Duke.
    /// </summary>
    public static int OldDukeID
    {
        get;
        private set;
    } = -1000;

    /// <summary>
    /// How long it takes for the Avatar to be summoned.
    /// </summary>
    public static int RiftSummonDelay => 19;

    /// <summary>
    /// How long it takes for the Old Duke to be taken into the Avatar's rift.
    /// </summary>
    public static int AttackDelay => 218;

    /// <summary>
    /// How long the Avatar spends attempting to shove the Old Duke into his rift.
    /// </summary>
    public static int AvatarAttackTime => 66;

    /// <summary>
    /// How long it takes for Old Duke's loot (and corpse) to drop down after the Avatar disappears.
    /// </summary>
    public static int LootDelay => 220;

    public override void OnModLoad()
    {
        // Prepare the override to Old Duke's AI. Instead of dashing at the player, releasing tooth balls, creating sharks, or otherwise living a fulfilling life, he fucking dies immediately.
        GlobalNPCEventHandlers.PreAIEvent += KillOldDukeWrapper;
    }

    public override void PostSetupContent()
    {
        // Store Old Duke's ID.
        if (ModContent.TryFind(CalamityCompatibility.ModName, "OldDuke", out ModNPC oldDuke))
            OldDukeID = oldDuke.Type;
    }

    private bool KillOldDukeWrapper(NPC npc)
    {
        // Just don't bother.
        if (InfernumCompatibilitySystem.InfernumModeIsActive)
            return true;

        // Override Old Duke's AI with a dedicated wrapper method.
        if (npc.type == OldDukeID && !BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() && !WorldSaveSystem.AvatarHasKilledOldDuke)
        {
            DoBehavior_OldDukeAI(npc);
            return false;
        }

        return true;
    }

    public static void DoBehavior_OldDukeAI(NPC npc)
    {
        // Disable damage.
        npc.dontTakeDamage = true;
        npc.damage = 0;

        // Rotate forward.
        npc.rotation = npc.rotation.AngleTowards(0f, 0.06f);

        // Decide a target.
        npc.TargetClosest();
        Player target = Main.player[npc.target];

        ref float aiTimer = ref npc.ai[2];

        // Increment the AI timer.
        aiTimer++;

        switch ((int)npc.ai[0])
        {
            // Die.
            case 0:
            case 2:
                // Slow down.
                npc.velocity *= 0.91f;

                if (aiTimer <= 2f)
                    npc.velocity = Vector2.UnitY * -10f;

                // Look at the player.
                int direction = (target.Center.X - npc.Center.X).NonZeroSign();
                npc.direction = direction;
                npc.spriteDirection = -npc.direction;

                // Move the camera towards the Old Duke.
                CalamityCompatibility.ResetStealthBarOpacity(Main.LocalPlayer);
                float cameraZoomInterpolant = InverseLerp(0f, 11f, aiTimer);
                CameraPanSystem.PanTowards(npc.Center, cameraZoomInterpolant);

                // Summon the Avatar's rift.
                if (Main.netMode != NetmodeID.MultiplayerClient && aiTimer == RiftSummonDelay)
                    NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y + 200, ModContent.NPCType<AvatarRift>(), npc.whoAmI, (int)AvatarRift.RiftAttackType.KillOldDuke);

                // Use open mouth frames.
                if (aiTimer >= RiftSummonDelay + AttackDelay - 32f)
                    npc.ai[0] = 2f;

                if (aiTimer == RiftSummonDelay + AttackDelay - 33f)
                {
                    npc.velocity = npc.SafeDirectionTo(target.Center) * 5f;
                    SoundEngine.PlaySound(GennedAssets.Sounds.Custom.AvatarOldDukeDeath with { Volume = 2f });
                    ScreenShakeSystem.StartShakeAtPoint(npc.Center, 5f);
                }

                // Disappear as the Avatar violently attacks the Old Duke.
                if (aiTimer >= RiftSummonDelay + AttackDelay)
                {
                    if (Main.rand.NextBool(10))
                        ScreenShakeSystem.StartShakeAtPoint(npc.Center, 7.5f);

                    // Shrink and disappear.
                    npc.scale -= 0.12f;
                    if (Main.netMode != NetmodeID.MultiplayerClient && npc.scale <= 0f)
                    {
                        npc.active = false;
                        WorldSaveSystem.AvatarHasKilledOldDuke = true;

                        NetMessage.SendData(MessageID.WorldData);
                    }
                }

                break;
        }
    }
}
