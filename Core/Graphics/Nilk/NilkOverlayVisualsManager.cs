using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.Nilk;

[Autoload(Side = ModSide.Client)]
public class NilkOverlayVisualsManager : ModSystem
{
    public record NilkOverlayElementData
    {
        /// <summary>
        /// How long this element should be active for, in frames.
        /// </summary>
        public int Time;

        /// <summary>
        /// How long this element should be active for, in frames.
        /// </summary>
        public int Lifetime;

        /// <summary>
        /// Whether this element is active or not.
        /// </summary>
        public bool Active;

        /// <summary>
        /// The rotation of this element.
        /// </summary>
        public float Rotation;

        /// <summary>
        /// The screen position of this element.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// The opacity of this element.
        /// </summary>
        public float Opacity => InverseLerpBump(0f, 30f, 30f, Lifetime, Time);

        public NilkOverlayElementData(int lifetime)
        {
            Lifetime = lifetime;
            Active = false;
        }

        /// <summary>
        /// Makes this element activate and appear on-screen.
        /// </summary>
        public void Activate()
        {
            Position = Main.rand.NextVector2Square(0.2f, 0.8f) * OverlayTarget.Size();
            Rotation = Main.rand.NextFloat(TwoPi);
            Time = 0;
            Active = true;
        }

        public void Update()
        {
            if (!Active)
                return;

            Time++;
            if (Time >= Lifetime)
            {
                Time = 0;
                Active = false;
            }
        }
    }

    /// <summary>
    /// The cryptid Fanny texture, as added by CalRemix. Only exists if CalRemix is enabled.
    /// </summary>
    public static LazyAsset<Texture2D>? FannyCryptidTexture
    {
        get;
        private set;
    }

    /// <summary>
    /// The fanny overlay data.
    /// </summary>
    public static readonly NilkOverlayElementData FannyData = new NilkOverlayElementData(600);

    /// <summary>
    /// The render target responsible for screen overlay visuals.
    /// </summary>
    public static ManagedRenderTarget OverlayTarget
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        OverlayTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
        RenderTargetManager.RenderTargetUpdateLoopEvent += DrawToOverlay;

        if (ModLoader.TryGetMod("CalRemix", out _))
            FannyCryptidTexture = LazyAsset<Texture2D>.FromPath("CalRemix/UI/Fanny/HelperFannyCryptid");
    }

    private void DrawToOverlay()
    {
        if (NilkEffectManager.NilkInsanityInterpolant <= 0f)
            return;

        if (FannyData.Opacity <= 0.01f && Main.rand.NextBool(1800) && NilkEffectManager.NilkInsanityInterpolant >= 1f && ModReferences.CalamityRemixMod is not null && Main.instance.IsActive)
            FannyData.Activate();

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        gd.SetRenderTarget(OverlayTarget);
        gd.Clear(Color.Transparent);
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

        DrawElements();

        Main.spriteBatch.End();
        gd.SetRenderTarget(null);
    }

    public static void DrawElements()
    {
        if (FannyData.Active && FannyCryptidTexture is not null)
        {
            FannyData.Update();
            if (FannyData.Time == 2)
                SoundEngine.PlaySound(SoundID.Cockatiel with { MaxInstances = 0, Volume = 0.3f, Pitch = -0.8f });

            Texture2D fannyTexture = FannyCryptidTexture.Value;
            Main.spriteBatch.Draw(fannyTexture, FannyData.Position, null, Color.White * FannyData.Opacity, FannyData.Rotation, fannyTexture.Size() * 0.5f, 1.5f, 0, 0f);
        }
    }
}
