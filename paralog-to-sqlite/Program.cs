using CommandLine;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;

namespace paralog_to_sqlite
{
    class Options
    {
        [Option("paralog-database", DefaultValue = "paralog.pmz", Required = false, HelpText = "Paralog database file")]
        public string ParalogDatabase { get; set; }

        [Option("sqlite-database", DefaultValue = "sqlite.db", Required = false, HelpText = "SQLite3 database file")]
        public string SqliteDatabase { get; set; }
    }
    class Program
    {
        static void exec(SQLiteConnection sqlite, string query)
        {
            using (var cmd = sqlite.CreateCommand())
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
        }

        static void Main(string[] args)
        {
            var opt = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, opt))
            {
                return;
            }

            Console.Write("Creating sqlite database ... ");
            var sqlite = new SQLiteConnection("Data Source=" + opt.SqliteDatabase);
            sqlite.Open();
            exec(sqlite, "create table dropzone(name, lat, lon, note)");
            exec(sqlite, "create table aircraft(type, tail, pic)");
            exec(sqlite, "create table jump(n, ts, dz_id, ac_id, type, exit, open, note)");
            exec(sqlite, "create table profile(jump_id, type, qne)");
            exec(sqlite, "create table profile_point(profile_id, t, alt, lat, lon)");
            //exec(sqlite, "create table team(name, ...)");
            //exec(sqlite, "create table equipment(name, type)");
            //exec(sqlite, "create table jump_equipment(jump_id, equipment_id)");
            Console.WriteLine("done");

            Console.Write("Loading paralog database ...");
            using (var fs = new FileStream(opt.ParalogDatabase, FileMode.Open))
            using (var gs = new GZipStream(fs, CompressionMode.Decompress))
            {
                var xdoc = XDocument.Load(gs);
                Console.WriteLine("done");

                var jumps = xdoc.Descendants("jump");
                using (var tr = sqlite.BeginTransaction())
                {
                    CopyDropzones(sqlite, tr, jumps);
                    CopyAircraft(sqlite, tr, jumps);
                    CopyJumps(sqlite, tr, jumps);
                    tr.Commit();
                }
            }
        }

        private static void CopyJumps(SQLiteConnection sqlite, SQLiteTransaction tr, IEnumerable<XElement> jumps)
        {
            Console.Write("Copying {0} jumps ", jumps.Count());
            int n = 0;
            foreach (var jump in jumps)
            {
                ++n;
                if (n % 100 == 0)
                    Console.Write(".");
                using (var cmd = sqlite.CreateCommand())
                {
                    cmd.Transaction = tr;
                    cmd.CommandText = 
                        "insert into jump values(@n, @ts, " +
                            "(select rowid from dropzone where name=@dz), " +
                            "(select rowid from aircraft where type=@ac), " +
                            "@type, @exit, @open, @note)";
                    cmd.Prepare();
                    var p = cmd.Parameters;
                    p.AddWithValue("@n", int.Parse(jump.Attribute("n").Value));
                    p.AddWithValue("@ts", jump.Attribute("ts").Value);
                    var dz = jump.Element("dz");
                    p.AddWithValue("@dz", dz != null ? dz.Value : null);
                    p.AddWithValue("@ac", jump.Element("ac").Value);
                    var type = jump.Element("type");
                    p.AddWithValue("@type", type != null ? type.Value : null);
                    p.AddWithValue("@exit", int.Parse(jump.Element("exit").Value));
                    p.AddWithValue("@open", int.Parse(jump.Element("open").Value));
                    var note = jump.Element("note");
                    p.AddWithValue("@note", note != null ? note.Value : null);
                    cmd.ExecuteNonQuery();
                }
                var jumpId = sqlite.LastInsertRowId;
                var profile = jump.Element("profile");
                if (profile != null)
                {
                    CopyJumpProfile(sqlite, tr, jumpId, profile);
                }
            }
            Console.WriteLine("done");
        }

        private static void CopyJumpProfile(SQLiteConnection sqlite, SQLiteTransaction tr, long jumpId, XElement profile)
        {
            using (var cmd = sqlite.CreateCommand())
            {
                cmd.Transaction = tr;
                cmd.CommandText = "insert into profile values(@jumpId, @type, @qne)";
                cmd.Prepare();
                var p = cmd.Parameters;
                p.AddWithValue("@jumpId", jumpId);
                p.AddWithValue("@type", profile.Attribute("type").Value);
                p.AddWithValue("@qne", profile.Element("qne").Value);
                cmd.ExecuteNonQuery();

            }
            var numberFormat = new CultureInfo("en-US").NumberFormat; // floating point numbers are in en-US
            var profileId = sqlite.LastInsertRowId;
            foreach (var profilePoint in profile.Elements("waypoints").Elements("wpt"))
            {
                using (var cmd = sqlite.CreateCommand())
                {
                    cmd.Transaction = tr;
                    cmd.CommandText = "insert into profile_point values(@profileId, @t, @alt, null, null)";
                    cmd.Prepare();
                    var p = cmd.Parameters;
                    p.AddWithValue("@profileId", profileId);
                    p.AddWithValue("@t", double.Parse(profilePoint.Attribute("t").Value, numberFormat));
                    p.AddWithValue("@alt", double.Parse(profilePoint.Attribute("a").Value, numberFormat));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void CopyAircraft(SQLiteConnection sqlite, SQLiteTransaction tr, IEnumerable<XElement> jumps)
        {
            Console.Write("Copying aircraft ...");
            var acs =
                from el in jumps
                group el by new { name = el.Element("ac") == null ? "N/A" : el.Element("ac").Value } into g
                select g;
            foreach (var ac in acs)
            {
                using (var cmd = sqlite.CreateCommand())
                {
                    cmd.Transaction = tr;
                    cmd.CommandText = "insert into aircraft values(@type, null, null)";
                    cmd.Prepare();
                    cmd.Parameters.AddWithValue("@type", ac.Key.name);
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("done");
        }

        private static void CopyDropzones(SQLiteConnection sqlite, SQLiteTransaction tr, IEnumerable<XElement> jumps)
        {
            Console.Write("Copying dropzones ... ");
            var dzs =
                from el in jumps
                group el by new { name = el.Element("dz") == null ? "N/A" : el.Element("dz").Value } into g
                select g;

            foreach (var dz in dzs)
            {
                using (var cmd = sqlite.CreateCommand())
                {
                    cmd.Transaction = tr;
                    cmd.CommandText = "insert into dropzone values(@name, null, null, null)";
                    cmd.Prepare();
                    cmd.Parameters.AddWithValue("name", dz.Key.name);
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("done");
        }
    }
}
