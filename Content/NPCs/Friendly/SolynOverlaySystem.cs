using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.NPCs.Friendly;

public class SolynOverlaySystem : ModSystem
{
    private static float opacity;

    private readonly List<SolynOffScreenCache> solynOffScreenCache = [];

    private readonly struct SolynOffScreenCache(string name, Vector2 position, Color color, Vector2 npcDistancePosition, string npcDistanceText, NPC solyn, Vector2 textSize)
    {
        private readonly Color namePlateColor = color;

        private readonly Vector2 namePlatePosition = position.Floor();

        private readonly Vector2 distanceDrawPosition = npcDistancePosition.Floor();

        private readonly Vector2 textSize = textSize;

        private readonly string nameToShow = name;

        private readonly string distanceString = npcDistanceText;

        private readonly NPC solyn = solyn;

        public void DrawNPCName(SpriteBatch spriteBatch)
        {
            float scale = solyn.As<BattleSolyn>().WorldMapIconScale;
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, nameToShow, namePlatePosition - Vector2.UnitY * scale * 40f, namePlateColor * opacity, 0f, Vector2.Zero, Vector2.One * scale, -1f, 2f);
        }

        public void DrawNPCHead()
        {
            float scale = solyn.As<BattleSolyn>().WorldMapIconScale * opacity * 0.75f;
            Color borderColor = Color.White * opacity;
            Vector2 headDrawPosition = new Vector2(namePlatePosition.X + textSize.X * 0.5f - 4f, namePlatePosition.Y - 12f);
            Main.TownNPCHeadRenderer.DrawWithOutlines(solyn, ModContent.GetModHeadSlot(ModContent.GetInstance<Solyn>().HeadTexture), headDrawPosition.Floor(), borderColor * opacity, 0f, scale, 0);
        }

        public void DrawNPCDistance(SpriteBatch spriteBatch)
        {
            float scale = solyn.As<BattleSolyn>().WorldMapIconScale * 0.85f;
            spriteBatch.DrawString(FontAssets.MouseText.Value, distanceString, new Vector2(distanceDrawPosition.X - 2f, distanceDrawPosition.Y), Color.Black * opacity, 0f, default, scale, 0, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, distanceString, new Vector2(distanceDrawPosition.X + 2f, distanceDrawPosition.Y), Color.Black * opacity, 0f, default, scale, 0, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, distanceString, new Vector2(distanceDrawPosition.X, distanceDrawPosition.Y - 2f), Color.Black * opacity, 0f, default, scale, 0, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, distanceString, new Vector2(distanceDrawPosition.X, distanceDrawPosition.Y + 2f), Color.Black * opacity, 0f, default, scale, 0, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, distanceString, distanceDrawPosition, namePlateColor * opacity, 0f, default, scale, 0, 0f);
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int playerNameIndex = layers.FindIndex((layer) => layer.Name == "Vanilla: MP Player Names");
        if (playerNameIndex != -1)
        {
            layers.Insert(playerNameIndex, new LegacyGameInterfaceLayer("Solyn Name", () =>
            {
                Draw();
                return true;
            }, InterfaceScaleType.Game));
        }
    }

