using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.GlobalInstances;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class HotbarUICensorSystem : ModSystem
{
    public static float CensorOpacity
    {
        get;
        set;
    }

    public static List<int> BlockedInventorySlotIndices
    {
        get
        {
            // Censor the first and last inventory slots by default.
            // These slots can be moved around by the player if desired.
            List<int> result =
            [
                0,
                9
            ];

            int normalityID = Main.LocalPlayer.FindItem(ModReferences.Calamity?.Find<ModItem>("NormalityRelocator")?.Type ?? -1000);

            // Censor RoD and the Normality Relocator separately.
            // These slots will remain censored even if moved around.
            // Also block the RoH if somehow the player gets another one after their first one is broken.
            for (int i = 0; i < Main.InventorySlotsTotal; i++)
            {
                if (Main.LocalPlayer.inventory[i].IsAir)
                    continue;

                int itemID = Main.LocalPlayer.inventory[i].type;
                if (itemID == ItemID.RodofDiscord || itemID == ItemID.RodOfHarmony || itemID == normalityID)
                    result.Add(i);
            }

            return result;
        }
    }

    public override void Load()
    {
        On_Main.DrawInventory += DrawCensors;
        On_Main.GUIHotbarDrawInner += DrawCensors2;
        On_Player.ItemCheck_CheckCanUse += DisallowCensoredItemUsage;
        GlobalItemEventHandlers.CanUseItemEvent += DisallowRoHUsage;
    }

    private bool DisallowRoHUsage(Item item, Player player)
    {
        if (CensorOpacity > 0f && item.type == ItemID.RodOfHarmony)
            return false;

        return true;
    }

    private bool DisallowCensoredItemUsage(On_Player.orig_ItemCheck_CheckCanUse orig, Player self, Item sItem)
    {
        // Disallow item usage in censored slots.
        int inventoryIndex = Main.LocalPlayer.selectedItem;
        if (inventoryIndex >= 0 && CensorOpacity > 0f && BlockedInventorySlotIndices.Contains(inventoryIndex) && sItem == Main.LocalPlayer.HeldItem)
            return false;

        return orig(self, sItem);
    }

    private void DrawCensors(On_Main.orig_DrawInventory orig, Main self)
    {
        orig(self);

        // Draw censors if they're visible.
        if (CensorOpacity > 0f)
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            foreach (int inventoryIndex in BlockedInventorySlotIndices)
            {
                int x = inventoryIndex % 10;
                int y = inventoryIndex / 10;
                Vector2 drawPosition = (new Vector2(x * 60f + 34f, y * 55f + 28f) * Main.inventoryScale * Main.UIScale).Floor();
                DrawCensor(drawPosition);
            }

            Main.spriteBatch.ResetToDefaultUI();
        }
    }

    private void DrawCensors2(On_Main.orig_GUIHotbarDrawInner orig, Main self)
    {
        orig(self);

        // Draw censors if they're visible.
        if (!Main.playerInventory && !Main.LocalPlayer.ghost && CensorOpacity > 0f)
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            foreach (int inventoryIndex in BlockedInventorySlotIndices)
            {
                // Skip drawing censors outside of the central hotbar.
                if (inventoryIndex >= 10)
                    continue;

                int xDrawPosition = (TextureAssets.InventoryBack.Width() - 8) * inventoryIndex + 20;
                int yDrawPosition = (int)((1f - Main.hotbarScale[inventoryIndex]) * 22f) + 20;
                DrawCensor(new(xDrawPosition, yDrawPosition));
            }

            Main.spriteBatch.ResetToDefaultUI();
        }
    }

    public static void DrawCensor(Vector2 drawPosition)
    {
        // Apply a static shader.
        ManagedShader censorShader = ShaderManager.GetShader("NoxusBoss.StaticOverlayShader");
        censorShader.SetTexture(MulticoloredNoise, 1, SamplerState.PointWrap);
        censorShader.Apply();

        // Calculate draw variables.
        float censorScale = 42f;
        ulong offsetSeed = (ulong)(Main.GameUpdateCount / 5u + drawPosition.X) << 10;
        float offsetAngle = Utils.RandomFloat(ref offsetSeed) * 500f;
        Vector2 censorDrawPosition = drawPosition + offsetAngle.ToRotationVector2() * 3f;

        // Draw the censor with the static on top.
        Main.spriteBatch.Draw(WhitePixel, censorDrawPosition, null, Color.Black * CensorOpacity, 0f, Vector2.Zero, censorScale, 0, 0f);
    }

    public override void PostUpdateEverything()
    {
        // Disable censors if Nameless is not present or the world is not a GFB seed.
        if (!Main.zenithWorld || NamelessDeityBoss.Myself is null)
            CensorOpacity = 0f;
    }

    public override void OnWorldLoad()
    {
        CensorOpacity = 0f;
    }
}
