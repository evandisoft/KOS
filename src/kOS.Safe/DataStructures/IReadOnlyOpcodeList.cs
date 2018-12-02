using System;
using kOS.Safe.Compilation;

namespace kOS.Safe.DataStructures {
    /// <summary>
    /// Intended to allow the subset of generic List Opcode that is readonly.
    /// Here I have implemented only access to count and index access.
    /// Any other readonly methods of List can be added here. Methods not
    /// available in List can be added here too, provided they are readonly, but
    /// you then have to provide an implementation in OpcodeList.
    /// </summary>
    public interface IReadOnlyOpcodeList {
        Opcode this[int index]{
            get;
        }
        int Count { get; }
    }
}
