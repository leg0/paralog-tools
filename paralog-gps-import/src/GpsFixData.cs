using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Paralog_gps.Nmea
{
/**
 * An object of this class represents a GPGGA sentence.
 * @author lego
 */
public class GpsFixData {

    public const string PREFIX = "$GPGGA";

    /**
     *
     * @param gpgaa an NMEA 0183 GPGGA sencence.
     * @throws java.util.NoSuchElementException if the given sentence does not
     *  contain enough tokens to compose GPGGA sencence.
     * @throws cx.ath.skyflyer.gpslog.UnsupportedTypeException if the sentence
     *  anything but GPGGA.
     */
    public GpsFixData(string gpgga) 
    {
        sentence = gpgga;
        
        string[] x = gpgga.Split(new char[] {',', '*'});

        string type = x[0];
        if (!type.Equals(PREFIX))
            throw new Exception("Error");
            //throw new UnsupportedTypeException(GpsFixData.PREFIX, type);

        parse(x);
    }

    public String toString() {
        return sentence;
    }

    /**
     *
     * @return a string representation of altitude together with altitude unit.
     */
    public Distance Altitude
    {
        get { return m_Altitude; }
    }


    public string FixQuality
    {
        get { return m_FixQuality; }
    }

    public Latitude Latitude
    {
        get { return m_Latitude; }
    }

    public Longitude Longitude
    {
        get { return m_Longitude; }
    }

    public string NumberOfSatellites
    {
        get { return m_NumberOfSatellites; }
    }

    public Time Time
    {
        get { return m_Time; }
    }

    private void parse(string[] x)
    {
        // assert(parser != null);

        m_Time = new Time(x[1]);// parser.next();
        m_Latitude = new Latitude(x[2], x[3]);// parser.next() + parser.next();
        m_Longitude = new Longitude(x[4], x[5]);// parser.next() + parser.next();
        m_FixQuality = x[6];// parser.next();
        m_NumberOfSatellites = x[7];// parser.next();
        //parser.next();
        m_Altitude = new Distance(x[9], x[10]);// parser.next() + parser.next();
    }

    //private Tokenizer parser;
    private string sentence;

    // $GPGGA,170834,4124.8963,N,08151.6838,W,1,05,1.5,280.2,M,-34.0,M,,,*75

    //Time  	170834  	17:08:34 UTC
    private Time m_Time;

    //Latitude 	4124.8963, N 	41d 24.8963' N or 41d 24' 54" N
    private Latitude m_Latitude;

    //Longitude 	08151.6838, W 	81d 51.6838' W or 81d 51' 41" W
    private Longitude m_Longitude;

    //Fix Quality:
    // 0 = Invalid
    // 1 = GPS fix
    // 2 = DGPS fix 	1 	Data is from a GPS fix
    private string m_FixQuality;

    //Number of Satellites 	05 	5 Satellites are in view
    private string m_NumberOfSatellites;

    //Horizontal Dilution of Precision (HDOP) 	1.5 	Relative accuracy of horizontal position
    //private String m_HorizontalDilutionOfPrecision;

    //Altitude 	280.2, M 	280.2 meters above mean sea level
    private Distance m_Altitude;

    //Height of geoid above WGS84 ellipsoid 	-34.0, M 	-34.0 meters
    //Time since last DGPS update 	blank 	No last update
    //DGPS reference station id 	blank 	No station id
    //Checksum 	*75 	Used by program to check for transmission errors}
}
}
