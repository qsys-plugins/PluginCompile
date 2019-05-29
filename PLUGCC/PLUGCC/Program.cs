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
      "^(\t|\x20*)--\\[\\[\\s*\\#(include|encode) \\\"([^\\\"]*)\\\"\\s*\\]\\]",
              RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant
            | RegexOptions.Compiled
            );

        static bool _addIncludeComments = false;
        static bool _isDebug = false;
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Compile...");
            string workspace = args[0];
            string mainFile = args[1];
            if (args.Length == 3 && args[2] == "true")
            {
                _addIncludeComments = true;
            }
            if (args.Length == 4 && args[3] == "debug")
            {
                _isDebug = true;
            }
            string folder = Path.GetDirectoryName(mainFile);
            string qplugPath = Path.Combine(folder, workspace + ".qplug");
            Console.WriteLine(mainFile);
            string code = System.IO.File.ReadAllText(mainFile);

            string result = ParseIncludes(code, folder, "");
            Console.WriteLine("Writing plugin: {0}", qplugPath);
            System.IO.File.WriteAllText(qplugPath, result);
            //Console.WriteLine(result);

        }

        static string ParseIncludes(string code, string codeFolder, string lastWhitespace, string recursivePath = "")
        {
            string result = regex.Replace(code, new MatchEvaluator(delegate (Match match)
            {
                if (_isDebug)
                {
                    System.Diagnostics.Debugger.Launch();
                }
                string whitespace = lastWhitespace + (match.Groups[1].Value ?? "");
                string actionType = match.Groups[2].Value;
                string includeFile = match.Groups[3].Value;
                Console.WriteLine("Found {0} {1}", actionType, includeFile);
                includeFile = Path.Combine(codeFolder, includeFile);
                Console.WriteLine("Searching for: {0}", includeFile);
                if (actionType == "encode")
                {
                    return match.Result(ConvertFileBase64(includeFile));
                }
                else
                {
                    if (File.Exists(includeFile))
                    {
                        if (!recursivePath.Contains(includeFile))
                        {
                            string includeCode = System.IO.File.ReadAllText(includeFile);
                            Console.WriteLine("Inserting include: {0}", includeFile);
                            string parsedIncludeCode = ParseIncludes(includeCode, codeFolder, whitespace.Replace("\r", "").Replace("\n",""), recursivePath + " >> " + includeFile);
                            if (_addIncludeComments)
                            {
                                parsedIncludeCode = "\r\n--[[ BEGIN INCLUDE " + includeFile + " ]]\r\n" + parsedIncludeCode + "\r\n--[[ END INCLUDE " + includeFile + " ]]\r\n";
                            }
                            // add existing tabs in
                            StringBuilder final = new StringBuilder();
                            using (StringReader sr = new StringReader(parsedIncludeCode))
                            {
                                while (true)
                                {
                                    string current = sr.ReadLine();
                                    if (current != null)
                                    {
                                        final.AppendLine(whitespace + current);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            return (match.Result(final.ToString()));
                        }
                        else
                        {
                            return (match.Result("ERROR - RECURSIVE INCLUDE: " + recursivePath + " >> " + includeFile));
                        }
                    }
                    else
                    {
                        return (match.Result("ERROR - NOT FOUND:: " + includeFile));
                    }
                }
            }));

            return result;
        }

        static string ConvertFileBase64(string filePath)
        {
            string result = "";
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    Byte[] bytes = File.ReadAllBytes(filePath);
                    result = Convert.ToBase64String(bytes);
                }
                else
                {
                    result = string.Format("File {0} does not exist", filePath);
                }

            }
            catch (Exception EX)
            {
                result = string.Format("Could not read {0} because of {1}", filePath, EX.Message);
            }
            return result;
        }
    }
}
