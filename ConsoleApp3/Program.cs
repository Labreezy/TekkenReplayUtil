using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Squalr.Engine.Logging;
using Squalr.Engine.OS;
using Squalr.Engine.Memory;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Media;
using System.Windows.Forms;
using System.Text;

namespace TekkenReplayUtil
{
    class EngineLogEvents : ILoggerObserver
    {
        public void OnLogEvent(LogLevel logLevel, string message, string innerMessage)
        {
            Console.WriteLine(message);
            Console.WriteLine(innerMessage);
        }
    }
    class Program
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        static uint WM_KEYDOWN = 0x100;
        static uint WM_KEYUP = 0x101;
        static ulong bufferhook;
        static byte[] replayarr;
        static ulong inputbuf;
        static ulong baseAddr;
        static ulong fcaddr;
        static ulong moveidaddr;
        static List<uint[]> p1klist = new List<uint[]>();
        static List<uint[]> p2klist = new List<uint[]>();
        static Dictionary<string, string> names = new Dictionary<string, string>()
        {
            {"[ARMOR_KING]","ArmorKing" },
            {"[ANNA]","Anna" },
            {"[SHAHEEN]", "Shaheen" },
            {"[ALISA]", "Alisa" },
            {"[ASUKA]", "Asuka"},
            {"[BOB_SATSUMA]", "Bob" },
            {"[BRYAN]", "Bryan" },
            {"[GIGAS]", "Gigas" },
            {"[Chloe]", "Chloe" },
            {"[Dragunov]", "Dragunov" },
            {"[DEVIL_JIN]", "DevilJin" },
            {"[EDDY]", "Eddy" },
            {"[Vampire]", "Eliza" },
            {"[FENG]", "Feng" },
            {"[FRV]", "MasterRaven" },
            {"[HEIHACHI]", "Heihachi" },
            {"[HWOARANG]", "Hwoarang" },
            {"[CLAUDIO]", "Claudio" },
            {"[Jack]", "Jack" },
            {"[JIN]", "Jin" },
            {"[KING]", "King" },
            {"[Kuma]", "Kuma" },
            {"[KAZUMI]", "Kazumi" },
            {"[Lars]", "Lars" },
            {"[LAW]", "Law" },
            {"[LEE]", "Lee" },
            {"[Lei_Wulong]", "Lei" },
            {"[Eleonor]", "Leo" },
            {"[EMILIE]", "Lili" },
            {"[KATARINA]", "Katarina" },
            {"[MARDUK]", "Marduk" },
            {"[Miguel]", "Miguel" },
            {"[Mr.X]", "Akuma" },
            {"[Geese_Howard]", "Geese"},
            {"[Noctis]", "Noctis" },
            {"[JOSIE]", "Josie" },
            {"[NINA]", "Nina" },
            {"[PANDA]", "Panda" },
            {"[Paul]", "Paul" },
            {"[Steve_Fox]", "Steve" },
            {"[Lin_Xiaoyu]", "Xiaoyu" },
            {"[YOSHIMITSU]", "Yoshimitsu" },
        };
        static void Main(string[] args)
        {
            var procname = "TekkenGame-Win64-Shipping";
            Process proc = Process.GetProcessesByName(procname).FirstOrDefault();
            if (proc != null)
            {
                Processes.Default.OpenedProcess = proc;
            }
            else
            {
                Console.WriteLine("Tekken isn't open!");
                Environment.Exit(1);
            }
            var modules = Query.Default.GetModules();
            baseAddr = 11;
            foreach (var mod in modules)
            {
                if (mod.Name.Equals(procname + ".exe"))
                {
                    baseAddr = mod.BaseAddress;
                    break;
                }
            }
            if (baseAddr == 11)
            {
                Console.WriteLine("Couldn't Find Default Module!");
                Environment.Exit(2);
            }
            IntPtr hWnd = proc.MainWindowHandle;
            ShowWindow(hWnd, 3);
            SetForegroundWindow(hWnd);
            bufferhook = baseAddr + 0x50F9A17;
            var fcresult = Task.Run(() =>
           {
               var success = false;
               ulong lastoff = 0x1A4D0;
               ulong result = 0;
               while (!success)
               {
                   var r1 = Reader.Default.Read<ulong>(baseAddr + 0x344E320, out success);
                   Thread.Sleep(1);
               }
               bool s1;
               while (result <= lastoff)
               {
                   ulong part1 = Reader.Default.Read<ulong>(baseAddr + 0x344E320, out _);
                   ulong part2 = Reader.Default.Read<ulong>(part1, out s1);
                   result = part2 + 0x1A4D0;
               }
               return result;
           });
            fcaddr = fcresult.Result;
            moveidaddr = fcaddr - 0x1A4D0 + 0x31C;
            var movetimeraddr = moveidaddr - 0x31C + 0x1F0;
            if (args.Length == 0) {
                ulong newasm = Allocator.Default.AllocateMemory(0x800);
                ulong bufarray = Allocator.Default.AllocateMemory(0x20000);
                byte[] originalcode = Reader.Default.ReadBytes(bufferhook, 10, out _);
                byte[] jmpbytes = assemble64("mov r13, 0x" + newasm.ToString("X") +
                "\r\njmp r13\r\nnop");
                byte[] hookbytes = assemble64(
                    "mov r13, 0x" + bufarray.ToString("X") +
                    "\r\nmov rdx, 0x" + (newasm + 0x7F8).ToString("X") +
                    "\r\ncmp dword [rdx], 0" +
                    "\r\njne write" +
                    "\r\ncmp rax, 1" +
                    "\r\nje backtocode" +
                    "\r\nwrite:" +
                    "\r\nadd r13d, dword [rdx]" +
                    "\r\nmov [r13], r8w" +
                    "\r\nadd dword [rdx], 2" +
                    "\r\nbacktocode:" +
                    "\r\nxor r13, r13");
                    byte[] jmpbackbytes = assemble64("mov r8, 0x" + (bufferhook + 10).ToString("X") +
                    "\r\njmp r8");
                hookbytes = hookbytes.Concat(originalcode).Concat(jmpbackbytes).ToArray();
                while (Reader.Default.Read<UInt32>(fcaddr, out _) != 0)
                {
                    Thread.Sleep(1);
                }
                while(Reader.Default.Read<UInt32>(moveidaddr, out _) != 32769 && Reader.Default.Read<UInt32>(movetimeraddr, out _) != 10)
                {
                    Thread.Sleep(1);
                }
                SystemSounds.Beep.Play();
                Console.WriteLine("Hooking");
                Writer.Default.WriteBytes(newasm, hookbytes);
                Writer.Default.WriteBytes(bufferhook, jmpbytes);
                ulong p1nameaddr = Reader.Default.Read<ulong>(baseAddr + 0x3419720, out _) + 0x2E8;
                ulong p2nameaddr = Reader.Default.Read<ulong>(baseAddr + 0x341C660, out _) + 0x2E8;
                var stalecount = 0;
                var fc = Reader.Default.Read<UInt32>(fcaddr, out _);
                UInt32 nfc;
                bool success = true;
                byte[] p1namearr = Reader.Default.ReadBytes(p1nameaddr, 16, out _).TakeWhile(character => character != 0).ToArray();
                byte[] p2namearr = Reader.Default.ReadBytes(p2nameaddr, 16, out _).TakeWhile(character => character != 0).ToArray();
                string p1name = Encoding.ASCII.GetString(p1namearr);
                string p2name = Encoding.ASCII.GetString(p2namearr);
                while (stalecount < 400 && success)
                {
                    nfc = Reader.Default.Read<UInt32>(fcaddr, out success);
                    if (nfc == fc && nfc != 0)
                    {
                        stalecount++;
                        Thread.Sleep(1);
                    }
                    else
                    {
                        stalecount = 0;
                        Thread.Sleep(1);
                    }
                    fc = nfc;
                }
                Writer.Default.WriteBytes(bufferhook, originalcode);
                SystemSounds.Beep.Play();
                var recordingLength = Reader.Default.Read<Int32>(newasm + 0x7F8, out _);
                Allocator.Default.DeallocateMemory(newasm);
                byte[] recording = Reader.Default.ReadBytes(bufarray, recordingLength, out _);
                Allocator.Default.DeallocateMemory(bufarray);
                string p1shortname = names[p1name];
                string p2shortname = names[p2name];
                string recname = p1shortname + "_vs_" + p2shortname + "_insertstagehere.bin";
                File.WriteAllBytes(recname, recording);
                Console.WriteLine("Recording over, 0x" + recordingLength.ToString("X") + " bytes written");
                MessageBox.Show("REMEMBER WHAT STAGE YOU PLAYED ON, RENAME THE FILENAME ACCORDINGLY");
            } else if (args.Length == 1)
            {
                ulong newasm = Allocator.Default.AllocateMemory(0x800);
                ulong bufarray = Allocator.Default.AllocateMemory(0x20000);
                byte[] replay = File.ReadAllBytes(args[0]);
                Writer.Default.WriteBytes(bufarray, replay);
                Writer.Default.Write<Int32>(bufarray + 0x18000, replay.Length);
                ulong stoploc = bufarray + 0x18080;
                byte[] originalcode = Reader.Default.ReadBytes(bufferhook, 10, out _);
                byte[] jmpbytes = assemble64("mov r13, 0x" + newasm.ToString("X") +
                "\r\njmp r13\r\nnop");
                byte[] hookbytes = assemble64(
                    "mov r13, 0x" + bufarray.ToString("X") +
                    "\r\nmov rdx, 0x" + (newasm + 0x7F8).ToString("X") +
                    "\r\ncmp dword [rdx], 0" +
                    "\r\njne write" +
                    "\r\ncmp rax, 1" +
                    "\r\nje backtocode" +
                    "\r\nwrite:" +
                    "\r\nadd r13d, dword [rdx]" +
                    "\r\nmov r8w, word [r13]" +
                    "\r\nmov r9w, word [r13]" +
                    "\r\nmov dword [rcx + rax*4 + 0x20], r8d" +
                    "\r\nmov dword [rcx + rax*4 + 0x28], r9d" +
                    "\r\nsub r13d, dword [rdx]" +
                    "\r\nadd dword [rdx], 2" +
                    "\r\nmov r8d, [r13+0x18000]" +
                    "\r\ncmp dword [rdx], r8d" +
                    "\r\njl backtocode" +
                    "\r\nmov byte [r13+0x18080], 1" +
                    "\r\nbacktocode:" +
                    "\r\nxor r13, r13" +
                    "\r\nmov r8, 0x" + (bufferhook + 10).ToString("X") +
                    "\r\njmp r8"
                    );
                while (Reader.Default.Read<UInt32>(fcaddr, out _) != 0)
                {
                    Thread.Sleep(1);
                }
                while (Reader.Default.Read<UInt32>(moveidaddr, out _) != 32769 && Reader.Default.Read<UInt32>(movetimeraddr, out _) != 10)
                {
                    Thread.Sleep(1);
                }
                SystemSounds.Beep.Play();
                Console.WriteLine("Hooking");
                Writer.Default.WriteBytes(newasm, hookbytes);
                Writer.Default.WriteBytes(bufferhook, jmpbytes);
                var stalecount = 0;
                var fc = Reader.Default.Read<UInt32>(fcaddr, out _);
                UInt32 nfc;
                while (stalecount < 400 && Reader.Default.Read<byte>(bufarray + 0x18080, out _) == 0)
                {
                    nfc = Reader.Default.Read<UInt32>(fcaddr, out _);
                    if (nfc == fc && nfc != 0)
                    {
                        stalecount++;
                        Thread.Sleep(1);
                    }
                    else
                    {
                        stalecount = 0;
                        Thread.Sleep(1);
                    }
                    fc = nfc;
                }
                Writer.Default.WriteBytes(bufferhook, originalcode);
                Allocator.Default.DeallocateMemory(bufarray);
                Allocator.Default.DeallocateMemory(newasm);
                SystemSounds.Beep.Play();
                Console.WriteLine("Playback Complete!");
            }
        }
        static byte[] assemble64(String assembly)
        {
            string cd = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string nasmpath = Path.Combine(cd, "nasm.exe");
            Console.WriteLine(nasmpath);
            string asmInStr = "[BITS 64]" + Environment.NewLine + assembly;
            try
            {
                String asmpath = Path.Combine(Path.GetTempPath(), "T7ASM" + Guid.NewGuid() + ".asm");
                String outpath = Path.Combine(Path.GetTempPath(), "T7ASM" + Guid.NewGuid() + ".bin");

                File.WriteAllText(asmpath, asmInStr);
                ProcessStartInfo psi = new ProcessStartInfo(nasmpath);
                psi.Arguments = "-f bin \"" + asmpath + "\" -o \"" + outpath + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = false;

                Process nasmproc = Process.Start(psi);
                //var outmsg = nasmproc.StandardOutput.ReadToEnd();
                //var errmsg = nasmproc.StandardError.ReadToEnd();
                nasmproc.WaitForExit();

                if (File.Exists(outpath))
                {
                    return File.ReadAllBytes(outpath);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("NASM fucked up");
                Console.WriteLine(e.ToString());
            }
                return new byte[] { };
        }
        static void record(string filepath)
        {
            ulong newasm = Allocator.Default.AllocateMemory(0x800);
            ulong bufarray = Allocator.Default.AllocateMemory(0x20000);
            byte[] originalcode = Reader.Default.ReadBytes(bufferhook, 10, out _);

            

            // File.WriteAllBytes(filepath, buflist.ToArray());
        }
        static void playback(IntPtr hWnd)
        {
            UInt32 framecounter = Reader.Default.Read<UInt32>(fcaddr, out _);
            if (framecounter != 1)
            {
                var wresult = Task.Run(() =>
                {
                    while (Reader.Default.Read<UInt32>(fcaddr, out _) != 1)
                    {
                        Thread.Sleep(1);
                    }
                });
            }
            uint[] lastframe1 = p1klist[0];
            uint[] lastframe2 = p2klist[0];
            foreach (var vkc in lastframe1)
            {
                SendMessage(hWnd, WM_KEYDOWN, (IntPtr)vkc, (IntPtr)0);
            }
            foreach(var vkc in lastframe2)
            {
                SendMessage(hWnd, WM_KEYDOWN, (IntPtr)vkc, (IntPtr)0);
            }
            while(framecounter == Reader.Default.Read<UInt32>(fcaddr, out _))
            {
                Thread.Sleep(1);
            }
            framecounter = Reader.Default.Read<UInt32>(fcaddr, out _);
            var i = 1;
            while(i < p1klist.Count)
            {
                foreach(var vkc in p1klist[i])
                {
                    if (!lastframe1.Contains(vkc))
                    {
                        SendMessage(hWnd, WM_KEYDOWN, (IntPtr)vkc, (IntPtr)0);
                    }
                }
                foreach(var vkc in lastframe1)
                {
                    if (!p1klist[i].Contains(vkc))
                    {
                        SendMessage(hWnd, WM_KEYUP, (IntPtr)vkc, (IntPtr)0xC0000001);
                    }
                }
                foreach (var vkc in p2klist[i])
                {
                    if (!lastframe2.Contains(vkc))
                    {
                        SendMessage(hWnd, WM_KEYDOWN, (IntPtr)vkc, (IntPtr)0);
                    }
                }
                foreach(var vkc in lastframe2)
                {
                    if (!p2klist[i].Contains(vkc))
                    {
                        SendMessage(hWnd, WM_KEYUP, (IntPtr)vkc, (IntPtr)0xC0000001);
                    }
                }
                lastframe1 = p1klist[i];
                lastframe2 = p2klist[i];
                while (framecounter == Reader.Default.Read<UInt32>(fcaddr, out _))
                {
                    Thread.Sleep(1);
                }
                framecounter = Reader.Default.Read<UInt32>(fcaddr, out _);
                i++;
                Console.WriteLine("Played Frame");
            }
            Console.WriteLine("Done With Playback!");
        }
            
    }
}
