using System;

[Flags]
/// <summary>
/// Header signature classification used for headered ROM formats and common container magics.
/// </summary>
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

