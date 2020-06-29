using CommandLine;
using CommandLine.Text;
using System;
using System.IO;
using Paralog_tools;

namespace Paralog_gps
{
    class Options
    {
        [Option('d', "database", Required = true, HelpText = "Paralog database file.")]
        public string Database { get; set; }

        [Option('t', "type", Required = false, Default = "flysight", HelpText = "Type of input file.")]
        public string Type { get; set; }

        [Option('i', "input-file", Required = true, HelpText = "Name of input file.")]
        public string InputFile { get; set; }

        [Option('j', "jump-number", Required = true, HelpText = "Number of jump to modify/create.")]
        public int JumpNumber { get; set; }

        [Option('c', "create", Required = false, Default = false, HelpText = "Create a record for jump if it doesn't exist.")]
        public bool ShouldCreateJump { get; set; }

        [Option("dropzone", Required = false, HelpText = "Dropzone. Used when new jump is created, otherwise ignored.")]
        public string DropZone { get; set; }

        [Option("aircraft", Required = false, HelpText = "Aircraft name. Used when new jump is created, otherwise ignored.")]
        public string Aircraft { get; set; }
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
            var inputFile = opt.InputFile;
            var jumpNumber = opt.JumpNumber;

            Logbook logbook = null;
            try
            {
                // load file
                Console.WriteLine("Loading {0} ...", xmlfile);
                logbook = Logbook.LoadFromFile(xmlfile);

                // backup
                Console.WriteLine("Backing up {0} to {0}.bak ...", xmlfile, xmlfile);
                File.Copy(xmlfile, xmlfile + ".bak");

                // find jump
                Console.Write("Looking for jump {0} ... ", jumpNumber);

                var jump = logbook.FindJump(jumpNumber);
                if (jump == null)
                {
                    Console.WriteLine("not found");
                    if (opt.ShouldCreateJump)
                    {
                        Console.Write("Creating record for jump {0} ...", jumpNumber);
                        jump = logbook.CreateJump(jumpNumber, opt.DropZone, opt.Aircraft);
                        if (jump != null)
                        {
                            Console.WriteLine("done");
                        }
                        else
                        {
                            Console.WriteLine("failed");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Note: To create a new jump, use --create command line switch.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("found");
                }

                if (!jump.CreateProfileFromFile(inputFile, opt.Type))
                {
                    Console.WriteLine("Failed to import '{0}' from file '{1}'.", opt.Type, inputFile);
                    return;
                }

                logbook.SaveFile(xmlfile);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Input file '{0}' not found.", ex.FileName);
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
