using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Paralog_gps
{
    class NmeaFileParser : IEnumerable<Waypoint>
    {
        private StreamReader nmeaReader;

        public NmeaFileParser(string nmeaFile)
        {
            nmeaReader = new StreamReader(nmeaFile);
        }

        private IEnumerator<Waypoint> ge()
        {
            Nmea.Time firstTime = null;
            var ci = new CultureInfo("en-US");

            while (!nmeaReader.EndOfStream)
            {
                var line = nmeaReader.ReadLine();
                if (!line.StartsWith(Nmea.GpsFixData.PREFIX))
                    continue;

                var gpgga = new Nmea.GpsFixData(line);
                if (gpgga.FixQuality.Equals("0"))
                    // no fix
                    continue;

                if (firstTime == null)
                    firstTime = gpgga.Time;

                var ms = (int)((gpgga.Time - firstTime) * 1000.0);
                yield return new Waypoint
                {
                    altitude = gpgga.Altitude.Value.ToString(ci),
                    latitude = gpgga.Latitude.Degrees.ToString(ci),
                    longitude = gpgga.Longitude.Degrees.ToString(ci),
                    time = (((double)ms) / 1000.0).ToString(ci)
                };
            }
        }

        public IEnumerator<Waypoint> GetEnumerator()
        {
            return ge();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ge();
        }
    }
}
