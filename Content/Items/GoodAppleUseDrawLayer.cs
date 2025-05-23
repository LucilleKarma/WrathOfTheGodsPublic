﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items;

public class GoodAppleUseDrawLayer : PlayerDrawLayer
{
    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
    {
        return drawInfo.drawPlayer.HeldMouseItem()?.type == ModContent.ItemType<GoodApple>() && drawInfo.drawPlayer.itemAnimation != 0;
    }

    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.HeldItem);

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        // Check if the apple has been bitten into. If it has, draw it manually with a special texture.
        bool appleBitten = drawInfo.drawPlayer.itemAnimation <= 9;
        if (!appleBitten)
            return;

        // Draw the bitten apple.
        Texture2D appleTexture = GennedAssets.Textures.Items.GoodAppleBitten.Value;
        Vector2 drawPosition = drawInfo.ItemLocation.Floor() - Main.screenPosition;
        Vector2 origin = appleTexture.Size();
        if (!drawInfo.itemEffect.HasFlag(SpriteEffects.FlipHorizontally))
            origin.X = 0f;

        DrawData bittenAppleData = new DrawData(appleTexture, drawPosition, null, Color.White, drawInfo.rotation, origin, 1f, drawInfo.itemEffect);
        drawInfo.DrawDataCache.Add(bittenAppleData);

        // Remove the original draw data.
        drawInfo.drawPlayer.itemLocation = Vector2.Zero;
        drawInfo.ItemLocation = Vector2.Zero;
    }
}
