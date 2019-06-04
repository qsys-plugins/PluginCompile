using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography;

namespace PLUGCC
{
    class Program
    {
        public static Regex includeRegex = new Regex(
      "^(\t|\x20*)--\\[\\[\\s*\\#include \\\"([^\\\"]*)\\\"\\s*\\]\\](\r?\n?|$)",
              RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant
            | RegexOptions.Compiled
            );
        public static Regex encodeRegex = new Regex(
      "--\\[\\[\\s*\\#encode \\\"([^\\\"]*)\\\"\\s*\\]\\]",
              RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant
            | RegexOptions.Compiled
            );
        static bool _addIncludeComments = false;
        static bool _doSignPlugin = false;
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Compile...");
            string workspace = args[0];
            string mainFile = args[1];
            if (args.Length > 3 && args[2] == "sign")
            {
                _doSignPlugin = true;
                Console.WriteLine("SignPlugin is true");
            }
            if (args.Length > 4 && args[3] == "verbose")
            {
                _addIncludeComments = true;
                Console.WriteLine("Output Verbose is true");
            }
            if (args.Length == 5 && args[4] == "debug")
            {

                System.Diagnostics.Debugger.Launch();
                Console.WriteLine("Debugger is enabled");
            }
            string folder = Path.GetDirectoryName(mainFile);
            string qplugPath = Path.Combine(folder, workspace + ".qplug");
            Console.WriteLine(mainFile);
            string code = System.IO.File.ReadAllText(mainFile);

            // parse includes
            string result = ParseIncludes(code, folder, "");
            // parse encodes
            result = ParseEncodes(result, folder);
            Console.WriteLine("Writing plugin: {0}", qplugPath);
            if (_doSignPlugin)
            {
                result = SignAndAppend(result);
            }
            System.IO.File.WriteAllText(qplugPath, result);
        }

        static string ParseEncodes(string code, string codeFolder)
        {
            string result = encodeRegex.Replace(code, new MatchEvaluator(delegate (Match match)
            {
                string includeFile = match.Groups[1].Value;
                includeFile = Path.Combine(codeFolder, includeFile);
                return match.Result(ConvertFileBase64(includeFile));
            }));
            return result;
        }

        static string ParseIncludes(string code, string codeFolder, string lastWhitespace, string recursivePath = "")
        {
            string result = includeRegex.Replace(code, new MatchEvaluator(delegate (Match match)
            {
                string whitespace = lastWhitespace + (match.Groups[1].Value ?? "");
                string includeFile = match.Groups[2].Value;
                includeFile = Path.Combine(codeFolder, includeFile);

                if (File.Exists(includeFile))
                {
                    if (!recursivePath.Contains(includeFile))
                    {
                        string includeCode = System.IO.File.ReadAllText(includeFile);
                        Console.WriteLine("Including {0}", includeFile);
                        string parsedIncludeCode = ParseIncludes(includeCode, codeFolder, whitespace.Replace("\r", "").Replace("\n", ""), recursivePath + " >> " + includeFile);
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
                        return match.Result(final.ToString());
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


        static string SignAndAppend(string code)
        {
            string result = "";
            //byte[] encryptedData;
            byte[] dataToEncrypt = Encoding.UTF32.GetBytes(code);
            SHA256 s = SHA256Managed.Create();

            using (RSACryptoServiceProvider RSA1 = new RSACryptoServiceProvider())
            {

                UTF8Encoding ByteConverter = new UTF8Encoding();

                string path = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                var directory = System.IO.Path.GetDirectoryName(new Uri(path).LocalPath);

                // when you want to create new Public and Private keys
                //System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "QPLUG_Private_key.xml"),RSA1.ToXmlString(true));
                //System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "QPLUG_Public_key.xml"), RSA1.ToXmlString(false));

                // load the private key
                RSA1.FromXmlString(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "QPLUG_Private_key.xml")));
                // generate the signed plugin hash
                var eSign = RSA1.SignData(dataToEncrypt, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                Console.WriteLine("Signing Plugin with QSC key...");
                // append signed hash to the plugin code
                StringBuilder sb = new StringBuilder(code);
                sb.AppendLine("--[[--BEGIN CERTIFIED SIGNATURE-");
                string signature = Convert.ToBase64String(eSign);
                // break into pretty chunks (32 characters per line)
                foreach (var chunk in ChunkString(signature, 32))
                    sb.AppendLine(chunk);
                sb.AppendLine("------END CERTIFIED SIGNATURE-]]");
                result = sb.ToString();
            }

            return result;

        }

        static IEnumerable<string> ChunkString(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }

        public static void VerifyData(string keys, byte[] data, byte[] signature)
        {
            System.Diagnostics.Debug.WriteLine("######");

            using (RSACryptoServiceProvider RSA2 = new RSACryptoServiceProvider())
            {
                RSA2.FromXmlString(keys);
                var dec2 = RSA2.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                System.Diagnostics.Debug.WriteLine($"ENC3 ISVALID: {dec2}");

            }
        }
    }
}