using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace Paralog_gps
{
    public class JumpData
    {
        public JumpData(XmlNode n)
        {
            jump_ = n;
        }

        public string Aircraft { get; set; }
        public string Dropzone { get; set; }
        // ...

        public bool CreateProfileFromFile(string inputFile, string type)
        {
            if (type == "nmea")
            {
                createGpsProfileFromData(new NmeaFileParser(inputFile));
                return true;
            }
            else if (type == "flysight")
            {
                createGpsProfileFromData(new FlysightParser(inputFile));
                return true;
            }
            else if (type == "protrack")
            {
                //createProfileFromProtrack(inputFile, doc, jump);
                Console.WriteLine("Not yet implemented.");
                return false;
            }
            else
                return false;
        }

        //private static void createProfileFromProtrack(string protrackFile, XmlDocument doc, XmlNode jump)
        //{
        //    createBarometricProfileFromData(new ProtrackParser(protrackFile, doc, jump));
        //}

        //private static void createProfileFromProtrack(string protrackFile, XmlDocument doc, XmlNode jump)
        //{
        //    createBarometricProfileFromData(new NeptuneParser(protrackFile, doc, jump));
        //}

        /// <summary>
        /// Create progile
        /// </summary>
        /// <param name="wpts"></param>
        /// <param name="doc"></param>
        /// <param name="jump"></param>
        private void createGpsProfileFromData(IEnumerable<Waypoint> wpts)
        {
            var jump = jump_;
            var doc = jump.OwnerDocument;
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

        private XmlNode jump_;
    }
}