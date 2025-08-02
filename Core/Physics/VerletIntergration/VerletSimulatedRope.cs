using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Core.Physics.VerletIntergration;

/// <summary>
/// Represents a collection of verlet points as a rope.
/// </summary>
public class VerletSimulatedRope
{
    private float projectionTextureWidth;

    /// <summary>
    /// The ideal overall length of the rope in pixels.
    /// </summary>
    public float IdealRopeLength;

    /// <summary>
    /// The true length of the rope in pixels.
    /// </summary>
    public float RopeLength
    {
        get
        {
            float length = 0f;

            for (int i = 1; i < Rope.Count; i++)
                length += Rope[i].Position.Distance(Rope[i - 1].Position);

            return length;
        }
    }

    /// <summary>
    /// The end position of the rope.
    /// </summary>
    public Vector2 EndPosition => Rope.Last().Position;

    /// <summary>
    /// The end direction of the rope.
    /// </summary>
    public Vector2 EndDirection => (Rope[^1].Position - Rope[^2].Position).SafeNormalize(Vector2.Zero);

    /// <summary>
    /// The list of rope segments.
    /// </summary>
    public List<VerletSimulatedSegment> Rope;

    public VerletSimulatedRope(Vector2 position, Vector2 velocity, int totalPoints, float length)
    {
        Rope ??= [];

        IdealRopeLength = length;
        for (int i = 0; i < totalPoints; i++)
            Rope?.Add(new(position + Main.rand.NextVector2Circular(2f, 2f), velocity, i == 0));
    }

    /// <summary>
    /// Updates the rope, locking its first segment in place at a desired position to prevent it from falling forever.
    /// </summary>
    /// <param name="topPosition">Where the first rope segment should be locked.</param>
    /// <param name="gravity">The gravity imposed upon the rope.</param>
    /// <param name="externalForce">An optional external force applied to the entire rope.</param>
    public void Update(Vector2 topPosition, float gravity, Vector2 externalForce = default)
    {
        Rope[0].Position = topPosition;
        Rope[0].Locked = true;
        UpdateWithoutLocking(gravity, externalForce);
    }

    /// <summary>
    /// Updates the rope. This will allow the entire rope to fall forever.
    /// </summary>
    /// <param name="gravity">The gravity imposed upon the rope.</param>
    /// <param name="externalForce">An optional external force applied to the entire rope.</param>
    public void UpdateWithoutLocking(float gravity, Vector2 externalForce = default)
    {
        VerletSimulations.TileCollisionVerletSimulation(Rope, IdealRopeLength / Rope.Count, externalForce, Rope.Count + 5, gravity);
    }

    public void DrawProjection(Texture2D projection, Vector2 drawOffset, bool flipHorizontally, Func<float, Color> colorFunction, int? projectionWidth = null, int? projectionHeight = null, float widthFactor = 1f, float lengthStretch = 1.3f, bool unscaledMatrix = false)
    {
        // Initialize the rope drawer primitive.
        ManagedShader projectionShader = ShaderManager.GetShader("NoxusBoss.PrimitiveProjection");

        // This variable is used as a proxy to allow for dynamic updating in the width function for the primitive drawer.
        // Using projection.Width directly inside the width function can (and has, in the past) lead to problems where it'll receive the asynchronous load dummy texture and
        // interpret that as the texture to evaluate the width of, resulting in cases where the drawn width is 1 (since said dummy texture is 1x1 in size).
        projectionTextureWidth = projection.Width;

        Main.instance.GraphicsDevice.Textures[1] = projection;
        Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.AnisotropicClamp;
        Main.instance.GraphicsDevice.BlendState = BlendState.NonPremultiplied;

        projectionShader.TrySetParameter("horizontalFlip", flipHorizontally);
        projectionShader.TrySetParameter("heightRatio", projection.Height / projectionTextureWidth);
        projectionShader.TrySetParameter("lengthRatio", RopeLength / IdealRopeLength * lengthStretch);

        List<Vector2> ropePositions = Rope.Select(r => r.Position).ToList();
        PrimitiveSettings settings = new PrimitiveSettings(_ => projectionTextureWidth * widthFactor, new(colorFunction), _ => drawOffset + Main.screenPosition, Shader: projectionShader, ProjectionAreaWidth: projectionWidth, ProjectionAreaHeight: projectionHeight, UseUnscaledMatrix: unscaledMatrix);
        PrimitiveRenderer.RenderTrail(ropePositions, settings, 24);
    }

    public void DrawProjectionButItActuallyWorks(Texture2D projection, Vector2 drawOffset, bool flipHorizontally, Func<float, Color> colorFunction, int? projectionWidth = null, int? projectionHeight = null, float widthFactor = 1f, bool unscaledMatrix = false)
    {
        // Initialize the rope drawer primitive.
        ManagedShader projectionShader = ShaderManager.GetShader("NoxusBoss.PrimitiveProjection");

        // This variable is used as a proxy to allow for dynamic updating in the width function for the primitive drawer.
        // Using projection.Width directly inside the width function can (and has, in the past) lead to problems where it'll receive the asynchronous load dummy texture and
        // interpret that as the texture to evaluate the width of, resulting in cases where the drawn width is 1 (since said dummy texture is 1x1 in size).
        projectionTextureWidth = projection.Width;

        Main.instance.GraphicsDevice.Textures[1] = projection;
        Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.AnisotropicClamp;
        Main.instance.GraphicsDevice.BlendState = BlendState.NonPremultiplied;

        projectionShader.TrySetParameter("horizontalFlip", flipHorizontally);
        projectionShader.TrySetParameter("heightRatio", projection.Height / projectionTextureWidth);
        projectionShader.TrySetParameter("lengthRatio", 1f);

        List<Vector2> ropePositions = Rope.Select(r => r.Position).ToList();
        PrimitiveSettings settings = new PrimitiveSettings(_ => projectionTextureWidth * widthFactor, new(colorFunction), _ => drawOffset + Main.screenPosition, Shader: projectionShader, ProjectionAreaWidth: projectionWidth, ProjectionAreaHeight: projectionHeight, UseUnscaledMatrix: unscaledMatrix);
        PrimitiveRenderer.RenderTrail(ropePositions, settings, 24);
    }

    public void DrawProjectionScuffed(Texture2D projection, Vector2 drawOffset, bool flipHorizontally, Func<float, Color> colorFunction, Func<float, float> widthFunction, int? projectionWidth = null, int? projectionHeight = null, float lengthStretch = 1.3f)
    {
        // Initialize the rope drawer primitive.
        ManagedShader projectionShader = ShaderManager.GetShader("NoxusBoss.PrimitiveProjection");
        Main.instance.GraphicsDevice.Textures[1] = projection;
        Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.AnisotropicClamp;
        Main.instance.GraphicsDevice.BlendState = BlendState.NonPremultiplied;

        projectionShader.TrySetParameter("horizontalFlip", flipHorizontally);
        projectionShader.TrySetParameter("heightRatio", projection.Height / (float)projection.Width);
        projectionShader.TrySetParameter("lengthRatio", RopeLength / IdealRopeLength * lengthStretch);

        List<Vector2> ropePositions = Rope.Select(r => r.Position).ToList();
        PrimitiveSettings settings = new PrimitiveSettings(new(widthFunction), new(colorFunction), _ => drawOffset + Main.screenPosition, Shader: projectionShader, ProjectionAreaWidth: projectionWidth, ProjectionAreaHeight: projectionHeight, UseUnscaledMatrix: false);
        PrimitiveRenderer.RenderTrail(ropePositions, settings, 90);
    }
}
