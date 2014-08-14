using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using CommandLine;
using CommandLine.Text;
using Nortal.Utilities.Csv;

namespace Paralog_gps
{
    class Options
    {
        [Option('d', "database", Required = true, HelpText = "Paralog database")]
        public string Database { get; set; }

        [Option('t', "type", Required = false, DefaultValue = "flysight", HelpText = "Type of GPS log")]
        public string Type { get; set; }

        [Option('i', "input-file", Required = true, HelpText = "Name of GPS log file")]
        public string InputFile { get; set; }

        [Option('j', "jump-number", Required = true, HelpText = "Number of jump to modify")]
        public int JumpNumber { get; set; }

        [Option('c', "create", Required = false, DefaultValue = false, HelpText = "Create a record for jump if it doesn't exist.")]
        public bool ShouldCreateJump { get; set; }

        [Option("dropzone", Required = false, HelpText = "Dropzone. Used when new jump is created, otherwise ignored.")]
        public string DropZone { get; set; }

        [Option("aircraft", Required = false, HelpText = "Aircraft name. Used when new jump is created, otherwise ignored.")]
        public string Aircraft { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText c) => HelpText.DefaultParsingErrorsHandler(this, c));
        }
    }
    
    class Program
    {
        private static XmlDocument LoadFile(String filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            GZipStream gs = new GZipStream(fs, CompressionMode.Decompress);
            XmlTextReader reader = new XmlTextReader(gs);
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
            reader.Close();
            gs.Close();
            fs.Close();

            return doc;
        }

        private static void SaveFile(String filename, XmlDocument doc)
        {
            FileStream fs = new FileStream(filename, FileMode.Truncate);
            GZipStream gs = new GZipStream(fs, CompressionMode.Compress, false);
            doc.Save(gs);
            gs.Close();
            fs.Close();
        }

        private static XmlNode FindJump(XmlDocument doc, int jumpNumber)
        {
            var query = String.Format("/pml/log/jump[@n={0}]", jumpNumber);
            var jumps = doc.SelectNodes(query);
            return (jumps.Count == 0) ? null : jumps[0];
        }

        private static XmlNode CreateJump(XmlDocument doc, int jumpNumber, string dropZone, string aircraft)
        {
            var jump = doc.CreateElement("jump");
            var n = doc.CreateAttribute("n");
            n.Value = jumpNumber.ToString();
            jump.Attributes.Append(n);

            var note = doc.CreateElement("note");
            note.InnerText = "Imported by paralog-gps.";
            jump.AppendChild(note);

            if (dropZone.Length > 0)
            {
                var dz = doc.CreateElement("dz");
                dz.InnerText = dropZone;
                jump.AppendChild(dz);
            }

            if (aircraft.Length > 0)
            {
                var ac = doc.CreateElement("ac");
                ac.InnerText = aircraft;
                jump.AppendChild(ac);
            }
            // TODO: attr.ts = take date and time from GPS data.
            // TODO: attr.mod = ts
            // TODO: determine dz by GPS coordinates (if unable, require command line argument).

            return jump;
        }

        private static void CreateProfile()
        {
        }

        static void Main(String[] args)
        {
            var opt = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, opt))
            {
                return;
            }

            var xmlfile = opt.Database;
            var inputFile = opt.InputFile;
            var jumpNumber = opt.JumpNumber;

            XmlDocument doc = null;
            try
            {
                // load file
                Console.WriteLine("Loading {0} ...", xmlfile);
                doc = LoadFile(xmlfile);

                // backup
                Console.WriteLine("Backing up {0} to {0}.bak ...", xmlfile, xmlfile);
                File.Copy(xmlfile, xmlfile + ".bak");

                // find jump
                Console.Write("Looking for jump {0} ... ", jumpNumber);

                XmlNode jump = FindJump(doc, jumpNumber);
                if (jump == null)
                {
                    Console.WriteLine("not found");
                    if (opt.ShouldCreateJump)
                    {
                        Console.Write("Creating record for jump {0} ...", jumpNumber);
                        jump = CreateJump(doc, jumpNumber, opt.DropZone, opt.Aircraft);

                        var log = doc.GetElementsByTagName("log");
                        log[0].AppendChild(jump);

                        var sz = log[0].Attributes["size"].Value;
                        log[0].Attributes["size"].Value = (int.Parse(sz) + 1).ToString();

                        Console.WriteLine("done");
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("found");
                }

                if (opt.Type == "nmea")
                {
                    createProfileFromNmea(inputFile, doc, jump);
                }
                else if (opt.Type == "flysight")
                {
                    createProfileFromFlysight(inputFile, doc, jump);
                }
                else
                {
                    Console.WriteLine("Invalid input file type: {0}", opt.Type);
                    return;
                }

                // save file
                SaveFile(xmlfile, doc);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Input file {0} not found.", ex.FileName);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}.", ex.Message);
                return;
            }
        }

        private static void createProfileFromNmea(string nmeafile, XmlDocument doc, XmlNode jump)
        {
            createProfileFromData(new NmeaFileParser(nmeafile), doc, jump);
        }

        private static void createProfileFromFlysight(string flysightFile, XmlDocument doc, XmlNode jump)
        {
            createProfileFromData(new FlysightParser(flysightFile), doc, jump);
        }

        /// <summary>
        /// Create progile
        /// </summary>
        /// <param name="wpts"></param>
        /// <param name="doc"></param>
        /// <param name="jump"></param>
        private static void createProfileFromData(IEnumerable<Waypoint> wpts, XmlDocument doc, XmlNode jump)
        {
            var ci = new CultureInfo("en-US");

            // create profile from Flysight file.
            Console.WriteLine("Creating profile ...");
            var profile = doc.CreateElement("profile");
            var type = doc.CreateAttribute("type");
            type.Value = "gps";
            profile.Attributes.Append(type);

            var qne = doc.CreateElement("qne");
            qne.InnerText = "0.0";
            profile.AppendChild(qne);

            var waypoints = doc.CreateElement("waypoints");
            var waypointsSize = 0;

            // if jump does not have ts attribute, take it from the first waypoint.
            var wp1 = wpts.First();
            if (jump.Attributes["ts"] == null && wp1 != null && wp1.timestamp != null)
            {
                var ts = doc.CreateAttribute("ts");
                ts.Value = wp1.timestamp;
                jump.Attributes.Append(ts);
            }

            foreach (var wp in wpts)
            {
                // altitude
                var a = doc.CreateAttribute("a");
                a.Value = wp.altitude;

                // time
                var t = doc.CreateAttribute("t");
                t.Value = wp.time;

                // latitude
                var lat = doc.CreateElement("lat");
                lat.InnerText = wp.latitude;

                // longitude
                var lon = doc.CreateElement("lon");
                lon.InnerText = wp.longitude;

                var wpt = doc.CreateElement("wpt");
                wpt.AppendChild(lat);
                wpt.AppendChild(lon);
                wpt.Attributes.Append(a);
                wpt.Attributes.Append(t);

                waypoints.AppendChild(wpt);
                ++waypointsSize;
            }

            Console.WriteLine("Added {0} waypoints.", waypointsSize);

            var waypointsSizeAttr = doc.CreateAttribute("size");
            waypointsSizeAttr.Value = waypointsSize.ToString();
            waypoints.Attributes.Append(waypointsSizeAttr);

            profile.AppendChild(waypoints);
            jump.AppendChild(profile);
        }
    }
}
