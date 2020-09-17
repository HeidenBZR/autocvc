﻿using System;
using System.IO;
using LyricInputHelper.Classes;
using LyricInputHelper.UI;
using System.Windows.Forms;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms.VisualStyles;
using System.Threading;
using System.Diagnostics;

namespace LyricInputHelper
{

    class Program
    {
        public enum Mode
        {
            Unknown = -1,
            Standalone,
            Plugin,
            Resampler,
            Wavtool,
            Presamp
        };
        public static Mode ProgramMode;
        public static Ust Ust;
        public static Atlas Atlas;
        public static string LOG_DIR { get { return GetTempFile("LyricInputHelper", @"log.txt"); } }
        public static string[] args;
        public static NumberFormatInfo NFI;
        public static Settings Settings;
        static Mutex Mutex;

        static void InitLang()
        {
            try
            {
                Lang.Init();
            }
            catch(ExecutionEngineException ex)
            {
                Program.ErrorMessage(ex, "Error on Lang Init");
            }
        }

        private static bool isDebug;

        [STAThread]
        static void Main(string[] args)
        {
#if DEBUG
                Debug();
#endif
            InitLang();
            try
            {
                NFI = new CultureInfo("en-US", false).NumberFormat;
                Program.args = args;
                ProgramMode = DetectMode();
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(LOG_DIR,
                        $"{ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n{ex.Message}\r\n",
                        Encoding.UTF8
                        );

                }
                catch (Exception ex2)
                {

                    MessageBox.Show($"{ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n" +
                        $"{ex.Message}\r\n\r\n" +
                        $"{ex2.Message}\r\n{ex2.Source}\r\n{ex2.TargetSite.ToString()}\r\n" +
                        $"{ex2.Message}\r\n", "Error");
                }
            }
            Init();
        }

        static void CheckFolder(params string[] path)
        {
            DebugLog("CheckFolder: " + string.Join(", ", path));
            try
            {
                string dir;
                for (int i = 0; i < path.Length - 2; i++)
                {
                    dir = Path.Combine(path.ToList().Take(i + 2).ToArray());
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, "Error on file access");
            }
        }

        public static string GetResourceFile(params string[] path)
        {
            DebugLog("GetResourceFile: " + string.Join(", ", path));
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = path.ToList().Prepend(root).ToArray();
            CheckFolder(path);
            return Path.Combine(path);
        }

