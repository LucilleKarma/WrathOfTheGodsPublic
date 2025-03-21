﻿using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Content.Particles;

public class PaleDuckweedParticle : Particle
{
    public int UniqueID;

    public int FrameY;

    public override BlendState BlendState => BlendState.Additive;

    public override string AtlasTextureName => "NoxusBoss.PaleDuckweedParticle.png";

    public PaleDuckweedParticle(Vector2 position, Vector2 velocity, Color color, int lifetime)
    {
        FrameY = Main.rand.Next(3);
        Position = position;
        Velocity = velocity;
        DrawColor = color;
        Lifetime = lifetime;
        Direction = Main.rand.NextFromList(-1, 1);
        UniqueID = Main.rand.Next(100000);
        Scale = Vector2.One * Main.rand.NextFloat(0.45f, 0.7f);
    }

    public override void Update()
    {
        // Fade in and out based on the lifetime of the duckweed.
        Opacity = InverseLerpBump(0f, 120f, Lifetime - 60f, Lifetime, Time);

        // Rise upward in water and bob in place above it.
        if (Collision.WetCollision(Position - Vector2.One * Scale * 6f, (int)(Scale.X * 12f), (int)(Scale.Y * 12f)))
        {
            if (Collision.SolidCollision(Position + Vector2.UnitX * Direction * 100f, 1, 1))
                Direction *= -1;

            Velocity.X = Lerp(Velocity.X, Direction * Lerp(0.3f, 0.7f, UniqueID % 9f / 9f), 0.025f);
            Velocity.Y = Clamp(Velocity.Y - 0.008f, -0.4f, 0.4f);
        }
        else
        {
            Velocity.X *= 0.985f;
            Velocity.Y = Clamp(Velocity.Y + 0.1f, -1f, 5f);
        }

        // Get pushed around by players.
        Velocity += Main.LocalPlayer.velocity / (Pow(Main.LocalPlayer.Distance(Position), 2f) * 0.1f + 4f);
        Velocity = Velocity.ClampLength(0f, 12f);
        if (Velocity.Length() >= 8f)
            Velocity *= 0.96f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle frame = Texture.Frame.Subdivide(1, 3, 0, FrameY);
        Vector2 origin = frame.Size() * 0.5f;
        SpriteEffects spriteDirection = Direction.ToSpriteDirection();

        // Draw a weakly pulsating backglow behind the duckweed.
        float pulse = (Main.GlobalTimeWrappedHourly * 0.5f + UniqueID * 0.21586f) % 1f;
        spriteBatch.Draw(BloomCircleSmall, Position - Main.screenPosition, null, DrawColor * Opacity * 0.4f, Rotation, BloomCircleSmall.Size() * 0.5f, Scale * 0.34f, 0, 0f);
        spriteBatch.Draw(Texture, Position - Main.screenPosition, frame, DrawColor * Opacity * (1f - pulse) * 0.61f, Rotation, origin, Scale * (pulse * 1.1f + 1f), spriteDirection);
        spriteBatch.Draw(Texture, Position - Main.screenPosition, frame, DrawColor * Opacity, Rotation, origin, Scale, spriteDirection);
    }
}
