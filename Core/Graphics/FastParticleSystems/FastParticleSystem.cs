using System.Buffers;
using System.Runtime.CompilerServices;

using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Threading;

using Terraria;

using MatrixSIMD = System.Numerics.Matrix3x2;
using Vector2SIMD = System.Numerics.Vector2;

namespace NoxusBoss.Core.Graphics.FastParticleSystems;

public class FastParticleSystem : IDisposable
{
    private int particleSpawnIndex;

    protected static readonly Vector2SIMD topLeftOffset = new Vector2SIMD(-1f, -1f);

    protected static readonly Vector2SIMD topRightOffset = new Vector2SIMD(1f, -1f);

    protected static readonly Vector2SIMD bottomLeftOffset = new Vector2SIMD(-1f, 1f);

    protected static readonly Vector2SIMD bottomRightOffset = new Vector2SIMD(1f, 1f);

    /// <summary>
    /// The set of all active particles.
    /// </summary>
    public FastParticle[] particles;

    /// <summary>
    /// The buffer responsible for all vertex data on the particles.
    /// </summary>
    public DynamicVertexBuffer vertexData;

    /// <summary>
    /// The buffer responsible for vertex indices relative to the vertex buffer.
    /// </summary>
    public DynamicIndexBuffer indexData;

    /// <summary>
    /// The maximum amount of particles that can exist.
    /// </summary>
    public readonly int MaxParticles;

    /// <summary>
    /// The maximum amount of vertices that can be contained within the vertex buffer.
    /// </summary>
    public readonly int MaxVertices;

    /// <summary>
    /// The maximum amount of indices that can be contained within the vertex buffer.
    /// </summary>
    public readonly int MaxIndices;

    /// <summary>
    /// The optional extra update actions that should be performed on particles.
    /// </summary>
    public readonly ParticleUpdateAction? ExtraUpdates;

    /// <summary>
    /// The update preparation actions for this particle system.
    /// </summary>
    public readonly Action RenderPreparations;

    public delegate void ParticleUpdateAction(ref FastParticle particle);

    public FastParticleSystem(int maxParticles, Action renderPreparations, ParticleUpdateAction? extraUpdates = null)
    {
        MaxParticles = maxParticles;
        MaxVertices = maxParticles * 4;
        MaxIndices = maxParticles * 6;

        Main.QueueMainThreadAction(() =>
        {
            vertexData = new(Main.instance.GraphicsDevice, VertexPosition2DColorTexture.VertexDeclaration2D, MaxVertices, BufferUsage.WriteOnly);
            indexData = new(Main.instance.GraphicsDevice, IndexElementSize.ThirtyTwoBits, MaxIndices, BufferUsage.WriteOnly);
        });

        particles = new FastParticle[MaxParticles];

        RenderPreparations = renderPreparations;
        ExtraUpdates = extraUpdates;
    }

    protected void PopulateVertexBuffer()
    {
        VertexPosition2DColorTexture[] vertices = ArrayPool<VertexPosition2DColorTexture>.Shared.Rent(MaxVertices);
        for (int i = 0; i < MaxParticles; i++)
            PopulateVertexBufferIndex(vertices, i);

        vertexData?.SetData(vertices, 0, MaxVertices, SetDataOptions.None);

        ArrayPool<VertexPosition2DColorTexture>.Shared.Return(vertices);
    }

    protected virtual void PopulateVertexBufferIndex(VertexPosition2DColorTexture[] vertices, int particleIndex)
    {
        ref FastParticle particle = ref particles[particleIndex];

        Color color = particle.Active ? particle.Color : Color.Transparent;
        Vector2SIMD center = Unsafe.As<Vector2, Vector2SIMD>(ref particle.Position);
        Vector2SIMD size = Unsafe.As<Vector2, Vector2SIMD>(ref particle.Size);
        MatrixSIMD particleRotationMatrix = MatrixSIMD.CreateRotation(particle.Rotation);

        Vector2SIMD topLeftPosition = center + Vector2SIMD.Transform(topLeftOffset * size, particleRotationMatrix);
        Vector2SIMD topRightPosition = center + Vector2SIMD.Transform(topRightOffset * size, particleRotationMatrix);
        Vector2SIMD bottomLeftPosition = center + Vector2SIMD.Transform(bottomLeftOffset * size, particleRotationMatrix);
        Vector2SIMD bottomRightPosition = center + Vector2SIMD.Transform(bottomRightOffset * size, particleRotationMatrix);

        int vertexIndex = particleIndex * 4;
        vertices[vertexIndex] = new VertexPosition2DColorTexture(Unsafe.As<Vector2SIMD, Vector2>(ref topLeftPosition), color, Vector2.Zero, 0f);
        vertices[vertexIndex + 1] = new VertexPosition2DColorTexture(Unsafe.As<Vector2SIMD, Vector2>(ref topRightPosition), color, Vector2.UnitX, 0f);
        vertices[vertexIndex + 2] = new VertexPosition2DColorTexture(Unsafe.As<Vector2SIMD, Vector2>(ref bottomRightPosition), color, Vector2.One, 0f);
        vertices[vertexIndex + 3] = new VertexPosition2DColorTexture(Unsafe.As<Vector2SIMD, Vector2>(ref bottomLeftPosition), color, Vector2.UnitY, 0f);
    }

