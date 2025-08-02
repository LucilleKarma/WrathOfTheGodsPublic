using Microsoft.Xna.Framework;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.Items.Placeable.Trophies;
using NoxusBoss.Content.Rarities;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using static NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix.CalRemixCompatibilitySystem;

namespace NoxusBoss.Content.Items.LoreItems;

public class LoreAvatar : BaseLoreItem
{
    public override int TrophyID => ModContent.ItemType<AvatarTrophy>();

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        // Don't attempt to alter tooltips added by Terraria.
        if (line.Mod != "NoxusBoss")
            return true;

        Color poemColor = new Color(252, 37, 74);
        Vector2 drawPosition = new Vector2(line.X, line.Y);

        // Use a special font.
        line.Font = FontRegistry.Instance.AvatarPoemText;
        line.BaseScale *= new Vector2(0.407f, 0.405f);
        /* if (FontRegistry.RussianGameCulture.IsActive)
            line.BaseScale *= 1f; */

        // Draw lines.
        List<string> lines =
        [
            line.Text.Replace("\t", "       ")
        ];

        for (int i = 0; i < lines.Count; i++)
        {
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, lines[i], drawPosition, poemColor, Color.Black, line.Rotation, line.Origin, line.BaseScale, line.MaxWidth, line.Spread * 0.6f);
            drawPosition.X += line.Font.MeasureString(lines[i]).X * line.BaseScale.X;
        }

        return false;
    }

    public override void SetDefaults()
    {
        Item.rare = ModContent.RarityType<AvatarRarity>();
        base.SetDefaults();
    }

    public override void SetupCalRemixCompatibility()
    {
        FannyDialog fanny = new FannyDialog("AvatarLore1", "FannyAwooga").WithDuration(4.25f).WithCondition(_ => FannyDialog.JustReadLoreItem(Type)).WithoutClickability();
        FannyDialog evilFanny = new FannyDialog("AvatarLore2", "EvilFannyIdle").WithDuration(7.5f).WithEvilness().WithParentDialog(fanny, 2f);
        evilFanny.Register();
        fanny.Register();
    }
}
