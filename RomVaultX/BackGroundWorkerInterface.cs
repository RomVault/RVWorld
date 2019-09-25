namespace RomVaultX
{
    public class bgwText
    {
        public bgwText(string Text)
        {
            this.Text = Text;
        }

        public string Text { get; private set; }
    }

    public class bgwText2
    {
        public bgwText2(string Text)
        {
            this.Text = Text;
        }

        public string Text { get; private set; }
    }

    public class bgwText3
    {
        public bgwText3(string Text)
        {
            this.Text = Text;
        }

        public string Text { get; private set; }
    }


    public class bgwSetRange
    {
        public bgwSetRange(int MaxVal)
        {
            this.MaxVal = MaxVal;
        }

        public int MaxVal { get; private set; }
    }

    public class bgwSetRange2
    {
        public bgwSetRange2(int MaxVal)
        {
            this.MaxVal = MaxVal;
        }

        public int MaxVal { get; private set; }
    }


    public class bgwValue2
    {
        public bgwValue2(int Value)
        {
            this.Value = Value;
        }

        public int Value { get; private set; }
    }


    public class bgwRange2Visible
    {
        public bgwRange2Visible(bool Visible)
        {
            this.Visible = Visible;
        }

        public bool Visible { get; private set; }
    }

    public class bgwShowError
    {
        public bgwShowError(string filename, string error)
        {
            this.error = error;
            this.filename = filename;
        }

        public string filename { get; private set; }
        public string error { get; private set; }
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

        public string FixDir { get; private set; }
        public string FixZip { get; private set; }
        public string FixFile { get; private set; }
        public string Size { get; private set; }
        public string Dir { get; private set; }
        public string SourceDir { get; private set; }
        public string SourceZip { get; private set; }
        public string SourceFile { get; private set; }
    }

    public class bgwShowFixError
    {
        public bgwShowFixError(string FixError)
        {
            this.FixError = FixError;
        }

        public string FixError { get; private set; }
    }
}