        public static string GetResourceFolder(params string[] path)
        {
            DebugLog("GetResourceFolder: " + string.Join(", ", path));
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = path.ToList().Prepend(root).ToArray();
            CheckFolder(path);
            var dir = Path.Combine(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetTempFile(params string[] path)
        {
            DebugLog("GetTempFile: " + string.Join(", ", path));
            var root = Path.GetTempPath();
            path = path.ToList().Prepend(root).ToArray();
            CheckFolder(path);
            return Path.Combine(path);
        }

        public static string GetTempFolder(params string[] path)
        {
            DebugLog("GetTempFolder: " + string.Join(", ", path));
            var root = Path.GetTempPath();
            path = path.ToList().Prepend(root).ToArray();
            CheckFolder(path);
            var dir = Path.Combine(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        static void SaveArgs()
        {
            string text = "";
            for (int i = 0; i < args.Length; i++)
            {
                text += $"Arg #{i}: \"{args[i]}\"\n";
            }
            Log(text);
        }

        static Mode DetectMode()
        {
            Mode mode;
            switch (args.Length)
            {
                case 0:
                    mode = Mode.Standalone;
                    break;
                case 1:
                    if (IsTempUst(args[0]))
                    {
                        mode = Mode.Plugin;
                    }
                    else if (IsBat(args[0]))
                    {
                        mode = Mode.Presamp;
                    }
                    else
                    {
                        mode = Mode.Unknown;
                    }
                    break;
                case 6:
                case 11:
                    mode = Mode.Wavtool;
                    break;
                case 12:
                    mode = Mode.Resampler;
                    break;
                default:
                    mode = Mode.Unknown;
                    break;
            }
            Console.WriteLine($"{mode} ProgramMode");
            Mutex = CreateMutex(ProgramMode);
            return mode;
        }

        static void Init()
        {
            DebugLog("Init");
            InitLog(ProgramMode.ToString());
            if (isDebug)
                Log($"args[{args.Length}]: {string.Join(", ", args)}");

            if (ProgramMode == Mode.Plugin)
                PluginModeInit();

            if (ProgramMode == Mode.Standalone)
                StandaloneModeInit();

            if (ProgramMode == Mode.Wavtool || ProgramMode == Mode.Resampler)
            {
                Log("Try to create presamp");
                if (!MutexCheck(CreateMutex(Mode.Presamp)))
                {
                    Log("presamp is running");
                    return;
                }
                RunPresamp();
            }

            Log("Init finished");
        }

        static bool MutexCheck(Mutex mutex)
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(1), false))
            {
                return false;
            }
            return true;
        }

        static void RunPresamp()
        {
            Log("starting presamp");
            var batPath = GetBatPath();
            if (batPath == null)
            {
                Log("Bat path is not found");
                return;
            }
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = $"/C ping 127.0.0.1 -n 2 && \"{Application.ExecutablePath}\" {batPath}";
            Info.WindowStyle = ProcessWindowStyle.Hidden;
            Info.CreateNoWindow = true;
            Info.FileName = "cmd.exe";
            Process.Start(Info);
            Application.Exit();
        }

        static string GetBatPath()
        {
            var dirs = Directory.GetDirectories(GetTempFolder());
            foreach (var dir in dirs)
            {
                if (Path.GetFileName(dir).StartsWith("utau"))
                    return Path.Combine(dir, "temp.bat");
            }
            return null;
        }


        public static void Try(Action action, string errorMessage = "An error occured.")
        {
#if !DEBUG
            try
            {
#endif
                action.Invoke();
#if !DEBUG
            }
            catch (Exception ex)
            {
                ErrorMessage(ex, message);
            }
#endif
        }

        static void PluginModeInit()
        {
            Try(() =>
            {
                Ust = new Ust(args[0]);
                Ust.SetAtlas(Atlas);
                if (Ust.IsLoaded)
                    Log("Ust loaded");
                else
                    Log($"Error reading UST");
            }, "Error on reading Ust");

            Try(() =>
            {
                if (Ust.IsLoaded)
                {
                    Atlas = new Atlas(Ust.VoiceDir);
                    Ust.SetAtlas(Atlas);
                }
                if (Atlas.IsLoaded)
                    Log("Atlas loaded");
                else
                    Log("Error reading Atlas");
            }, "Error on reading Atlas");

            Try(() =>
            {
                var singer = new Singer(Ust.VoiceDir);
                if (singer.IsLoaded)
                    Log("Singer loaded");
                else
                    Log($"Error reading singer {Ust.VoiceDir}");
            }, "Error on reading Singer");

            Log("All files loaded successfully");

            Try(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }, "Error on end init");

            var window = new PluginWindow();
            window.Init(Ust, Atlas);
            Application.Run(window);
        }

        public static void ErrorMessage (Exception ex, string name = "Error")
        {
            var error = $"{name}: {ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n{ex.StackTrace}\r\n";
            Log(error);
            DebugLog(error);
            MessageBox.Show(error, name);
        }

