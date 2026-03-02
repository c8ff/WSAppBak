using System;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace WSAppBak
{
    internal class WSAppBak
    {
        private string AppName = "Windows Store App Backup";

        private string AppCreator = "Kiran Murmu";

        private string AppCurrentDirctory = Directory.GetCurrentDirectory();

        private string WSAppXmlFile = "AppxManifest.xml";

        private bool Checking = true;

        private string WSAppName;

        private string WSAppPath;

        private string WSAppVersion;

        private string WSAppFileName;

        private string WSAppOutputPath;

        private string WSAppProcessorArchitecture;

        private string WSAppPublisher;

        private string RunProcess(string fileName, string args)
        {
            string result = "";
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            while (!process.StandardOutput.EndOfStream)
            {
                string text = process.StandardOutput.ReadLine();
                Console.WriteLine(text);
                if (text.Length > 0)
                {
                    result = text;
                }
            }
            return result;
        }

        public void ReadArg(string[] args)
        {
            var keepCerts = false;
            foreach (var s in args)
            {
                if (s.Contains("help"))
                {
                    Console.WriteLine("Usage: WsAppBak.exe <app-path> (output-path, default=./output) (--keep-certs)");
                    return;
                }
                else if (s.Contains("--keep-certs")) {
                    keepCerts = true;
                }
            }

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: WsAppBak.exe <app-path> <output-path, default=./output>");
                return;
            }

            /// Console.Clear();
            Console.WriteLine("\t\t'{0}' by {1}", AppName, AppCreator);
            Console.WriteLine("================================================================================");

            WSAppPath = args[0];
            if (WSAppPath.Contains("\""))
            {
                WSAppPath = WSAppPath.Replace("\"", "");
                WSAppPath = "\"" + WSAppPath + "\"";
            }
            if (!File.Exists(WSAppPath + "\\" + WSAppXmlFile))
            {
                Console.WriteLine("Xml wasn't found in the specified app path.");
                return;
            }

            WSAppOutputPath = args.Length >= 2 ? args[1] : "./output";
            if (WSAppOutputPath.Contains("\""))
            {
                WSAppOutputPath = WSAppOutputPath.Replace("\"", "");
                WSAppOutputPath = "\"" + WSAppOutputPath + "\"";
            }

            if (!Directory.Exists(WSAppOutputPath))
            {
                Console.WriteLine("Output path doesn't exist, creating " + WSAppOutputPath + ".");
                Directory.CreateDirectory(WSAppOutputPath);
            }

            WSAppFileName = Path.GetFileName(WSAppPath);
            using (XmlReader xmlReader = XmlReader.Create(WSAppPath + "\\" + WSAppXmlFile))
            {
                while (xmlReader.Read())
                {
                    if (xmlReader.IsStartElement() && xmlReader.Name == "Identity")
                    {
                        string text = xmlReader["Name"];
                        if (text != null)
                        {
                            WSAppName = text;
                        }
                        string text2 = xmlReader["Publisher"];
                        if (text2 != null)
                        {
                            WSAppPublisher = text2;
                        }
                        string text3 = xmlReader["Version"];
                        if (text3 != null)
                        {
                            WSAppVersion = text3;
                        }
                        string text4 = xmlReader["ProcessorArchitecture"];
                        if (text4 != null)
                        {
                            WSAppProcessorArchitecture = text4;
                        }
                    }
                }
            }

            MakeAppx(keepCerts);
        }

        private void MakeAppx(bool keepCerts)
        {
            // Make appx
            string fileName = AppCurrentDirctory + "\\WSAppBak\\MakeAppx.exe";
            string appxPath = Path.GetFullPath(WSAppOutputPath + "\\" + WSAppFileName + ".appx");
            string pvkPath = Path.GetFullPath(WSAppOutputPath + "\\" + WSAppFileName + ".pvk");
            string cerPath = Path.GetFullPath(WSAppOutputPath + "\\" + WSAppFileName + ".cer");
            string pfxPath = Path.GetFullPath(WSAppOutputPath + "\\" + WSAppFileName + ".pfx");
            string runArgs = "pack -d \"" + WSAppPath + "\" -p \"" + appxPath + "\" -l";
            if (!File.Exists(fileName))
            {
                Checking = false;
                Console.WriteLine("\nCan't create '.appx' file, 'MakeAppx.exe' file not found!");
                Console.Write("Press any Key to exit...");
                Console.ReadKey();
                return;
            }

            if (File.Exists(appxPath))
            {
                Console.WriteLine("Deleting already existing appx.");
                File.Delete(appxPath);
            }

            Console.WriteLine("\nPlease wait.. Creating '.appx' package file.\n");
            if (!RunProcess(fileName, runArgs).ToLower().Contains("succeeded"))
            {
                Checking = false;
                Console.Write("Package '{0}' creation failed... press any Key to exit.", appxPath);
                Console.ReadKey();
                return;
            }

            // Making certificates.

            Console.WriteLine("Package '{0}' creation succeeded.", appxPath);

            fileName = AppCurrentDirctory + "\\WSAppBak\\MakeCert.exe";
            runArgs = "-n \"" + WSAppPublisher + "\" -r -a sha256 -len 2048 -cy end -h 0 -eku 1.3.6.1.5.5.7.3.3 -b 01/01/2000 -sv \"" + pvkPath + "\" \"" + cerPath + "\"";
            if (!File.Exists(fileName))
            {
                Checking = false;
                Console.WriteLine("\n'MakeCert.exe' not found. Cannot create certificate for the package.");
                Console.Write("Press any Key to exit...");
                Console.ReadKey();
                return;
            }

            var createCerts = true;
            var pvkExists = File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".pvk");
            var cerExists = File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".cer");

            if (keepCerts && pvkExists && cerExists)
            {
                Console.WriteLine("certificates exists, skipping re-creating them.");
                createCerts = false;
            }
            else if (pvkExists)
            {
                Console.WriteLine("deleting pvk certificate");
                File.Delete(pvkPath);
            }
            if (createCerts && cerExists)
            {
                Console.WriteLine("deleting cer certificate");
                File.Delete(cerPath);
            }

            if (createCerts) {
                Console.WriteLine("\nPlease wait.. Creating certificate for the package.\n");
                Console.Write("Certificate creation: ");
                if (!RunProcess(fileName, runArgs).ToLower().Contains("succeeded"))
                {
                    Checking = false;
                    Console.WriteLine("\nFailed to create Certificate for the package... Prees any Key exit.");
                    Console.ReadKey();
                    return;
                }
            }

            // Convert Pvk into Pfx.
            fileName = AppCurrentDirctory + "\\WSAppBak\\Pvk2Pfx.exe";
            runArgs = "-pvk \"" + pvkPath + "\" -spc \"" + cerPath + "\" -pfx \"" + pfxPath + "\"";
            if (!File.Exists(fileName))
            {
                Checking = false;
                Console.WriteLine("\nCan't convert Certificate to sign the package, 'Pvk2Pfx.exe' file not found!");
                Console.Write("Press any Key to exit...");
                Console.ReadKey();
                return;
            }
            if (File.Exists(pfxPath))
            {
                File.Delete(pfxPath);
            }
            Console.WriteLine("\nPlease wait.. Converting certificate to sign the package.\n");
            Console.Write("Certificate convertion: ");
            if (RunProcess(fileName, runArgs).Length != 0)
            {
                Checking = false;
                Console.WriteLine("\nCan't convert certificate to sign the package... Prees any Key exit...");
                Console.ReadKey();
                return;
            }
            Console.Write("succeeded");

            // Sign application

            fileName = AppCurrentDirctory + "\\WSAppBak\\SignTool.exe";
            runArgs = "sign -fd SHA256 -a -f \"" + pfxPath + "\" \"" + appxPath + "\"";
            if (!File.Exists(fileName))
            {
                Checking = false;
                Console.WriteLine("\nCan't Sign the package, 'SignTool.exe' file not found!");
                Console.Write("Press any Key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("\n\nPlease wait.. Signing the package, this may take some minutes.\n");
            if (!RunProcess(fileName, runArgs).ToLower().Contains("successfully signed"))
            {
                Checking = false;
                Console.WriteLine("\nCan't Sign the package, Press any Key to exit...");
                Console.ReadKey();
                return;
            }
            Checking = false;
            Console.WriteLine("Package signing succeeded. Please install the '.cer' file to [Local Computer\\Trusted Root Certification Authorities] before install the App Package or use 'WSAppPkgIns.exe' file to install the App Package!");

            Console.Write("\n\nWould you like to install the certificate? (y/n): ");

            bool installCertificate;
            while (true) {
                var result = Convert.ToString(Console.ReadLine()).ToLower();
                if (result.Equals("y") || result.Equals("n")) {
                    installCertificate = result.Equals("y");
                    break;
                }
            }


            if (installCertificate) {
                var argspw = "Import-Certificate -FilePath \"" + cerPath + "\" -CertStoreLocation Cert:\\LocalMachine\\Root";
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = argspw,
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                };
                process.Start();
                process.WaitForExit();

                var result = process.ExitCode;
                if (result == 0)
                {
                    Console.WriteLine("Successfully installed certificate.");
                }
                else {
                    Console.WriteLine("Coudln't install certificate. Checka eso bro.");
                    return;
                }
            }

            Console.Write("Would you like to install package now with Add-AppxPackage? (y/n): ");

            bool installPackage;
            while (true)
            {
                var result = Convert.ToString(Console.ReadLine()).ToLower();
                if (result.Equals("y") || result.Equals("n"))
                {
                    installPackage = result.Equals("y");
                    break;
                }
            }

            if (installPackage)
            {
                var argspw = "Add-AppxPackage \"" + appxPath + "\"";
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = argspw,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                var first = true;
                while (!process.StandardError.EndOfStream)
                {
                    if (first) {
                        first = false;
                        Console.WriteLine("\n");
                    }
                    string text = process.StandardError.ReadLine();
                    Console.Error.WriteLine(text);
                }

                process.WaitForExit();
                var result = process.ExitCode;
                if (result == 0)
                {
                    Console.WriteLine("Successfully installed package.");
                } else {
                    Console.WriteLine("\n\nUnable to install package. Check the error message for more information.");
                }
            }
        }
    }
}