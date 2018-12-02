using System;
using kOS.Safe.Compilation;
using System.Collections.Generic;

namespace kOS.Safe.DataStructures {
    /// <summary>
    /// This list extends List and implements the IReadOnlyOpcodes interface, 
    /// which only allows access, not modification of the OpcodeList.
    /// This is useful because after a certain point, no changes should be made
    /// to the list holding the opcodes because it will be shared by multiple
    /// Procedures at runtime and any changes will destroy the assumptions
    /// of the instructionPointer.
    /// 
    /// So in Procedure and ProcedureCall, a reference to an IReadOnlyOpcodes
    /// is stored, and passed an OpcodeList object.
    /// </summary>
    public class OpcodeList:List<Opcode>,IReadOnlyOpcodeList {}
}
