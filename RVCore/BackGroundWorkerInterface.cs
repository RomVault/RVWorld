/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using Compress;

namespace RVCore
{
    public class bgwProgress
    {
        public bgwProgress(int Progress)
        {
            this.Progress = Progress;
        }

        public int Progress { get; }
    }


    public class bgwText
    {
        public bgwText(string Text)
        {
            this.Text = Text;
        }

        public string Text { get; }
    }

    public class bgwText2
    {
        public bgwText2(string Text)
        {
            this.Text = Text;
        }

        public string Text { get; }
    }

    public class bgwText3
    {
        public bgwText3(string Text)
        {
            this.Text = Text;
        }

        public string Text { get; }
    }


    public class bgwSetRange
    {
        public bgwSetRange(int MaxVal)
        {
            this.MaxVal = MaxVal;
        }

        public int MaxVal { get; }
    }

    public class bgwSetRange2
    {
        public bgwSetRange2(int MaxVal)
        {
            this.MaxVal = MaxVal;
        }

        public int MaxVal { get; }
    }


    public class bgwValue2
    {
        public bgwValue2(int Value)
        {
            this.Value = Value;
        }

        public int Value { get; }
    }


    public class bgwRange2Visible
    {
        public bgwRange2Visible(bool Visible)
        {
            this.Visible = Visible;
        }

        public bool Visible { get; }
    }

    public class bgwShowCorrupt
    {
        public bgwShowCorrupt(ZipReturn zr, string filename)
        {
            this.zr = zr;
            this.filename = filename;
        }

        public ZipReturn zr { get; }
        public string filename { get; }
    }

    public class bgwShowError
    {
        public bgwShowError(string filename, string error)
        {
            this.error = error;
            this.filename = filename;
        }

        public string filename { get; }
        public string error { get; }
    }


    public class bgwShowFix
    {
        public bgwShowFix(string fixDir, string fixZip, string fixFile, ulong? size, string dir, string sourceDir, string sourceZip, string sourceFile)
        {
            FixDir = fixDir;
            FixZip = fixZip;
            FixFile = fixFile;
            Size = size.ToString();
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

    public class bgwShowFixError
    {
        public bgwShowFixError(string FixError)
        {
            this.FixError = FixError;
        }

        public string FixError { get; }
    }
}