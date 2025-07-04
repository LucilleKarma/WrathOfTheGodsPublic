using System.Reflection;
using Luminance.Core.Graphics;
using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using NoxusBoss.Assets;
using Terraria;
using Terraria.ModLoader;
using static NoxusBoss.Core.Autoloaders.SolynBooks.SolynBookAutoloader;

namespace NoxusBoss.Content.Items;

public partial class SolynBooksSystem : ModSystem
{
    /// <summary>
    /// The 0-1 interpolant which dictates how many frames in a second of time are a consequence of deliberate lag when hovering over the book of shaders.
    /// </summary>
    /// 
    /// <remarks>
    /// As an example, a value of 0.6 indicates that 60% of frames in a second are consumed by deliberate lag.
    /// </remarks>
    public static float BookOfShadersSlowdownRatio => 0.56f;

    private static void LoadBookOfShaders()
    {
        new ManagedILEdit("Use shader on Book of Shaders tooltip background", ModContent.GetInstance<NoxusBoss>(), edit =>
        {
            IL_Main.MouseText_DrawItemTooltip += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Main.MouseText_DrawItemTooltip -= edit.SubscriptionWrapper;
        }, UseBookOfShadersBgShader).Apply();
    }

    private static void UseBookOfShadersBgShader(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);
        MethodInfo drawInventoryBgMethod = typeof(Utils).GetMethod("DrawInvBG", UniversalBindingFlags, new Type[]
        {
            typeof(SpriteBatch),
            typeof(Rectangle),
            typeof(Color)
        })!;
        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCallOrCallvirt(drawInventoryBgMethod)))
        {
            edit.LogFailure("Could not find the DrawInvBG call.");
            return;
        }
        cursor.EmitDelegate(() =>
        {
            if (Main.HoverItem.type == Books["TheBookOfShaders"].Type && !IsUnobtainedItemInUI(Main.HoverItem))
            {
                Main.spriteBatch.PrepareForShaders(null, true);

                ManagedShader bookOfShadersShader = ShaderManager.GetShader("NoxusBoss.BookOfShadersBackgroundShader");
                bookOfShadersShader.SetTexture(GennedAssets.Textures.Extra.Psychedelic, 1, SamplerState.LinearWrap);
                bookOfShadersShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 2, SamplerState.LinearWrap);
                bookOfShadersShader.Apply();

                // Obligatory making the shader book deliberately laggy.
                int millisecondsToThrowAway = (int)(BookOfShadersSlowdownRatio / (1f - BookOfShadersSlowdownRatio) * 60f);
                Thread.Sleep(millisecondsToThrowAway);
            }
        });

        cursor.Goto(0);
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt(drawInventoryBgMethod)))
        {
            edit.LogFailure("Could not find the DrawInvBG call.");
            return;
        }
        cursor.EmitDelegate(() =>
        {
            if (Main.HoverItem.type == Books["TheBookOfShaders"].Type && !IsUnobtainedItemInUI(Main.HoverItem))
            {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
        });
    }
}