    public void Draw()
    {
        solynOffScreenCache.Clear();

        // Calculate screen values relative to world space.
        PlayerInput.SetZoom_World();
        int screenWidthWorld = Main.screenWidth;
        int screenHeightWorld = Main.screenHeight;
        Vector2 screenPositionWorld = Main.screenPosition;
        PlayerInput.SetZoom_UI();

        bool dontDrawIcon = false;
        if (AvatarOfEmptiness.Myself is not null)
        {
            var currentState = AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState;

            if (currentState == AvatarOfEmptiness.AvatarAIType.Awaken_RiftSizeIncrease)
                dontDrawIcon = true;
            if (currentState == AvatarOfEmptiness.AvatarAIType.Awaken_LegEmergence)
                dontDrawIcon = true;
            if (currentState == AvatarOfEmptiness.AvatarAIType.Awaken_ArmJutOut)
                dontDrawIcon = true;
            if (currentState == AvatarOfEmptiness.AvatarAIType.Awaken_HeadEmergence)
                dontDrawIcon = true;
            if (currentState == AvatarOfEmptiness.AvatarAIType.Awaken_Scream)
                dontDrawIcon = true;
            if (currentState == AvatarOfEmptiness.AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic)
                dontDrawIcon = true;
        }

        // Check for and save all offscreen Solyn instances in a centralized list, as long as the Avatar isn't doing his phase 2 animation.
        if (!dontDrawIcon)
        {
            BattleSolyn.solynMappingsCacheByIndex.TryGetValue(Main.myPlayer, out NPC? clientsSolynIndex);
            bool clientHasSolynInstance = clientsSolynIndex is not null && clientsSolynIndex.ModNPC is BattleSolyn s && !s.Invisible;

            int solynID = ModContent.NPCType<BattleSolyn>();
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.type != solynID || n.ModNPC is not BattleSolyn solyn)
                    continue;

                // If the player client has their own Solyn instance, only show a head icon for that one.
                if (clientHasSolynInstance && n.whoAmI != clientsSolynIndex!.whoAmI)
                    continue;

                // If the player client does not have their own Solyn instance, only show a head for the "real" Solyn instance.
                if (!clientHasSolynInstance && solyn.IsMultiplayerClone)
                    continue;

                string name = n.TypeName;

                // Calculate distance-related draw information.
                GetDistance(screenWidthWorld, screenHeightWorld, screenPositionWorld, font, n, name, out Vector2 namePlatePosition, out float namePlateDistance, out Vector2 textSize);

                // Calculate draw caches.
                if (namePlateDistance > 0f)
                {
                    float distanceFromPlayer = n.Distance(Main.LocalPlayer.Center);
                    string text = Language.GetTextValue("GameUI.PlayerDistance", (int)(distanceFromPlayer / 8f));
                    Vector2 distanceBasedSize = font.MeasureString(text);
                    distanceBasedSize.X = namePlatePosition.X + textSize.X * 0.5f + 15.5f;
                    distanceBasedSize.Y = namePlatePosition.Y + textSize.Y / 2f - distanceBasedSize.Y / 2f - 20f;
                    solynOffScreenCache.Add(new SolynOffScreenCache(name, namePlatePosition, Color.White, distanceBasedSize, text, n, textSize));
                }
            }
        }

        opacity = Saturate(opacity + (solynOffScreenCache.Count != 0).ToDirectionInt() * 0.015f);

        // Draw everything from the aforementioned caches.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);

        for (int i = 0; i < solynOffScreenCache.Count; i++)
            solynOffScreenCache[i].DrawNPCName(Main.spriteBatch);

        for (int i = 0; i < solynOffScreenCache.Count; i++)
            solynOffScreenCache[i].DrawNPCDistance(Main.spriteBatch);

        for (int i = 0; i < solynOffScreenCache.Count; i++)
            solynOffScreenCache[i].DrawNPCHead();
    }

    private static void GetDistance(int testWidth, int testHeight, Vector2 testPosition, DynamicSpriteFont font, NPC entity, string nameToShow, out Vector2 namePlatePos, out float namePlateDist, out Vector2 textSize)
    {
        // Initialize out variables.
        namePlateDist = 0f;
        namePlatePos = font.MeasureString(nameToShow);

        Vector2 center = new Vector2(testWidth / 2 + testPosition.X, testHeight / 2 + testPosition.Y);
        Vector2 zoomedEntityOffset = entity.position + (entity.position - center) * (Main.GameViewMatrix.Zoom - Vector2.One);

        float dx = zoomedEntityOffset.X + entity.width / 2 - center.X;
        float dy = zoomedEntityOffset.Y - namePlatePos.Y - center.Y - 2f;
        float lengthIdk = Sqrt(dx * dx + dy * dy);

        // Calculate the max size of everything.
        int maxSize = testHeight;
        if (testHeight > testWidth)
            maxSize = testWidth;
        maxSize = maxSize / 2 - 50;

        // Place a lower bound on the max size.
        if (maxSize < 100)
            maxSize = 100;

        if (lengthIdk < maxSize)
        {
            namePlatePos.X = zoomedEntityOffset.X + entity.width / 2 - namePlatePos.X / 2f - testPosition.X;
            namePlatePos.Y = zoomedEntityOffset.Y - namePlatePos.Y - testPosition.Y - 2f;
        }
        else
        {
            namePlateDist = lengthIdk;
            lengthIdk = maxSize / lengthIdk;
            namePlatePos.X = testWidth / 2 + dx * lengthIdk - namePlatePos.X / 2f;
            namePlatePos.Y = testHeight / 2 + dy * lengthIdk + Main.UIScale * 40f;
        }

        // Calculate the text size for the font.
        textSize = font.MeasureString(nameToShow);

        // Reorient the name plate position in accordance with the UI scale.
        namePlatePos = (namePlatePos + textSize * 0.5f) / Main.UIScale + textSize * 0.5f;
        namePlatePos.Y -= 28f;
    }
}
