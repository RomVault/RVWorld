using System.IO;
using System.Xml.Serialization;

namespace ROMVault
{
    public class defaults
    {
        public int mainX;
        public int mainY;
        public int mainWidth;
        public int mainHeight;

        public int splitDatInfoGameInfo_pos = int.MinValue;
        public int splitGameListRomList_pos = int.MinValue;
        public int splitListArt_pos = int.MinValue;

        public int gg0_width = int.MinValue;
        public int gg1_width = int.MinValue;
        public int gg2_width = int.MinValue;
        public int gg3_width = int.MinValue;

        public int rg0_width = int.MinValue;
        public int rg1_width = int.MinValue;
        public int rg2_width = int.MinValue;
        public int rg3_width = int.MinValue;
        public int rg4_width = int.MinValue;
        public int rg5_width = int.MinValue;
        public int rg6_width = int.MinValue;
        public int rg7_width = int.MinValue;
        public int rg8_width = int.MinValue;
        public int rg9_width = int.MinValue;
        public int rg10_width = int.MinValue;
        public int rg11_width = int.MinValue;
        public int rg12_width = int.MinValue;
        public int rg13_width = int.MinValue;
        public int rg14_width = int.MinValue;

        public int nfo_FontSize = int.MinValue;

        public static void WriteDefaults(defaults settings)
        {
            try
            {
                if (!Directory.Exists("config"))
                    Directory.CreateDirectory("config");

                string settingsPath = "screenpos.xml";
                settingsPath = Path.Combine("config", settingsPath);
                using (StreamWriter sw = new StreamWriter(settingsPath))
                {
                    XmlSerializer x = new XmlSerializer(typeof(defaults));
                    x.Serialize(sw, settings);
                    sw.Flush();
                }
            }
            catch
            { }
        }

        public static defaults ReadDefaults()
        {
            try
            {
                if (!Directory.Exists("config"))
                    return null;

                string settingsPath = "screenpos.xml";
                settingsPath = Path.Combine("config", settingsPath);
                if (!File.Exists(settingsPath))
                    return null;

                string strXml = System.IO.File.ReadAllText(settingsPath);
                defaults retDefaults;
                using (TextReader sr = new StringReader(strXml))
                {
                    XmlSerializer x = new XmlSerializer(typeof(defaults));
                    retDefaults = (defaults)x.Deserialize(sr);
                }

                return retDefaults;
            }
            catch
            {
                return null;
            }
        }
    }
}
