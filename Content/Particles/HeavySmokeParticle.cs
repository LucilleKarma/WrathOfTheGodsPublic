using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Content.Particles;

public class HeavySmokeParticle : Particle
{
    public int FrameX;

    public bool Glowing;

    public float HueShift;

    public float Spin;

    public override int FrameCount => 7;

    public override BlendState BlendState => Glowing ? BlendState.Additive : SubtractiveBlending;

    public override string AtlasTextureName => "NoxusBoss.HeavySmokeParticle.png";

    public HeavySmokeParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale, float opacity, float rotationSpeed = 0f, bool glowing = false, float hueShift = 0f)
    {
        Position = position;
        Velocity = velocity;
        DrawColor = color;
        Scale = Vector2.One * scale;
        FrameX = Main.rand.Next(FrameCount);
        Lifetime = lifetime;
        Opacity = opacity;
        Spin = rotationSpeed;
        Glowing = glowing;
        Rotation = Main.rand.NextFloat(TwoPi);
        HueShift = hueShift;
    }

    public override void Update()
    {
        if (LifetimeRatio < 0.1f)
            Scale += Vector2.One * 0.01f;
        if (LifetimeRatio > 0.9f)
            Scale *= 0.975f;

        // Ensure the color's hue stays within natural boundaries while its hue shifts.
        Vector3 hueSaturationLightness = Main.rgbToHsl(DrawColor);
        DrawColor = Main.hslToRgb((hueSaturationLightness.X + HueShift) % 1f, hueSaturationLightness.Y, hueSaturationLightness.Z);

        // Fade out and spin.
        Opacity *= 0.98f;
        Rotation += Spin * Velocity.X.NonZeroSign();

        // Slow down.
        Velocity *= 0.98f;

        // Fade out as this particle begins to die.
        DrawColor *= InverseLerp(1f, 0.85f, LifetimeRatio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        int timeFrame = (int)Math.Floor(Time / (Lifetime / 6f));
        Rectangle frame = new Rectangle(FrameX * 80, timeFrame * 80, 80, 80);
        SpriteEffects visualDirection = Direction.ToSpriteDirection();
        spriteBatch.Draw(Texture, Position - Main.screenPosition, frame, DrawColor * Opacity, Rotation, frame.Size() * 0.5f, Scale, visualDirection);
    }
}
