using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Enemies.RiftEclipse;

public class DarkHarbinger : ModNPC
{
    #region Fields and Properties

    public bool OnScreen
    {
        get => NPC.localAI[1] == 1f;
        set => NPC.localAI[1] = value.ToInt();
    }

    public ref float Frame => ref NPC.localAI[2];

    public override string Texture => GetAssetPath("Content/NPCs/Enemies/RiftEclipse", Name);

    #endregion Fields and Properties

    #region Initialization

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 7;
        this.ExcludeFromBestiary();
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 10f;
        NPC.damage = 0;
        NPC.width = 42;
        NPC.height = 50;
        NPC.defense = 0;
        NPC.lifeMax = 500;
        NPC.aiStyle = -1;
        AIType = -1;
        NPC.knockBackResist = 0f;
        NPC.noGravity = false;
        NPC.noTileCollide = false;
        NPC.dontCountMe = true;
        NPC.behindTiles = true;
        NPC.dontTakeDamage = true;
    }

    public override void OnSpawn(IEntitySource source)
    {
        ResetRotation();
        NPC.rotation = 0f;
        OnScreen = true;
    }

    #endregion Initialization

    #region AI
    public override void AI()
    {
        Player closest = Main.player[Player.FindClosest(NPC.Center, 1, 1)];

        // Check if the snowman is offscreen.
        // If it is, update it's frame and pose.
        if (!NPC.WithinRange(closest.Center, 1100f))
        {
            if (OnScreen)
            {
                ResetRotation();
                Frame++;
                OnScreen = false;

                // If the last frame was used, disappear without a trace.
                if (Frame >= Main.npcFrameCount[Type])
                    NPC.active = false;
            }
        }
        else
            OnScreen = true;

        // Hide the snowman's name.
        NPC.ShowNameOnHover = false;
    }

    public void ResetRotation()
    {
        float originalRotation = NPC.rotation;

        do
        {
            NPC.rotation = Main.rand.NextFloatDirection() * 0.23f;
            NPC.spriteDirection = NPC.rotation.NonZeroSign();
        }
        while (Distance(NPC.rotation, originalRotation) < 0.2f);
    }
    #endregion AI

    #region Drawing

    public override void FindFrame(int frameHeight)
    {
        NPC.frame.Y = (int)Frame * frameHeight;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = GennedAssets.Textures.RiftEclipse.DarkHarbinger.Value;
        Vector2 drawPosition = NPC.Bottom - screenPos + Vector2.UnitY * NPC.scale * (Frame * 2f + 6f);
        SpriteEffects direction = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        Main.spriteBatch.Draw(texture, drawPosition, NPC.frame, NPC.GetAlpha(drawColor), NPC.rotation, NPC.frame.Size() * new Vector2(0.5f, 1f), NPC.scale, direction, 0f);

        Main.spriteBatch.PrepareForShaders();

        // Prepare the cavity shader.
        ManagedShader cavityShader = ShaderManager.GetShader("NoxusBoss.DarkHarbingerCavityShader");
        cavityShader.SetTexture(LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Items/Dyes/EntropicDyeTexture"), 1, SamplerState.PointWrap);
        cavityShader.Apply();

        // Draw the cavity mask.
        Texture2D shaderMask = GennedAssets.Textures.RiftEclipse.DarkHarbingerShader.Value;
        Main.spriteBatch.Draw(shaderMask, drawPosition, NPC.frame, NPC.GetAlpha(Color.White), NPC.rotation, NPC.frame.Size() * new Vector2(0.5f, 1f), NPC.scale, direction, 0f);

        Main.spriteBatch.ResetToDefault();

        return false;
    }

    #endregion Drawing

    #region Spawning

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing && Main.raining && spawnInfo.PlayerFloorY <= Main.worldSurface - 36 && !NPC.AnyNPCs(Type))
            return 0.018f;

        return 0f;
    }

    #endregion Spawning
}
