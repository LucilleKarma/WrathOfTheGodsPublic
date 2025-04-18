using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.DataStructures.ShapeCurves;
using NoxusBoss.Core.Graphics;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Terraria.Utilities;

namespace NoxusBoss.Core.World.GameScenes.Stargazing;

[Autoload(Side = ModSide.Client)]
public class StargazingScene : ModSystem
{
    private static NPC riftDummy;

    private static bool isActive;

    private static string solynDialogueToSpellOut;

    internal static readonly List<StarViewObject> starViewObjects = new List<StarViewObject>(8);

    /// <summary>
    /// The text update timer used by Solyn when speaking.
    /// </summary>
    public static int TextUpdateTimer
    {
        get;
        set;
    }

    /// <summary>
    /// How much longer the meteor shower should go on for, in frames.
    /// </summary>
    public static int MeteorShowerCountdown
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the stargazing scene is active or not.
    /// </summary>
    public static bool IsActive
    {
        get => isActive;
        set
        {
            if (value)
                BlockerSystem.Start(true, false, () => isActive);
            isActive = value;
        }
    }

    /// <summary>
    /// Whether Solyn is present during the stargazing scene.
    /// </summary>
    public static bool SolynIsPresent
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn has already spoken about the meteor shower before.
    /// </summary>
    public static bool SolynSpokeAboutMeteorShower
    {
        get;
        set;
    }

    /// <summary>
    /// The dialogue Solyn has said thus far.
    /// </summary>
    public static string SolynSpokenDialogue
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The dialogue Solyn is trying to say.
    /// </summary>
    public static string SolynDialogueToSpellOut
    {
        get => solynDialogueToSpellOut;
        set
        {
            if (solynDialogueToSpellOut == value)
                return;

            SolynSpokenDialogue = string.Empty;
            solynDialogueToSpellOut = value;
        }
    }

    /// <summary>
    /// The opacity of Solyn's head when speaking.
    /// </summary>
    public static float SolynHeadOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The dialogue used when a meteor shower occurs.
    /// </summary>
    public static string MeteorShowerDialogue => Language.GetTextValue("Mods.NoxusBoss.StarGazing.MeteorShower.SolynDialogue");

    /// <summary>
    /// The view offset of the overall telescope.
    /// </summary>
    public static Vector2 ViewOffset
    {
        get;
        set;
    }

    /// <summary>
    /// The set of all meteor shower stars.
    /// </summary>
    public static List<MeteorShowerStar> MeteorShowerStars
    {
        get;
        set;
    } = new List<MeteorShowerStar>();

    /// <summary>
    /// The velocity of the view camera.
    /// </summary>
    public static Vector2 CameraVelocity
    {
        get;
        set;
    }

    /// <summary>
    /// The drums texture used in the star scene.
    /// </summary>
    public static LazyAsset<Texture2D> DrumsTexture
    {
        get;
        private set;
    }

    /// <summary>
    /// The galaxy texture used in the star scene.
    /// </summary>
    public static LazyAsset<Texture2D> GalaxyTexture
    {
        get;
        private set;
    }

    /// <summary>
    /// The moon texture used in the star scene.
    /// </summary>
    public static LazyAsset<Texture2D> MoonTexture
    {
        get;
        private set;
    }

    /// <summary>
    /// The currently selected star view object.
    /// </summary>
    public static StarViewObject? SelectedObject
    {
        get;
        internal set;
    }

    /// <summary>
    /// The character used internally to represent long pauses in dialogue.
    /// </summary>
    public const char SpacingCharacter = '^';

    /// <summary>
    /// The maximum offset that the view camera can reach.
    /// </summary>
    public static readonly Vector2 ViewEdge = Vector2.One * 1100f;

