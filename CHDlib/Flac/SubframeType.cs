namespace CUETools.Codecs.Flake
{
    /// <summary>
    /// FLAC subframe coding type.
    /// </summary>
    public enum SubframeType
    {
        Constant = 0,
        Verbatim = 1,
        Fixed = 8,
        LPC = 32
    }
}