    protected void PopulateIndexBuffer()
    {
        int[] indices = ArrayPool<int>.Shared.Rent(MaxIndices);

        // Construct clockwise indices.
        for (int i = 0; i < MaxParticles; i++)
        {
            int bufferIndex = i * 6;
            int vertexIndex = i * 4;

            indices[bufferIndex] = vertexIndex;
            indices[bufferIndex + 1] = vertexIndex + 1;
            indices[bufferIndex + 2] = vertexIndex + 2;
            indices[bufferIndex + 3] = vertexIndex + 2;
            indices[bufferIndex + 4] = vertexIndex + 3;
            indices[bufferIndex + 5] = vertexIndex;
        }

        indexData?.SetData(indices, 0, MaxIndices);

        ArrayPool<int>.Shared.Return(indices);
    }

    private void RenderInternal()
    {
        RenderPreparations();

        Main.instance.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        Main.instance.GraphicsDevice.Indices = indexData;
        Main.instance.GraphicsDevice.SetVertexBuffer(vertexData);
        Main.instance.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, MaxVertices, 0, MaxIndices / 3);
        Main.instance.GraphicsDevice.SetVertexBuffer(null);
        Main.instance.GraphicsDevice.Indices = null;
    }

    /// <summary>
    /// Tries to create a new particle at a given position with a given velocity.
    /// </summary>
    /// <param name="spawnPosition">The particle's spawn position.</param>
    /// <param name="velocity">The particle's initial velocity.</param>
    /// <param name="size">The particle's size.</param>
    /// <param name="color">The particle's color.</param>
    public void CreateNew(Vector2 spawnPosition, Vector2 velocity, Vector2 size, Color color) =>
        CreateNew(spawnPosition, velocity, size, color, 0f);

    /// <summary>
    /// Tries to create a new particle at a given position with a given velocity.
    /// </summary>
    /// <param name="spawnPosition">The particle's spawn position.</param>
    /// <param name="velocity">The particle's initial velocity.</param>
    /// <param name="size">The particle's size.</param>
    /// <param name="color">The particle's color.</param>
    /// <param name="extraData">Extra data for the particle.</param>
    public void CreateNew(Vector2 spawnPosition, Vector2 velocity, Vector2 size, Color color, float extraData)
    {
        ref FastParticle particle = ref particles[particleSpawnIndex];
        particle.Active = true;
        particle.Time = 0;
        particle.Position = spawnPosition + size * velocity.SafeNormalize(Vector2.Zero) * 0.5f;
        particle.Velocity = velocity;
        particle.Rotation = velocity.ToRotation() + PiOver2;
        particle.Color = color;
        particle.Size = size;
        particle.ExtraData = extraData;

        particleSpawnIndex = (particleSpawnIndex + 1) % MaxParticles;
    }

    /// <summary>
    /// Disposes this particle system, freeing buffers and returning the particles back to the pool.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        vertexData?.Dispose();
        indexData?.Dispose();
    }

    /// <summary>
    /// Updates all particles in the system.
    /// </summary>
    public void UpdateAll()
    {
        if (!particles.Any(p => p.Active))
            return;

        FastParallel.For(0, MaxParticles, (start, end, context) =>
        {
            for (int i = start; i < end; i++)
            {
                if (!particles[i].Active)
                    continue;

                ref FastParticle particle = ref particles[i];

                ExtraUpdates?.Invoke(ref particle);

                particle.Position += particle.Velocity;
                particle.Time++;
            }
        });
    }

    /// <summary>
    /// Renders all particles in the system.
    /// </summary>
    public void RenderAll()
    {
        PopulateVertexBuffer();
        PopulateIndexBuffer();
        RenderInternal();
    }
}
