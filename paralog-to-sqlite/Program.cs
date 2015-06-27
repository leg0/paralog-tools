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
using CommandLine.Text;
using System.Diagnostics;

namespace paralog_to_sqlite
{
    class Options
    {
        [Option('i', "paralog-database", DefaultValue = "paralog.pmz", Required = false, HelpText = "Paralog database file")]
        public string ParalogDatabase { get; set; }

        [Option('o', "sqlite-database", DefaultValue = "sqlite.db", Required = false, HelpText = "SQLite3 database file")]
        public string SqliteDatabase { get; set; }

        [Option('f', "force-overwrite", DefaultValue = false, Required = false, HelpText = "Force overwriting of sqlite database")]
        public bool ForceOverwrite { get; set; }

        [Option('u', "update", DefaultValue = false, Required = false, HelpText = "Update sqlite database, new data only")]
        public bool Update { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText c) => HelpText.DefaultParsingErrorsHandler(this, c));
        }
    }
    class Program
    {
        Options opt;
        SQLiteConnection sqlite;

        Program(Options opt)
        {
            this.opt = opt;
            if (!File.Exists(opt.ParalogDatabase))
            {
                Console.WriteLine("Paralog database {0} does not exist.", opt.ParalogDatabase);
                throw new Exception();
            }

            if (opt.Update)
            {
                if (!File.Exists(opt.SqliteDatabase))
                {
                    Console.WriteLine("Sqlite database {0} missing.", opt.SqliteDatabase);
                    opt.Update = false;
                }
            }
            else if (opt.ForceOverwrite)
            {
                File.Delete(opt.SqliteDatabase + ".bak");
                File.Move(opt.SqliteDatabase, opt.SqliteDatabase + ".bak");
            }
            else if (File.Exists(opt.SqliteDatabase))
            {
                Console.WriteLine("Sqlite database {0} already exists.", opt.SqliteDatabase);
                throw new Exception();
            }

            sqlite = new SQLiteConnection("Data Source=" + opt.SqliteDatabase);
            sqlite.Open();
        }

        void ExecuteNonQuery(string query)
        {
            var cmd = sqlite.CreateCommand();
            cmd.CommandText = query;
            cmd.ExecuteNonQuery();
        }

        object ExecuteScalar(string query)
        {
            var cmd = sqlite.CreateCommand();
            cmd.CommandText = query;
            return cmd.ExecuteScalar();
        }

        static void Main(string[] args)
        {
            var opt = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, opt))
            {
                return;
            }

            var p = new Program(opt);
            p.Run();
        }

        void CreateTables()
        {
            Console.Write("Creating sqlite database ... ");
            ExecuteNonQuery("create table dropzone(name unique, lat, lon, note)");
            ExecuteNonQuery("create table aircraft(type unique, tail , pic)");
            ExecuteNonQuery("create table jump(n, ts, dz_id, ac_id, type, exit, open, delay, vmax, vavg, note)");
            ExecuteNonQuery("create table profile(jump_id, type, qne)");
            ExecuteNonQuery("create table profile_point(profile_id, t, alt, lat, lon)");
            //exec(sqlite, "create table team(name, ...)");
            //exec(sqlite, "create table equipment(name, type)");
            //exec(sqlite, "create table jump_equipment(jump_id, equipment_id)");
            Console.WriteLine("done");
        }

        static XDocument LoadParalogDatabase(string paralogDatabase)
        {
            Console.Write("Loading paralog database ...");
            using (var fs = new FileStream(paralogDatabase, FileMode.Open))
            using (var gs = new GZipStream(fs, CompressionMode.Decompress))
            {
                var xdoc = XDocument.Load(gs);
                Console.WriteLine("done");
                return xdoc;
            }
        }

        void Run()
        {
            var minJumpToCopy = 1L;
            if (!opt.Update)
            {
                CreateTables();
            }
            else
            {
                minJumpToCopy = (long)ExecuteScalar("select coalesce(max(n), 0)+1 from jump");
            }

            var xdoc = LoadParalogDatabase(opt.ParalogDatabase);
            var jumps = from j in xdoc.Descendants("jump")
                        where int.Parse(j.Attribute("n").Value) >= minJumpToCopy
                        select j;
            using (var tr = sqlite.BeginTransaction())
            {
                CopyDropzones(tr, jumps);
                CopyAircraft(tr, jumps);
                CopyJumps(tr, jumps);
                tr.Commit();
            }
        }

        static void AddWithIntValue(SQLiteParameterCollection p, string paramName, XAttribute el)
        {
            if (el == null)
                p.AddWithValue(paramName, null);
            else
                p.AddWithValue(paramName, int.Parse(el.Value));
        }

        static void AddWithIntValue(SQLiteParameterCollection p, string paramName, XElement el)
        {
            if (el == null)
                p.AddWithValue(paramName, null);
            else
                p.AddWithValue(paramName, int.Parse(el.Value));
        }

        static CultureInfo en_us = new CultureInfo("en-US");

        static void AddWithDoubleValue(SQLiteParameterCollection p, string paramName, XElement el)
        {
            if (el == null)
                p.AddWithValue(paramName, null);
            else
                p.AddWithValue(paramName, double.Parse(el.Value, en_us));
        }

        static void AddWithStringValue(SQLiteParameterCollection p, string paramName, XElement el)
        {
            p.AddWithValue(paramName, el == null ? null : el.Value);
        }

        void CopyJumps(SQLiteTransaction tr, IEnumerable<XElement> jumps)
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
                            "@type, @exit, @open, @delay, @vmax, @vavg, @note)";
                    cmd.Prepare();
                    var p = cmd.Parameters;
                    AddWithIntValue(p, "@n", jump.Attribute("n"));
                    var ts = jump.Attribute("ts").Value;
                    if (!ts.EndsWith("Z")) ts += ":00";
                    p.AddWithValue("@ts", ts);
                    AddWithStringValue(p, "@dz", jump.Element("dz"));
                    AddWithStringValue(p, "@ac", jump.Element("ac"));
                    AddWithStringValue(p, "@type", jump.Element("type"));
                    AddWithIntValue(p, "@exit", jump.Element("exit"));
                    AddWithIntValue(p, "@open", jump.Element("open"));
                    AddWithIntValue(p, "@delay", jump.Element("ffTime"));
                    AddWithDoubleValue(p, "@vmax", jump.Element("vMax"));
                    AddWithDoubleValue(p, "@vavg", jump.Element("vAvg"));
                    AddWithStringValue(p, "@note", jump.Element("note"));
                    cmd.ExecuteNonQuery();
                }
                var jumpId = sqlite.LastInsertRowId;
                var profile = jump.Element("profile");
                if (profile != null)
                {
                    CopyJumpProfile(tr, jumpId, profile);
                }
            }
            Console.WriteLine("done");
        }

        void CopyJumpProfile(SQLiteTransaction tr, long jumpId, XElement profile)
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

        void CopyAircraft(SQLiteTransaction tr, IEnumerable<XElement> jumps)
        {
            Console.Write("Copying aircraft ...");
            var acs =
                from el in jumps
                group el by new { name = el.Element("ac") == null ? "N/A" : el.Element("ac").Value } into g
                select g;
            foreach (var ac in acs)
            {
                try
                {
                    var cmd = sqlite.CreateCommand();
                    cmd.Transaction = tr;
                    cmd.CommandText = "insert into aircraft values(@type, null, null)";
                    cmd.Prepare();
                    cmd.Parameters.AddWithValue("@type", ac.Key.name);
                    cmd.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    // Probably unique constraint violation.
                    Debug.WriteLine("{0}", e);
                }
            }
            Console.WriteLine("done");
        }

        void CopyDropzones(SQLiteTransaction tr, IEnumerable<XElement> jumps)
        {
            Console.Write("Copying dropzones ... ");
            var dzs =
                from el in jumps
                group el by new { name = el.Element("dz") == null ? "N/A" : el.Element("dz").Value } into g
                select g;

            foreach (var dz in dzs)
            {
                try
                {
                    var cmd = sqlite.CreateCommand();
                    cmd.Transaction = tr;
                    cmd.CommandText = "insert into dropzone values(@name, null, null, null)";
                    cmd.Prepare();
                    cmd.Parameters.AddWithValue("name", dz.Key.name);
                    cmd.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    // Probably about violating unique constraint, which is ok.
                    // Otherwise, .. whatever.
                    Debug.WriteLine("{0}", e);
                }
            }
            Console.WriteLine("done");
        }
    }
}
