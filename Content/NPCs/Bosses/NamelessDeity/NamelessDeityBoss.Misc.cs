using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    #region Multiplayer Syncs

    public override void SendExtraAI(BinaryWriter writer)
    {
        BitsByte flags = new BitsByte()
        {
            [0] = StarShouldBeHeldByLeftHand,
            [1] = WaitingForPhase2Transition,
            [2] = WaitingForDeathAnimation,
            [3] = HasExperiencedFinalAttack,
            [4] = TargetIsUsingRodOfHarmony,
            [5] = ShouldStartTeleportAnimation,
            [6] = DrawHandsSeparateFromRT
        };
        writer.Write(flags);

        writer.Write(CurrentPhase);
        writer.Write(SwordSlashCounter);
        writer.Write(SwordSlashDirection);
        writer.Write(SwordAnimationTimer);
        writer.Write(TeleportInTime);
        writer.Write(TeleportOutTime);

        writer.Write(DifficultyFactor);
        writer.Write(PunchOffsetAngle);
        writer.Write(FightTimer);
        writer.Write(ZPosition);
        writer.Write(NamelessDeitySky.HeavenlyBackgroundIntensity);

        writer.WriteVector2(GeneralHoverOffset);
        writer.WriteVector2(LightSlashPosition);
        writer.WriteVector2(CensorPosition);
        writer.WriteVector2(PunchDestination);
        writer.WriteVector2(PreviousPunchImpactPosition);

        // Write lists.
        writer.Write(Hands.Count);
        for (int i = 0; i < Hands.Count; i++)
            Hands[i].WriteTo(writer);

        writer.Write(StarSpawnOffsets.Count);
        for (int i = 0; i < StarSpawnOffsets.Count; i++)
            writer.WriteVector2(StarSpawnOffsets[i]);

        // Write state data.
        List<EntityAIState<NamelessAIType>> stateStack = (StateMachine?.StateStack ?? new Stack<EntityAIState<NamelessAIType>>()).ToList();
        writer.Write(stateStack.Count);
        for (int i = stateStack.Count - 1; i >= 0; i--)
        {
            writer.Write(stateStack[i].Time);
            writer.Write((byte)stateStack[i].Identifier);
        }
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        BitsByte flags = reader.ReadByte();
        StarShouldBeHeldByLeftHand = flags[0];
        WaitingForPhase2Transition = flags[1];
        WaitingForDeathAnimation = flags[2];
        HasExperiencedFinalAttack = flags[3];
        TargetIsUsingRodOfHarmony = flags[4];
        ShouldStartTeleportAnimation = flags[5];
        DrawHandsSeparateFromRT = flags[6];

        CurrentPhase = reader.ReadInt32();
        SwordSlashCounter = reader.ReadInt32();
        SwordSlashDirection = reader.ReadInt32();
        SwordAnimationTimer = reader.ReadInt32();
        TeleportInTime = reader.ReadInt32();
        TeleportOutTime = reader.ReadInt32();

        DifficultyFactor = reader.ReadSingle();
        PunchOffsetAngle = reader.ReadSingle();
        FightTimer = reader.ReadInt32();
        ZPosition = reader.ReadSingle();
        NamelessDeitySky.HeavenlyBackgroundIntensity = reader.ReadSingle();

        GeneralHoverOffset = reader.ReadVector2();
        LightSlashPosition = reader.ReadVector2();
        CensorPosition = reader.ReadVector2();
        PunchDestination = reader.ReadVector2();
        PreviousPunchImpactPosition = reader.ReadVector2();

        // Read lists.
        Hands.Clear();
        StarSpawnOffsets.Clear();
        StateMachine.StateStack.Clear();

        int handCount = reader.ReadInt32();
        for (int i = 0; i < handCount; i++)
            Hands.Add(NamelessDeityHand.ReadFrom(reader));

        int starOffsetCount = reader.ReadInt32();
        for (int i = 0; i < starOffsetCount; i++)
            StarSpawnOffsets.Add(reader.ReadVector2());

        // Read state data.
        StateMachine.StateStack.Clear();
        int stateStackCount = reader.ReadInt32();
        for (int i = 0; i < stateStackCount; i++)
        {
            int time = reader.ReadInt32();
            byte stateType = reader.ReadByte();
            StateMachine.StateStack.Push(StateMachine.StateRegistry[(NamelessAIType)stateType]);
            StateMachine.StateRegistry[(NamelessAIType)stateType].Time = time;
        }
    }

    #endregion Multiplayer Syncs

    #region Hit Effects

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (NPC.soundDelay >= 1)
            return;

        NPC.soundDelay = 12;

        if (Main.zenithWorld)
            SoundEngine.PlaySound(GennedAssets.Sounds.NPCHit.NamelessDeityGFBHurt with { PitchVariance = 0.32f, Volume = 0.67f, MaxInstances = 50 }, NPC.Center);
        else
            SoundEngine.PlaySound(GennedAssets.Sounds.NPCHit.NamelessDeityHurt with { PitchVariance = 0.32f, Volume = 0.67f }, NPC.Center);
    }

    public override bool CheckDead()
    {
        // Disallow natural death. The time check here is as a way of catching cases where multiple hits happen on the same frame and trigger a death.
        // If it just checked the attack state, then hit one would trigger the state change, set the HP to one, and the second hit would then deplete the
        // single HP and prematurely kill Nameless.
        if (CurrentState == NamelessAIType.DeathAnimation && AITimer >= 10f)
            return true;

        // Keep Nameless' HP at its minimum.
        NPC.life = 1;

        if (!WaitingForDeathAnimation)
        {
            WaitingForDeathAnimation = true;
            NPC.dontTakeDamage = true;
            NPC.netUpdate = true;
        }
        return false;
    }

    // Ensure that Nameless' contact damage adheres to the special boss-specific cooldown slot, to prevent things like lava cheese.
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        // This is quite scuffed, but since there's no equivalent easy Colliding hook for NPCs, it is necessary to increase Nameless' "effective hitbox" to an extreme
        // size via a detour and then use the CanHitPlayer hook to selectively choose whether the target should be inflicted damage or not (in this case, based on hands that can do damage).
        // This is because NPC collisions are fundamentally based on rectangle intersections. CanHitPlayer does not allow for the negation of that. But by increasing the hitbox by such an
        // extreme amount that that check is always passed, this issue is mitigated. Again, scuffed, but the onus is on TML to make this easier for modders to do.
        if (Hands.Where(h => h.CanDoDamage).Any())
            return Hands.Where(h => h.CanDoDamage).Any(h => Utils.CenteredRectangle(h.FreeCenter, TeleportVisualsAdjustedScale * 106f).Intersects(target.Hitbox));

        return CurrentState == NamelessAIType.SwordConstellation && NPC.ai[2] == 1f;
    }

    private void ExpandEffectiveHitboxForHands(On_NPC.orig_GetMeleeCollisionData orig, Rectangle victimHitbox, int enemyIndex, ref int specialHitSetter, ref float damageMultiplier, ref Rectangle npcRect)
    {
        orig(victimHitbox, enemyIndex, ref specialHitSetter, ref damageMultiplier, ref npcRect);

        // See the big comment in CanHitPlayer.
        if (Main.npc[enemyIndex].type == Type && Main.npc[enemyIndex].As<NamelessDeityBoss>().CurrentState == NamelessAIType.RealityTearPunches)
            npcRect.Inflate(4000, 4000);
    }

    // Timed DR but a bit different. I'm typically very, very reluctant towards this mechanic, but given that this boss exists in shadowspec tier, I am willing to make
    // an exception. This will not cause the dumb "lol do 0 damage for 30 seconds" problems that Calamity had in the past.
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        // Calculate how far ahead Nameless' life ratio is relative to how long he's existed so far.
        // This value is clamped to ensure that the value does not dip below 0 if Nameless actually has more HP than expected.
        float desiredLifeRatio = 1f - InverseLerp(0f, IdealFightDuration, FightTimer);
        float aheadLifeRatioInterpolant = Saturate((desiredLifeRatio - LifeRatio) * 2f);

        // Modify the ahead life ratio interpolant to a given sharpness. This makes the effects of timed DR come on more strongly the more the player has exceeded.
        float damageReductionInterpolant = Pow(aheadLifeRatioInterpolant, 1f / TimedDRSharpness);

        float damageReductionFactor = Lerp(1f, 1f - MaxTimedDRDamageReduction, damageReductionInterpolant);
        modifiers.FinalDamage *= damageReductionFactor;
    }

    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        if (projectile.ModProjectile is not null and IProjOwnedByBoss<NamelessDeityBoss>)
            return false;
        if (projectile.ModProjectile is not null and IProjOwnedByBoss<AvatarOfEmptiness>)
            return true;

        return null;
    }

    public override bool CanBeHitByNPC(NPC attacker)
    {
        return attacker.type == ModContent.NPCType<AvatarOfEmptiness>();
    }

    #endregion Hit Effects

    #region Gotta Manually Disable Despawning Lmao

    // Disable natural despawning for Nameless.
    public override bool CheckActive() => false;

    #endregion Gotta Manually Disable Despawning Lmao
}
