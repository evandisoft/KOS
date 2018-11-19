using System;
using kOS.Safe.Execution;
using coll = System.Collections.Generic;
using System.Collections.Generic;

namespace kOS.Safe
{
    public class ProcessManager:CPU
    {
        List<Process> processes = new List<Process> ();

        public ProcessManager(SafeSharedObjects safeSharedObjects):base(safeSharedObjects)
        {
            
        }

        override internal void ContinueExecution(bool doProfiling){
            // TODO: this is just "getting started" code
            // it will be replaced later.
            if (processes.Count==0){
                Process process = new Process();
                processes.Add(process);
                KOSThread kOSThread = new KOSThread();
                process.AddThread(kOSThread);
                ProcedureCall procedureCall = new ProcedureCall(GetCurrentContext().Program);
                kOSThread.AddProcedureCall (procedureCall);
            }

            for (int i = processes.Count;i>= 0;i--) {
                var status = processes[i].Execute();

                switch (status) {

                case ProcessStatus.Finished:
                    processes.RemoveAt(i);
                    break;

                }
            }
        }
    }
}
