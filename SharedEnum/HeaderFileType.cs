using System;

[Flags]
public enum HeaderFileType
{
    Nothing = 0,
    ZIP = 1,
    GZ = 2,
    SevenZip = 3,
    RAR = 4,

    CHD = 5,

    A7800 = 6,
    Lynx = 7,
    NES = 8,
    FDS = 9,
    PCE = 10,
    PSID = 11,
    SNES = 12,
    SPC = 13,

    HeaderMask = 0x1f,
    Required = 0x80
}

public static class FindHeaderFileType
{
    public static HeaderFileType GetTypeFromFileName(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return HeaderFileType.Nothing;
        
        switch (header.ToLower())
        {
            case "a7800.xml":
            case "no-intro_a7800.xml":
                return HeaderFileType.A7800;
            case "fds.xml":
            case "no-intro_fds.xml":
                return HeaderFileType.FDS;
            case "lynx.xml":
            case "no-intro_lynx.xml":
                return HeaderFileType.Lynx;
            case "nes.xml":
            case "nes0.xml":
            case "no-intro_nes.xml":
            case "nongoodnes.xml":
                return HeaderFileType.NES;
            case "pce.xml":
                return HeaderFileType.PCE;
            case "psid.xml":
                return HeaderFileType.PSID;
            case "snes.xml":
                return HeaderFileType.SNES;
            case "spc.xml":
                return HeaderFileType.SPC;
            default:
                return HeaderFileType.Nothing;
        }
    }
}
