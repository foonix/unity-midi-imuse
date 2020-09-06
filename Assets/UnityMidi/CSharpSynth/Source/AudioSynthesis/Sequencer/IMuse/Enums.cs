using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioSynthesis.Sequencer.IMuse
{
    internal enum IMuseSysExType
    {
        AllocatePart = 0x0,
        ShutdownPart = 0x1,
        SongStart = 0x2,
        AdlibInstrumentDefinitionPart = 0x10,
        AdlibInstrumentDefinitionGlobal = 0x11,
        ParameterAdjust = 0x21,
        HookJump = 0x30,
        HookGlobalTranspose = 0x31,
        HookPartSetActive = 0x32,
        HookSetVolume = 0x33,
        HookSetProgram = 0x34,
        HookSetTranspose = 0x35,
        Marker = 0x40,
        SetLoop = 0x50,
        ClearLoop = 0x51,
        SetInstrument = 0x60,
    }

    internal enum SysExVendor
    {
        IMuse = 0x7D,
    }

    public enum MidiFileType
    {
        GMID,
        RLND,
        ADLB
    }
}
