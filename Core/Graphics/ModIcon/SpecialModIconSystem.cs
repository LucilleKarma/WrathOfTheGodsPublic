using System.Reflection;
using Luminance.Core.Graphics;
using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.World.WorldSaving;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.ModIcon;

public class SpecialModIconSystem : ModSystem
{
    /// <summary>
    /// The mod icon responsible for Wrath of the Gods.
    /// </summary>
    private static UIArbitraryDrawImage wotgModIcon;

    /// <summary>
    /// TModLoader's internally defined UIModItem type.
    /// </summary>
    private static Type? modItemUIType;

    /// <summary>
    /// The IL edit responsible for the animated icon.
    /// </summary>
    public static ILHook OnInitializeHook
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether the mod icon should be joke-ified or not.
    /// </summary>
    public static bool JokeMode
    {
        get
        {
            DateTime now = DateTime.Now;
            return now.Month == 4 && now.Day == 1;
        }
    }

    /// <summary>
    /// The name of WoTG if Joke Mode is enabled.
    /// </summary>
    public const string JokeModeModName = "Calamity Mod Infernum Mode 2";

    public override void OnModLoad()
    {
        // Collect reflection information on the method to modify.
        modItemUIType = typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.UI.UIModItem");
        MethodInfo? onInitializeMethod = modItemUIType?.GetMethod("OnInitialize", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance) ?? null;

        if (onInitializeMethod is not null)
        {
            // Apply the IL edit.
            new ManagedILEdit("Create Animated Icon", Mod, edit =>
            {
                OnInitializeHook = new ILHook(onInitializeMethod, edit.SubscriptionWrapper);
            }, edit =>
            {
                OnInitializeHook?.Undo();
            }, AnimateModIcon).Apply();
        }

        // If the method does not exist for some reason, the edit is not applied and a warning is applied.
        else
            ModContent.GetInstance<NoxusBoss>().Logger.Warn("The animated icon IL edit could not find the relevant reflection information to apply the edit to.");

        if (JokeMode)
        {
            typeof(Mod).GetProperty("DisplayName", UniversalBindingFlags)?.SetValue(Mod, JokeModeModName);
            typeof(Mod).GetField("displayNameClean", UniversalBindingFlags)?.SetValue(Mod, null);
        }
    }

    // Undo the IL edit if it was successfully applied.
    public override void OnModUnload() => OnInitializeHook?.Undo();

    private static void AnimateModIcon(ILContext context, ManagedILEdit edit)
    {
        if (modItemUIType is null)
            return;

        ILCursor cursor = new ILCursor(context);

        // Temporarily store reflection data for searches below.
        FieldInfo? modIconField = modItemUIType.GetField("_modIcon", BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo? modNameProperty = modItemUIType.GetProperty("ModName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        if (modIconField is null)
        {
            edit.LogFailure("The _modIcon field could not be found.");
            return;
        }
        if (modNameProperty is null || modNameProperty.GetMethod is null)
        {
            edit.LogFailure("The ModName property could not be found.");
            return;
        }

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, modNameProperty.GetMethod);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Action<string, object>>((modName, instance) =>
        {
            if (JokeMode && modName == ModContent.GetInstance<NoxusBoss>().Name)
            {
                object? localMod = modItemUIType?.GetField("_mod", UniversalBindingFlags)?.GetValue(instance);
                object? properties = localMod?.GetType()?.GetField("properties", UniversalBindingFlags)?.GetValue(localMod);
                localMod?.GetType().GetField("DisplayNameClean", UniversalBindingFlags)?.SetValue(localMod, ModContent.GetInstance<NoxusBoss>().DisplayName);
                properties?.GetType().GetField("displayName", UniversalBindingFlags)?.SetValue(properties, ModContent.GetInstance<NoxusBoss>().DisplayName);
            }
        });

        // Search for the _modIcon storage. This is the UI element responsible for drawing the mod's icon, and is what must be replaced in order to perform custom animations.
        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStfld(modIconField)))
        {
            edit.LogFailure("The _modIcon storage could not be found.");
            return;
        }

