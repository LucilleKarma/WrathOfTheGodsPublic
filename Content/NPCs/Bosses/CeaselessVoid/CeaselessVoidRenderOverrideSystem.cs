using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using CeaselessVoidNPC = CalamityMod.NPCs.CeaselessVoid.CeaselessVoid;

namespace NoxusBoss.Content.NPCs.Bosses.CeaselessVoid;

[JITWhenModsEnabled(CalamityCompatibility.ModName)]
[ExtendsFromMod(CalamityCompatibility.ModName)]
public class CeaselessVoidRenderOverrideSystem : GlobalNPC
{
    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color lightColor)
    {
        if (npc.type == ModContent.NPCType<CeaselessVoidNPC>())
        {
            RenderCeaselessVoid(npc, screenPos, lightColor);
            return false;
        }

        return true;
    }

    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;

        int riftID = ModContent.NPCType<CeaselessVoidRift>();
        if (npc.type == ModContent.NPCType<CeaselessVoidNPC>() && !NPC.AnyNPCs(riftID))
        {
            int x = (int)npc.Center.X;
            int y = FindGround(npc.Center.ToTileCoordinates(), Vector2.UnitY).Y * 16;

            // Ensure that the rift doesn't spawn inside of blocks.
            for (int tries = 0; tries < 100; tries++)
            {
                tries++;

                Tile left = Framing.GetTileSafely(x - tries, y);
                Tile right = Framing.GetTileSafely(x + tries, y);

                bool openAirLeft = Collision.SolidCollision(new Vector2(x - tries, y) - Vector2.One * 250f, 500, 500);
                bool openAirRight = Collision.SolidCollision(new Vector2(x + tries, y) - Vector2.One * 250f, 500, 500);
                bool validWallLeft = left.WallType == WallID.PinkDungeonUnsafe || left.WallType == WallID.PinkDungeonSlabUnsafe || left.WallType == WallID.PinkDungeonTileUnsafe;
                bool validWallRight = right.WallType == WallID.PinkDungeonUnsafe || right.WallType == WallID.PinkDungeonSlabUnsafe || right.WallType == WallID.PinkDungeonTileUnsafe;

                if (openAirLeft && validWallLeft)
                {
                    x -= tries;
                    break;
                }
                if (openAirRight && validWallRight)
                {
                    x += tries;
                    break;
                }
            }

            NPC.NewNPC(npc.GetSource_Death(), x, y, riftID, 1);
        }
    }

    private static void RenderCeaselessVoid(NPC npc, Vector2 screenPos, Color lightColor)
    {
        Texture2D texture = GennedAssets.Textures.CeaselessVoid.CeaselessVoidBody.Value;
        Texture2D glowmask = GennedAssets.Textures.CeaselessVoid.CeaselessVoidGlow.Value;
        Texture2D center = GennedAssets.Textures.CeaselessVoid.CeaselessVoidCenter.Value;

        Vector2 drawPosition = npc.Center - screenPos;
        SpriteEffects direction = npc.spriteDirection.ToSpriteDirection();

        Main.spriteBatch.PrepareForShaders(null, npc.IsABestiaryIconDummy);

        Main.spriteBatch.Draw(texture, drawPosition, npc.frame, npc.GetAlpha(lightColor), npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);
        Main.spriteBatch.Draw(glowmask, drawPosition, npc.frame, npc.GetAlpha(Color.White), npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);

        ManagedShader riftShader = ShaderManager.GetShader("NoxusBoss.CeaselessVoidInnerRiftShader");
        riftShader.TrySetParameter("textureSize", center.Size());
        riftShader.TrySetParameter("center", new Vector2(0.48f, 0.4f));
        riftShader.TrySetParameter("darkeningRadius", 0.2f);
        riftShader.TrySetParameter("pitchBlackRadius", 0.075f);
        riftShader.TrySetParameter("brightColorReplacement", new Vector3(1.4f, 0f, 0.16f));
        riftShader.TrySetParameter("bottomLightColorInfluence", new Vector3(-0.3f, 0.6f, 0.6f));
        riftShader.SetTexture(GennedAssets.Textures.FirstPhaseForm.RiftInnerTexture, 1, SamplerState.PointWrap);
        riftShader.Apply();

        Main.spriteBatch.Draw(center, drawPosition, null, npc.GetAlpha(lightColor), npc.rotation, center.Size() * 0.5f, npc.scale, direction, 0f);

        if (npc.IsABestiaryIconDummy)
            Main.spriteBatch.ResetToDefaultUI();
        else
            Main.spriteBatch.ResetToDefault();
    }
}
