using System;

namespace Trrntzip
{
    public class ZippedFile
    {
        public int Index;
        public string Name;
        public ulong Size;
        public uint CRC;

        public byte[] ByteCRC
        {
            get
            {
                byte[] bcrc = new byte[4];
                bcrc[3] = (byte) (CRC & 0xff);
                bcrc[2] = (byte) ((CRC >> 8) & 0xff);
                bcrc[1] = (byte) ((CRC >> 16) & 0xff);
                bcrc[0] = (byte) ((CRC >> 24) & 0xff);
                return bcrc;
            }
            set
            {
                CRC = value[3] +
                      ((uint) value[2] << 8) +
                      ((uint) value[1] << 16) +
                      ((uint) value[0] << 24);
            }
        }

        public string StringCRC => BitConverter.ToString(ByteCRC).ToLower().Replace("-", "");
    }
}