        static void StandaloneModeInit()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new StandaloneWindow());

        }

        static Mutex CreateMutex(Mode mode)
        {
            return new Mutex(false, $"Heiden.BZR LyricInputHelper {mode}");
        }

        static bool IsTempUst(string dir)
        {
            string filename = dir.Split('\\').Last();
            return filename.StartsWith("tmp") && filename.EndsWith("tmp");
        }

        static bool IsBat(string dir)
        {
            return dir.EndsWith(dir);
        }

        static void InitLog(string type)
        {
            DebugLog("InitLog");
            try
            {
                if (!File.Exists(LOG_DIR))
                    File.Create(LOG_DIR).Close();
                using (StreamWriter log = new StreamWriter(LOG_DIR, true, System.Text.Encoding.UTF8))
                {
                    log.WriteLine($"\n\n====== New instance. Mode: {type} ======");
                    log.WriteLine(DateTime.Now.ToString(format: "d.MMM.yyyy, HH:mm:ss"));
                    log.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n" +
                    $"{ex.StackTrace}\r\n", "Error");
            }
        }

        public static void Log(string text, bool saveUST = true, bool appendTextbox = false)
        {
            DebugLog("File log attempt: " + text);
            try
            {
                using (StreamWriter log = new StreamWriter(LOG_DIR, true, System.Text.Encoding.UTF8))
                {
                    string type;
                    switch (args.Length)
                    {
                        case 6:
                            type = "wavtool for R";
                            break;
                        default:
                            type = ProgramMode.ToString();
                            break;
                    }
                    log.WriteLine(text);
                    log.Close();

                }
            }
            catch (Exception ex)
            {
                var error = $"{ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n{ex.StackTrace}\r\n";
                DebugLog(error);
                MessageBox.Show(error, "Error");
            }

            try
            {
                if (saveUST && Ust != null && Ust.IsLoaded)
                {
                    DebugLog("Save debug Ust attempt");
                    File.Copy(args[0], GetTempFile("autocvc", "Ust.tmp"), true);
                }
            }
            catch (Exception ex)
            {
                var error = $"{ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n{ex.StackTrace}\r\n";
                DebugLog(error);
                MessageBox.Show(error, "Error");
            }

            try
            {
                if (ProgramMode == Mode.Plugin)
                {
                    DebugLog("Set Status attempt");
                    PluginWindow.SetStatus(text, appendTextbox);
                }
            }
            catch (Exception ex)
            {
                var error = $"{ex.Message}\r\n{ex.Source}\r\n{ex.TargetSite.ToString()}\r\n{ex.StackTrace}\r\n";
                DebugLog(error);
                MessageBox.Show(error, "Error");
            }

        }

        private static void Debug()
        {
            isDebug = true;
            DebugLog("Debug started");
            DebugLog("Checking log dir...");
            if (File.Exists(LOG_DIR))
            {
                DebugLog("Log file exists.");
            }
            else
            {
                DebugLog("Log file doesn't exist, abort.");
                return;
            }
            DebugLog();
            DebugLog("Checking write permissions...");
            try
            {
                File.AppendAllText(LOG_DIR, "");
                DebugLog("Write permissions are ok.");
            }
            catch (Exception ex)
            {
                DebugLog($"Write permissions are bad! Exception handled:");
                DebugLog();
                DebugLog(ex.Message);
                DebugLog();
                DebugLog(ex.StackTrace);
                DebugLog();
                DebugLog("Abort.");
                return;
            }
            DebugLog();
            DebugLog("Checking Atlas access...");
            Atlas = new Atlas(Atlas.GetAtlasPath("CVC RUS"));
            try
            {
                Atlas.ReadAtlas();
                DebugLog("Atlas access is ok.");
            }
            catch (Exception ex)
            {
                DebugLog($"Atlas access is bad! Exception handled:");
                DebugLog();
                DebugLog(ex.Message);
                DebugLog();
                DebugLog(ex.StackTrace);
                DebugLog();
                DebugLog("Abort.");
                return;
            }
            DebugLog();
            DebugLog("Debug finished.");
        }

        public static void DebugLog(string message = "")
        {
            if (isDebug)
                Console.WriteLine(message);
        }

        public static void Assert(bool passed, string message = "")
        {
#if DEBUG
            if (!passed)
                throw new Exception(message);
#endif
        }

    }
}
