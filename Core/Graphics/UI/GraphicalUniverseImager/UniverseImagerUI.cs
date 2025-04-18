using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.GameScenes.AvatarAppearances;
using NoxusBoss.Core.World.TileDisabling;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;

public class UniverseImagerUI : ModSystem
{
    /// <summary>
    /// The countdown that dictates how long the left button ticker should be down.
    /// </summary>
    public int LeftButtonClickTimer;

    /// <summary>
    /// The countdown that dictates how long the right button ticker should be down.
    /// </summary>
    public int RightButtonClickTimer;

    /// <summary>
    /// The countdown that dictates how long the music left button ticker should be down.
    /// </summary>
    public int LeftMusicButtonClickTimer;

    /// <summary>
    /// The countdown that dictates how long the music right button ticker should be down.
    /// </summary>
    public int RightMusicButtonClickTimer;

    /// <summary>
    /// The horizontal scroll of the main UI panel.
    /// </summary>
    public float MainPanelHorizontalScroll
    {
        get;
        set;
    }

    /// <summary>
    /// The ideal horizontal scroll of the main UI panel.
    /// </summary>
    public float IdealMainPanelHorizontalScroll
    {
        get;
        set;
    }

    /// <summary>
    /// The 0-1 interpolant for how far along the main panel click animation is.
    /// </summary>
    public float MainPanelClickAnimationInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The 0-1 interpolant for how much the rift eclipse options UI have appeared.
    /// </summary>
    public float EclipseOptionsAppearanceInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the eclipse scroll bar ticker is being dragged currently.
    /// </summary>
    public bool DraggingEclipseScrollTicker
    {
        get;
        set;
    }

    /// <summary>
    /// The tile entity that's currently visible.
    /// </summary>
    public TEGraphicalUniverseImager? VisibleTileEntity
    {
        get;
        set;
    }

    /// <summary>
    /// The tile entity that's affecting the background.
    /// </summary>
    public TEGraphicalUniverseImager? ActiveTileEntity
    {
        get;
        set;
    }

    /// <summary>
    /// The previously clicked option in the UI.
    /// </summary>
    public GraphicalUniverseImagerOption? ClickedOption
    {
        get;
        set;
    }

    /// <summary>
    /// The render target that shows the various backgrounds.
    /// </summary>
    public InstancedRequestableTarget BackgroundPreviewTarget
    {
        get;
        set;
    } = new InstancedRequestableTarget();

    /// <summary>
    /// The render target that shows the overall carousel.
    /// </summary>
    public InstancedRequestableTarget CarouselTarget
    {
        get;
        set;
    } = new InstancedRequestableTarget();

    /// <summary>
    /// Whether the music options selector is ready for relase yet.
    /// </summary>
    public static bool MusicOptionsExist => false;

    public override void OnModLoad()
    {
        Main.ContentThatNeedsRenderTargets.Add(BackgroundPreviewTarget);
        Main.ContentThatNeedsRenderTargets.Add(CarouselTarget);
    }

    public override void OnWorldLoad() => Reset();

    public override void OnWorldUnload() => Reset();

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int inventoryIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");
        if (inventoryIndex == -1)
            return;

        layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer("Wrath of the Gods: Graphical Universe Imager UI", () =>
        {
            RenderUI();
            return true;
        }, InterfaceScaleType.Game));
    }

    /// <summary>
    /// Resets all local UI variables.
    /// </summary>
    public void Reset()
    {
        MainPanelHorizontalScroll = 0f;
        MainPanelClickAnimationInterpolant = 0f;
        IdealMainPanelHorizontalScroll = 0f;
        EclipseOptionsAppearanceInterpolant = 0f;
        DraggingEclipseScrollTicker = false;
        ClickedOption = null;
    }

    private void RenderUI()
    {
        if (VisibleTileEntity is null)
        {
            Reset();
            return;
        }

        // Make sure the tile didn't get broken.
        Point16 tilePosition = VisibleTileEntity.Position;
        TEGraphicalUniverseImager? tileEntity = FindTileEntity<TEGraphicalUniverseImager>(tilePosition.X, tilePosition.Y, GraphicalUniverseImagerTile.Width, GraphicalUniverseImagerTile.Height);
        if (tileEntity is null ||
            Framing.GetTileSafely(tilePosition.X, tilePosition.Y).TileType != ModContent.TileType<GraphicalUniverseImagerTile>())
        {
            VisibleTileEntity = null;
            return;
        }

        float opacity = VisibleTileEntity.UIAppearanceInterpolant;
        Vector2 drawPosition = tilePosition.ToVector2() * 16f + new Vector2(GraphicalUniverseImagerTile.Width * 8f, 0f) - Main.screenPosition - Vector2.UnitY * 142f;
        GraphicalUniverseImagerSettings settings = VisibleTileEntity.Settings;
        GraphicalUniverseImagerSettings original = settings.GenerateCopy();

        bool selectedEclipse = (VisibleTileEntity.Settings.Option?.LocalizationKey ?? string.Empty) == RiftEclipseSkyScene.RiftEclipseGUIOption.LocalizationKey;
        EclipseOptionsAppearanceInterpolant = Saturate(EclipseOptionsAppearanceInterpolant + selectedEclipse.ToDirectionInt() * 0.027f);
        if (EclipseOptionsAppearanceInterpolant > 0f)
            RenderRiftEclipseOptions(settings, drawPosition, opacity);

        RenderMainOptions(settings, drawPosition, opacity);
        if (MusicOptionsExist)
            RenderMusicOption(settings, drawPosition, InverseLerp(0.5f, 1f, opacity));

        if (!settings.SameAs(original))
            PacketManager.SendPacket<GraphicalUniverseImagerSettingsPacket>(tileEntity.ID);
    }

    private void RenderMainOptions(GraphicalUniverseImagerSettings settings, Vector2 drawPosition, float opacity)
    {
        List<GraphicalUniverseImagerOption> options = GraphicalUniverseImagerOptionManager.options.Values.ToList();

        // Update panel variables.
        MainPanelHorizontalScroll = Lerp(MainPanelHorizontalScroll, IdealMainPanelHorizontalScroll, 0.12f).StepTowards(IdealMainPanelHorizontalScroll, 0.001f);
        if (MainPanelClickAnimationInterpolant > 0f)
        {
            MainPanelClickAnimationInterpolant += 0.15f;
            if (MainPanelClickAnimationInterpolant > 1f)
                MainPanelClickAnimationInterpolant = 0f;
        }

        Vector2 backdropArea = GennedAssets.Textures.GraphicalUniverseImager.MainSelector_Backdrop.Size();
        Vector2 carouselTargetArea = new Vector2((backdropArea.X + 40f) * 5f, backdropArea.Y);
        bool renderingOutline = Utils.CenteredRectangle(drawPosition, backdropArea).Intersects(Utils.CenteredRectangle(Main.MouseScreen, Vector2.One));
        CarouselTarget.Request((int)carouselTargetArea.X, (int)carouselTargetArea.Y, 0, () =>
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
            for (int i = 0; i < options.Count; i++)
            {
                float outlineOpacity = VisibleTileEntity?.Settings.Option == options[i] ? 1f : 0f;
                float scrollOffset = (i - MainPanelHorizontalScroll).Modulo(options.Count) - 2f;
                float scale = 1f;

                // Make the panel shrink a bit when clicked.
                if (MainPanelClickAnimationInterpolant > 0f && options[i] == ClickedOption)
                    scale -= Convert01To010(MainPanelClickAnimationInterpolant.Squared()) * 0.11f;

                Vector2 panelDrawOffset = Vector2.UnitX * scrollOffset * (backdropArea.X + 40f);
                Rectangle panelArea = Utils.CenteredRectangle(drawPosition, backdropArea * scale);

                RenderPanelCarousel(settings, options[i], i, renderingOutline && Abs(scrollOffset) <= 0.3f, carouselTargetArea * 0.5f + panelDrawOffset, outlineOpacity, opacity, scale);
            }
            Main.spriteBatch.End();
        });

        int mainOptionIndex = (int)(IdealMainPanelHorizontalScroll + 2f).Modulo(options.Count);
        HandleCarouselClickInteractions(settings, options[mainOptionIndex], Utils.CenteredRectangle(drawPosition, backdropArea));

        if (!CarouselTarget.TryGetTarget(0, out RenderTarget2D? target) || target is null)
            return;

        Main.spriteBatch.PrepareForShaders();

        ManagedShader gradientShader = ShaderManager.GetShader("NoxusBoss.HorizontalGradientFade");
        gradientShader.TrySetParameter("leftFadeStart", 0.2f);
        gradientShader.TrySetParameter("leftFadeEnd", 0.4f);
        gradientShader.TrySetParameter("rightFadeStart", 0.6f);
        gradientShader.TrySetParameter("rightFadeEnd", 0.8f);
        gradientShader.Apply();

        Main.spriteBatch.Draw(target, drawPosition, null, Color.White, 0f, target.Size() * 0.5f, 1f, 0, 0f);

        Main.spriteBatch.ResetToDefault();

        // Draw the left scroll arrow.
        float arrowOffset = 24f;
        Vector2 leftArrowDrawPosition = drawPosition - Vector2.UnitX * (backdropArea.X * 0.5f + arrowOffset);
        RenderMainPanelArrow(leftArrowDrawPosition, opacity, false, ref LeftButtonClickTimer);

        // Draw the right scroll arrow.
        Vector2 rightArrowDrawPosition = drawPosition + Vector2.UnitX * (backdropArea.X * 0.5f + arrowOffset);
        RenderMainPanelArrow(rightArrowDrawPosition, opacity, true, ref RightButtonClickTimer);
    }

    private void RenderPanelCarousel(GraphicalUniverseImagerSettings settings, GraphicalUniverseImagerOption viewedOption, int identifier, bool renderingOutline, Vector2 drawPosition, float outlineOpacity, float opacity, float scale)
    {
        if (VisibleTileEntity is null)
            return;

        Vector2 viewportArea = ViewportSize;
        Rectangle mouseArea = Utils.CenteredRectangle(Main.MouseScreen, Vector2.One);
        DynamicSpriteFont font = FontAssets.DeathText.Value;
        Texture2D backdrop = GennedAssets.Textures.GraphicalUniverseImager.MainSelector_Backdrop;
        Texture2D border = GennedAssets.Textures.GraphicalUniverseImager.MainSelector_ActiveBorder;
        if (renderingOutline)
            border = GennedAssets.Textures.GraphicalUniverseImager.MainSelector_HoverBorder;

        BackgroundPreviewTarget.Request(400, 400, identifier, () => viewedOption.PortraitRenderFunction(settings));
        if (!BackgroundPreviewTarget.TryGetTarget(identifier, out RenderTarget2D? target) || target is null)
            return;

        // Render the shader-affected backdrop.
        ManagedShader portraitShader = ShaderManager.GetShader("NoxusBoss.PortraitOverlayShader");
        portraitShader.SetTexture(target, 1);
        portraitShader.Apply();
        Main.spriteBatch.Draw(backdrop, drawPosition, null, Color.White * opacity, 0f, backdrop.Size() * 0.5f, scale, 0, 0f);
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        // Render the info text.
        string text = Language.GetTextValue(viewedOption.LocalizationKey);
        Vector2 textDrawPosition = drawPosition - Vector2.One * backdrop.Size() * 0.5f * scale + Vector2.One * 10f;
        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, text, textDrawPosition, Color.White * opacity, 0f, Vector2.Zero, Vector2.One * scale * 0.37f);

        // Render the icon.
        Texture2D icon = viewedOption.IconTexture.Value;
        Vector2 iconDrawPosition = drawPosition + (backdrop.Size() * 0.5f - new Vector2(28f, 26f)) * scale;
        Main.spriteBatch.Draw(icon, iconDrawPosition, null, Color.White * opacity, 0f, icon.Size() * 0.5f, scale, 0, 0f);

        // Draw the border if selected.
        Color borderColor = Color.White * outlineOpacity * opacity;
        if (renderingOutline)
            borderColor = Color.White * opacity;

        Main.spriteBatch.Draw(border, drawPosition, null, borderColor, 0f, border.Size() * 0.5f, scale, 0, 0f);
    }

    private void HandleCarouselClickInteractions(GraphicalUniverseImagerSettings settings, GraphicalUniverseImagerOption viewedOption, Rectangle panelArea)
    {
        Rectangle mouseArea = Utils.CenteredRectangle(Main.MouseScreen, Vector2.One);
        if (mouseArea.Intersects(panelArea))
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.blockMouse = true;
            if (Main.mouseLeft && Main.mouseLeftRelease && (MainPanelHorizontalScroll % 1f <= 0.1f || MainPanelHorizontalScroll % 1f >= 0.9f))
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                MainPanelClickAnimationInterpolant = 0.01f;

                ClickedOption = viewedOption;
                if (settings.Option != viewedOption)
                    settings.Option = viewedOption;
                else
                    settings.Option = null;
            }
        }
    }

    private void RenderRiftEclipseOptions(GraphicalUniverseImagerSettings settings, Vector2 drawPosition, float opacity)
    {
        float riseInterpolant = EasingCurves.Cubic.Evaluate(EasingType.InOut, EclipseOptionsAppearanceInterpolant);
        Vector2 scrollBarDrawPosition = drawPosition - Vector2.UnitY * Lerp(50f, 100f, riseInterpolant);

        opacity *= riseInterpolant;

        // Draw the eclipse intensity scroll bar.
        float scrollbarWidth = 150f;
        Texture2D scrollBar = GennedAssets.Textures.GraphicalUniverseImager.Scrollbar.Value;
        Rectangle scrollBarCenterFrame = new Rectangle(6, 0, 8, 16);
        Vector2 scrollBarCenterScale = new Vector2((scrollbarWidth - 12f) / scrollBarCenterFrame.Width, 1f);
        Main.spriteBatch.Draw(scrollBar, scrollBarDrawPosition, scrollBarCenterFrame, Color.White * opacity, 0f, scrollBarCenterFrame.Size() * 0.5f, scrollBarCenterScale, 0, 0f);

        Rectangle scrollBarLeftFrame = new Rectangle(0, 0, 6, 16);
        Rectangle scrollBarRightFrame = new Rectangle(14, 0, 6, 16);
        Main.spriteBatch.Draw(scrollBar, scrollBarDrawPosition - Vector2.UnitX * scrollbarWidth * 0.5f, scrollBarLeftFrame, Color.White * opacity, 0f, scrollBarLeftFrame.Size() * new Vector2(0f, 0.5f), 1f, 0, 0f);
        Main.spriteBatch.Draw(scrollBar, scrollBarDrawPosition + Vector2.UnitX * scrollbarWidth * 0.5f, scrollBarRightFrame, Color.White * opacity, 0f, scrollBarRightFrame.Size() * new Vector2(1f, 0.5f), 1f, 0, 0f);

        // Draw the scroll bar ticker.
        ref float barTickerInterpolant = ref settings.RiftSize;
        Texture2D scrollBarTicker = GennedAssets.Textures.GraphicalUniverseImager.ScrollbarTicker.Value;

        float maxTickerOffset = scrollbarWidth * 0.5f - 10f;
        Vector2 tickerOffset = Vector2.UnitX * Lerp(-maxTickerOffset, maxTickerOffset, barTickerInterpolant);
        Vector2 tickerDrawPosition = scrollBarDrawPosition + tickerOffset;

        float tickerLeft = scrollBarDrawPosition.X - maxTickerOffset;
        float tickerRight = scrollBarDrawPosition.X + maxTickerOffset;

        Rectangle mouseArea = Utils.CenteredRectangle(Main.MouseScreen, Vector2.One);
        bool hoveringOverBar = Utils.CenteredRectangle(scrollBarDrawPosition, new Vector2(scrollbarWidth, scrollBarCenterFrame.Height)).Intersects(mouseArea);
        bool hoveringOverTicker = Utils.CenteredRectangle(tickerDrawPosition, scrollBarTicker.Size()).Intersects(mouseArea);
        Color tickerColor = DraggingEclipseScrollTicker || hoveringOverTicker ? Color.Yellow : Color.White;
        Main.spriteBatch.Draw(scrollBarTicker, tickerDrawPosition, null, tickerColor * opacity, 0f, scrollBarTicker.Size() * 0.5f, 1f, 0, 0f);

        if (hoveringOverBar)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.blockMouse = true;
            if (!hoveringOverTicker && Main.mouseLeft && Main.mouseLeftRelease)
            {
                barTickerInterpolant = InverseLerp(tickerLeft, tickerRight, Main.MouseScreen.X);
                DraggingEclipseScrollTicker = true;
            }
        }

        // Handle scroll bar dragging.
        if (hoveringOverTicker && Main.mouseLeft && Main.mouseLeftRelease && !DraggingEclipseScrollTicker)
            DraggingEclipseScrollTicker = true;
        if (!Main.mouseLeft)
            DraggingEclipseScrollTicker = false;
        if (DraggingEclipseScrollTicker)
            barTickerInterpolant = InverseLerp(tickerLeft, tickerRight, Main.MouseScreen.X);

        // Render detail selection icons.
        float expandInterpolant = EasingCurves.Cubic.Evaluate(EasingType.InOut, Pow(EclipseOptionsAppearanceInterpolant, 0.7f));
        Vector2 detailIconDrawCenter = scrollBarDrawPosition - Vector2.UnitY * riseInterpolant * 54f;
        RenderRiftEclipseDetailIcon(settings, GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Fog, detailIconDrawCenter - Vector2.UnitX * expandInterpolant * 30f, opacity);
        RenderRiftEclipseDetailIcon(settings, GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Blizzard, detailIconDrawCenter + Vector2.UnitX * expandInterpolant * 30f, opacity);
        RenderRiftEclipseDetailIcon(settings, GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.None, detailIconDrawCenter - Vector2.UnitX * expandInterpolant * 90f, opacity);
        RenderRiftEclipseDetailIcon(settings, GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.BloodRain, detailIconDrawCenter + Vector2.UnitX * expandInterpolant * 90f, opacity);
    }

    private static void RenderRiftEclipseDetailIcon(GraphicalUniverseImagerSettings settings, GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting detail, Vector2 drawPosition, float opacity)
    {
        Texture2D icon = detail switch
        {
            GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Blizzard => GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBox_Blizzard.Value,
            GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.BloodRain => GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBox_BloodMoon.Value,
            GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Fog => GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBox_Fog.Value,
            _ => GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBox_Base.Value,
        };

        Texture2D background = GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBoxButton;
        Rectangle mouseArea = Utils.CenteredRectangle(Main.MouseScreen, Vector2.One);
        Rectangle iconArea = Utils.CenteredRectangle(drawPosition, background.Size());
        if (mouseArea.Intersects(iconArea))
        {
            background = GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBoxButtonHovered;
            Main.LocalPlayer.mouseInterface = true;
            Main.blockMouse = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
                settings.EclipseAmbienceSettings = detail;
        }
        if (detail == settings.EclipseAmbienceSettings)
            background = GennedAssets.Textures.GraphicalUniverseImager.EclipseSelectionBoxButtonPressed;

        Main.spriteBatch.Draw(background, drawPosition, null, Color.White * opacity, 0f, background.Size() * 0.5f, 1f, 0, 0f);
        Main.spriteBatch.Draw(icon, drawPosition, null, Color.White * opacity, 0f, icon.Size() * 0.5f, 1f, 0, 0f);
    }

    private void RenderMainPanelArrow(Vector2 drawPosition, float opacity, bool right, ref int clickTimer)
    {
        Texture2D arrow = GennedAssets.Textures.GraphicalUniverseImager.MainlSelector_Arrows.Value;
        Vector2 arrowArea = arrow.Frame(2, 3, 0, 0).Size();
        Rectangle mouseArea = Utils.CenteredRectangle(Main.MouseScreen, Vector2.One);

        if (clickTimer > 0)
            clickTimer--;

        int frameY = 0;
        if (Utils.CenteredRectangle(drawPosition, arrowArea).Intersects(mouseArea))
        {
            frameY = 1;

            Main.LocalPlayer.mouseInterface = true;
            Main.blockMouse = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                IdealMainPanelHorizontalScroll += right.ToDirectionInt();
                clickTimer = 7;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }
        if (clickTimer >= 1)
            frameY = 2;

        Rectangle arrowFrame = arrow.Frame(2, 3, right.ToInt(), frameY);
        Main.spriteBatch.Draw(arrow, drawPosition, arrowFrame, Color.White * opacity, 0f, arrowFrame.Size() * 0.5f, 1f, 0, 0f);
    }

    private void RenderMusicOption(GraphicalUniverseImagerSettings settings, Vector2 drawPosition, float opacity)
    {
        Main.spriteBatch.UseBlendState(BlendState.NonPremultiplied);

        DynamicSpriteFont font = FontAssets.DeathText.Value;

        string musicText = Language.GetTextValue(settings.Music.LocalizationKey);
        float textScale = 0.36f;
        Vector2 textSize = font.MeasureString(musicText);
        float backgroundWidth = MathF.Max(textSize.X * textScale, 100f) + 62f;
        Vector2 backgroundDrawPosition = drawPosition + Vector2.UnitY * 92f;

        // Draw the background.
        Texture2D background = GennedAssets.Textures.GraphicalUniverseImager.SmallSelector_Backdrop;
        Rectangle leftFrame = new Rectangle(0, 0, 10, 34);
        Rectangle centerFrame = new Rectangle(12, 0, 2, 34);
        Rectangle rightFrame = new Rectangle(18, 0, 10, 34);
        Vector2 scrollBarCenterScale = new Vector2((backgroundWidth - leftFrame.Width - rightFrame.Width) / centerFrame.Width, 1f);
        Main.spriteBatch.Draw(background, backgroundDrawPosition, centerFrame, Color.White * opacity, 0f, centerFrame.Size() * 0.5f, scrollBarCenterScale, 0, 0f);
        Main.spriteBatch.Draw(background, backgroundDrawPosition - Vector2.UnitX * backgroundWidth * 0.5f, leftFrame, Color.White * opacity, 0f, leftFrame.Size() * new Vector2(0f, 0.5f), 1f, 0, 0f);
        Main.spriteBatch.Draw(background, backgroundDrawPosition + Vector2.UnitX * backgroundWidth * 0.5f, rightFrame, Color.White * opacity, 0f, rightFrame.Size() * new Vector2(1f, 0.5f), 1f, 0, 0f);

        Vector2 textDrawPosition = backgroundDrawPosition - Vector2.UnitX * textSize * textScale * 0.5f + Vector2.UnitY * 4f;
        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, musicText, textDrawPosition, Color.White * opacity, Color.Black * opacity, 0f, textSize * new Vector2(0f, 0.5f), Vector2.One * textScale);

        List<GraphicalUniverseImagerMusicOption> options = GraphicalUniverseImagerMusicManager.musicOptions.Values.ToList();
        int musicIndex = options.IndexOf(settings.Music);
        RenderMusicSelectionArrow(options, backgroundDrawPosition + new Vector2(backgroundWidth * -0.5f + 14f, 1f), opacity.Squared(), false, ref LeftMusicButtonClickTimer, ref musicIndex);
        RenderMusicSelectionArrow(options, backgroundDrawPosition + new Vector2(backgroundWidth * 0.5f - 14f, 1f), opacity.Squared(), true, ref RightMusicButtonClickTimer, ref musicIndex);
        settings.Music = options[musicIndex];

        Main.spriteBatch.ResetToDefault();
    }

    private static void RenderMusicSelectionArrow(List<GraphicalUniverseImagerMusicOption> options, Vector2 drawPosition, float opacity, bool right, ref int clickTimer, ref int musicIndex)
    {
        Texture2D arrow = GennedAssets.Textures.GraphicalUniverseImager.SmallSelector_Arrows.Value;
        Vector2 arrowArea = arrow.Frame(2, 3, 0, 0).Size();
        Rectangle mouseArea = Utils.CenteredRectangle(Main.MouseScreen, Vector2.One);

        if (clickTimer > 0)
            clickTimer--;

        int frameY = 0;
        if (Utils.CenteredRectangle(drawPosition, arrowArea).Intersects(mouseArea))
        {
            frameY = 1;

            Main.LocalPlayer.mouseInterface = true;
            Main.blockMouse = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                musicIndex += right.ToDirectionInt();
                if (musicIndex < 0)
                    musicIndex = options.Count - 1;
                if (musicIndex >= options.Count)
                    musicIndex = 0;

                clickTimer = 7;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }
        if (clickTimer >= 1)
            frameY = 2;

        Rectangle arrowFrame = arrow.Frame(2, 3, right.ToInt(), frameY);
        Main.spriteBatch.Draw(arrow, drawPosition, arrowFrame, Color.White * opacity, 0f, arrowFrame.Size() * 0.5f, 1f, 0, 0f);
    }

    public override void PreUpdateEntities()
    {
        ActiveTileEntity = null;
        if (ModContent.GetInstance<GraphicalUniverseImagerSky>().Opacity <= 0f)
            GraphicalUniverseImagerSky.RenderSettings = null;

        if (TileDisablingSystem.TilesAreUninteractable)
            return;

        float minDistance = 999999f;
        TEGraphicalUniverseImager? closest = null;
        foreach (var tePair in TileEntity.ByPosition)
        {
            if (tePair.Value is not TEGraphicalUniverseImager gui)
                continue;

            Vector2 worldPosition = tePair.Key.ToWorldCoordinates();
            if (Main.LocalPlayer.WithinRange(worldPosition, minDistance))
            {
                minDistance = Main.LocalPlayer.Distance(worldPosition);
                closest = gui;
            }
        }

        if (minDistance >= GraphicalUniverseImagerTile.InfluenceRadius || closest is null)
            return;

        ActiveTileEntity = closest;
        if (closest.Settings is null)
            return;

        GraphicalUniverseImagerSky.RenderSettings = closest.Settings;

        if (closest.Settings.Option is null)
            return;

        ModContent.GetInstance<GraphicalUniverseImagerSky>().ShouldBeActive = true;
        ModContent.GetInstance<GraphicalUniverseImagerSky>().DisableDelay = 10;
    }
}
