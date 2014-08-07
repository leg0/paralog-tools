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
                    //Console.WriteLine("Jump {0} not found in file {1}.", jumpNumber, xmlfile);
                    return;
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
            var ci = new CultureInfo("en-US");

            // create profile from NMEA file.
            Console.WriteLine("Creating profile ...");
            var profile = doc.CreateElement("profile");
            var type = doc.CreateAttribute("type");
            type.Value = "gps";
            profile.Attributes.Append(type);

            var qne = doc.CreateElement("qne");
            qne.InnerText = "0.0";
            profile.AppendChild(qne);

            var waypoints = doc.CreateElement("waypoints");

            Nmea.Time firstTime = null;
            var nmeaReader = new StreamReader(nmeafile);

            int waypointsSize = 0;

            while (!nmeaReader.EndOfStream)
            {
                String line = nmeaReader.ReadLine();
                if (!line.StartsWith(Nmea.GpsFixData.PREFIX))
                    continue;

                Nmea.GpsFixData gpgga = new Nmea.GpsFixData(line);
                if (gpgga.FixQuality.Equals("0"))
                    // no fix
                    continue;

                if (firstTime == null)
                    firstTime = gpgga.Time;

                // altitude
                var a = doc.CreateAttribute("a");
                a.Value = gpgga.Altitude.Value.ToString(ci);
                // time
                var t = doc.CreateAttribute("t");
                int ms = (int)((gpgga.Time - firstTime) * 1000.0);
                t.Value = (((double)ms) / 1000.0).ToString(ci);

                // latitude
                var lat = doc.CreateElement("lat");
                lat.InnerText = (gpgga.Latitude.Degrees).ToString(ci);

                // longitude
                var lon = doc.CreateElement("lon");
                lon.InnerText = (gpgga.Longitude.Degrees).ToString(ci);

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

        private static void createProfileFromFlysight(string flysightfile, XmlDocument doc, XmlNode jump)
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

            var flysightReader = new StreamReader(flysightfile);
            var waypointsSize = 0;
            var csvSettings = new CsvSettings();
            csvSettings.FieldDelimiter = ',';
            csvSettings.RowDelimiter = "\r\n";
            var p = new CsvParser(flysightReader, csvSettings);
            DateTime firstTime = DateTime.Now;
            int lineNumber = 0;
            while (p.HasMoreRows)
            {
                var line = p.ReadNextRow();
                if (line == null)
                    break;

                // The first two lines are headers.
                ++lineNumber;
                if (lineNumber < 3)
                    continue;
                else if (lineNumber == 3)
                    firstTime = DateTime.Parse(line[0]);

                // altitude
                var a = doc.CreateAttribute("a");
                a.Value = line[3].ToString(ci);
                // time
                var t = doc.CreateAttribute("t");
                DateTime dt = DateTime.Parse(line[0]);
                var diff = dt.Subtract(firstTime);
                t.Value = (((double)diff.TotalMilliseconds) / 1000.0).ToString(ci);

                // latitude
                var lat = doc.CreateElement("lat");
                lat.InnerText = line[1];

                // longitude
                var lon = doc.CreateElement("lon");
                lon.InnerText = line[2];

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
