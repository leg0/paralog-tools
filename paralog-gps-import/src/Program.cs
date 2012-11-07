using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.IO.Compression;
using System.Globalization;

namespace Paralog_gps
{
    class Program
    {
        private static void Usage()
        {
            Console.WriteLine("Usage:\n\tparalog-gps <paralog-file> <nmea-file> <jump-number>");
            Console.WriteLine("\n\nFor example:\n\tparalog-gps logbook.pmz jump-771.txt 771\n");
        }

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

        private static XmlNode FindJump(XmlDocument doc, String jumpNumber)
        {
            String query = String.Format("/pml/log/jump/@n={0}", jumpNumber);
            XmlNodeList jumps = doc.SelectNodes("/pml/log/jump");
            XmlNode jump = null;
            foreach (XmlNode j in jumps)
            {
                XmlAttribute attr = j.Attributes["n"];
                if (attr != null && attr.Value.Equals(jumpNumber))
                {
                    return j;
                }
            }

            return null;
        }

        private static void CreateProfile()
        {
        }

        static void Main(String[] args)
        {
            if (args.Length != 3)
            {
                Usage();
                return;
            }

            String xmlfile = args[0];
            String nmeafile = args[1];
            String jumpNumber = args[2];

            CultureInfo ci = new CultureInfo("en-US");

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

                // create profile from NMEA file.
                Console.WriteLine("Creating profile ...");
                XmlElement profile = doc.CreateElement("profile");
                XmlAttribute type = doc.CreateAttribute("type");
                type.Value = "gps";
                profile.Attributes.Append(type);

                XmlElement qne = doc.CreateElement("qne");
                qne.InnerText = "0.0";
                profile.AppendChild(qne);

                XmlElement waypoints = doc.CreateElement("waypoints");

                Nmea.Time firstTime = null;
                StreamReader nmeaReader = new StreamReader(nmeafile);

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
                    XmlAttribute a = doc.CreateAttribute("a");
                    a.Value = gpgga.Altitude.Value.ToString(ci);
                    // time
                    XmlAttribute t = doc.CreateAttribute("t");
                    int ms = (int)((gpgga.Time - firstTime) * 1000.0);
                    t.Value = (((double)ms) / 1000.0).ToString(ci);

                    // latitude
                    XmlElement lat = doc.CreateElement("lat");
                    lat.InnerText = (gpgga.Latitude.Degrees).ToString(ci);

                    // longitude
                    XmlElement lon = doc.CreateElement("lon");
                    lon.InnerText = (gpgga.Longitude.Degrees).ToString(ci);

                    XmlElement wpt = doc.CreateElement("wpt");
                    wpt.AppendChild(lat);
                    wpt.AppendChild(lon);
                    wpt.Attributes.Append(a);
                    wpt.Attributes.Append(t);

                    waypoints.AppendChild(wpt);
                    ++waypointsSize;
                }

                Console.WriteLine("Added {0} waypoints.", waypointsSize);

                XmlAttribute waypointsSizeAttr = doc.CreateAttribute("size");
                waypointsSizeAttr.Value = waypointsSize.ToString();
                waypoints.Attributes.Append(waypointsSizeAttr);

                profile.AppendChild(waypoints);
                jump.AppendChild(profile);

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
    }
}
