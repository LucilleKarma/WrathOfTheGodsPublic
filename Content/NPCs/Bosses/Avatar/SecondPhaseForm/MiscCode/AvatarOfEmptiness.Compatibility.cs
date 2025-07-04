using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.LoreItems;
using NoxusBoss.Content.Items.Pets;
using NoxusBoss.Content.Items.Placeable;
using NoxusBoss.Content.Items.Placeable.Trophies;
using NoxusBoss.Core.CrossCompatibility.Inbound.BossChecklist;
using NoxusBoss.Core.CrossCompatibility.Inbound.ClickerClass;
using NoxusBoss.Core.CrossCompatibility.Inbound.Infernum;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness : IBossChecklistSupport, IInfernumBossIntroCardSupport, IClickerClassPhaseReach
{
    public static IEnumerable<float> PhaseThresholdLifeRatios
    {
        get
        {
            yield return Phase3LifeRatio;
            yield return Phase4LifeRatio;
        }
    }

    public bool IsMiniboss => false;

    public string ChecklistEntryName => "AvatarOfEmptiness";

    public bool IsDefeated => BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>();

    public float ProgressionValue => 27f;

    public List<int> Collectibles => new List<int>()
    {
        ModContent.ItemType<LoreAvatar>(),
        ModContent.ItemType<GraphicalUniverseImager>(),
        MaskID,
        ModContent.ItemType<AvatarTrophy>(),
        RelicID,
        ModContent.ItemType<OblivionChime>(),
    };

    public bool UsesCustomPortraitDrawing => true;

    public LocalizedText IntroCardTitleName => this.GetLocalization("InfernumCompatibility.Title");

    public int IntroCardAnimationDuration => SecondsToFrames(1.45f);

    public SoundStyle ChooseIntroCardLetterSound() => default;

    public SoundStyle ChooseIntroCardMainSound() => default;

    /// <summary>
    /// The palette that the intro card text can cycle through.
    /// </summary>
    public static readonly Palette IntroCardTextPalette = new Palette().
        AddColor(Color.White).
        AddColor(Color.Aqua).
        AddColor(Color.Red);

    public void DrawCustomPortrait(SpriteBatch spriteBatch, Rectangle area, Color color)
    {
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, SubtractiveBlending, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        Main.spriteBatch.Draw(BloomCircleSmall, area.Center.ToVector2(), null, Color.White * 0.6f, 0f, BloomCircleSmall.Size() * 0.5f, 4f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, area.Center.ToVector2(), null, Color.White * 0.2f, 0f, BloomCircleSmall.Size() * 0.5f, 3.4f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, area.Center.ToVector2(), null, Color.White * 0.1f, 0f, BloomCircleSmall.Size() * 0.5f, 3.3f, 0, 0f);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        Texture2D bossChecklistTexture = GennedAssets.Textures.SecondPhaseForm.AvatarOfEmptiness_BossChecklist.Value;
        Vector2 centeredDrawPosition = area.Center.ToVector2() - bossChecklistTexture.Size() * 0.5f;
        spriteBatch.Draw(bossChecklistTexture, centeredDrawPosition, color);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
    }

    public Color GetIntroCardTextColor(float horizontalCompletion, float animationCompletion) => IntroCardTextPalette.SampleColor(animationCompletion);

    public bool ShouldDisplayIntroCard() => CurrentState == AvatarAIType.Awaken_HeadEmergence;
}
