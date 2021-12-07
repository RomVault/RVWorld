/*
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using DATReader.DatStore;
using Newtonsoft.Json.Linq;
using RVIO;

namespace DATReader.DatWriter
{
    public class DatJSONWriter
    {

        public void WriteDat(string strFilename, DatHeader datHeader, bool newStyle = false)
        {
            string dir = Path.GetDirectoryName(strFilename);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            JObject jObj = new JObject();

            JObject header = new JObject();
            WriteHeader(header, datHeader);
            jObj.Add("Header",header);

            JArray jDir=new JArray();
            jObj.Add("root",jDir);

            writeBase(jDir, datHeader.BaseDir, newStyle);

            System.IO.File.WriteAllText(strFilename,jObj.ToString());

        }

        private void WriteHeader(JObject header, DatHeader datHeader)
        {
            header.Add("name", datHeader.Name);
            header.Add("rootdir", datHeader.RootDir);
            header.Add("header", datHeader.Header);
            header.Add("type", datHeader.Type);
            header.Add("description", datHeader.Description);
            header.Add("category", datHeader.Category);
            header.Add("version", datHeader.Version);
            header.Add("date", datHeader.Date);
            header.Add("author", datHeader.Author);
            header.Add("email", datHeader.Email);
            header.Add("homepage", datHeader.Homepage);
            header.Add("url", datHeader.URL);
            header.Add("comment", datHeader.Comment);
            header.Add("forcepacking", datHeader.Compression);
        }

        private void writeBase(JArray jObj, DatDir baseDirIn, bool newStyle)
        {
            DatBase[] dirChildren = baseDirIn.ToArray();

            if (dirChildren == null)
                return;
            foreach (DatBase baseObj in dirChildren)
            {
                if (baseObj is DatDir baseDir)
                {
                    if (baseDir.DGame != null)
                    {
                        DatGame g = baseDir.DGame;
                        JObject game = new JObject();
                        jObj.Add(game);

                        game.Add("name", baseDir.Name);

                        if (baseDir.DatFileType == DatFileType.DirTorrentZip)
                        {
                            game.Add("type", "trrntzip");
                        }
                        else if (baseDir.DatFileType == DatFileType.DirRVZip)
                        {
                            game.Add("type", "rvzip");
                        }
                        else if (baseDir.DatFileType == DatFileType.Dir7Zip)
                        {
                            game.Add("type", "7zip");
                        }
                        else if (baseDir.DatFileType == DatFileType.Dir)
                        {
                            game.Add("type", "dir");
                        }

                        if (!g.IsEmuArc)
                        {
                            //         sw.WriteItem("cloneof", g.CloneOf);
                            //         sw.WriteItem("romof", g.RomOf);
                        }

                        game.Add("description", g.Description);
                        //if (newStyle && !string.IsNullOrWhiteSpace(g.Comments))
                        //    game.Add("comments", g.Comments);
                        if (g.IsEmuArc)
                        {
                            JObject t = new JObject("tea");
                            game.Add(t);
                            t.Add("titleid", g.TitleId);
                            t.Add("source", g.Source);
                            t.Add("publisher", g.Publisher);
                            t.Add("developer", g.Developer);
                            t.Add("year", g.Year);
                            t.Add("genre", g.Genre);
                            t.Add("subgenre", g.SubGenre);
                            t.Add("ratings", g.Ratings);
                            t.Add("score", g.Score);
                            t.Add("players", g.Players);
                            t.Add("enabled", g.Enabled);
                            t.Add("crc", g.CRC);
                            t.Add("cloneof", g.CloneOf);
                            t.Add("relatedto", g.RelatedTo);
                        }
                        else
                        {
                            if(g.Year!=null) game.Add("year", g.Year);
                            if(g.Manufacturer!=null) game.Add("manufacturer", g.Manufacturer);
                        }

                        JArray ga = new JArray();
                        game.Add("objects",ga);
                        writeBase(ga, baseDir, newStyle);
                    }
                    else
                    {
                        JObject d = new JObject();
                        jObj.Add(d);
                        d.Add("name", baseDir.Name);
                        d.Add("type", "dir");


                        JArray f1 =new JArray();
                        d.Add("objects",f1);
                        writeBase(f1, baseDir, newStyle);
                    }
                    continue;
                }

                if (baseObj is DatFile baseRom)
                {
                    if (baseRom.Name.Substring(baseRom.Name.Length - 1) == "/" && newStyle)
                    {
                        JObject d = new JObject();
                        jObj.Add(d);

                        d.Add("name", baseRom.Name.Substring(0, baseRom.Name.Length - 1));
                        d.Add("type", "dir");
                        //sw.WriteItem("merge", baseRom.Merge);
                        if (baseRom.DateModified != "1996/12/24 23:32:00") d.Add("date", baseRom.DateModified);
                    }
                    else
                    {
                        JObject d = new JObject();
                        jObj.Add(d);
                        d.Add("name", baseRom.Name);
                        if (baseRom.DatFileType == DatFileType.FileTorrentZip)
                        {
                            d.Add("type", "filetrrntzip");
                        }
                        //else if (baseRom.DatFileType == DatFileType.FileRVZip)
                        //{
                        //    d.Add("type", "filervzip");
                        //}
                        //sw.WriteItem("merge", baseRom.Merge);
                        d.Add("size", baseRom.Size);
                        d.Add("crc", ByteToStr( baseRom.CRC));
                        d.Add("sha1", ByteToStr(baseRom.SHA1));
                        if (baseRom.MD5!=null) d.Add("md5", ByteToStr(baseRom.MD5));
                        if (baseRom.DateModified != "1996/12/24 23:32:00") d.Add("date", baseRom.DateModified);
                        if (baseRom.Status != null && baseRom.Status.ToLower() != "good") d.Add("status", baseRom.Status);
                    }
                }
            }
        }

        private static string ByteToStr(byte[] b)
        {
            return b == null ? "" : BitConverter.ToString(b).ToLower().Replace("-", "");
        }
    }

}
*/