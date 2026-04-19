namespace CUETools.Codecs.Flake
{
    /// <summary>
    /// Strategy used to select the LPC window for FLAC encoding/analysis.
    /// </summary>
    public enum WindowMethod
    {
        Invalid,
        Evaluate,
        Search,
        Estimate,
        Estimate2,
        Estimate3,
        EstimateN,
        Evaluate2,
        Evaluate2N,
        Evaluate3,
        Evaluate3N,
        EvaluateN,
    }
}
