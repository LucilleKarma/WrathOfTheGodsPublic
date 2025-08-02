using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class AvatarSlamTelegraph : ModProjectile, IDrawsWithShader, IProjOwnedByBoss<NamelessDeityBoss>
{
    /// <summary>
    /// The amount of time, in frames, that this telegraph has existed for.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 7000;
    }

    public override void SetDefaults()
    {
        Projectile.width = 1400;
        Projectile.height = 1400;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = AvatarOfEmptiness.RealityShatter_TelegraphTime;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        Time++;
    }

    public float TelegraphWidthFunction(float completionRatio)
    {
        // Calculate the 0-1 interpolant of how complete the telegraph is.
        float telegraphCompletion = InverseLerp(0f, AvatarOfEmptiness.RealityShatter_TelegraphTime, Time);

        // Use the projectile's width as a base for the telegraph's width.
        float baseWidth = Projectile.width * 0.98f;

        // Make it so that the width expands outward in a cute, slightly cartoonish way as it appears.
        float fadeInScale = Clamp(EasingCurves.Elastic.Evaluate(EasingType.Out, InverseLerp(0f, 0.25f, telegraphCompletion)), 0f, 10f);

        // Make the width increase as the telegraph nears its completion. This corresponds with a decrease in opacity, as though the telegraph is dissipating.
        float fadeOutScale = InverseLerp(0.7f, 1f, telegraphCompletion) * 2f;

        // Combine the scale factors and use them
        float widthScaleFactor = (fadeInScale + fadeOutScale) * 0.5f;
        return widthScaleFactor * baseWidth;
    }

    public Color TelegraphColorFunction(float completionRatio)
    {
        // Calculate the 0-1 interpolant of how complete the telegraph is.
        float telegraphCompletion = InverseLerp(0f, AvatarOfEmptiness.RealityShatter_TelegraphTime, Time);

        // Make the telegraph fade out at its top and bottom.
        float endFadeOpacity = InverseLerpBump(0f, 0.2f, 0.64f, 1f, completionRatio);

        // Calculate the overall opacity based on the Projectile.Opacity, endFadeOpacity, and the telegraph's lifetime.
        // As the telegraph approaches its death, it fades out.
        float opacity = InverseLerpBump(0f, 0.6f, 0.7f, 1f, telegraphCompletion) * Projectile.Opacity * endFadeOpacity * 0.5f;

        // Calculate the color with the opacity in mind.
        Color color = Color.Lerp(Color.Cyan, Color.Red, Pow(telegraphCompletion, 0.75f)) * opacity;
        color.A = 0;

        return color;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        // Configure the streak shader's texture.
        ManagedShader streakShader = ShaderManager.GetShader("NoxusBoss.GenericTrailStreak");
        streakShader.SetTexture(GennedAssets.Textures.TrailStreaks.StreakBloomLine, 1);

        List<Vector2> telegraphPoints = Projectile.GetLaserControlPoints(12, 7500f, Vector2.UnitY);
        PrimitiveSettings settings = new PrimitiveSettings(TelegraphWidthFunction, TelegraphColorFunction, Shader: streakShader);
        PrimitiveRenderer.RenderTrail(telegraphPoints, settings, 16);
    }
}
