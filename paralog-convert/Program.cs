using System;
using System.IO;
using System.Xml;

using CommandLine;
using CommandLine.Text;
using Paralog_tools;


namespace Paralog_convert
{
    class Options
    {
        [Option('d', "database", Required = true, HelpText = "Paralog database file.")]
        public string Database { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o => Main(o))
                .WithNotParsed(errs => {});
        }
        static void Main(Options opt)
        {
            var xmlfile = opt.Database;
            Logbook logbook = null;
            Console.WriteLine("Loading {0} ...", xmlfile);
            logbook = Logbook.LoadFromFile(xmlfile);

            Console.WriteLine("Backing up {0} to {0}.bak ...", xmlfile, xmlfile);
            //File.Copy(xmlfile, xmlfile + ".bak");

            var gpsProfiles = logbook.SelectNodes("/pml/log/jump/profile[@type='gps']");
            Console.WriteLine($"Found {gpsProfiles.Count} jumps with GPS profile.");
            foreach(XmlNode profile in gpsProfiles)
            {                
                var waypoints = profile.SelectSingleNode("waypoints/@size");
                Console.WriteLine($"Jump {profile.ParentNode.Attributes["n"].Value} Profile has {waypoints.Value} waypoints");

                foreach (XmlNode wpt in profile.SelectNodes("waypoints/wpt"))
                {
                    var lat = wpt.SelectSingleNode("lat");                    
                    var la = wpt.OwnerDocument.CreateAttribute("la");
                    la.Value = lat.InnerText;
                    wpt.Attributes.Append(la);

                    var lon = wpt.SelectSingleNode("lon");
                    var lo = wpt.OwnerDocument.CreateAttribute("lo");
                    lo.Value = lon.InnerText;
                    wpt.Attributes.Append(lo);

                    wpt.RemoveChild(lat);
                    wpt.RemoveChild(lon);
                }
            }

            logbook.SaveFile(xmlfile+".new");
        }
    }
}
