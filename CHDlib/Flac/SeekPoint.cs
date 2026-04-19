namespace CUETools.Codecs.Flake
{
    /// <summary>
    /// Seek point entry used for FLAC stream navigation.
    /// </summary>
    public struct SeekPoint
    {
        public long number;
        public long offset;
        public int framesize;
    }
}
