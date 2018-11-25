using System.Collections.Generic;
using kOS.Safe.Compilation;

namespace kOS.Communication
{
	// I don't think this is actually being used by anyone
    public class RunCommand : InterProcCommand
    {
        public List<Opcode> Program;

        public override void Execute(SharedObjects shared)
        {
            if (shared.Cpu != null)
            {
                shared.Cpu.RunProgram(Program);
            }
        }
    }
}
