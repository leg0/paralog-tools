﻿using System.IO;
using System.IO.Compression;
using System.Xml;

namespace Paralog_tools
{
    public class Logbook
    {
        public static Logbook LoadFromFile(string file)
        {
            return new Logbook(file);
        }

        private Logbook(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            GZipStream gs = new GZipStream(fs, CompressionMode.Decompress);
            XmlTextReader reader = new XmlTextReader(gs);
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = false;
            doc.Load(reader);
            reader.Close();
            gs.Close();
            fs.Close();

            doc_ = doc;
        }

        public void SaveFile(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            GZipStream gs = new GZipStream(fs, CompressionMode.Compress, false);
            doc_.PreserveWhitespace = true;
            doc_.Save(gs);
            gs.Close();
            fs.Close();
        }

        //public XmlNodeList Jumps => doc_.SelectNodes("/pml/log/jump");
        public XmlNodeList SelectNodes(string xpath) => doc_.SelectNodes(xpath);

        public JumpData FindJump(int jumpNumber)
        {
            var query = string.Format("/pml/log/jump[@n={0}]", jumpNumber);
            var jumps = doc_.SelectNodes(query);
            return (jumps.Count == 0) ? null : new JumpData(jumps[0]);
        }

        public JumpData CreateJump(int jumpNumber, string dropZone, string aircraft)
        {
            var jump = doc_.CreateElement("jump");
            var n = doc_.CreateAttribute("n");
            n.Value = jumpNumber.ToString();
            jump.Attributes.Append(n);

            var note = doc_.CreateElement("note");
            note.InnerText = "Imported by paralog-gps.";
            jump.AppendChild(note);

            if (dropZone.Length > 0)
            {
                var dz = doc_.CreateElement("dz");
                dz.InnerText = dropZone;
                jump.AppendChild(dz);
            }

            if (aircraft.Length > 0)
            {
                var ac = doc_.CreateElement("ac");
                ac.InnerText = aircraft;
                jump.AppendChild(ac);
            }
            // TODO: attr.ts = take date and time from GPS data.
            // TODO: attr.mod = ts
            // TODO: determine dz by GPS coordinates (if unable, require command line argument).


            var log = doc_.GetElementsByTagName("log");
            log[0].AppendChild(jump);

            var sz = log[0].Attributes["size"].Value;
            log[0].Attributes["size"].Value = (int.Parse(sz) + 1).ToString();

            return new JumpData(jump);
        }


        private XmlDocument doc_;
    }
}