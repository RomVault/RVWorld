using System.IO;
using System.Text;

namespace Compress.SevenZip.Structure
{
    public class BindPair
    {
        public ulong InIndex;
        public ulong OutIndex;

        public void Read(BinaryReader br)
        {
            InIndex = br.ReadEncodedUInt64();
            OutIndex = br.ReadEncodedUInt64();
        }

        public void Write(BinaryWriter bw)
        {
            bw.WriteEncodedUInt64(InIndex);
            bw.WriteEncodedUInt64(OutIndex);
        }


        public void Report(ref StringBuilder sb)
        {
            sb.AppendLine("      InIndex  = " + InIndex);
            sb.AppendLine("      OutIndex = " + OutIndex);
        }
    }
}