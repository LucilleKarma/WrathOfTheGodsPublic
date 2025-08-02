using System.Numerics;
using System.Runtime.CompilerServices;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Threading;
using Terraria;
using Terraria.Graphics;
using XNAColor = Microsoft.Xna.Framework.Color;
using XNAMatrix = Microsoft.Xna.Framework.Matrix;
using XNAVector2 = Microsoft.Xna.Framework.Vector2;
using XNAVector3 = Microsoft.Xna.Framework.Vector3;

namespace NoxusBoss.Core.Graphics.ClothSimulations;

public class ClothSimulation
{
    /// <summary>
    /// Represents a point that can exist in a cloth simulation.
    /// </summary>
    public class ClothPoint
    {
        public bool Locked;

        public Vector3 Position;

        public Vector3 Velocity;
    }

    /// <summary>
    /// The points that compose the cloth.
    /// </summary>
    public ClothPoint[] Cloth;

    /// <summary>
    /// The desired spacing between points in the cloth.
    /// </summary>
    public readonly float DesiredSpacing;

    /// <summary>
    /// The width of the simulation space.
    /// </summary>
    public readonly int Width;

    /// <summary>
    /// The height of the simulation space.
    /// </summary>
    public readonly int Height;

    public ClothSimulation(int width, int height, float desiredSpacing)
    {
        Width = width;
        Height = height;
        DesiredSpacing = desiredSpacing;
        Cloth = new ClothPoint[width * height];

        for (int i = 0; i < Cloth.Length; i++)
        {
            int x = i % Width;
            int y = i / Width;

            Cloth[i] = new ClothPoint()
            {
                Position = new Vector3(x * desiredSpacing * 0.8f, y * desiredSpacing, 0f)
            };
        }
    }

    public void Update(Vector3 generalForce, float decelerationFactor)
    {
        FastParallel.For(0, Width * Height, (int from, int to, object context) =>
        {
            for (int i = from; i < to; i++)
            {
                int x = i % Width;
                int y = i / Width;
                ClothPoint point = Cloth[i];
                Vector3 position = point.Position;

                if (point.Locked)
                    continue;

                float xInterpolant = x / (float)Width;
                float windSpeed = DesiredSpacing * Sqrt(xInterpolant) * 0.14f;
                Vector3 force = generalForce - point.Velocity * decelerationFactor;
                force.X += (Cos(Main.GlobalTimeWrappedHourly * 25f + position.X * 0.4f + position.Y * 0.09f) * 0.5f + 0.076f) * windSpeed;
                force.Y += Cos(Main.GlobalTimeWrappedHourly * -4.6f + position.X * 0.03f) * windSpeed * 0.15f;

                if (x >= 1)
                {
                    ClothPoint left = this[x - 1, y];
                    Vector3 leftOffset = left.Position - position;
                    force += (leftOffset.Length() - DesiredSpacing) * Vector3.Normalize(leftOffset) * 0.25f;
                }
                if (x < Width - 1)
                {
                    ClothPoint right = this[x + 1, y];
                    Vector3 rightOffset = right.Position - position;
                    force += (rightOffset.Length() - DesiredSpacing) * Vector3.Normalize(rightOffset) * 0.25f;
                }
                if (y >= 1)
                {
                    ClothPoint top = this[x, y - 1];
                    Vector3 topOffset = top.Position - position;
                    force += (topOffset.Length() - DesiredSpacing) * Vector3.Normalize(topOffset) * 0.25f;
                }
                if (y < Height - 1)
                {
                    ClothPoint bottom = this[x, y + 1];
                    Vector3 bottomOffset = bottom.Position - position;
                    force += (bottomOffset.Length() - DesiredSpacing) * Vector3.Normalize(bottomOffset) * 0.25f;
                }

                point.Velocity += force;
                if (point.Velocity.Length() > 15f)
                    point.Velocity = Vector3.Normalize(point.Velocity) * 15f;
            }
        });

        FastParallel.For(0, Width * Height, (int from, int to, object context) =>
        {
            for (int i = from; i < to; i++)
            {
                ClothPoint cloth = Cloth[i];
                cloth.Position += cloth.Velocity;
            }
        });
    }

    /// <summary>
    /// Renders the cloth with a given texture.
    /// </summary>
    /// <param name="drawOffset">The draw offset of the overall cloth.</param>
    /// <param name="color">The color of the cloth.</param>
    /// <param name="texture">The texture to overlay on the cloth.</param>
    /// <param name="projection">The matrix that affects the rendering.</param>
    public void Render(XNAVector2 drawOffset, VertexColors color, XNAMatrix projection, Texture2D texture)
    {
        if (!ShaderManager.HasFinishedLoading)
            return;

        short[] indices = new short[(Width - 1) * (Height - 1) * 6];
        VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                XNAVector3 position = Unsafe.As<Vector3, XNAVector3>(ref Cloth[y * Width + x].Position);
                XNAColor colorTop = XNAColor.Lerp(color.TopLeftColor, color.TopRightColor, x / (float)Width);
                XNAColor colorBottom = XNAColor.Lerp(color.BottomLeftColor, color.BottomRightColor, x / (float)Width);

                vertices[y * Width + x] = new VertexPositionColorTexture(position, XNAColor.Lerp(colorTop, colorBottom, y / (float)Height), new XNAVector2(x / (float)Width, y / (float)Height));
            }
        }

        int index = 0;
        for (int y = 0; y < Width - 1; y++)
        {
            for (int x = 0; x < Height - 1; x++)
            {
                short topLeft = (short)(y * Width + x);
                short topRight = (short)(y * Width + x + 1);
                short bottomLeft = (short)((y + 1) * Width + x);
                short bottomRight = (short)((y + 1) * Width + x + 1);

                // Triangle 1 (Top Left, Top Right, Bottom Left)
                indices[index++] = topLeft;
                indices[index++] = topRight;
                indices[index++] = bottomLeft;

                // Triangle 2 (Bottom Right, Bottom Left, Top Right)
                indices[index++] = bottomLeft;
                indices[index++] = topRight;
                indices[index++] = bottomRight;
            }
        }

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        XNAMatrix world = XNAMatrix.CreateTranslation(drawOffset.X, drawOffset.Y, 0f);

        ManagedShader clothShader = ShaderManager.GetShader("NoxusBoss.ClothShader");
        clothShader.TrySetParameter("size", new XNAVector2(Width, Height) * DesiredSpacing);
        clothShader.TrySetParameter("uWorldViewProjection", world * projection);
        clothShader.SetTexture(texture, 1, SamplerState.PointClamp);
        clothShader.Apply();

        gd.RasterizerState = RasterizerState.CullCounterClockwise;
        gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, (Width - 2) * (Height - 2));
    }

    public ClothPoint this[int x, int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Cloth[x + Width * y];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Cloth[x + Width * y] = value;
    }
}
