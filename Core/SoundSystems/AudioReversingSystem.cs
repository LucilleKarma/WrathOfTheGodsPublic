using Microsoft.Xna.Framework;
using MonoStereo.Filters;
using MonoStereo.Structures;
using NoxusBoss.Core.CrossCompatibility.Inbound.MonoStereo;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.SoundSystems;

[ExtendsFromMod(MonoStereoSystem.ModName)]
public class AudioReversingSystem : ModSystem
{
    [ExtendsFromMod(MonoStereoSystem.ModName)]
    public class CustomAudioTimeController
    {
        private readonly SpeedChangeFilter speedFilter = new SpeedChangeFilter();
        private readonly ReverseFilter reverseFilter = new ReverseFilter();
        private float timeSpeed = 1f;

        public float TimeSpeed
        {
            get => timeSpeed;
            set
            {
                if (timeSpeed == value)
                    return;

                if (value != 0f)
                    reverseFilter.Reversing = value < 0f;

                speedFilter.Speed = Math.Abs(value);
                timeSpeed = value;
            }
        }

        public void ApplyTo(MonoStereoProvider provider)
        {
            if (provider.Filters.Contains(reverseFilter))
                return;

            provider.AddFilter(speedFilter);
            provider.AddFilter(reverseFilter);
        }

        public void RemoveFrom(MonoStereoProvider provider)
        {
            while (provider.Filters.Contains(reverseFilter))
            {
                provider.RemoveFilter(speedFilter);
                provider.RemoveFilter(reverseFilter);
            }
        }

        public bool IsAppliedTo(MonoStereoProvider provider) => provider.Filters.Contains(reverseFilter);
    }

    /// <summary>
    /// The audio filter responsible for reversing sounds.
    /// </summary>
    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    public static CustomAudioTimeController Filter
    {
        get;
        private set;
    }

    public static event Func<bool> ReversingConditionEvent;

    public static event Func<bool> FreezingConditionEvent;

    public override void OnModLoad()
    {
        if (MonoStereoSystem.Enabled)
            LoadWrapper();
    }

    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    private static void LoadWrapper()
    {
        Filter = new CustomAudioTimeController();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (MonoStereoSystem.Enabled)
            UpdateWrapper();
    }

    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    private static void UpdateWrapper()
    {
        bool timeIsReversed = false;
        bool timeIsFrozen = false;
        foreach (Delegate d in ReversingConditionEvent.GetInvocationList())
            timeIsReversed |= ((Func<bool>)d).Invoke();
        foreach (Delegate d in FreezingConditionEvent.GetInvocationList())
            timeIsFrozen |= ((Func<bool>)d).Invoke();

        float idealSpeed = timeIsReversed ? -1f : 1f;
        if (timeIsFrozen)
            idealSpeed = 0.05f;

        if (!Main.gamePaused && Distance(Filter.TimeSpeed, idealSpeed) >= 0.01f)
            Filter.TimeSpeed = Clamp(Filter.TimeSpeed + Sign(idealSpeed - Filter.TimeSpeed) * 0.01667f, -1f, 1f);

        MonoStereoMod.MonoStereoAudioTrack? currentTrack = MonoStereoMod.MonoStereoMod.GetSong(Main.curMusic);
        if (currentTrack is null)
            return;

        Filter.ApplyTo(currentTrack);
    }
}
