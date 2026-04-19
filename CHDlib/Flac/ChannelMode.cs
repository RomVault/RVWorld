namespace CUETools.Codecs.Flake
{
    /// <summary>
    /// FLAC channel coding mode.
    /// </summary>
    public enum ChannelMode
    {
        NotStereo = 0,
        LeftRight = 1,
        LeftSide = 8,
        RightSide = 9,
        MidSide = 10
    }
}
