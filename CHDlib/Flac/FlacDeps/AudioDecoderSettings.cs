using System;
using System.ComponentModel;
using System.IO;

namespace CHDReaderTest.Flac.FlacDeps
{
    /// <summary>
    /// Describes a decoder configuration and factory for opening audio sources.
    /// </summary>
    public interface IAudioDecoderSettings
    {
        string Name { get; }

        string Extension { get; }

        Type DecoderType { get; }

        int Priority { get; }

        IAudioDecoderSettings Clone();
    }

    /// <summary>
    /// Convenience helpers for <see cref="IAudioDecoderSettings"/>.
    /// </summary>
    public static class IAudioDecoderSettingsExtensions
    {
        /// <summary>
        /// Returns true when the settings object exposes at least one browsable property.
        /// </summary>
        /// <param name="settings">Decoder settings instance.</param>
        public static bool HasBrowsableAttributes(this IAudioDecoderSettings settings)
        {
            bool hasBrowsable = false;
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(settings))
            {
                bool isBrowsable = true;
                foreach (var attribute in property.Attributes)
                {
                    var browsable = attribute as BrowsableAttribute;
                    isBrowsable &= browsable == null || browsable.Browsable;
                }
                hasBrowsable |= isBrowsable;
            }
            return hasBrowsable;
        }

        /// <summary>
        /// Initializes settings by resetting all properties to their default values.
        /// </summary>
        /// <param name="settings">Decoder settings instance.</param>
        public static void Init(this IAudioDecoderSettings settings)
        {
            // Iterate through each property and call ResetValue()
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(settings))
                property.ResetValue(settings);
        }

        /// <summary>
        /// Opens an audio source for the given path using the decoder type declared by the settings.
        /// </summary>
        /// <param name="settings">Decoder settings instance.</param>
        /// <param name="path">Source path.</param>
        /// <param name="IO">Optional stream to use instead of opening from <paramref name="path"/>.</param>
        /// <returns>Audio source instance.</returns>
        public static IAudioSource Open(this IAudioDecoderSettings settings, string path, Stream IO = null)
        {
            return Activator.CreateInstance(settings.DecoderType, settings, path, IO) as IAudioSource;
        }
    }
}
