using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Graphics.Automators;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class VoidBlot : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>, IDrawsWithShader
{
    /// <summary>
    /// Whether the blot has played its spawn sound yet or not.
    /// </summary>
    public bool HasPlayedSpawnSound
    {
        get => Projectile.localAI[0] == 1f;
        set => Projectile.localAI[0] = value.ToInt();
    }

    /// <summary>
    /// How long this blot has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// How long the void blot should linger for.
    /// </summary>
    public static int Lifetime => SecondsToFrames(1.2333f);

    /// <summary>
    /// How long the void blot should take disappearing as it dies.
    /// </summary>
    public static int DisappearTime => SecondsToFrames(0.1167f);

    /// <summary>
    /// How long it takes for the void blot to fill with black.
    /// </summary>
    public static int FillInTime => SecondsToFrames(0.416f);

    /// <summary>
    /// How long it takes for the void blot to start the fill in visual.
    /// </summary>
    public static int FillInDelay => SecondsToFrames(0.416f);

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Avatar/Projectiles", Name);

    public override void SetStaticDefaults()
    {
        Main.projFrames[Type] = 2;
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9;
    }

    public override void SetDefaults()
    {
        Projectile.width = 500;
        Projectile.height = 500;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.hide = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        if (!HasPlayedSpawnSound)
        {
            if (Main.rand.NextBool(8))
                SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.ErasureRiftOpen with { MaxInstances = 0, Volume = 0.7f }, Projectile.Center);
            HasPlayedSpawnSound = true;
        }

        Time++;

        // Fade in.
        Projectile.Opacity = InverseLerp(Lifetime, Lifetime - DisappearTime, Time) * InverseLerp(3600f, 3570f, Projectile.timeLeft);

        // Die once the natural lifetime has been exceeded.
        if (Time >= Lifetime)
            Projectile.Kill();
    }

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.ErasureRiftClose with { MaxInstances = 0, Volume = 0.7f }, Projectile.Center);

        ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 3f);
        AvatarOfEmptiness.CreateTwinkle(Projectile.Center, Vector2.One * 2f, false);
    }

    public void DrawSelf()
    {
        float timeLeft = Lifetime - Time;
        float contractInterpolant = EasingCurves.Elastic.Evaluate(EasingType.InOut, InverseLerp(-15f, 28f, timeLeft).Squared());
        Texture2D pencilSketch = TextureAssets.Projectile[Type].Value;
        Vector2 scale = Projectile.Size / pencilSketch.Size() * MathF.Max(Projectile.scale, 1f) * contractInterpolant;
        Main.spriteBatch.Draw(pencilSketch, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(Color.MediumPurple), Projectile.rotation, pencilSketch.Size() * 0.5f, scale, 0, 0f);
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        // Apply the blot shader.
        ManagedShader blotShader = ShaderManager.GetShader("NoxusBoss.VoidBlotEdgeShader");
        blotShader.TrySetParameter("edgeColor", Color.Lerp(Color.DarkGray, Color.MediumPurple, Projectile.identity / 3f % 1f) * 0.3f);
        blotShader.TrySetParameter("identity", Projectile.identity * 0.17f);
        blotShader.TrySetParameter("scale", InverseLerpBump(FillInDelay, FillInDelay + FillInTime, Lifetime - DisappearTime, Lifetime, Time).Squared());
        blotShader.SetTexture(ViscousNoise, 1, SamplerState.LinearWrap);
        blotShader.SetTexture(DendriticNoise, 2, SamplerState.LinearWrap);
        blotShader.Apply();
        DrawSelf();
    }

    public override bool? CanDamage() => Time >= FillInDelay + FillInTime + 5f;

    public override bool PreDraw(ref Color lightColor)
    {
        Main.spriteBatch.PrepareForShaders();

        // Apply the blot shader.
        ManagedShader blotShader = ShaderManager.GetShader("NoxusBoss.VoidBlotShader");
        blotShader.TrySetParameter("scale", InverseLerpBump(FillInDelay, FillInDelay + FillInTime, Lifetime - DisappearTime, Lifetime, Time).Squared());
        blotShader.SetTexture(ViscousNoise, 1, SamplerState.LinearWrap);
        blotShader.Apply();
        DrawSelf();

        Main.spriteBatch.ResetToDefault();

        return false;
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        overPlayers.Add(index);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        return CircularHitboxCollision(Projectile.Center, Projectile.width * 0.33f, targetHitbox);
    }
}
