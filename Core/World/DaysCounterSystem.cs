using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.World
{
    public class DaysCounterSystem : ModSystem
    {
        /// <summary>
        /// How many days have passed so far for the given world.
        /// </summary>
        public static int DayCounter
        {
            get;
            set;
        }

        public override void OnModLoad() => On_Main.UpdateTime_StartDay += IncrementDayCounter;

        private void IncrementDayCounter(On_Main.orig_UpdateTime_StartDay orig, ref bool stopEvents)
        {
            DayCounter++;
            orig(ref stopEvents);
        }

        public override void OnWorldLoad() => DayCounter = 0;

        public override void OnWorldUnload() => DayCounter = 0;

        public override void SaveWorldData(TagCompound tag) => tag[nameof(DayCounter)] = DayCounter;

        public override void LoadWorldData(TagCompound tag) => DayCounter = tag.GetInt(nameof(DayCounter));
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(DayCounter);
        }

        public override void NetReceive(BinaryReader reader)
        {
            DayCounter = reader.ReadInt32();
        }
    }
}
