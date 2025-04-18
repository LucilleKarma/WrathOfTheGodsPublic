using CalamityMod;
using Luminance.Assets;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.CeaselessVoid;

public class CeaselessVoidRift : ModNPC
{
    #region Initialization

    /// <summary>
    /// How long this rift has existed for, in frames.
    /// </summary>
    public ref float Time => ref NPC.ai[1];

    /// <summary>
    /// Whether the mouse is hovering over this rift.
    /// </summary>
    public bool MouseOverSelf => Vector2.Transform(Main.MouseScreen, Matrix.Invert(Main.GameViewMatrix.TransformationMatrix)).WithinRange(NPC.Center - Main.screenPosition, NPC.Size.Length() * NPC.scale * 0.24f);

    /// <summary>
    /// Whether the player is currently interacting with the rift with their mouse.
    /// </summary>
    public bool InteractingWithRift => MouseOverSelf && Main.LocalPlayer.WithinRange(NPC.Center, NPC.Size.Length() * NPC.scale * 0.45f) && Time >= 120f && !Main.mapFullscreen;

    /// <summary>
    /// Whether this rift can be entered.
    /// </summary>
    public static bool CanEnterRift => ModContent.GetInstance<AntiseedEvent>().Finished && !AnyBosses();

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.TrailingMode[NPC.type] = 3;
        NPCID.Sets.TrailCacheLength[NPC.type] = 50;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        EmptinessSprayer.NPCsToNotDelete[Type] = true;

        // Apply miracleblight immunities.
        CalamityCompatibility.MakeImmuneToMiracleblight(NPC);

        this.ExcludeFromBestiary();
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 0f;
        NPC.damage = 0;
        NPC.width = 432;
        NPC.height = 496;
        NPC.defense = 0;
        NPC.lifeMax = 75000;
        NPC.aiStyle = -1;
        NPC.knockBackResist = 0f;
        NPC.canGhostHeal = false;
        NPC.dontTakeDamage = true;
        NPC.ShowNameOnHover = false;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.HitSound = null;
        NPC.DeathSound = null;
        NPC.value = 0;
        NPC.netAlways = true;
        NPC.friendly = true;
        NPC.hide = true;
        AIType = -1;
    }

    public override bool NeedSaving() => true;

    #endregion Initialization

    #region AI
    public override void AI()
    {
        Time++;

        float growInterpolant = InverseLerp(0f, 105f, Time);
        float elasticScale = EasingCurves.Elastic.Evaluate(EasingType.InOut, growInterpolant);
        elasticScale *= InverseLerp(-0.35f, 0f, elasticScale);

        NPC.scale = elasticScale * 0.5f + InverseLerp(0f, 10f, Time) * 0.6f;
        NPC.timeLeft = 7200;

        if (InteractingWithRift && CanEnterRift && Main.mouseRight && Main.mouseRightRelease)
        {
            if (AvatarUniverseExplorationSystem.InAvatarUniverse)
                AvatarUniverseExplorationSystem.Exit();
            else
                AvatarUniverseExplorationSystem.Enter();
        }

        if (AvatarUniverseExplorationSystem.EnteringRift)
        {
            float suckIntensity = AvatarRiftSuckVisualsManager.ZoomInInterpolant * InverseLerp(1720f, 300f, Main.LocalPlayer.Distance(NPC.Center));
            Main.LocalPlayer.Center = Vector2.Lerp(Main.LocalPlayer.Center, NPC.Center, suckIntensity * 0.4f);
            Main.LocalPlayer.velocity *= 1f - suckIntensity * 0.56f;
            Main.LocalPlayer.mount?.Dismount(Main.LocalPlayer);
        }

        if (CalamityCompatibility.Enabled)
            DisableProximityRage(NPC);
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    private static void DisableProximityRage(NPC npc)
    {
        npc.Calamity().ProvidesProximityRage = false;
    }

    #endregion AI

    #region Drawing

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Main.spriteBatch.PrepareForShaders();

        Texture2D innerRiftTexture = GennedAssets.Textures.FirstPhaseForm.RiftInnerTexture.Value;
        float squishX = (1f - NPC.scale) * 0.25f;
        float squishY = squishX * -0.67f;
        Vector2 textureArea = NPC.Size * new Vector2(1f + squishX, 1f + squishY) * NPC.scale;

        ManagedShader riftShader = ShaderManager.GetShader("NoxusBoss.CeaselessVoidRiftShader");
        riftShader.TrySetParameter("textureSize", innerRiftTexture.Size());
        riftShader.TrySetParameter("center", Vector2.One * 0.5f);
        riftShader.TrySetParameter("darkeningRadius", 0.2f);
        riftShader.TrySetParameter("pitchBlackRadius", 0.13f);
        riftShader.TrySetParameter("brightColorReplacement", new Vector3(1f, 0f, 0.2f));
        riftShader.TrySetParameter("time", Time / 120f);
        riftShader.TrySetParameter("erasureInterpolant", InverseLerp(0.7f, 0f, NPC.scale));
        riftShader.TrySetParameter("redEdgeBuffer", 0.05f);
        riftShader.SetTexture(innerRiftTexture, 1, SamplerState.LinearWrap);
        riftShader.SetTexture(PerlinNoise, 2, SamplerState.LinearWrap);
        riftShader.Apply();

        Color color = AvatarUniverseExplorationSystem.InAvatarUniverse ? new Color(80, 80, 80) : Color.White;
        Main.spriteBatch.Draw(innerRiftTexture, NPC.Center - screenPos, null, NPC.GetAlpha(color), 0f, innerRiftTexture.Size() * 0.5f, textureArea / innerRiftTexture.Size(), 0, 0f);

        Main.spriteBatch.ResetToDefault();

        if (AvatarOfEmptiness.Myself is null && AvatarRift.Myself is null && (Main.screenPosition + Main.ScreenSize.ToVector2() * 0.5f).WithinRange(NPC.Center, 4000f))
        {
            ManagedScreenFilter spaghettificationShader = ShaderManager.GetFilter("NoxusBoss.AvatarRiftSpaghettificationShader");
            spaghettificationShader.TrySetParameter("distortionRadius", NPC.scale * 180f);
            spaghettificationShader.TrySetParameter("distortionIntensity", NPC.scale * 0.7f);
            spaghettificationShader.TrySetParameter("distortionPosition", Vector2.Transform(NPC.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
            spaghettificationShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom.X);
            spaghettificationShader.SetTexture(AvatarRiftTargetManager.AvatarRiftTarget, 1, SamplerState.LinearClamp);
            spaghettificationShader.Activate();
        }

        return false;
    }

    #endregion Drawing

    #region Gotta Manually Disable Despawning Lmao

    // Disable natural despawning for the Rift.
    public override bool CheckActive() => false;

    #endregion Gotta Manually Disable Despawning Lmao
}
