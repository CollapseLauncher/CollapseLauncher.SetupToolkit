using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using WixSharp;
using WixSharp.CommonTasks;
using File = WixSharp.File;

[assembly: InternalsVisibleTo(assemblyName: "CollapseLauncher.Setup.aot")] // assembly name + '.aot suffix
namespace ColapseLauncher.Setup
{
    public static class SetupProc
    {
        const string USAGE_STRING = @"CollapseLauncher.Setup [Publish directory] [Stable/Preview] [Build WSX Only: true/false]";
        public static int Main(params string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine(USAGE_STRING);
                return -1;
            }

            if (!(args[1].Equals("Stable", StringComparison.OrdinalIgnoreCase)
               || args[1].Equals("Preview", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine(USAGE_STRING);
                return -2;
            }

            if (!bool.TryParse(args[2], out bool isWsxOnly))
            {
                Console.WriteLine(USAGE_STRING);
                return -3;
            }
            
            try
            {
                string sourceDir = args[0];
                string targetDir = Path.Combine(".\\", "app");
                string channelName = args[1];

                string sourceBinaryDir = Path.Combine(sourceDir, $"{channelName}-build");

                Version binaryVersion = GetBinaryVersion(sourceBinaryDir);
                string binaryVersionString = binaryVersion.ToVersionString();
                string targetBinaryTempDir = Path.Combine(targetDir, $"app-{binaryVersionString}");

                CopyToDir(sourceBinaryDir, targetBinaryTempDir);
                CopyTo(Path.Combine(sourceBinaryDir, "icon.ico"), Path.Combine(targetDir, "icon.ico"));
                CopyTo(Path.Combine(sourceDir, "CollapseLauncher.exe"), Path.Combine(targetDir, "CollapseLauncher.exe"));
                CopyTo(Path.Combine(sourceDir, "Update.exe"), Path.Combine(targetDir, "Update.exe"));

                string installPath = @"%ProgramFiles%\Collapse Launcher";

                ManagedProject project =
                    new ManagedProject("Collapse Launcher",
                        new Dir(installPath,
                            GetDirEntity(targetBinaryTempDir),
                            GetFileEntity(Path.Combine(targetDir, "icon.ico")),
                            GetFileEntity(Path.Combine(targetDir, "CollapseLauncher.exe")),
                            GetFileEntity(Path.Combine(targetDir, "Update.exe")),
                            new Dir($"%ProgramMenu%\\Collapse Launcher",
                                new ExeFileShortcut
                                {
                                    Name = "Collapse Launcher",
                                    Target = "[INSTALLDIR]CollapseLauncher.exe",
                                    Arguments = ""
                                },
                                new ExeFileShortcut
                                {
                                    Name = "Collapse Launcher",
                                    Target = "[INSTALLDIR]CollapseLauncher.exe",
                                    Arguments = "",
                                    AttributesDefinition = "Directory=DesktopFolder"
                                },
                                new ExeFileShortcut
                                {
                                    Name = "Uninstall Collapse Launcher",
                                    Target = "[System64Folder]msiexec.exe",
                                    Arguments = "/x [ProductCode]"
                                }
                            )))
                    // .SetAddBinaries()
                    ;

                Guid projectId = new Guid("1b671b04-ce52-4284-aaf1-1608c2761eea");
                string setupProjectId = "CollapseLauncher_" + projectId.ToString();

                project.Language = "en-us";
                project.Version = binaryVersion;
                project.Platform = Platform.x64;
                project.GUID = projectId;
                project.LicenceFile = Path.Combine(targetBinaryTempDir, "LICENSE.rtf"); // Typo on the Property Name???
                project.Id = setupProjectId;
                project.ProductId = projectId;
                project.UI = WUI.WixUI_InstallDir;
                project.PreserveTempFiles = true;
                project.PreserveDbgFiles = true;
                project.Scope = InstallScope.perMachine;

                project.ControlPanelInfo.Name = "Collapse Launcher";
                project.ControlPanelInfo.Manufacturer = "Collapse Project Team";
                project.ControlPanelInfo.Comments = "Collapse Launcher, An Advanced Launcher for miHoYo/HoYoverse Games";
                project.ControlPanelInfo.HelpLink = "https://github.com/CollapseLauncher/Collapse";
                project.ControlPanelInfo.Readme = "https://github.com/CollapseLauncher/Collapse/blob/main/README.md";
                project.ControlPanelInfo.ProductIcon = Path.Combine(targetDir, "icon.ico");
                project.ControlPanelInfo.Contact = "bagusnl+collapse@protonmail.com";
                project.BannerImage = ".\\WizardBannerDesignSmall.png";
                project.BackgroundImage = ".\\WizardBannerDesign.png";
                project.ValidateBackgroundImage = false;

                if (isWsxOnly)
                    project.BuildWxs(Compiler.OutputType.MSI, $".\\CL-{channelName}-{binaryVersionString}_InstallerScript.wsx");
                else
                    project.BuildMsi();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return int.MinValue;
            }

            return 0;
        }

        private static T SetAddBinaries<T>(this T project)
            where T : Project
        {
            foreach (File file in project.AllFiles)
            {
                if (file.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                 || file.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                 || file.Name.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase))
                {
                    project.AddBinary(new Binary(new Id(Guid.NewGuid()
                        .ToString().ToIdString()), file.Name));
                }
            }
            return project;
        }

