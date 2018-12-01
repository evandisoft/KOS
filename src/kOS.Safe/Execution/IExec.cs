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
        /// <summary>
        /// Gets the process manager.
        /// </summary>
        /// <value>The process manager.</value>
        ProcessManager ProcessManager { get; }
        /// <summary>
        /// Gets the current KOSProcess associated with this execution.
        /// </summary>
        /// <value>The process.</value>
        KOSProcess Process { get; }
        /// <summary>
        /// Gets the current KOSThread associated with this execution.
        /// </summary>
        /// <value>The thread.</value>
        KOSThread Thread { get; }
        /// <summary>
        /// Gets the argument stack.
        /// </summary>
        /// <value>The stack.</value>
        ArgumentStack Stack { get; } // manages the argument stack
        /// <summary>
        /// Gets the store. This manages storage of local and global variables.
        /// </summary>
        /// <value>The store.</value>
        VariableStore Store { get; } // manages variable storing and retrieval

        /// <summary>
        /// Pops the last argument off the stack. If it is a variable name, looks
        /// it up in the current Store, and returns its value. If it is a value,
        /// that value is returned directly.
        /// 
        /// "<paramref name="barewordOkay"/> currently does nothing.
        /// </summary>
        /// <returns>The value.</returns>
        object PopValue(bool barewordOkay=false);
    }
}
