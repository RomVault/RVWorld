using System;
using System.Collections.Generic;

namespace CHDReaderTest.Flac.FlacDeps
{
    /// <summary>
    /// Abstraction for a readable audio source that can decode into PCM buffers.
    /// </summary>
    public interface IAudioSource
    {
        IAudioDecoderSettings Settings { get; }

        AudioPCMConfig PCM { get; }
        string Path { get; }

        TimeSpan Duration { get; }
        long Length { get; }
        long Position { get; set; }
        long Remaining { get; }

        int Read(AudioBuffer buffer, int maxLength);
        void Close();
    }

    /// <summary>
    /// Describes a single playable audio title within a container (e.g., disc image).
    /// </summary>
    public interface IAudioTitle
    {
        List<TimeSpan> Chapters { get; }
        AudioPCMConfig PCM { get; }
        string Codec { get; }
        string Language { get; }
        int StreamId { get; }
        //IAudioSource Open { get; }
    }

    /// <summary>
    /// Collection of <see cref="IAudioTitle"/> items.
    /// </summary>
    public interface IAudioTitleSet
    {
        List<IAudioTitle> AudioTitles { get; }
    }

    /// <summary>
    /// Convenience helpers for formatting and duration queries on <see cref="IAudioTitle"/>.
    /// </summary>
    public static class IAudioTitleExtensions
    {
        public static TimeSpan GetDuration(this IAudioTitle title)
        {
            var chapters = title.Chapters;
            return chapters[chapters.Count - 1];
        }


        public static string GetRateString(this IAudioTitle title)
        {
            var sr = title.PCM.SampleRate;
            if (sr % 1000 == 0) return $"{sr / 1000}KHz";
            if (sr % 100 == 0) return $"{sr / 100}.{sr / 100 % 10}KHz";
            return $"{sr}Hz";
        }

        public static string GetFormatString(this IAudioTitle title)
        {
            switch (title.PCM.ChannelCount)
            {
                case 1: return "mono";
                case 2: return "stereo";
                default: return "multi-channel";
            }
        }
    }

    /// <summary>
    /// Wraps a single <see cref="IAudioSource"/> as a one-title <see cref="IAudioTitle"/>.
    /// </summary>
    public class SingleAudioTitle : IAudioTitle
    {
        public SingleAudioTitle(IAudioSource source) { this.source = source; }
        public List<TimeSpan> Chapters => new List<TimeSpan> { TimeSpan.Zero, source.Duration };
        public AudioPCMConfig PCM => source.PCM;
        public string Codec => source.Settings.Extension;
        public string Language => "";
        public int StreamId => 0;
        IAudioSource source;
    }

    /// <summary>
    /// Wraps a single <see cref="IAudioSource"/> as a one-title set.
    /// </summary>
    public class SingleAudioTitleSet : IAudioTitleSet
    {
        public SingleAudioTitleSet(IAudioSource source) { this.source = source; }
        public List<IAudioTitle> AudioTitles => new List<IAudioTitle> { new SingleAudioTitle(source) };
        IAudioSource source;
    }
}
