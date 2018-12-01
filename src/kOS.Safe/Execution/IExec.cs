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
    /// <summary>
    /// Interface to be passed to Opcodes, allowing them to access or execute
    /// Anything they need.
    /// </summary>
    public interface IExec {
        SafeSharedObjects Shared { get; }
        ProcessManager ProcessManager { get; }
        KOSProcess Process { get; }
        KOSThread Thread { get; }
        ArgumentStack Stack { get; } // manages the argument stack
        VariableStore Store { get; } // manages variable storing and retrieval
        object PopValue(bool barewordOkay=false);
    }
}
