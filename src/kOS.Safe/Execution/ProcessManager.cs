using System;
using kOS.Safe.Execution;
using kOS.Safe.Compilation;
using coll = System.Collections.Generic;
using System.Collections.Generic;

namespace kOS.Safe
{
    public class ProcessManager:CPU
    {
        readonly List<KOSProcess> processes = new List<KOSProcess> ();

        public ProcessManager(SafeSharedObjects safeSharedObjects):base(safeSharedObjects)
        {
            
        }

        Boolean running = false;
        override internal void ContinueExecution(bool doProfiling){
            Deb.logmisc ("ContinueExecution","Processes",processes.Count,
                         "Program", GetCurrentContext().Program.Count,
                         "Program pointer",GetCurrentContext().InstructionPointer);

            // TODO: this is just "getting started" code
            // it will be replaced later.
            if (!running && processes.Count==0 && GetCurrentContext().Program.Count>1
               ){
                foreach(var opcode in GetCurrentContext().Program){
                    Deb.logcompile(opcode.Label, opcode);
                }
                Deb.clearMiscLog();
                Deb.miscIsLogging=true;
                Deb.logmisc("Creating Dummy processes");
                KOSProcess process = new KOSProcess(this);
                processes.Add(process);
                KOSThread thread = new KOSThread(process);
                process.AddThread(thread);
                ProcedureExec exec = new ProcedureExec(thread, GetCurrentContext().Program);
                thread.AddProcedureExec(exec);
                running=true;
            } else if(running && processes.Count==0 && GetCurrentContext().Program.Count>1) {
                Deb.logmisc("Resetting program");

                Opcode opcode = new OpcodeEOF();
                GetCurrentContext().Program=new List<Opcode> { opcode };
                GetCurrentContext().InstructionPointer=0;
                running=false;
                Deb.miscIsLogging=false;
            }

            for (int i = processes.Count-1;i>= 0;i--) {
                Deb.logmisc ("i", i, "total", processes.Count);
                var status = processes[i].Execute();
                Deb.logmisc ("From Process Execute. status", status);

                switch (status) {

                case ProcessStatus.Finished:
                    Deb.logmisc ("Removing process", i);
                    processes.RemoveAt(i);
                    break;

                }
            }


        }
    }
}
