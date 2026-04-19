/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using Compress;

namespace RomVaultCore
{
    /// <summary>
    /// Progress update message for background worker operations.
    /// </summary>
    public class bgwProgress
    {
        /// <summary>
        /// Creates a progress update message.
        /// </summary>
        /// <param name="Progress">Progress value (typically 0-100).</param>
        public bgwProgress(int Progress)
        {
            this.Progress = Progress;
        }

        /// <summary>
        /// Progress value (typically 0-100).
        /// </summary>
        public int Progress { get; }
    }


    /// <summary>
    /// Primary status text update message for background worker operations.
    /// </summary>
    public class bgwText
    {
        /// <summary>
        /// Creates a status text update.
        /// </summary>
        /// <param name="Text">Message text.</param>
        public bgwText(string Text)
        {
            this.Text = Text;
        }

        /// <summary>
        /// Message text.
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    /// Secondary status text update message for background worker operations.
    /// </summary>
    public class bgwText2
    {
        /// <summary>
        /// Creates a status text update.
        /// </summary>
        /// <param name="Text">Message text.</param>
        public bgwText2(string Text)
        {
            this.Text = Text;
        }

        /// <summary>
        /// Message text.
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    /// Tertiary status text update message for background worker operations.
    /// </summary>
    public class bgwText3
    {
        /// <summary>
        /// Creates a status text update.
        /// </summary>
        /// <param name="Text">Message text.</param>
        public bgwText3(string Text)
        {
            this.Text = Text;
        }

        /// <summary>
        /// Message text.
        /// </summary>
        public string Text { get; }
    }


    /// <summary>
    /// Sets the primary progress bar range for background worker operations.
    /// </summary>
    public class bgwSetRange
    {
        /// <summary>
        /// Creates a range update.
        /// </summary>
        /// <param name="MaxVal">Maximum value for the progress bar.</param>
        public bgwSetRange(int MaxVal)
        {
            this.MaxVal = MaxVal;
        }

        /// <summary>
        /// Maximum value for the progress bar.
        /// </summary>
        public int MaxVal { get; }
    }

    /// <summary>
    /// Sets the secondary progress bar range for background worker operations.
    /// </summary>
    public class bgwSetRange2
    {
        /// <summary>
        /// Creates a range update.
        /// </summary>
        /// <param name="MaxVal">Maximum value for the secondary progress bar.</param>
        public bgwSetRange2(int MaxVal)
        {
            this.MaxVal = MaxVal;
        }

        /// <summary>
        /// Maximum value for the secondary progress bar.
        /// </summary>
        public int MaxVal { get; }
    }


    /// <summary>
    /// Sets the secondary progress value for background worker operations.
    /// </summary>
    public class bgwValue2
    {
        /// <summary>
        /// Creates a secondary progress value update.
        /// </summary>
        /// <param name="Value">Value to set.</param>
        public bgwValue2(int Value)
        {
            this.Value = Value;
        }

        /// <summary>
        /// Value to set.
        /// </summary>
        public int Value { get; }
    }


    /// <summary>
    /// Toggles visibility of the secondary progress UI.
    /// </summary>
    public class bgwRange2Visible
    {
        /// <summary>
        /// Creates a visibility update message.
        /// </summary>
        /// <param name="Visible">True to show secondary progress UI; otherwise false.</param>
        public bgwRange2Visible(bool Visible)
        {
            this.Visible = Visible;
        }

        /// <summary>
        /// True to show secondary progress UI; otherwise false.
        /// </summary>
        public bool Visible { get; }
    }

    /// <summary>
    /// Error report message for background worker operations.
    /// </summary>
    public class bgwShowError
    {
        /// <summary>
        /// Creates an error report message.
        /// </summary>
        /// <param name="filename">File name associated with the error.</param>
        /// <param name="error">Error message.</param>
        public bgwShowError(string filename, string error)
        {
            this.error = error;
            this.filename = filename;
        }

        /// <summary>
        /// File name associated with the error.
        /// </summary>
        public string filename { get; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string error { get; }
    }


    /// <summary>
    /// Fix-operation progress message describing a source->destination action.
    /// </summary>
    public class bgwShowFix
    {
        /// <summary>
        /// Creates a fix-operation progress message.
        /// </summary>
        /// <param name="fixDir">Destination directory.</param>
        /// <param name="fixZip">Destination archive name (if applicable).</param>
        /// <param name="fixFile">Destination file name.</param>
        /// <param name="size">Optional file size in bytes.</param>
        /// <param name="dir">DAT directory path for the item being fixed.</param>
        /// <param name="sourceDir">Source directory.</param>
        /// <param name="sourceZip">Source archive name (if applicable).</param>
        /// <param name="sourceFile">Source file name.</param>
        public bgwShowFix(string fixDir, string fixZip, string fixFile, ulong? size, string dir, string sourceDir, string sourceZip, string sourceFile)
        {
            FixDir = fixDir;
            FixZip = fixZip;
            FixFile = fixFile;
            Size = size == null ? "" : ((ulong)size).ToString("N0");
            Dir = dir;
            SourceDir = sourceDir;
            SourceZip = sourceZip;
            SourceFile = sourceFile;
        }

        public string FixDir { get; }
        public string FixZip { get; }
        public string FixFile { get; }
        public string Size { get; }
        public string Dir { get; }
        public string SourceDir { get; }
        public string SourceZip { get; }
        public string SourceFile { get; }
    }

    /// <summary>
    /// Fix-operation error message.
    /// </summary>
    public class bgwShowFixError
    {
        /// <summary>
        /// Creates a fix-operation error message.
        /// </summary>
        /// <param name="FixError">Error message.</param>
        public bgwShowFixError(string FixError)
        {
            this.FixError = FixError;
        }

        /// <summary>
        /// Error message.
        /// </summary>
        public string FixError { get; }
    }
}
