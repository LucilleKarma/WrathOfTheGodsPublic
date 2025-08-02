using System.Runtime.InteropServices;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;

public class MarsMissileBodyRenderer : ModSystem
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct VertexPositionColorNormalTexture : IVertexType
    {
        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        /// <summary>
        /// The position of this vertex.
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// The color of this vertex.
        /// </summary>
        public readonly Color Color;

        /// <summary>
        /// The texture coordinate associated with the vertex.
        /// </summary>
        public readonly Vector2 TextureCoords;

        /// <summary>
        /// The normal vector of this vertex.
        /// </summary>
        public readonly Vector3 Normal;

        /// <summary>
        /// The vertex's unmanaged declaration.
        /// </summary>
        public static readonly VertexDeclaration VertexDeclaration;

        static VertexPositionColorNormalTexture()
        {
            VertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
                new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0)
            );
        }

        public VertexPositionColorNormalTexture(Vector3 position, Color color, Vector2 textureCoordinates, Vector3 normal)
        {
            Position = position;
            Color = color;
            TextureCoords = textureCoordinates;
            Normal = normal;
        }
    }

    public static void RenderMissileInstance(Vector3 start, Matrix rotation, float baseStartingWidth, float baseEndingWidth, float bodyHeight, float tipHeight)
    {
        Vector3 finStart = start + Vector3.Transform(Vector3.UnitY * 2f, rotation);
        for (int i = 0; i < 4; i++)
        {
            float finAngle = TwoPi * i / 4f;
            DrawObelisk(5, Matrix.CreateRotationX(0.55f) * Matrix.CreateRotationZ(finAngle) * Matrix.CreateRotationX(PiOver2) * rotation, finStart, Vector3.UnitY * bodyHeight * 0.4f, baseEndingWidth, 0f);
        }

        DrawObelisk(5, rotation, start, Vector3.UnitY * bodyHeight, baseStartingWidth, baseEndingWidth);

        start += Vector3.Transform(Vector3.UnitY * bodyHeight, rotation);
        DrawObelisk(5, rotation, start, Vector3.UnitY * tipHeight, baseEndingWidth, 0f);
    }

    public static void DrawObelisk(int totalFaces, Matrix rotationMatrix, Vector3 startingPoint, Vector3 heightOffset, float outerRadius, float innerRadius)
    {
        Matrix correctiveOffset = Matrix.CreateTranslation(-startingPoint.X, -startingPoint.Y, -startingPoint.Z);
        Matrix antiCorrectiveOffset = Matrix.CreateTranslation(startingPoint.X, startingPoint.Y, startingPoint.Z);
        Matrix correctiveRotationMatrix = correctiveOffset * rotationMatrix * antiCorrectiveOffset;

        VertexPositionColorNormalTexture CreateVertex(Vector3 position, Vector2 textureCoordinate, Vector3 normal)
        {
            Vector3 alteredPosition = Vector3.Transform(position, correctiveRotationMatrix);
            Color color = Lighting.GetColor((int)(alteredPosition.X / 16f), (int)(alteredPosition.Y / 16f));
            return new(position, color, textureCoordinate, normal);
        }

        static short[] CreateIndices(int shapeCount)
        {
            short[] indices = new short[(shapeCount + 1) * 6];

            for (int i = 0; i < shapeCount + 1; i++)
            {
                short connectToIndex = (short)(i * 2);
                indices[i * 6] = connectToIndex;
                indices[i * 6 + 1] = (short)(connectToIndex + 1);
                indices[i * 6 + 2] = (short)(connectToIndex + 2);
                indices[i * 6 + 3] = (short)(connectToIndex + 2);
                indices[i * 6 + 4] = (short)(connectToIndex + 1);
                indices[i * 6 + 5] = (short)(connectToIndex + 3);
            }

            return indices;
        }

        List<VertexPositionColorNormalTexture[]> vertices = new List<VertexPositionColorNormalTexture[]>(totalFaces);
        List<short[]> indices = new List<short[]>(totalFaces);

        for (int i = 0; i <= totalFaces; i++)
        {
            Vector3 faceOffsetDirection = new Vector3(Cos(TwoPi * i / totalFaces), 0f, Sin(TwoPi * i / totalFaces));
            Vector3 faceOffsetDirectionNext = new Vector3(Cos(TwoPi * (i + 1) / totalFaces), 0f, Sin(TwoPi * (i + 1) / totalFaces));

            Vector3 normalLeft = faceOffsetDirection;
            Vector3 normalRight = faceOffsetDirectionNext;

            VertexPositionColorNormalTexture[] baseVertices = new VertexPositionColorNormalTexture[4];
            baseVertices[0] = CreateVertex(startingPoint + faceOffsetDirection * outerRadius, new(0f, 0f), normalLeft);
            baseVertices[1] = CreateVertex(startingPoint + faceOffsetDirection * innerRadius + heightOffset, new(0f, 1f), normalLeft);
            baseVertices[2] = CreateVertex(startingPoint + faceOffsetDirectionNext * outerRadius, new(1f, 0f), normalRight);
            baseVertices[3] = CreateVertex(startingPoint + faceOffsetDirectionNext * innerRadius + heightOffset, new(1f, 1f), normalRight);

            vertices.Add(baseVertices);
            indices.Add(CreateIndices(1));
        }

        Matrix viewMatrix = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)) *
            Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -300f, 300f);
        Matrix projectionMatrix = correctiveRotationMatrix * viewMatrix;

        ManagedShader tileShader = ShaderManager.GetShader("NoxusBoss.GenesisTileShader");
        tileShader.TrySetParameter("uWorldViewProjection", projectionMatrix);
        tileShader.TrySetParameter("lightDirection", Vector3.Transform(Vector3.UnitZ, Matrix.Invert(rotationMatrix)));
        tileShader.TrySetParameter("viewPosition", Vector2.Transform(Main.LocalPlayer.Center - Main.screenPosition, projectionMatrix));
        tileShader.SetTexture(WhitePixel, 1, SamplerState.LinearWrap);
        tileShader.Apply();

        vertices = vertices.OrderByDescending(v =>
        {
            float z = Vector3.Transform(v[0].Position, correctiveRotationMatrix * viewMatrix).Z;
            return z;
        }).ToList();

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        for (int i = 0; i < vertices.Count; i++)
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices[i], 0, vertices[i].Length, indices[i], 0, 2);
    }
}
