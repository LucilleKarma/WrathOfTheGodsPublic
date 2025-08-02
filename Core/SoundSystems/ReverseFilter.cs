using System.Collections.Concurrent;
using MonoStereo;
using MonoStereo.Filters;
using MonoStereo.Structures;
using NAudio.Utils;
using NoxusBoss.Core.CrossCompatibility.Inbound.MonoStereo;
using Terraria.ModLoader;

namespace NoxusBoss.Core.SoundSystems
{
#pragma warning disable CS8603 // Possible null reference return.
    [ExtendsFromMod(MonoStereoSystem.ModName)]
    [JITWhenModsEnabled(MonoStereoSystem.ModName)]
    public class ReverseFilter : AudioFilter
    {
        // Apply first as we are going to directly seek the audio in some cases
        public override FilterPriority Priority => FilterPriority.ApplyFirst;

        // Shorthand for fetching the source as a song.
        // This is utilized safely.
        public Song SongSource => Source as Song;

        // Same as above.
        public ISeekable SeekableSource => SongSource.Source as ISeekable;

        /// <summary>
        /// If this is true, the recorded audio will be played back in reverse.<br/>
        /// Otherwise, the audio will be recorded into memory for later reversal.<br/>
        /// <br/>
        /// If <see cref="Reversing"/> is <see langword="true"/>, but there is no recorded audio available,
        /// the filter will attempt to seek backwards and reverse the audio in real-time.
        /// </summary>
        public bool Reversing
        {
            get => reversing;
            set
            {
                // Assign every recording to match the state specified.
                foreach (KeyValuePair<MonoStereoProvider, ReversableRecording> kvp in RecordedBuffers)
                {
                    // If the states are already equal, we don't want to
                    // reverse the recorded audio when it shouldn't be reversed.
                    if (kvp.Value.Reversing == value)
                        continue;

                    // Reverse the recorded samples, and mark
                    //that this source is now reading in reverse.
                    kvp.Value.Reverse();
                    kvp.Value.Reversing = value;

                    // Seek to the current position, accounting for how many samples are recorded.
                    // This filter is only applied when the provider is a song, and the
                    // song's source is seekable - so we know this is safe to do.
                    ((kvp.Key as Song)!.Source as ISeekable)!.Position = kvp.Value.RecordingStart + kvp.Value.Buffer.Count;
                }

                reversing = value;
            }
        }

        private bool reversing;

        // The ReversableRecording collection represents the
        // cached samples for all of the sources this filter is applied to.
        private readonly Dictionary<MonoStereoProvider, ReversableRecording> RecordedBuffers = [];

        /// <summary>
        /// This class is only intended to be used with the <see cref="ReverseFilter"/><br/>
        /// This stores data for all of the sources this filter is applied to.
        /// </summary>
        public class ReversableRecording
        {
            public ConcurrentQueue<float> Buffer { get; set; } = [];
            public float[] InsuranceBuffer = null!;
            public long RecordingStart = -1;
            public bool Reversing;

            public void Reverse()
            {
                // Reversing the stored samples whenever the playback mode is toggled
                // means that reverse can be switched on and off without lots of memory allocation overhead
                float[] buffer = Buffer.Reverse().ToArray();

                // Make sure that the channels don't end up flipped
                for (int i = 0; i < buffer.Length; i += 2)
                    (buffer[i], buffer[i + 1]) = (buffer[i + 1], buffer[i]);

                lock (Buffer)
                {
                    Buffer = new(buffer);
                }
            }
        }

        // This is basically just a RecordedBuffers.TryGet() with some extra null safety
        private bool GetRecording(out ReversableRecording recording) => GetRecording(Source, out recording);

        private bool GetRecording(MonoStereoProvider sampleProvider, out ReversableRecording recording)
        {
            if (RecordedBuffers.TryGetValue(sampleProvider, out ReversableRecording? foundRecording))
                recording = foundRecording;

            else
                recording = null!;

            return recording is not null;
        }

        // Ensure an entry exists whenever this filter is applied, but
        // if and ONLY if the song's source is seekable. Reversing would
        // be far less effective if it is not seekable.
        public override void Apply(MonoStereoProvider provider)
        {
            if (provider is Song song && song.Source is ISeekable)
                RecordedBuffers.Add(provider, new());
        }

        // Whenever this filter is removed from a source, we no longer
        // need to keep track of cached samples for that source
        public override void Unapply(MonoStereoProvider provider)
        {
            // Ensure that we have an entry for this source
            if (!GetRecording(provider, out ReversableRecording? recording))
                return;

            RecordedBuffers.Remove(provider);

            // Make sure to seek to wherever we finished reading last.
            // This filter is only applied when the provider is a song, and the
            // song's source is seekable - so we know this is safe to do.
            ((provider as Song)!.Source as ISeekable)!.Position = recording.RecordingStart + recording.Buffer.Count;
        }

        public override void PostProcess(float[] buffer, int offset, int samplesRead)
        {
            // Ensure that we have an entry for this source
            if (!GetRecording(out ReversableRecording? recording))
                return;

            // If we are actively reversing, we don't want to "record" reversed samples.
            if (recording.Reversing)
                return;

            // Add all of the samples that we just read to the "recording"
            ConcurrentQueue<float> sampleQueue = recording.Buffer;
            float volume = Source.Volume;

            for (int i = 0; i < samplesRead; i++)
                sampleQueue.Enqueue(buffer[offset++] / volume);
        }

