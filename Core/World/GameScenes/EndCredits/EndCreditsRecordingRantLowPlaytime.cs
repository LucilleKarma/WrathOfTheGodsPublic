using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.EndCredits;

public class EndCreditsRecordingRantLowPlaytime : NamelessDeitySubtitleSystem
{
    public override List<NamelessDeityDialog> Sentences => new List<NamelessDeityDialog>()
    {
        // Hey. You know...I can't really tell you what to do and all, but...
        new(0f, "RecordingRantPlaytimeSubtitle1"),

        // It would be nice if you left things like this a secret for others to find in-game, you know?
        new(4.779f, "RecordingRantPlaytimeSubtitle2"),

        // You don't have to upload absolutely everything for others to see.
        new(9.478f, "RecordingRantPlaytimeSubtitle3"),
        
        // I created this cutscene for those who have earned it, after all.
        new(13.483f, "RecordingRantPlaytimeSubtitle4"),

        // Although I am INCREDIBLY suspicious of how little time it took you to get here.
        new(17.211f, "RecordingRantPlaytimeSubtitle5"),

        // Maybe you're just a fast player...a speed-runner, as they call them.
        new(21.584f, "RecordingRantPlaytimeSubtitle6"),
        
        // Or you cheated your way in here with a fresh character.
        new(25.538f, "RecordingRantPlaytimeSubtitle7"),

        // Either way, only you know for sure.
        new(28.691f, "RecordingRantPlaytimeSubtitle8"),

        // Anyways, I hope you at least consider my word.
        new(31.189f, "RecordingRantPlaytimeSubtitle9"),

        // All I ask from you is to do the right thing.
        new(34.382f, "RecordingRantPlaytimeSubtitle10")
    };

    public override float SubtitleDisappearTime => SecondsToFrames(37.701f);

    public override void UpdateUI(GameTime gameTime)
    {
        DialogueTimer = 0;
        if (!ModContent.GetInstance<EndCreditsScene>().IsActive || ModContent.GetInstance<EndCreditsScene>().State != EndCreditsScene.CreditsState.RecordingSoftwareRant_HowDidYouDoThisSoQuicklyLmao)
            return;

        // Disallow game pausing, to ensure that the "music" doesn't drift.
        Main.gamePaused = false;
        DialogueTimer = ModContent.GetInstance<EndCreditsScene>().RantTimer;

        if (Main.musicVolume < 0.2f)
            Main.musicVolume = 0.2f;

        if (DialogueTimer >= SubtitleDisappearTime - 5 && NamelessDeityBoss.Myself is not null)
            NamelessDeityBoss.Myself.active = false;
    }
}
