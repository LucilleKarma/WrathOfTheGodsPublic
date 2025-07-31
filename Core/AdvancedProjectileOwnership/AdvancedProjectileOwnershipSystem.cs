using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.AdvancedProjectileOwnership;

public class AdvancedProjectileOwnershipSystem : GlobalProjectile
{
    public enum EntityOwnerType : byte
    {
        NPC,
        Projectile,
        Player,
        Unknown
    }

    internal static readonly Dictionary<int, Entity> ownerRelationship = [];

    public override void PostAI(Projectile projectile)
    {
        // Check if the entity is still active or not.
        // If it isn't, sever the owner relationship.
        if (ownerRelationship.TryGetValue(projectile.identity, out Entity? owner))
        {
            if (!owner.active)
                ownerRelationship.Remove(projectile.identity);
        }
    }

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        int ownerIndex = int.MinValue;
        bool hasOwner = ownerRelationship.TryGetValue(projectile.identity, out Entity? owner);
        EntityOwnerType ownerType = EntityOwnerType.Unknown;
        if (owner is NPC npc)
        {
            ownerIndex = npc.whoAmI;
            ownerType = EntityOwnerType.NPC;
        }
        else if (owner is Player player)
        {
            ownerIndex = player.whoAmI;
            ownerType = EntityOwnerType.Player;
        }
        else if (owner is Projectile projectileOwner)
        {
            ownerIndex = projectileOwner.whoAmI;
            ownerType = EntityOwnerType.Projectile;
        }

        if (ownerIndex == int.MinValue)
            hasOwner = false;

        bitWriter.WriteBit(hasOwner);

        if (hasOwner)
        {
            binaryWriter.Write((byte)ownerType);
            binaryWriter.Write(ownerIndex);
        }
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        // Check if a packet is expected. If one is, process it.
        // If not, that means that no owner is registered (either due to never receiving one or due to said owner dying), and the relationship state should be updated accordingly.
        bool expectingPacket = bitReader.ReadBit();
        if (expectingPacket)
        {
            EntityOwnerType ownerType = (EntityOwnerType)binaryReader.ReadByte();
            int ownerIndex = binaryReader.ReadInt32();
            Entity? owner = null;

            switch (ownerType)
            {
                case EntityOwnerType.NPC:
                    owner = Main.npc[ownerIndex];
                    break;
                case EntityOwnerType.Projectile:
                    owner = Main.projectile[ownerIndex];
                    break;
                case EntityOwnerType.Player:
                    owner = Main.player[ownerIndex];
                    break;
            }

            if (owner is not null)
                ownerRelationship[projectile.identity] = owner;
        }

        else
            ownerRelationship.Remove(projectile.identity);
    }
}
