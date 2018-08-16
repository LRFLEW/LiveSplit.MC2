using System;
using System.Diagnostics;
using LiveSplit.ComponentUtil;

namespace LiveSplit.MC2
{
    class Hooks : IDisposable
    {
        Component _parent;
        Process _mc2;
        IntPtr _baseaddr;
        IntPtr _hook;
        Version _version;

        MemoryWatcher<byte> _loading, _disclaimer;
        MemoryWatcherList _memory = new MemoryWatcherList();

        X86Generator _maingen = new X86Generator();
        X86Generator _game = new X86Generator();
        X86Generator _movie = new X86Generator();
        X86Generator _frontend = new X86Generator();
        X86Generator _raceeditor = new X86Generator();
        X86Generator _carviewer = new X86Generator();
        X86Generator _jmp = new X86Generator();

        public Hooks(Component parent)
        {
            _parent = parent;
        }

        private void AddHooks()
        {
            _maingen.Clear();
            int loading = _maingen.DataByte(0x00);

            int gamehead = WriteHead(_maingen, _game);
            int moviehead = WriteHead(_maingen, _movie);
            int frontendhead = WriteHead(_maingen, _frontend);
            int raceeditorhead = WriteHead(_maingen, _raceeditor);
            int carviewerhead = WriteHead(_maingen, _carviewer);

            int game = GenWrap(_maingen, loading, gamehead, off: 1);
            int movie = GenWrap(_maingen, loading, moviehead);
            int frontend = GenWrap(_maingen, loading, frontendhead, end: 0x02);
            int raceeditor = GenWrap(_maingen, loading, raceeditorhead);
            int carviewer = GenWrap(_maingen, loading, carviewerhead);

            _hook = _maingen.Install(_mc2);
            InstallJmp(_game, game);
            InstallJmp(_movie, movie);
            InstallJmp(_frontend, frontend);
            InstallJmp(_raceeditor, raceeditor);
            InstallJmp(_carviewer, carviewer);

            _loading = new MemoryWatcher<byte>(_hook);
            _loading.OnChanged += On_Loading;
            _memory.Add(_loading);

            _disclaimer = new MemoryWatcher<byte>(_baseaddr + 0x2622B0);
            _disclaimer.OnChanged += On_Disclaimer;
            _memory.Add(_disclaimer);
        }

        private bool TestProcess(Process process)
        {
            if (!process.ProcessName.Contains("mc2")) return false;
            if (process.HasExited) return false;
            if (process.Is64Bit()) return false;
            _baseaddr = process.MainModuleWow64Safe().BaseAddress;
            _version = Version.GetVersion(process, _baseaddr);
            if (_version == null) return false;

            // Steam's DRM encrypts the .text section, so verify the text is decrypted before hooking.
            GenSPM(_game, _baseaddr, _version.game);
            if (!_game.VerifyInstall(process)) return false;
            GenPPO(_movie, _baseaddr, _version.movie, _version.moviestr);
            if (!_movie.VerifyInstall(process)) return false;
            GenPPO(_frontend, _baseaddr, _version.frontend, _version.frontendstr);
            if (!_frontend.VerifyInstall(process)) return false;
            GenPPO(_raceeditor, _baseaddr, _version.raceeditor, _version.raceeditorstr);
            if (!_raceeditor.VerifyInstall(process)) return false;
            GenPPO(_carviewer, _baseaddr, _version.carviewer, _version.carviewerstr);
            if (!_carviewer.VerifyInstall(process)) return false;

            _mc2 = process;
            AddHooks();
            return true;
        }

        public void Update()
        {
            try
            {
                if (_mc2 != null && _mc2.HasExited)
                {
                    _memory.Clear();
                    _parent.On_Loading(false);
                    _mc2 = null;
                }

                if (_mc2 == null)
                    foreach (Process p in Process.GetProcesses())
                        if (TestProcess(p)) break;

                if (_mc2 != null) _memory.UpdateAll(_mc2);
            }
            catch
            {
                // Sometimes reads or writes fail due to race conditions.
                // Treat these exceptions as an exit event.
                _memory.Clear();
                _parent.On_Loading(false);
                _mc2 = null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_mc2 != null && !_mc2.HasExited)
                {
                    _game.WriteInstall(_mc2);
                    _movie.WriteInstall(_mc2);
                    _frontend.WriteInstall(_mc2);
                    _raceeditor.WriteInstall(_mc2);
                    _carviewer.WriteInstall(_mc2);

                    _mc2.FreeMemory(_hook);
                }
            }
            finally
            {
                _mc2 = null;
            }
        }

        private void On_Loading(byte old, byte current) => _parent.On_Loading(current == 2 ? _disclaimer.Current != 0 : current != 0);

        private void On_Disclaimer(byte old, byte current) => _parent.On_Loading(_loading.Current == 2 ? current != 0 : _loading.Current != 0);

        private static int GenWrap(X86Generator generator, int loading, int dest, short off = 0, byte end = 0x00) {
            int res = generator.MovByteMIL(loading, 0x01);
            generator.CallStdL(dest, off);
            generator.MovByteMIL(loading, end);
            generator.Retn((short) (off * 4));
            return res;
        }

        private static void GenSPM(X86Generator gen, IntPtr baseaddr, int codeoff)
        {
            gen.Clear();
            gen.SetInstall(baseaddr + codeoff);
            gen.SubRI(X86Generator.Registers.ESP, 0x28);
            gen.PushReg(X86Generator.Registers.ESI);
            gen.MovRR(X86Generator.Registers.ESI, X86Generator.Registers.ECX);
        }

        private static void GenPPO(X86Generator gen, IntPtr baseaddr, int codeoff, int stroff)
        {
            gen.Clear();
            gen.SetInstall(baseaddr + codeoff);
            gen.PushReg(X86Generator.Registers.ESI);
            gen.PushR(baseaddr + stroff);
        }

        private static int WriteHead(X86Generator gen, X86Generator ingen)
        {
            int res = gen.WriteGen(ingen);
            gen.JumpR(ingen.GetInstall() + ingen.GetCount());
            return res;
        }

        private void InstallJmp(X86Generator ingen, int hookoff)
        {
            _jmp.Clear();
            _jmp.SetInstall(ingen.GetInstall());
            _jmp.JumpR(_hook + hookoff);
            _jmp.WriteInstall(_mc2);
        }
    }
}
