using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.Players;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Buffs;

public class InvincibilityBuff : ModBuff
{
    /// <summary>
    /// The name of the referenced variable used when determining how strong the player's invincibility shader should look.
    /// </summary>
    public static string InvincibilityBuffInterpolantVariableName => "InvincibilityBuffInterpolant";

    /// <summary>
    /// The post-processing effect that handles the invincibility visual.
    /// </summary>
    public static readonly PlayerPostProcessingEffect InvincibilityEffect = new PlayerPostProcessingEffect(player =>
    {
        string paletteFilePath = "Content/Buffs/InvincibilityBuffPalette.json";
        Vector3[] palette = LocalDataManager.Read<Vector3[]>(paletteFilePath)["Standard"];

        ManagedShader lotusShader = ShaderManager.GetShader("NoxusBoss.PlayerInvincibilityShader");
        lotusShader.TrySetParameter("fadeInInterpolant", player.GetValueRef<float>(InvincibilityBuffInterpolantVariableName).Value);
        lotusShader.TrySetParameter("gradient", palette);
        lotusShader.TrySetParameter("gradientCount", palette.Length);
        lotusShader.Apply();
    }, true);

    public override string Texture => GetAssetPath("Content/Buffs", Name);

    public override void SetStaticDefaults()
    {
        PlayerDataManager.ImmuneToEvent += MakeImmune;
        PlayerDataManager.PreKillEvent += PreventDeath;
        PlayerDataManager.PostUpdateEvent += UpdateInvincibilityShader;
        Main.buffNoTimeDisplay[Type] = true;
    }

    private void UpdateInvincibilityShader(PlayerDataManager p)
    {
        bool fadeIn = p.Player.HasBuff<InvincibilityBuff>();

        Referenced<float> invincibilityInterpolantRef = p.Player.GetValueRef<float>(InvincibilityBuffInterpolantVariableName);
        invincibilityInterpolantRef.Value = Saturate(invincibilityInterpolantRef.Value + fadeIn.ToDirectionInt() * 0.023f);

        if (invincibilityInterpolantRef <= 0f)
            return;

        PlayerPostProcessingShaderSystem.ApplyPostProcessingEffect(p.Player, InvincibilityEffect);

        if (Main.rand.NextBool())
        {
            Dust twinkle = Dust.NewDustDirect(p.Player.TopLeft, p.Player.width, p.Player.height, DustID.AncientLight);
            twinkle.velocity = Main.rand.NextVector2Circular(0.4f, 0.85f);
            twinkle.color = Color.Lerp(Color.Gold, Color.White, Main.rand.NextFloat());
            twinkle.scale *= 0.7f;
            twinkle.noGravity = true;
        }
    }

    private static bool MakeImmune(PlayerDataManager p) => p.Player.HasBuff<InvincibilityBuff>();

    private static bool PreventDeath(PlayerDataManager p, ref PlayerDeathReason damageSource) => !p.Player.HasBuff<InvincibilityBuff>();

    public override bool PreDraw(SpriteBatch spriteBatch, int buffIndex, ref BuffDrawParams drawParams)
    {
        Main.spriteBatch.Draw(drawParams.Texture, drawParams.Position, null, drawParams.DrawColor, 0f, Vector2.Zero, 1f, 0, 0f);

        Main.spriteBatch.PrepareForShaders(null, true);
        Texture2D shaderMask = GennedAssets.Textures.Buffs.InvincibilityBuffShaderMask;

        Vector3[] palette = LotusOfCreationTile.ShaderPalette;
        ManagedShader lotusShader = ShaderManager.GetShader("NoxusBoss.LotusOfCreationShader");
        lotusShader.TrySetParameter("appearanceInterpolant", 1f);
        lotusShader.TrySetParameter("gradient", palette);
        lotusShader.TrySetParameter("gradientCount", palette.Length);
        lotusShader.Apply();

        Main.spriteBatch.Draw(shaderMask, drawParams.Position, null, drawParams.DrawColor, 0f, Vector2.Zero, 1f, 0, 0f);
        Main.spriteBatch.ResetToDefaultUI();

        return false;
    }
}
