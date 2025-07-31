using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.ParadiseReclaimed;
using NoxusBoss.Content.Projectiles.Typeless;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.MiscOPTools;

public class EmptinessSprayer : ModItem
{
    internal static int LaRugaID = -9999;

    /// <summary>
    /// The set of all NPCs that should not be deleted by the emptiness sprayer.
    /// </summary>
    public static bool[] NPCsToNotDelete
    {
        get;
        private set;
    } = NPCID.Sets.Factory.CreateBoolSet(false, NPCID.CultistTablet, NPCID.DD2LanePortal, NPCID.DD2EterniaCrystal, NPCID.TargetDummy);

    /// <summary>
    /// The set of all NPCs that should reflect the emptiness sprayer upon collision.
    /// </summary>
    public static bool[] NPCsThatReflectSpray
    {
        get;
        private set;
    } = NPCID.Sets.Factory.CreateBoolSet(false);

    public override string Texture => GetAssetPath("Content/Items/MiscOPTools", Name);

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;

        if (ModReferences.CalamityRemixMod is not null)
        {
            LaRugaID = ModReferences.CalamityRemixMod.Find<ModNPC>("LaRuga").Type;
            NPCsThatReflectSpray[LaRugaID] = true;
        }
    }

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 34;
        Item.useAnimation = 2;
        Item.useTime = 2;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.channel = true;
        Item.noUseGraphic = true;

        Item.useStyle = ItemUseStyleID.Shoot;

        Item.rare = ModContent.RarityType<AvatarRarity>();
        Item.value = Item.buyPrice(2, 0, 0, 0);

        Item.shoot = ModContent.ProjectileType<EmptinessSprayerHoldout>();
        Item.shootSpeed = 7f;
    }

    public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        DrawBottle(Main.UIScaleMatrix, position, null, Color.White, origin, scale, 0f, 0);
        return false;
    }

    public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        Texture2D glowmask = GennedAssets.Textures.MiscOPTools.EmptinessSprayer_SprayGlowmask.Value;

        SpriteEffects direction = Item.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        DrawBottle(Main.GameViewMatrix.TransformationMatrix, Item.position - Main.screenPosition, null, Color.White, glowmask.Size() * 0.5f, scale, rotation, direction);
        return false;
    }

    public static void DrawBottle(Matrix transformation, Vector2 drawPosition, Rectangle? frame, Color color, Vector2 origin, float scale, float rotation, SpriteEffects direction)
    {
        // Draw the bottle.
        Texture2D bottleTexture = TextureAssets.Item[ModContent.ItemType<EmptinessSprayer>()].Value;
        Main.spriteBatch.Draw(bottleTexture, drawPosition, frame, color, rotation, origin, scale, direction, 0f);

        // Prepare for shaders.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, transformation);

        Texture2D layerTexture = GennedAssets.Textures.MiscOPTools.EmptinessSprayer_SprayGlowmask.Value;
        ManagedShader maskShader = ShaderManager.GetShader("NoxusBoss.TextureMaskShader");
        maskShader.TrySetParameter("zoomFactor", Vector2.One * 0.02f);
        maskShader.SetTexture(ParadiseStaticTargetSystem.StaticTarget, 1, SamplerState.LinearClamp);
        maskShader.Apply();

        // Draw the bottle's noise overlay.
        Main.spriteBatch.Draw(layerTexture, drawPosition, frame, color, rotation, origin, scale, direction, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, transformation);
    }

    public override Vector2? HoldoutOffset()
    {
        return new Vector2(0f, 4.5f) + Main.rand.NextVector2Circular(0.9f, 1f);
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        position -= velocity * 4f + velocity.RotatedBy(PiOver2) * velocity.X.NonZeroSign() * 1.2f;
    }
}
