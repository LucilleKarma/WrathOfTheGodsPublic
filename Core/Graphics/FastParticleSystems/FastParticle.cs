using Microsoft.Xna.Framework;

namespace NoxusBoss.Core.Graphics.FastParticleSystems;

public struct FastParticle
{
    /// <summary>
    /// How long this particle has existed for, in frames.
    /// </summary>
    public int Time;

    /// <summary>
    /// Whether the particle is currently in use or not.
    /// </summary>
    public bool Active;

    /// <summary>
    /// The color of the particle.
    /// </summary>
    public Color Color;

    /// <summary>
    /// The particle's position in the world.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// The particle's velocity.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>
    /// The particle's size.
    /// </summary>
    public Vector2 Size;

    /// <summary>
    /// The particle's rotation.
    /// </summary>
    public float Rotation;

    /// <summary>
    /// Optional extra data for this particle.
    /// </summary>
    public float ExtraData;
}
