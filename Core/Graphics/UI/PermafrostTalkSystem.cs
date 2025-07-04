using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.Graphics.UI;

public class PermafrostTalkSystem : ModSystem
{
    /// <summary>
    /// Permafrost's NPC ID.
    /// </summary>
    public static int PermafrostID
    {
        get;
        private set;
    } = NPCID.None;

    /// <summary>
    /// The amount by which indicator arrows should fly away due to the player having the key.
    /// </summary>
    public static float TextFlyAwayDistance
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the talk indicator effect can apply at all. This depends on whether Calamity is enabled.
    /// </summary>
    public static bool EffectCanApply
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        EffectCanApply = ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal);
        if (!EffectCanApply)
            return;

        // Sigh.
        PermafrostID = cal.Find<ModNPC>("DILF").Type;

        ClientSideLoad();
    }

    public override void SaveWorldData(TagCompound tag) => tag["TextFlyAwayDistance"] = TextFlyAwayDistance;

    public override void LoadWorldData(TagCompound tag) => TextFlyAwayDistance = tag.GetFloat("TextFlyAwayDistance");

    private static void ClientSideLoad()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        GlobalNPCEventHandlers.PreDrawEvent += DrawPermafrostTalkIndicatorWrapper;
    }

    private static bool DrawPermafrostTalkIndicatorWrapper(NPC npc)
    {
        if (npc.type == PermafrostID && !npc.IsABestiaryIconDummy)
        {
            Main.spriteBatch.UseBlendState(BlendState.NonPremultiplied);
            DrawPermafrostTalkIndicator(npc);
            Main.spriteBatch.ResetToDefault();
        }

        return true;
    }

    public override void PreUpdateNPCs()
    {
        if (PermafrostKeepWorldGen.PlayerGivenKey && TextFlyAwayDistance < 4000f)
            TextFlyAwayDistance = (TextFlyAwayDistance + 1.85f) * 1.09f;
        else if (!PermafrostKeepWorldGen.PlayerGivenKey)
            TextFlyAwayDistance *= 0.9f;
    }

    private static void DrawPermafrostTalkIndicator(NPC npc)
    {
        float opacity = Pow(InverseLerp(350f, 0f, TextFlyAwayDistance), 2.5f);
        Vector2 drawCenter = npc.Center - Main.screenPosition - Vector2.UnitX * npc.spriteDirection * 4f;

        // Calculate jingle variables.
        float jingleTime = Main.GlobalTimeWrappedHourly * 3.5f % 20f;
        float jingleDecayFactor = Exp(MathF.Max(jingleTime - Pi, 0f) * -0.67f);
        float jinglePeriodFactor = Sin(jingleTime * 2.2f);
        float jingleInterpolant = jingleDecayFactor * jinglePeriodFactor;

        // Draw the arrow that points at Permafrost.
        float arrowDirection = PiOver2;
        float arrowRotation = arrowDirection - PiOver2 + jingleInterpolant * 0.3f;
        Vector2 arrowScale = new Vector2(1f - jingleInterpolant * 0.2f, 1f + jingleInterpolant * 0.15f) * (0.7f + Abs(jingleInterpolant) * 0.26f);

        int arrowFrame = (int)(Main.GlobalTimeWrappedHourly * 11f) % 6;
        float arrowHoverOffset = TextFlyAwayDistance + Abs(jingleInterpolant) * 36f + 80f;
        Texture2D arrowTexture = GennedAssets.Textures.PermafrostTalkIndicator.TalkUIArrow;
        Rectangle arrowFrameArea = arrowTexture.Frame(1, 6, 0, arrowFrame);

        Vector2 arrowDrawPosition = drawCenter - arrowDirection.ToRotationVector2() * arrowHoverOffset;
        Main.EntitySpriteDraw(arrowTexture, arrowDrawPosition, arrowFrameArea, Color.White * opacity, arrowRotation, arrowFrameArea.Size() * 0.5f, arrowScale, 0);
    }
}
