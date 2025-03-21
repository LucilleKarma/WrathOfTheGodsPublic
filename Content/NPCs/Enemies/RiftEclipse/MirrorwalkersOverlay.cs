﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.NPCs.Enemies.RiftEclipse;

public class MirrorwalkersOverlay : ModSystem
{
    private static float opacity;

    private readonly List<MirrorwalkerOnScreenCache> mirrorwalkerOnScreenCache = [];

    private readonly List<MirrorwalkerOffScreenCache> mirrorwalkerOffScreenCache = [];

    private readonly struct MirrorwalkerOnScreenCache(string name, Vector2 position, Color color)
    {
        private readonly Vector2 position = position;

        private readonly Color color = color;

        private readonly string text = name;

        public void DrawPlayerName_WhenPlayerIsOnScreen(SpriteBatch spriteBatch)
        {
            Vector2 drawPosition = position.Floor();
            spriteBatch.DrawString(FontAssets.MouseText.Value, text, drawPosition - Vector2.UnitX * 2f, Color.Black, 0f, default, 1f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, text, drawPosition + Vector2.UnitX * 2f, Color.Black, 0f, default, 1f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, text, drawPosition - Vector2.UnitY * 2f, Color.Black, 0f, default, 1f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, text, drawPosition + Vector2.UnitY * 2f, Color.Black, 0f, default, 1f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(FontAssets.MouseText.Value, text, drawPosition, color, 0f, default, 1f, SpriteEffects.None, 0f);
        }
    }

    private readonly struct MirrorwalkerOffScreenCache(string name, Vector2 position, Color color, Vector2 npcDistancePosition, string npcDistanceText, Player player, Vector2 textSize)
    {
        private readonly Color namePlateColor = color;

        private readonly Vector2 namePlatePosition = position.Floor();

        private readonly Vector2 distanceDrawPosition = npcDistancePosition.Floor();

        private readonly Vector2 textSize = textSize;

        private readonly string nameToShow = name;

        private readonly string distanceString = npcDistanceText;

        private readonly Player player = player;

        public void DrawPlayerName(SpriteBatch spriteBatch)
        {
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, nameToShow, namePlatePosition - Vector2.UnitY * 40f, namePlateColor * opacity, 0f, Vector2.Zero, Vector2.One, -1f, 2f);
        }

        public void DrawPlayerHead()
        {
            Color borderColor = Main.GetPlayerHeadBordersColor(player) * opacity;
            Vector2 headDrawPosition = new Vector2(namePlatePosition.X + textSize.X * 0.5f - 4f, namePlatePosition.Y - 12f);
            Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, headDrawPosition.Floor(), opacity, opacity * 0.8f, borderColor);
        }

        public void DrawPlayerDistance(SpriteBatch spriteBatch)
        {
            float scale = 0.85f;
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
            layers.Insert(playerNameIndex, new LegacyGameInterfaceLayer("Wrath of the Gods: Mirrorwalker Player Names", () =>
            {
                Draw();
                return true;
            }, InterfaceScaleType.Game));
        }
    }

    public void Draw()
    {
        mirrorwalkerOnScreenCache.Clear();
        mirrorwalkerOffScreenCache.Clear();

        // Calculate screen values relative to world space.
        PlayerInput.SetZoom_World();
        int screenWidthWorld = Main.screenWidth;
        int screenHeightWorld = Main.screenHeight;
        Vector2 screenPositionWorld = Main.screenPosition;
        PlayerInput.SetZoom_UI();

        // Calculate the text color.
        byte mouseTextColor = Main.mouseTextColor;
        float opacity = mouseTextColor / 255f;
        Color teamColor = Main.teamColor[Main.LocalPlayer.team] * opacity;
        teamColor.A = mouseTextColor;

        // Check for and save all mirrorwalkers in a centralized list.
        int mirrorwalkerID = ModContent.NPCType<Mirrorwalker>();
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC n = Main.npc[i];
            if (!n.active || n.type != mirrorwalkerID)
                continue;

            string name = "???";

            // Calculate distance-related draw information.
            GetDistance(screenWidthWorld, screenHeightWorld, screenPositionWorld, Main.LocalPlayer, font, n, name, out Vector2 namePlatePosition, out float namePlateDistance, out Vector2 textSize);

            // Calculate draw caches.
            if (namePlateDistance > 0f)
            {
                float distanceFromPlayer = n.Distance(Main.LocalPlayer.Center);
                string text = Language.GetTextValue("GameUI.PlayerDistance", (int)(distanceFromPlayer / 8f));
                Vector2 distanceBasedSize = font.MeasureString(text);
                distanceBasedSize.X = namePlatePosition.X + textSize.X * 0.5f + 15.5f;
                distanceBasedSize.Y = namePlatePosition.Y + textSize.Y / 2f - distanceBasedSize.Y / 2f - 20f;
                mirrorwalkerOffScreenCache.Add(new MirrorwalkerOffScreenCache(name, namePlatePosition, teamColor, distanceBasedSize, text, Main.LocalPlayer, textSize));
            }
            else
            {
                namePlatePosition -= Vector2.One * 24f;
                mirrorwalkerOnScreenCache.Add(new MirrorwalkerOnScreenCache(name, namePlatePosition, teamColor));
            }
        }

        // Make the general opacity increase if there are any mirrorwalkers offscreen, and vice versa.
        MirrorwalkersOverlay.opacity = Saturate(MirrorwalkersOverlay.opacity + (mirrorwalkerOffScreenCache.Count != 0).ToDirectionInt() * 0.015f);

        // Draw everything from the aforementioned caches.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
        for (int i = 0; i < mirrorwalkerOnScreenCache.Count; i++)
            mirrorwalkerOnScreenCache[i].DrawPlayerName_WhenPlayerIsOnScreen(Main.spriteBatch);

        for (int i = 0; i < mirrorwalkerOffScreenCache.Count; i++)
            mirrorwalkerOffScreenCache[i].DrawPlayerName(Main.spriteBatch);

        for (int i = 0; i < mirrorwalkerOffScreenCache.Count; i++)
            mirrorwalkerOffScreenCache[i].DrawPlayerDistance(Main.spriteBatch);

        for (int i = 0; i < mirrorwalkerOffScreenCache.Count; i++)
            mirrorwalkerOffScreenCache[i].DrawPlayerHead();
    }

    private static void GetDistance(int testWidth, int testHeight, Vector2 testPosition, Player localPlayer, DynamicSpriteFont font, NPC entity, string nameToShow, out Vector2 namePlatePos, out float namePlateDist, out Vector2 textSize)
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

        // Gravity potions my beloathed.
        if (localPlayer.gravDir == -1f)
            namePlatePos.Y = testHeight - namePlatePos.Y;
    }
}