        // Supply the mod name. Since this is right before the Store Field opcode the value that was going to originally stored is already on the stack and can be
        // safely included in this delegate.
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, modNameProperty.GetMethod);

        cursor.EmitDelegate<Func<UIImage, string, UIImage>>((originalImage, modName) =>
        {
            // Use a special icon portrait for Wrath of the Gods.
            // All other mods are left unaffected and the original UIImage portrait simply falls through.
            if (modName == ModContent.GetInstance<NoxusBoss>().Name)
                return GenerateWrathOfTheGodsIcon();

            return originalImage;
        });
    }

    private static UIArbitraryDrawImage GenerateWrathOfTheGodsIcon()
    {
        var iconTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/ModIcons/IconBase", AssetRequestMode.ImmediateLoad);
        wotgModIcon = new UIArbitraryDrawImage(DrawWrathOfTheGodsIcon, iconTexture);
        wotgModIcon.Left.Percent = 0f;
        wotgModIcon.Top.Percent = 0f;
        wotgModIcon.Width.Pixels = 80f;
        wotgModIcon.Height.Pixels = 80f;
        wotgModIcon.ScaleToFit = true;
        return wotgModIcon;
    }

    private static void DrawWrathOfTheGodsIcon(Texture2D texture, Vector2 drawPosition, Rectangle? rectangle, Color color, float rotation, Vector2 origin, Vector2 scale)
    {
        // Prepare for shader drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, DefaultRasterizerScreenCull, null, Main.UIScaleMatrix);
        Vector2 uiDrawPosition = Vector2.Transform(drawPosition, Main.UIScaleMatrix);
        Vector2 uiDrawScale = Vector2.Transform(Vector2.One * 80f, Main.UIScaleMatrix);
        Rectangle oldScissorRectangle = Main.instance.GraphicsDevice.ScissorRectangle;

        int bottom = Main.instance.GraphicsDevice.ScissorRectangle.Bottom;
        Rectangle newCutoutRectangle = new Rectangle((int)uiDrawPosition.X + 2, (int)uiDrawPosition.Y + 4, (int)uiDrawScale.X - 6, (int)uiDrawScale.Y - 14);

        // Ensure that the stars do not draw below the parent UI.
        if (newCutoutRectangle.Bottom > bottom)
            newCutoutRectangle.Height += bottom - newCutoutRectangle.Bottom;

        // Ensure that the stars do not draw above the parent UI.
        int distanceAboveThreshold = oldScissorRectangle.Y - newCutoutRectangle.Y;
        if (distanceAboveThreshold >= 1)
        {
            newCutoutRectangle.Y += distanceAboveThreshold;
            newCutoutRectangle.Height -= distanceAboveThreshold;
        }

        if (newCutoutRectangle.Height >= 1)
        {
            Main.instance.GraphicsDevice.ScissorRectangle = newCutoutRectangle;
            DrawBackground(drawPosition);
            Main.instance.GraphicsDevice.ScissorRectangle = oldScissorRectangle;
        }

        DrawIcon(drawPosition, rectangle, rotation, origin, scale);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, CullOnlyScreen, null, Main.UIScaleMatrix);
    }

    private static void DrawBackground(Vector2 drawPosition)
    {
        if (JokeMode)
            return;

        if (GlobalBossDownedSaveSystem.IsDefeated<NamelessDeityBoss>())
            DrawPostNamelessBackground(drawPosition);
        else if (GlobalBossDownedSaveSystem.IsDefeated<AvatarOfEmptiness>())
            DrawPostAvatarBackground(drawPosition);
        else
            DrawStandardBackground(drawPosition);
    }

    private static void DrawIcon(Vector2 drawPosition, Rectangle? rectangle, float rotation, Vector2 origin, Vector2 scale)
    {
        if (JokeMode)
        {
            Texture2D jokeIcon = GennedAssets.Textures.ModIcons.JokeIcon;
            Main.spriteBatch.Draw(jokeIcon, drawPosition, rectangle, Color.White, rotation, origin, scale, 0, 0f);
            return;
        }

        var iconTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/ModIcons/IconBase", AssetRequestMode.ImmediateLoad);
        Main.spriteBatch.Draw(iconTexture, drawPosition, rectangle, Color.White, rotation, origin, scale, 0, 0f);

        if (!GlobalBossDownedSaveSystem.IsDefeated<AvatarOfEmptiness>() || GlobalBossDownedSaveSystem.IsDefeated<NamelessDeityBoss>())
            DrawSolyn(drawPosition, rectangle, rotation, origin, scale);
    }

    private static void DrawSolyn(Vector2 drawPosition, Rectangle? rectangle, float rotation, Vector2 origin, Vector2 scale)
    {
        Texture2D solynTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/ModIcons/Solyn").Value;
        Rectangle solynFrame = solynTexture.Frame(1, 2, 0, (Main.GlobalTimeWrappedHourly % 5.1f <= 0.15f).ToInt());
        Color solynColor = Color.White;

        if (GlobalBossDownedSaveSystem.IsDefeated<NamelessDeityBoss>())
        {
            solynColor = new Color(255, 179, 250) * 0.87f;
            ManagedShader soulShader = ShaderManager.GetShader("NoxusBoss.SoulynShader");
            soulShader.TrySetParameter("outlineOnly", false);
            soulShader.TrySetParameter("imageSize", solynTexture.Size());
            soulShader.TrySetParameter("sourceRectangle", new Vector4(solynFrame.X, solynFrame.Y, solynFrame.Width, solynFrame.Height));
            soulShader.Apply();
        }

        Main.spriteBatch.Draw(solynTexture, drawPosition, solynFrame, solynColor, rotation, origin, scale, 0, 0f);
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        Texture2D appleTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/ModIcons/GoodApple").Value;
        Main.spriteBatch.Draw(appleTexture, drawPosition, rectangle, Color.White, rotation, origin, scale, 0, 0f);
    }

    private static void DrawPostAvatarBackground(Vector2 drawPosition)
    {
        Vector4[] why = new Vector4[]
        {
            new Color(84, 32, 49).ToVector4(),
            new Color(229, 44, 43).ToVector4(),
        };

        ManagedShader skyShader = ShaderManager.GetShader("NoxusBoss.AvatarPhase1BackgroundShader");
        skyShader.TrySetParameter("gradient", why);
        skyShader.TrySetParameter("gradientCount", why.Length);
        skyShader.SetTexture(WavyBlotchNoise, 1, SamplerState.LinearWrap);
        skyShader.SetTexture(WatercolorNoiseA, 2, SamplerState.LinearWrap);
        skyShader.Apply();

        Texture2D texture = WhitePixel.Value;
        Vector2 skySize = ViewportSize * 0.78f;
        Vector2 scale = skySize / texture.Size();
        Main.spriteBatch.Draw(texture, drawPosition, null, Color.White, 0f, texture.Size() * 0.5f, scale, 0, 0f);

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();
    }

    private static void DrawPostNamelessBackground(Vector2 drawPosition)
    {
        CosmicBackgroundSystem.StarZoomIncrement = 0.32f;
        CosmicBackgroundSystem.Draw(drawPosition + new Vector2(200f, 200f), 3f);
        CosmicBackgroundSystem.StarZoomIncrement = 0f;
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();
    }

    private static void DrawStandardBackground(Vector2 drawPosition)
    {
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        var texture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/ModIcons/IconStandardBackground", AssetRequestMode.ImmediateLoad);
        Main.spriteBatch.Draw(texture, drawPosition, null, Color.White, 0f, Vector2.Zero, 1f, 0, 0f);
    }
}
