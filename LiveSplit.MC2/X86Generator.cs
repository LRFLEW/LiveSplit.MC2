using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LiveSplit.ComponentUtil;

namespace LiveSplit.MC2
{
    class X86Generator
    {
        List<byte> _data = new List<byte>();
        List<int> _adds = new List<int>();
        List<int> _subs = new List<int>();

        IntPtr _install = IntPtr.Zero;

        public enum Registers
        {
            EAX, ECX, EDX, EBX,
            ESP, EBP, ESI, EDI,
        };

        public IntPtr GetInstall() => _install;
        public int GetCount() => _data.Count;
        public IntPtr LocalToPtr(int local) => _install + local;
        public byte[] ToArray() => _data.ToArray();
        public void Clear()
        {
            _data.Clear();
            _adds.Clear();
            _subs.Clear();
        }

        public IntPtr SetInstall(IntPtr ptr)
        {
            int delta = ptr.ToInt32() - _install.ToInt32();
            _install = ptr;
            if (delta == 0) return _install;
            foreach (int addr in _adds) AddOffset( delta, addr);
            foreach (int addr in _subs) AddOffset(-delta, addr);
            return _install;
        }

        public IntPtr WriteInstall(Process process)
        {
            process.WriteBytes(_install, _data.ToArray());
            return _install;
        }
        public bool VerifyInstall(Process process) => _data.SequenceEqual(process.ReadBytes(_install, _data.Count));

        public IntPtr Install(Process process)
        {
            IntPtr ptr = process.AllocateMemory(_data.Count);
            SetInstall(ptr);
            WriteInstall(process);
            return _install;
        }

        // Modifiers

        public int WriteGen(X86Generator asm)
        {
            int res = _data.Count;
            int delta = _install.ToInt32() + _data.Count - asm._install.ToInt32();
            _data.AddRange(asm._data);
            if (delta == 0) return res;
            foreach (int addr in asm._adds) AddOffset( delta, addr + res);
            foreach (int addr in asm._subs) AddOffset(-delta, addr + res);
            return res;
        }

        public int DataByte(byte value)
        {
            int ret = _data.Count;
            _data.Add(value);
            return ret;
        }

        public int DataInt16(short value)
        {
            int ret = _data.Count;
            _data.AddRange(BitConverter.GetBytes(value));
            return ret;
        }

        public int DataInt32(int value)
        {
            int ret = _data.Count;
            _data.AddRange(BitConverter.GetBytes(value));
            return ret;
        }

        public int SubRI(Registers reg, int imm, bool force32 = false)
        {
            int ret = _data.Count;
            if (!force32 && imm >= -128 && imm < 128)
            {
                _data.Add(0x83); // Sub R/M, Imm8
                _data.Add((byte) (0xE8 + reg)); // R/M = Reg, /5, Direct
                DataByte((byte) imm); // Imm8
            }
            else
            {
                _data.Add(0x81); // Sub R/M, Imm32
                _data.Add((byte) (0xE8 + reg)); // R/M = Reg, /5, Direct
                DataInt32(imm); // Imm32
            }
            return ret;
        }

        public int MovRR(Registers dest, Registers src)
        {
            // Could also use Mov R/M, R; current op-code chosen to match binary
            int ret = _data.Count;
            _data.Add(0x8B); // Mov R, R/M
            _data.Add((byte) (0xC0 | ((int) dest << 3) | (int) src)); // R/M = src, dest, Direct
            return ret;
        }

        public int PushStackO(int offset, bool force32 = false)
        {
            int ret = _data.Count;
            if (!force32 && offset >= -128 && offset < 128) {
                _data.Add(0xFF); // Push = FF /6
                _data.Add(0x74); // ModR/M = SIP, /6, disp8
                _data.Add(0x24); // SIB = ESP, none, *1
                DataByte((byte) offset); // Imm8
            }
            else
            {
                _data.Add(0xFF); // Push = FF /6
                _data.Add(0xB4); // ModR/M = SIP, /6, disp32
                _data.Add(0x24); // SIB = ESP, none, *1
                DataInt32(offset); // Imm32
            }
            return ret;
        }

        public int PushReg(Registers reg)
        {
            int ret = _data.Count;
            _data.Add((byte) (0x50 + reg));
            return ret;
        }

        public int PushI(int value)
        {
            int ret = _data.Count;
            _data.Add(0x68);
            DataInt32(value);
            return ret;
        }
        public int PushR(IntPtr value) => PushI(value.ToInt32());
        
        public int PushL(int value)
        {
            int ret = PushI(value);
            _adds.Add(_data.Count - 4);
            return ret;
        }

        public int CallStdL(int dest, int args)
        {
            int ret = _data.Count;
            for (int i=0; i < args; ++i) PushStackO(args * 4);
            _data.Add(0xE8); // Call near direct (relative)
            DataInt32(dest - (_data.Count + 4)); // Call Address
            return ret;
        }

        public int CallStdR(IntPtr dest, int args)
        {
            int ret = CallStdL(dest.ToInt32() - _install.ToInt32(), args);
            _subs.Add(_data.Count - 4);
            return ret;
        }

        public int JumpL(int dest, bool force32 = false)
        {
            int ret = _data.Count;
            int delta = dest - (ret + 2);
            if (!force32 && delta >= -128 && delta < 128)
            {
                _data.Add(0xEB); // Jump rel8
                DataByte((byte) delta); // Imm8
            }
            else
            {
                _data.Add(0xE9); // Jump rel32
                DataInt32(delta - 3); // Imm32
            }
            return ret;
        }

        public int JumpR(IntPtr dest)
        {
            int ret = JumpL(dest.ToInt32() - _install.ToInt32(), true);
            _subs.Add(_data.Count - 4);
            return ret;
        }

        public int MovByteMIR(IntPtr dest, byte value)
        {
            int ret = _data.Count;
            _data.Add(0xC6); // Mov r/m8, imm8
            _data.Add(0x05); // disp32, /0
            DataInt32(dest.ToInt32()); // offset
            DataByte(value); // value
            return ret;
        }

        public int MovByteMIL(int dest, byte value)
        {
            int ret = MovByteMIR(_install + dest, value);
            _adds.Add(_data.Count - 5);
            return ret;
        }

        public int Retn()
        {
            int ret = _data.Count;
            _data.Add(0xC3); // retn
            return ret;
        }

        public int Retn(short offset)
        {
            if (offset == 0) return Retn();
            int ret = _data.Count;
            _data.Add(0xC2); // retn imm16
            DataInt16(offset); // offset
            return ret;
        }

        // Private Helpers

        private void AddOffset(int offset, int addr)
        {
            int value = BitConverter.ToInt32(_data.ToArray(), addr) + offset;
            foreach (byte b in BitConverter.GetBytes(value)) _data[addr++] = b;
        }
    }
}
