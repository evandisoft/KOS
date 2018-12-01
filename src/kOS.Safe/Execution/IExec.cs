using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;
using System.Collections;

namespace kOS.Safe
{
    public interface IExec {
        KOSThread Thread { get; }
        KOSProcess Process { get; }
        ArgumentStack Stack { get; } // manages the argument stack
        VariableStore Store { get; } // manages variable storing and retrieval
        SafeSharedObjects Shared { get; }
        object PopValue(bool barewordOkay);
    }
}
