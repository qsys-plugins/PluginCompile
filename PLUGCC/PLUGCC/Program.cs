using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace PLUGCC
{
    class Program
    {
        public static Regex regex = new Regex(
      "--\\[\\[\\s*\\#include \\\"([^\\\"]*)\\\"\\s*\\]\\]",
              RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant
            | RegexOptions.Compiled
            );

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Compile...");
            string workspace = args[0];
            string mainFile = args[1];
            string folder = Path.GetDirectoryName(mainFile);
            string qplugPath = Path.Combine(folder, workspace + ".qplug");
            Console.WriteLine(mainFile);
            string code = System.IO.File.ReadAllText(mainFile);

            string result = regex.Replace(code, new MatchEvaluator(delegate (Match match)
            {
                //System.Diagnostics.Debugger.Launch();
                string includeFile = match.Groups[1].Value;
                Console.WriteLine("Found include: {0}", includeFile);
                includeFile = Path.Combine(folder, includeFile);
                Console.WriteLine("Searching for: {0}", includeFile);
                if (File.Exists(includeFile))
                {
                    string includeCode = System.IO.File.ReadAllText(includeFile);
                    Console.WriteLine("Inserting include: {0}", includeFile);
                    //Console.WriteLine("@@@@@@@@@@");
                    //Console.WriteLine(includeCode);
                    //Console.WriteLine("@@@@@@@@@@");
                    return (match.Result(includeCode));
                }
                else
                {
                    return (match.Result("NOT FOUND:: " + includeFile));
                }
            }));
            Console.WriteLine("Writing plugin: {0}", qplugPath);
            System.IO.File.WriteAllText(qplugPath, result);
            //Console.WriteLine(result);

        }
    }
}
