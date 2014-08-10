using Nortal.Utilities.Csv;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Paralog_gps
{
    class FlysightParser : IEnumerable<Waypoint>
    {
        private CsvParser parser;

        public FlysightParser(string flySightFile)
        {
            parser = new CsvParser(new StreamReader(flySightFile));
        }

        private IEnumerator<Waypoint> ge()
        {
            var lineNumber = 0;
            var firstTime = DateTime.Now;
            var ci = new CultureInfo("en-US");
            while (parser.HasMoreRows)
            {
                var line = parser.ReadNextRow();
                if (line == null)
                    break;

                // The first two lines are headers.
                ++lineNumber;
                if (lineNumber < 3)
                    continue;
                else if (lineNumber == 3)
                    firstTime = DateTime.Parse(line[0]);

                DateTime dt = DateTime.Parse(line[0]);
                var diff = dt.Subtract(firstTime);

                yield return new Waypoint
                {
                    altitude = line[3],
                    latitude = line[1],
                    longitude = line[2],
                    time = (((double)diff.TotalMilliseconds) / 1000.0).ToString(ci)
                };
            }
        }

        IEnumerator<Waypoint> IEnumerable<Waypoint>.GetEnumerator()
        {
            return ge();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ge();
        }
    }
}