    /// <summary>
    /// The Avatar rift background object.
    /// </summary>
    public static readonly StarViewObject Rift = new StarViewObject(Utils.CenteredRectangle(new Vector2(356f, 1123f), Vector2.One * 150f), drawPosition =>
    {
        if (BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() && !SolynIsPresent)
            return;

        if (riftDummy is null)
        {
            riftDummy = new NPC();
            riftDummy.SetDefaults(ModContent.NPCType<AvatarRift>());
        }
        riftDummy.TopLeft = drawPosition;
        riftDummy.scale = 1f;
        riftDummy.As<AvatarRift>().DrawnFromTelescope = true;
        riftDummy.As<AvatarRift>().PreDraw(Main.spriteBatch, Vector2.Zero, Color.White);

        // The above PreDraw call resets the sprite batch at the end. It must be reset again to ensure consistency with the rest of the scene for drawing after this.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }, "Rift");

    /// <summary>
    /// The screaming nebula background object.
    /// </summary>
    public static readonly StarViewObject ScreamingNebula = new StarViewObject(Utils.CenteredRectangle(new Vector2(-532f, -40f), new(600f, 840f)), _ => { }, "ScreamingNebula");

    /// <summary>
    /// The Moon background object.
    /// </summary>
    public static readonly StarViewObject Moon = new StarViewObject(Utils.CenteredRectangle(new Vector2(2093f, -111f), new(660f, 660f)), drawPosition =>
    {
        Texture2D moonTexture = MoonTexture.Value;
        Color moonColor = Color.White;
        Color backglowColor = Color.Wheat * 0.6f;
        if (Main.bloodMoon)
        {
            moonColor = new(255, 192, 192);
            backglowColor = new(255, 102, 102);
        }
        backglowColor *= 0.6f;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, SubtractiveBlending, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, new Color(Vector3.One - backglowColor.ToVector3()), 0f, BloomCircleSmall.Size() * 0.5f, 14f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        Main.spriteBatch.Draw(moonTexture, drawPosition, null, moonColor, 0f, moonTexture.Size() * 0.5f, 1f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, backglowColor with { A = 0 }, 0f, BloomCircleSmall.Size() * 0.5f, 7f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, backglowColor with { A = 0 } * 0.4f, 0f, BloomCircleSmall.Size() * 0.5f, 14f, 0, 0f);
    }, "Moon", () =>
    {
        if (Main.bloodMoon)
            return "Mods.NoxusBoss.StarGazing.Moon.SolynDialogueBloodMoon";

        return "Mods.NoxusBoss.StarGazing.Moon.SolynDialogue";
    });

    /// <summary>
    /// The bunny constellation object.
    /// </summary>
    public static readonly StarViewObject BunnyConstellation = new StarViewObject(Utils.CenteredRectangle(new Vector2(2774f, 1096f), Vector2.One * 440f), drawPosition =>
    {
        DrawConstellation("Bunny", drawPosition, 0.17f, 390f, 0f);
    }, "Bunny");

    /// <summary>
    /// The galactic center constellation object.
    /// </summary>
    public static readonly StarViewObject GalacticCenter = new StarViewObject(Utils.CenteredRectangle(new Vector2(1112f, 960f), Vector2.One * 300f), _ => { }, "GalacticCenter");

    /// <summary>
    /// The empty patch of space that's identifiable for some reason.
    /// </summary>
    public static readonly StarViewObject EmptyPatchOfSpace = new StarViewObject(Utils.CenteredRectangle(new Vector2(-156f, 1623f), Vector2.One * 60f), _ => { }, "EmptyPatchOfSpaceTfAreYouEvenDoing", () =>
    {
        int dialogIndex = Utils.Clamp((EmptyPatchOfSpace?.TotalTimesInteracted ?? 0) + 1, 1, 13);
        return $"Mods.NoxusBoss.StarGazing.EmptyPatchOfSpaceTfAreYouEvenDoing.SolynDialogue{dialogIndex}";
    });

    /// <summary>
    /// The easter egg drums from Outer Wilds.
    /// </summary>
    public static readonly StarViewObject Drums = new StarViewObject(Utils.CenteredRectangle(new Vector2(-493f, -829f), Vector2.Zero), drawPosition =>
    {
        if (Main.LocalPlayer.name != "Hatchling")
            return;

        Texture2D drumsTexture = DrumsTexture.Value;
        Main.spriteBatch.Draw(drumsTexture, drawPosition, null, Color.White * 0.7f, Main.GlobalTimeWrappedHourly * 0.32f, drumsTexture.Size() * 0.5f, 0.5f, 0, 0f);
    }, string.Empty);

    public override void OnModLoad()
    {
        Main.OnPostDraw += DrawStarViewWrapper;

        DrumsTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("Skies/StargazingScene", "Drums"));
        GalaxyTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("Skies/StargazingScene", "Galaxy"));
        MoonTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("Skies/StargazingScene", "Moon"));
    }

    public override void OnModUnload() => Main.OnPostDraw -= DrawStarViewWrapper;

    private void DrawStarViewWrapper(GameTime obj)
    {
        if (!IsActive)
        {
            SelectedObject = null;
            SolynDialogueToSpellOut = string.Empty;
            SolynSpokenDialogue = string.Empty;
            ViewOffset = new(-840f, 500f);
            CameraVelocity = Vector2.Zero;
            MeteorShowerStars.Clear();
            SolynSpokeAboutMeteorShower = false;
            SolynHeadOpacity = 0f;
            MeteorShowerCountdown = 0;
            return;
        }

        if (!SolynIsPresent && PlayerInput.Triggers.JustPressed.Inventory)
            IsActive = false;

        // Keep the player immune to damage so that they aren't bothered by dumb NPCs while stargazing.
        Main.LocalPlayer.SetImmuneTimeForAllTypes(5);

        HandleMeteorShower();
        HandleInputMovement();

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        DrawScene();
        Main.spriteBatch.End();

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        Main.DrawCursor(Main.DrawThickCursor());
        Main.spriteBatch.End();
    }

    /// <summary>
    /// Handles the processing of input for camera movement.
    /// </summary>
    private static void HandleInputMovement()
    {
        Vector2 idealVelocity = Vector2.Zero;
        if (PlayerInput.Triggers.Current.Left)
            idealVelocity += Vector2.UnitX * 10f;
        if (PlayerInput.Triggers.Current.Right)
            idealVelocity -= Vector2.UnitX * 10f;
        if (PlayerInput.Triggers.Current.Up)
            idealVelocity += Vector2.UnitY * 10f;
        if (PlayerInput.Triggers.Current.Down)
            idealVelocity -= Vector2.UnitY * 10f;
        CameraVelocity = Vector2.Lerp(CameraVelocity, idealVelocity, 0.2f);

        ViewOffset = Vector2.Clamp(ViewOffset + CameraVelocity, -ViewEdge, ViewEdge);
    }

    /// <summary>
    /// Handles the updating and starting of meteor showers.
    /// </summary>
    private static void HandleMeteorShower()
    {
        if (Main.rand.NextBool(5400))
            MeteorShowerCountdown = SecondsToFrames(8f);

        if (MeteorShowerCountdown >= 1)
        {
            if (MeteorShowerCountdown % 3 == 0)
            {
                float starScale = Lerp(1f, 2f, Pow(Main.rand.NextFloat(), 5f));
                MeteorShowerStar star = new MeteorShowerStar(starScale, new Vector2(-ViewEdge.X, ViewOffset.Y * -1.5f) + new Vector2(Main.rand.NextFloat(-900f, ViewEdge.X * 4f), -800f), Vector2.One * Main.rand.NextFloat(11f, 15f));
                MeteorShowerStars.Add(star);
            }

            MeteorShowerCountdown--;
        }
        else
            SolynSpokeAboutMeteorShower = false;

        if (SolynIsPresent && !SolynSpokeAboutMeteorShower && MeteorShowerCountdown >= 1)
        {
            bool solynIsCurrentlyTalking = !string.IsNullOrEmpty(SolynSpokenDialogue) && SolynHeadOpacity > 0f;
            if (!solynIsCurrentlyTalking)
            {
                SolynDialogueToSpellOut = MeteorShowerDialogue;
                SolynSpokeAboutMeteorShower = true;
            }
        }

        MeteorShowerStars.ForEach(s => s.Update());
        MeteorShowerStars.RemoveAll(s => s.Scale <= 0f);
    }

    /// <summary>
    /// Draws the overall star scene, including the background, stars, and the Avatar's rift.
    /// </summary>
    private static void DrawScene()
    {
        Vector2 pixelSize = Vector2.One * 4000f;
        Vector2 screenSize = ViewportSize;
        Vector2 screenCenter = screenSize * 0.5f;

        DrawSceneBackground(screenCenter, screenSize, pixelSize);
        DrawGenericStars(screenCenter, screenSize, pixelSize);
        DrawViewableObjects();
        DrawViewableObjectSelections();
        DrawMeteorShowerStars();
        DrawTelescopeVignette(screenCenter, pixelSize);
        CompleteSolynDialogue();
        DrawSolynDialogue(screenSize);
    }

    /// <summary>
    /// Draws the background for the star scene, including bits of accenting from nebula clouds and similar things.
    /// </summary>
    /// <param name="screenCenter">The screen center position.</param>
    /// <param name="screenSize">The size of the screen.</param>
    /// <param name="pixelSize">How big the view should be.</param>
    private static void DrawSceneBackground(Vector2 screenCenter, Vector2 screenSize, Vector2 pixelSize)
    {
        Vector2 nebulaSampleOffset = Vector2.UnitX * 300f;
        ManagedShader backgroundShader = ShaderManager.GetShader("NoxusBoss.TelescopeBackgroundShader");
        backgroundShader.TrySetParameter("upscaleFactor", 1f);
        backgroundShader.TrySetParameter("viewOffset", (ViewOffset + nebulaSampleOffset) / screenSize.X);
        backgroundShader.TrySetParameter("nebulaColorA", new Vector3(1f, 0.15f, 0.5f));
        backgroundShader.TrySetParameter("nebulaColorB", new Vector3(0f, 1f, 0.68f));
        backgroundShader.TrySetParameter("nebulaColorC", new Vector3(0.3f, 0f, 3f));
        backgroundShader.TrySetParameter("nebulaColorExponent", 1.5f);
        backgroundShader.TrySetParameter("nebulaColorIntensity", 3.3f);
        backgroundShader.SetTexture(FireNoiseA, 1, SamplerState.LinearWrap);
        backgroundShader.SetTexture(WavyBlotchNoise, 2, SamplerState.LinearWrap);
        backgroundShader.SetTexture(DendriticNoiseZoomedOut, 3, SamplerState.LinearWrap);
        backgroundShader.Apply();

        Color backgroundColor = new Color(7, 7, 15);
        if (Main.bloodMoon)
        {
            backgroundColor.R += 17;
            backgroundColor.B -= 4;
        }

        Main.spriteBatch.Draw(WhitePixel, screenCenter, null, backgroundColor, 0f, WhitePixel.Size() * 0.5f, pixelSize, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        Texture2D galaxy = GalaxyTexture.Value;
        Vector2 galaxyDrawPosition = new Vector2(1110f, 817f) + ViewOffset * 1.5f;
        Main.spriteBatch.Draw(galaxy, galaxyDrawPosition, null, Color.White * 0.72f, 0.98f, galaxy.Size() * 0.5f, 2.5f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }

    /// <summary>
    /// Draws generic stars for the star scene.
    /// </summary>
    /// <param name="screenCenter">The screen center position.</param>
    /// <param name="screenSize">The size of the screen.</param>
    /// <param name="pixelSize">How big the view should be.</param>
    private static void DrawGenericStars(Vector2 screenCenter, Vector2 screenSize, Vector2 pixelSize)
    {
        ManagedShader starFieldShader = ShaderManager.GetShader("NoxusBoss.TelescopeStarFieldShader");
        starFieldShader.TrySetParameter("scrollOffset", ViewOffset / pixelSize * 1.5f);
        starFieldShader.TrySetParameter("pixelSize", pixelSize);
        starFieldShader.TrySetParameter("twinkleSpeed", 1.6f);
        starFieldShader.SetTexture(ViscousNoise, 1, SamplerState.LinearWrap);
        starFieldShader.Apply();

        Main.spriteBatch.Draw(WhitePixel, screenCenter, null, Color.White with { A = 0 }, 0f, WhitePixel.Size() * 0.5f, pixelSize, 0, 0f);
        StarGazingStarsRenderer.Render(1f, Matrix.CreateTranslation(ViewOffset.X / screenSize.X * 0.5f, ViewOffset.Y / screenSize.Y * 0.5f, 0f) * Matrix.CreateScale(1f, 1.5f, 1f));
    }

    /// <summary>
    /// Renders all meteor shower stars.
    /// </summary>
    private static void DrawMeteorShowerStars()
    {
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        MeteorShowerStars.ForEach(s => s.Render(ViewOffset * 1.5f));
    }

    /// <summary>
    /// Draws all viewable objects, such as the Avatar's rift.
    /// </summary>
    private static void DrawViewableObjects()
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        foreach (StarViewObject viewObject in starViewObjects)
        {
            if (SolynIsPresent)
                viewObject.UpdateOutline(ViewOffset * 1.5f);
            viewObject.DrawAction?.Invoke(viewObject.InteractionArea.Center() + ViewOffset * 1.5f);
        }
    }

    /// <summary>
    /// Draws all viewable objects' selection info, assuming Solyn is present.
    /// </summary>
    private static void DrawViewableObjectSelections()
    {
        if (!SolynIsPresent)
            return;

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        foreach (StarViewObject viewObject in starViewObjects)
            viewObject.DrawSelected(ViewOffset * 1.5f);
    }

    /// <summary>
    /// Draws the circular telescope view vignette.
    /// </summary>
    /// <param name="screenCenter">The screen center position.</param>
    /// <param name="pixelSize">How big the view should be.</param>
    private static void DrawTelescopeVignette(Vector2 screenCenter, Vector2 pixelSize)
    {
        ManagedShader vignetteShader = ShaderManager.GetShader("NoxusBoss.GenericVignetteShader");
        vignetteShader.TrySetParameter("vignetteEdgeStart", 0.15f);
        vignetteShader.TrySetParameter("vignetteEdgeEnd", 0.21f);
        vignetteShader.Apply();

        Main.spriteBatch.Draw(WhitePixel, screenCenter, null, Color.Black, 0f, WhitePixel.Size() * 0.5f, pixelSize, 0, 0f);
    }

    /// <summary>
    /// Draws Solyn's dialogue.
    /// </summary>
    /// <param name="screenSize">The size of the screen.</param>
    private static void DrawSolynDialogue(Vector2 screenSize)
    {
        if (!SolynIsPresent)
            return;

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        bool speakingAboutMeteorShower = SolynSpokeAboutMeteorShower && SolynDialogueToSpellOut == MeteorShowerDialogue;
        SolynHeadOpacity = Saturate(SolynHeadOpacity + (SelectedObject is not null || speakingAboutMeteorShower).ToDirectionInt() * 0.045f);

        Texture2D solynHead = GennedAssets.Textures.Friendly.Solyn_Head;
        float solynHeadScale = EasingCurves.Elastic.Evaluate(EasingType.Out, SolynHeadOpacity) * 1.8f;
        Vector2 headIconDrawPosition = screenSize * new Vector2(1f, 0.75f) - Vector2.UnitX * 640f;
        Main.spriteBatch.Draw(solynHead, headIconDrawPosition, null, Color.White * SolynHeadOpacity, 0f, solynHead.Size() * 0.5f, solynHeadScale, SpriteEffects.FlipHorizontally, 0f);

        var font = FontRegistry.Instance.SolynText;
        float verticalSpacing = 10f;
        string formattedDialogue = SolynSpokenDialogue?.Replace(SpacingCharacter.ToString(), string.Empty) ?? string.Empty;
        Vector2 creditTextScale = Vector2.One * 0.7f;
        Color textColor = DialogColorRegistry.SolynTextColor * SolynHeadOpacity;
        foreach (string dialogueLine in Utils.WordwrapString(formattedDialogue, font, 800, 30, out _))
        {
            if (string.IsNullOrEmpty(dialogueLine))
                continue;

            bool containsManualEndline = dialogueLine.Contains('\n');
            string effectiveDialogueLine = dialogueLine.Replace("\n", string.Empty);
            Vector2 rantTextSize = font.MeasureString(effectiveDialogueLine);
            Vector2 rantDrawPosition = headIconDrawPosition + new Vector2(40f, verticalSpacing);
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, effectiveDialogueLine, rantDrawPosition, textColor, 0f, Vector2.UnitY * rantTextSize * 0.5f, creditTextScale, 0, 0f);

            verticalSpacing += creditTextScale.Y * (containsManualEndline ? 80f : 50f);
        }
    }

    /// <summary>
    /// Spells out Solyn's dialogue over time.
    /// </summary>
    private static void CompleteSolynDialogue()
    {
        if (!SolynIsPresent)
            return;

        if (SelectedObject is null || SolynDialogueToSpellOut is null)
        {
            if (SolynSpokenDialogue == Language.GetTextValue(Rift.DialogueKey()))
            {
                IsActive = false;
                SolynIsPresent = false;
                ModContent.GetInstance<StargazingEvent>().SafeSetStage(2);

                if (SolynEvent.Solyn is not null)
                    Main.LocalPlayer.SetTalkNPC(SolynEvent.Solyn.NPC.whoAmI);

                TotalScreenOverlaySystem.OverlayColor = Color.Black;
                TotalScreenOverlaySystem.OverlayInterpolant = 1f;
            }

            if (SolynDialogueToSpellOut != MeteorShowerDialogue)
                SolynDialogueToSpellOut = string.Empty;
        }

        TextUpdateTimer++;

        // Spell out dialogue.
        int textUpdateRate = 1;
        if (SolynSpokenDialogue.Length < SolynDialogueToSpellOut.Length && SolynHeadOpacity >= 0.7f)
        {
            char nextCharacter = SolynDialogueToSpellOut[SolynSpokenDialogue.Length];
            if (nextCharacter == SpacingCharacter)
                textUpdateRate = 45;

            if (TextUpdateTimer >= textUpdateRate)
            {
                if (nextCharacter != ' ' && nextCharacter != SpacingCharacter)
                    SoundEngine.PlaySound(GennedAssets.Sounds.Solyn.Speak with { Volume = 0.4f, MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew });
                SolynSpokenDialogue += nextCharacter;
                TextUpdateTimer = 0;

                if (SelectedObject is not null && SolynSpokenDialogue == SolynDialogueToSpellOut)
                    SelectedObject.TotalTimesInteracted++;
            }
        }
    }

    public static void DrawConstellation(string constellationName, Vector2 drawPosition, float rotation = 0f, float upscaleFactor = 200f, float starVarianceFactor = 0.09f)
    {
        // Don't do anything if no valid constellation could not be found.
        if (!ShapeCurveManager.TryFind(constellationName, out ShapeCurve? curve) || curve is null)
            return;

        Texture2D beautifulStarTexture = GennedAssets.Textures.GreyscaleTextures.Star.Value;
        curve = curve.Upscale(upscaleFactor);
        UnifiedRandom rng = new UnifiedRandom(1904);

        for (int i = 0; i < curve.ShapePoints.Count; i++)
        {
            Vector2 constellationOffset = curve.ShapePoints[i] + rng.NextVector2Circular(10f, 10f);
            Vector2 starDrawPosition = (drawPosition + constellationOffset).RotatedBy(rotation, drawPosition);
            Terraria.Star star = new Terraria.Star()
            {
                twinkle = Lerp(0.4f, 0.87f, Cos01(i / (float)curve.ShapePoints.Count * 20f + Main.GlobalTimeWrappedHourly * 1.75f)),
                scale = (1f - NamelessDeitySky.StarRecedeInterpolant) * 2f,
                position = starDrawPosition * starVarianceFactor
            };

            float opacity = star.twinkle * 0.36f;
            float starRotation = Main.GlobalTimeWrappedHourly * 0.5f + i;
            float hueInterpolant = rng.NextFloat().Squared();
            Color bloomColor = (EternalGardenSkyStarRenderer.StarPalette.SampleColor(hueInterpolant) with { A = 0 }) * opacity * 0.3f;
            Color twinkleColor = new Color(255, 255, 255, 0) * opacity * 0.6f;
            TwinkleParticle.DrawTwinkle(beautifulStarTexture, starDrawPosition, 8, starRotation, bloomColor, twinkleColor, Vector2.One * star.scale * star.twinkle * 0.017f, 0.7f);
        }
    }
}
