namespace CHDReaderTest.Flac.FlacDeps
{
    /// <summary>
    /// Abstraction for an audio destination/sink that can receive decoded PCM buffers.
    /// </summary>
    public interface IAudioDest
    {
        //IAudioEncoderSettings Settings { get; }

        string Path { get; }
        long FinalSampleCount { set; }

        void Write(AudioBuffer buffer);
        void Close();
        void Delete();
    }
}
