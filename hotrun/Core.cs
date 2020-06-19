using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hotrun {
    static class Core {
        static Exception MergeErrors(CompilerErrorCollection errors) {
            var msg = "";
            foreach (CompilerError e in errors) {
                msg += $"({e.Line},{e.Column}): [{e.ErrorNumber}] - {e.ErrorText}";
            }
            return new Exception(msg);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool RegisterHotKey(
            IntPtr hWnd,
            int id,
            uint fsModifiers,
            uint vk
        );

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool UnregisterHotKey(
            IntPtr hWnd,
            int id
        );       

        static Assembly Compile(string filename, string[] assemblies) {
            assemblies = assemblies ?? new string[] { };
            var presets = new [] {
                    "System.Windows.Forms.dll"
                };

            assemblies = assemblies.Union(presets).ToArray();

            var csc = new CSharpCodeProvider();
            var code = File.ReadAllText(filename);
            var opts = new CompilerParameters(assemblies) { GenerateInMemory = true }; // store to cache
            var compiled = csc.CompileAssemblyFromSource(opts, code);
            if (compiled.Errors.Count > 0) throw MergeErrors(compiled.Errors);
            return compiled.CompiledAssembly;
        }

        static Action FromAssembly(Assembly a) {
            var prog = a.GetTypes().First(x => x.Name == "Program");
            if (prog == null) throw new Exception("Class \"Program\" is not found.");
            var fn = prog.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (fn == null) throw new Exception("Function \"static void Main()\" is not found.");
            return () => fn.Invoke(null, null);
        }

        public static List<HkInfo> MakeHkMap(IDictionary<string, object> keyMap) {
            const int VK_MAX = 0xff;
            var hkMap = new List<HkInfo>();

            foreach (var hotkey in keyMap.Keys) {
                Keys vk = 0;
                Mod mod = Mod.Norepeat;

                foreach (var tok in hotkey.Split('+')) {
                    Keys rkey;
                    var rkeyParsed = Enum.TryParse(tok, true, out rkey);
                    if (rkeyParsed) {
                        if (vk != 0) {
                            Fail(hotkey, "too many vkeys");
                        }
                        else {
                            if ((int)rkey < VK_MAX) vk = rkey;
                        }
                    }

                    Mod rmod;
                    var rmodParsed = Enum.TryParse(tok, true, out rmod);
                    if (rmodParsed) {
                        mod |= rmod;
                    }

                    if (string.Compare(tok, "repeat", true) == 0) {
                        mod &= ~Mod.Norepeat;
                        rmodParsed = true;
                    }

                    if (!(rkeyParsed || rmodParsed)) {
                        Fail(hotkey, $"unknown key: {tok}");
                    }
                }

                if (vk == 0) {
                    Fail(hotkey, "no vk was specified");
                }

                var value = keyMap[hotkey];

                if (value is string) {
                    var path = value as string;
                    if (string.IsNullOrWhiteSpace(path)) {
                        Fail(hotkey, "value is empty");
                    }
                    else {
                        var a = TryGetAssembly(path);
                        var hk = new HkInfo {
                            Action = a == null ? RunCommand(path) : FromAssembly(a),
                            Mod = mod,
                            Vk = vk
                        };
                        hkMap.Add(hk);
                    }
                }
                else {
                    Fail(hotkey, "value type is not supported");
                }
            }

            return hkMap;
        }

        static Assembly TryGetAssembly(string path) {
            if (path.EndsWith(".script")) {
                return Compile(path, null);
            }
            else {
                try {
                    return Assembly.LoadFrom(path);
                }
                catch {
                    return null;
                }
            }
        }

        static void Fail(string msg) {
            throw new Exception(msg);
        }

        static void Fail(string hotkey, string msg) {
            Fail($"[{hotkey}]: {msg}");
        }

        static Action RunCommand(string cmd) {
            return () => {
                var fileName = Utils.GetFileName(cmd);
                var arguments = Utils.GetArguments(cmd);
                var psInfo = new ProcessStartInfo(fileName, arguments);
                psInfo.CreateNoWindow = true;
                Process.Start(psInfo);
            };
        }
    }

    enum Mod {
        Alt = 0x1,
        Ctrl = 0x2,
        Norepeat = 0x4000,
        Shift = 0x4,
        Win = 0x8
    }

    struct HkInfo {
        public Action Action { get; set; }
        public Mod Mod { get; set; }
        public Keys Vk { get; set; }
    }

    class HkWatcher : Form {
        readonly List<HkInfo> _hkMap = new List<HkInfo>();
        readonly TextBox _log = new TextBox();
        readonly NotifyIcon _notify = new NotifyIcon();

        public HkWatcher() {
            _log.Multiline = true;
            _log.Dock = DockStyle.Fill;
            Controls.Add(_log);

            Size = new Size(400, 400);
            ShowInTaskbar = false;

            var ok = false;

            try {
                var json = File.ReadAllText("hk.json");
                var keyMap = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());

                _hkMap = Core.MakeHkMap(keyMap);

                for (int i = 0; i < _hkMap.Count; ++i) {
                    var hk = _hkMap[i];

                    if (!Core.RegisterHotKey(
                        Handle,
                        i,
                        (uint)(hk.Mod),
                        (uint)hk.Vk
                    )) {
                        throw new Win32Exception();
                    }
                }

                ok = true;
                Log(json);
            }
            catch (Exception e) {
                Log(e.Message);
            }
                        
            Load += (s, ev) => {
                ToggleForm(!ok);
            };
            ToggleForm(false);

            _notify.Icon = Properties.Resources.app;
            _notify.Visible = true;
            _notify.ContextMenu = new ContextMenu(
                new[] { new MenuItem("quit", (s, ev) => Close()) }
            );
            _notify.Click += (s, ev) => ToggleForm(!Visible);
        }

        protected override void Dispose(bool disposing) {
            _log.Dispose();
            _notify.Dispose();

            foreach (var i in Enumerable.Range(0, _hkMap.Count)) {
                Core.UnregisterHotKey(Handle, i);
            }

            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x0312) {
                var index = (int)m.WParam;

                if (index >= 0 && index < _hkMap.Count) {
                    var hk = _hkMap[index];

                    Task.Factory.StartNew(hk.Action)
                        .ContinueWith(t => {
                            if (t.Exception != null) {
                                    Log(t.Exception.Message);
                                }
                        });
                }
            }

            base.WndProc(ref m);
        }

        void ToggleForm(bool show) {
            Visible = show;
            WindowState = Visible ? FormWindowState.Normal : FormWindowState.Minimized;
        }

        void Log(string msg) {
            _log.Text = msg;
        }
    }
}