        private static string CreateNewGuid() => Guid.NewGuid().ToString();

        private static Version GetBinaryVersion(string sourceDir)
        {
            string execPath = Path.Combine(sourceDir, "CollapseLauncher.exe");
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(execPath);
            string result = fileVersionInfo.FileVersion;
            Version version = new Version(result);
            return version;
        }

        private static string ToVersionString(this Version version)
        {
            string result = version.ToNoRevisionString().TrimEnd(".0".ToCharArray());
            return result;
        }

        public static void CopyTo(string fileFrom, string fileTo)
        {
            FileInfo fileInfoFrom = new FileInfo(fileFrom);
            FileInfo fileInfoTo = new FileInfo(fileTo);
            CopyTo(fileInfoFrom, fileInfoTo);
        }

        public static void CopyTo(FileInfo fileInfoFrom, FileInfo fileInfoTo) => fileInfoFrom.CopyTo(fileInfoTo.FullName, true);

        public static void CopyToDir(string dirFrom, string dirTo)
        {
            if (!Directory.Exists(dirTo))
                Directory.CreateDirectory(dirTo);

            DirectoryInfo dirFromInfo = new DirectoryInfo(dirFrom);

            foreach (FileInfo file in dirFromInfo.EnumerateFiles("*"))
            {
                string targetTo = Path.Combine(dirTo, file.Name);
                file.CopyTo(targetTo, true);
            }

            foreach (DirectoryInfo dirChildFromInfo in dirFromInfo.EnumerateDirectories("*"))
            {
                CopyToDir(dirChildFromInfo.FullName, Path.Combine(dirTo, dirChildFromInfo.Name));
            }
        }

        private static WixEntity GetDirEntity(string dirPath)
        {
            DirectoryInfo rootDirInfo = new DirectoryInfo(dirPath);
            Dir dir = new Dir(rootDirInfo.Name);

            AddRecursiveDir(dir, rootDirInfo);

            return dir;
        }

        private static WixEntity GetFileEntity(string path, Shortcut shortcut = null) => GetFileEntity(new FileInfo(path), shortcut);
        private static WixEntity GetFileEntity(FileInfo fileInfo, Shortcut shortcut = null) => shortcut == null ?
            new File(GetStringId(fileInfo.Name), fileInfo.FullName) :
            new File(GetStringId(fileInfo.Name), fileInfo.FullName, shortcut);

        private static void AddRecursiveDir(Dir rootDir, DirectoryInfo rootDirInfo)
        {
            foreach (FileInfo fileInfo in rootDirInfo.EnumerateFiles("*"))
            {
                rootDir.AddFile(GetFileEntity(fileInfo) as File);
            }

            foreach (DirectoryInfo dirInfo in rootDirInfo.EnumerateDirectories("*"))
            {
                Dir nextDir = new Dir(dirInfo.Name);
                AddRecursiveDir(nextDir, dirInfo);
                rootDir.AddDir(nextDir);
            }
        }

        private static Id GetStringId(string source)
        {
            source = source + Guid.NewGuid().ToString();
            return new Id(source.ToIdString());
        }

        private static string ToIdString(this string source)
        {
            source = '_' + source;
            source = source.Replace('-', '_');
            source = source.Replace('@', '_');
            source = source.Replace('.', '_');
            return source;
        }
    }
}