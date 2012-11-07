using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;

namespace Paralog_gps.Nmea
{
    public class GeographicPosition
    {
        public const string PREFIX = "$GPGLL";

        public GeographicPosition(string gpgll)
        {
            this.sentence = gpgll;
            string[] x = sentence.Split(',');

            if (x.Length < 5)
                throw new Exception("Error");

            if (!x[0].Equals(PREFIX))
                throw new Exception("Error");

            latitude = new Latitude(x[1], x[2]);
            longitude = new Longitude(x[3], x[4]);

            if (x.Length > 5)
                time = new Time(x[5]);

        }

        public Latitude Latitude 
        {
            get { return latitude; }
        }

        public Longitude Longitude
        {
            get { return longitude; }
        }

        public Time Time
        {
            get { return time; }
        }

        private string sentence;
        private Latitude latitude;
        private Longitude longitude;
        private Time time;
    }

    public enum NS
    {
        North, South 
    }

    public enum EW
    {
        East, West
    }

    public class Latitude
    {
        public Latitude(string value, string ns)
        {
            m_Value = double.Parse(value, new CultureInfo("en-US"));
            if (ns.Equals("S"))
                m_Value *= -1.0;
        }

        public Latitude(string value, NS ns)
        {
            m_Value = double.Parse(value, new CultureInfo("en-US"));
            if (ns == NS.South)
                m_Value *= -1.0;
        }

        public double Value
        {
            get { return m_Value; }
        }

        public double Degrees
        {
            get
            {
                double deg = Math.Truncate(m_Value / 100.0);
                double min = m_Value - deg * 100;
                deg += min/60;
                return deg;
            }
        }
        private double m_Value;
    }

    public class Longitude
    {
        public Longitude(string value, string ew)
        {
            m_Value = double.Parse(value, new CultureInfo("en-US"));
            if (ew.Equals("W"))
                m_Value *= -1.0;
        }

        public Longitude(string value, EW ew)
        {
            m_Value = double.Parse(value, new CultureInfo("en-US"));
            if (ew == EW.West)
                m_Value *= -1.0;
        }

        public double Value
        {
            get { return m_Value; }
        }

        public double Degrees
        {
            get 
            {
                double deg = Math.Truncate(m_Value / 100.0);
                double min = m_Value - deg * 100;
                deg += min / 60;
                return deg;
            }
        }

        private double m_Value;
    }

    [DebuggerDisplay("{Hours}:{Minutes}:{Seconds} | {Value}")]
    public class Time
    {
        public Time(string t)
        {
            m_Time = double.Parse(t, new CultureInfo("en-US"));
        }

        public double Value
        {
            get { return m_Time; }
        }

        public int Hours
        {
            get
            {
                return (int)Math.Truncate(m_Time / 10000.0);
            }
        }

        public int Minutes
        {
            get
            {
                double tmp = m_Time - 10000.0 * Hours;
                return (int)Math.Truncate(tmp/100.0);
            }
        }

        public double Seconds
        {
            get
            {
                double tmp = m_Time - 10000.0 * Hours - 100.0 * Minutes;
                return tmp;
            }
        }

        public static double operator -(Time a, Time b)
        {
            double aa = 3600.0 * a.Hours + 60.0 * a.Minutes + a.Seconds;
            double bb = 3600.0 * b.Hours + 60.0 * b.Minutes + b.Seconds;

            return aa - bb;
        }

        private double m_Time;
    }

    public enum SpeedUnit
    {
        km_h, mph
    }

    public enum DistanceUnit
    {
        meter, foot, mile, kilometer
    }

    public class Velocity { }

    public class Distance {
        public Distance(string value, string unit)
        {
            m_Value = double.Parse(value, new CultureInfo("en-US"));
        }

        public double Value
        {
            get { return m_Value; }
        }

        private double m_Value;

    }

}
