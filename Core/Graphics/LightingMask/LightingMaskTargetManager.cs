using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.LightingMask;

[Autoload(Side = ModSide.Client)]
public class LightingMaskTargetManager : ModSystem
{
    internal static InstancedRequestableTarget lightTarget = new InstancedRequestableTarget();

    /// <summary>
    /// The render target that holds all light information.
    /// </summary>
    public static Texture2D LightTarget
    {
        get
        {
            lightTarget.Request(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height, 0, () =>
            {
                int padding = 5;
                int left = (int)(Main.screenPosition.X / 16f - padding);
                int top = (int)(Main.screenPosition.Y / 16f - padding);
                int right = (int)(left + Main.instance.GraphicsDevice.Viewport.Width / 16f + padding);
                int bottom = (int)(top + Main.instance.GraphicsDevice.Viewport.Height / 16f + padding);
                Rectangle tileArea = new Rectangle(left, top, right - left, bottom - top);

                Vector3 screenPosition3 = new Vector3(Main.screenPosition, 0f);

                int horizontalSamples = tileArea.Width / 2 + 1;
                int verticalSamples = tileArea.Height / 2 + 1;
                int meshWidth = tileArea.Width + 1;
                int meshHeight = tileArea.Height + 1;
                int vertexCount = meshWidth * meshHeight;
                int indexCount = tileArea.Width * tileArea.Height * 6;
                short[] indices = new short[indexCount];
                VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[vertexCount];

                for (int j = 0; j < verticalSamples; j++)
                {
                    for (int i = 0; i < horizontalSamples; i++)
                    {
                        Lighting.GetCornerColors(tileArea.X + i * 2, tileArea.Y + j * 2, out Terraria.Graphics.VertexColors vertexColors);
                        bool rightEdge = i * 2 == tileArea.Width;
                        bool bottomEdge = j * 2 == tileArea.Height;

                        Vector2 topLeftUv = new Vector2(i * 2f / (meshWidth - 1), j * 2f / (meshHeight - 1));
                        Vector2 bottomRightUv = new Vector2((i * 2f + 1) / (meshWidth - 1), (j * 2f + 1) / (meshHeight - 1));

                        vertices[i * 2 + j * 2 * meshWidth] = new VertexPositionColorTexture(new Vector3(tileArea.X + i * 2, tileArea.Y + j * 2, 0f) * 16f - screenPosition3, vertexColors.TopLeftColor, topLeftUv);
                        if (!rightEdge)
                            vertices[i * 2 + 1 + j * 2 * meshWidth] = new VertexPositionColorTexture(new Vector3(tileArea.X + i * 2 + 1, tileArea.Y + j * 2, 0f) * 16f - screenPosition3, vertexColors.TopRightColor, new Vector2(bottomRightUv.X, topLeftUv.Y));
                        if (!bottomEdge)
                            vertices[i * 2 + (j * 2 + 1) * meshWidth] = new VertexPositionColorTexture(new Vector3(tileArea.X + i * 2, tileArea.Y + j * 2 + 1, 0f) * 16f - screenPosition3, vertexColors.BottomLeftColor, new Vector2(topLeftUv.X, bottomRightUv.Y));
                        if (!bottomEdge && !rightEdge)
                            vertices[i * 2 + 1 + (j * 2 + 1) * meshWidth] = new VertexPositionColorTexture(new Vector3(tileArea.X + i * 2 + 1, tileArea.Y + j * 2 + 1, 0f) * 16f - screenPosition3, vertexColors.BottomRightColor, bottomRightUv);
                    }
                }
                int currentIndex = 0;
                for (int j = 0; j < meshHeight - 1; j++)
                {
                    for (int i = 0; i < meshWidth - 1; i++)
                    {
                        indices[currentIndex] = (short)(i + j * meshWidth);
                        indices[currentIndex + 1] = (short)(i + 1 + j * meshWidth);
                        indices[currentIndex + 2] = (short)(i + (j + 1) * meshWidth);
                        indices[currentIndex + 3] = (short)(i + 1 + j * meshWidth);
                        indices[currentIndex + 4] = (short)(i + 1 + (j + 1) * meshWidth);
                        indices[currentIndex + 5] = (short)(i + (j + 1) * meshWidth);
                        currentIndex += 6;
                    }
                }

                ManagedShader shader = ShaderManager.GetShader("Luminance.StandardPrimitiveShader");
                shader.TrySetParameter("uWorldViewProjection", Matrix.CreateOrthographicOffCenter(0f, ViewportSize.X, ViewportSize.Y, 0f, 0f, 1f));
                shader.Apply();

                Main.instance.GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, currentIndex / 3);
            });

            if (lightTarget.TryGetTarget(0, out RenderTarget2D? target) && target is not null)
                return target;

            return WhitePixel.Value;
        }
    }

    public override void OnModLoad() => Main.ContentThatNeedsRenderTargets.Add(lightTarget);

    /// <summary>
    /// Prepares the light shader for application.
    /// </summary>
    public static void PrepareShader()
    {
        ManagedShader lightShader = ShaderManager.GetShader("NoxusBoss.LightMaskShader");
        lightShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        lightShader.TrySetParameter("screenSize", Main.ScreenSize.ToVector2());
        lightShader.SetTexture(LightTarget, 1);
        lightShader.Apply();
    }
}
