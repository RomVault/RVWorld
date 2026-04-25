using System.IO;
using System.Xml.Serialization;

namespace TrrntZipUICore
{
    public class TzipSettings
    {
        public static string outDir = "";

        public int InZip = 2;
        public int OutZip = 0;
        public bool Force = false;
        public bool Fix = true;
        public int ProcCount = 0;


        public static void WriteConfig(TzipSettings tZipSettings)
        {
            string tzipcfgFilename =
                string.IsNullOrEmpty(outDir)
                ? "Tzipcfg.xml"
                : Path.Combine(outDir, "Tzipcfg.xml");

            if (File.Exists(tzipcfgFilename))
            {
                File.Delete(tzipcfgFilename);
            }

            using (StreamWriter sw = new StreamWriter(tzipcfgFilename))
            {
                XmlSerializer x = new XmlSerializer(typeof(TzipSettings));
                x.Serialize(sw, tZipSettings);
                sw.Flush();
            }

        }
        public static TzipSettings ReadConfig()
        {
            string tzipcfgFilename =
                string.IsNullOrEmpty(outDir)
                ? "Tzipcfg.xml"
                : Path.Combine(outDir, "Tzipcfg.xml");

            if (!File.Exists(tzipcfgFilename))
            {
                TzipSettings tZipSettings = new TzipSettings()
                {
                    InZip = 2,
                    OutZip = 0,
                    Force = false,
                    Fix = true,
                    ProcCount = 0
                };
                return tZipSettings;
            }

            string strXml = File.ReadAllText(tzipcfgFilename);

            using (TextReader sr = new StringReader(strXml))
            {
                XmlSerializer x = new XmlSerializer(typeof(TzipSettings));
                TzipSettings tZipSettings = (TzipSettings)x.Deserialize(sr);
                return tZipSettings;
            }
        }
    }
}
