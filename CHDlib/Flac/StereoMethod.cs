
namespace CUETools.Codecs.Flake
{
	/// <summary>
	/// Strategy used to select stereo coding for FLAC encoding/analysis.
	/// </summary>
	public enum StereoMethod
	{
        Invalid,
		Independent,
		Estimate,
		Evaluate,
        Search,
        EstimateX,
        EvaluateX,
        EstimateFixed,
    }
}