        public override int ModifyRead(float[] buffer, int offset, int count)
        {
            // Ensure that we have an entry for this source
            if (!GetRecording(out ReversableRecording? recording))
                return base.ModifyRead(buffer, offset, count);

            // Cache the seek source to prevent multiple unnecessary casts.
            ISeekable seekSource = SeekableSource;

            // Mark the start of the recording
            if (recording.RecordingStart == -1)
                recording.RecordingStart = seekSource.Position;

            // If we are not actively reversing, don't modify how the reading happens
            if (!recording.Reversing)
                return base.ModifyRead(buffer, offset, count);

            // The sampleQueue is literally a queue of float samples
            ConcurrentQueue<float> sampleQueue = recording.Buffer;
            int samplesRead = 0;
            float volume = Source.Volume;

            lock (sampleQueue)
            {
                // Before we try manually seek backwards, try reading from
                // recorded audio. If the filter is set to reversing mode instead of recording,
                // the sample queue should already be reversed.
                for (int i = 0; i < count; i++)
                {
                    if (sampleQueue.TryDequeue(out float sample))
                    {
                        buffer[offset++] = sample * volume;
                        samplesRead++;
                    }

                    else
                        break;
                }
            }

            // If there was enough recorded audio to fulfill requirements, stop caring
            if (samplesRead == count)
                return samplesRead;

            int samplesRemaining = count - samplesRead;

            // If the source cannot be seeked, reversing won't be able to do anything past the cached samples.
            // We are only going to do the seeking if this provider is not only seekable, but loopable as well, since in this context,
            // we know that this filter will only be applied to one of the mod's song sources.
            // (All of the mod's sources are loopable by default.)
            //
            // If you want, you can also make this account for `ISeekable`s, and just have the position default
            // to 0 or do a sort of fake loop by seeking backwards from Length - although, again, in our context, it's
            // pretty safe to assume the source will be a loopable song.
            Song songSource = SongSource;
            ILoopTags loopSource = null!;

            // The BaseSource property is implemented on song sources that "wrap" other
            // song sources, like the buffered reader. It points to the underlying source
            // for access to source-specific info, but should NOT be used for modifying or
            // otherwise interacting with the base source, as we want to respect any custom
            // behavior of wrapping sources (like the buffered reader). If the source does not
            // wrap another source, it instead points to itself.
            if (SongSource.Source.BaseSource is ILoopTags source)
                loopSource = source;

            // When we reverse audio, we want to take both loop start and end into account.
            // If the source is not explicitly marked with loop tags, we default to the start
            // and end of the track.
            long startIndex = long.Max(loopSource?.LoopStart ?? -1, 0);
            long samplesAvailable = seekSource.Position - startIndex;

            // If this read is expected to cross over the beginning of the loop,
            // we want to seek to just before the end of the loop instead. That way,
            // when audio is read, it reads around the loop.
            if (samplesAvailable < samplesRemaining)
            {
                long endIndex = seekSource.Length;

                if (songSource.IsLooped && (loopSource?.LoopEnd ?? -1) != -1)
                    endIndex = loopSource!.LoopEnd;

                seekSource.Position = endIndex - (samplesRemaining - samplesAvailable);
            }

            // If this is the first time we are real-time seeking to reverse,
            // we need to seek to where the recording started. At this point,
            // our source's position is still wherever we stopped the recording.
            else if (seekSource.Position > recording.RecordingStart)
                seekSource.Position = recording.RecordingStart - samplesRemaining;

            // If our position has already been properly aligned, and
            // we don't need to account for looping, seeking backwards
            // is very easy.
            else
                seekSource.Position -= samplesRemaining;

            // We record where we're going to start the reading, because we're going to want to seek
            // back here after the reading is done. That way our "end position" is technically at the
            // "beginning" of our read section.
            long readStartPosition = seekSource.Position;
            recording.RecordingStart = readStartPosition;

            // "InsuranceBuffer" is an intermediary buffer used to read and reverse audio in real-time.
            recording.InsuranceBuffer = BufferHelpers.Ensure(recording.InsuranceBuffer, samplesRemaining);
            float[] insuranceBuffer = recording.InsuranceBuffer;

            // Read the source
            int sourceSamples = base.ModifyRead(insuranceBuffer, 0, samplesRemaining);

            for (int i = 0; i < sourceSamples; i += 2)
            {
                // Make sure that the channels don't end up flipped by doing i + 1 first.
                //
                // 0 left, 1 right, 2 left, etc... when reversed gives:
                // 0 right, 1 left, 2 right, etc...
                //
                // We still want the audio in the correct channel, so we just flip flop every 2 samples.
                //
                // Place the samples into the final buffer in reverse order
                // We use --offset instead of offset-- as a neat shortcut since sourceSamples is not 0-based indexing.
                buffer[sourceSamples + --offset] = insuranceBuffer[i + 1];
                buffer[sourceSamples + --offset] = insuranceBuffer[i];
            }

            // Seek back to where we "started" the reverse reading, as if
            // the reading were REALLY reversed, this is technically where it would
            // end, not begin.
            seekSource.Position = readStartPosition;

            return samplesRead + sourceSamples;
        }
    }
#pragma warning restore CS8603 // Possible null reference return.
}
