﻿using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace NoxusBoss.Content.Particles;

public class MetalSparkParticle : Particle
{
    public Color GlowColor;

    public new Vector2 Scale;

    public bool AffectedByGravity;

    public static AtlasTexture GlowTexture
    {
        get;
        private set;
    }

    public override BlendState BlendState => BlendState.Additive;

    public override string AtlasTextureName => "NoxusBoss.MetalSparkParticle.png";

    public MetalSparkParticle(Vector2 relativePosition, Vector2 velocity, bool affectedByGravity, int lifetime, Vector2 scale, float opacity, Color color, Color glowColor)
    {
        Position = relativePosition;
        Velocity = velocity;
        AffectedByGravity = affectedByGravity;
        Scale = scale;
        Opacity = opacity;
        Lifetime = lifetime;
        DrawColor = color;
        GlowColor = glowColor;

        if (Main.netMode != NetmodeID.Server)
            GlowTexture = AtlasManager.GetTexture("NoxusBoss.MetalSparkParticleGlow.png");
    }

    public override void Update()
    {
        if (AffectedByGravity)
        {
            Velocity.X *= 0.9f;
            Velocity.Y += 1.1f;
        }
        Rotation = Velocity.ToRotation() + PiOver2;
        DrawColor = Color.Lerp(DrawColor, new Color(122, 108, 95), 0.06f);
        GlowColor *= 0.95f;

        Scale.X *= 0.98f;
        Scale.Y *= 0.95f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Vector2 scale = Vector2.One * Scale;
        spriteBatch.Draw(Texture, Position - Main.screenPosition, null, DrawColor * Opacity, Rotation, null, scale, SpriteEffects.None);
        spriteBatch.Draw(GlowTexture, Position - Main.screenPosition, null, GlowColor * Opacity, Rotation, null, scale * 1.15f, SpriteEffects.None);
        spriteBatch.Draw(GlowTexture, Position - Main.screenPosition, null, GlowColor * Opacity * 0.6f, Rotation, null, scale * 1.35f, SpriteEffects.None);
        spriteBatch.Draw(GlowTexture, Position - Main.screenPosition, null, GlowColor * Opacity * 0.3f, Rotation, null, scale * 1.67f, SpriteEffects.None);
    }
}
