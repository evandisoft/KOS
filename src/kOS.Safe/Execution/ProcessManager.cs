using System;
using kOS.Safe.Execution;
using kOS.Safe.Compilation;
using coll = System.Collections.Generic;
using System.Collections.Generic;
using kOS.Safe.Encapsulation;
using kOS.Safe.Utilities;

namespace kOS.Safe
{
    public class InstructionCounter {
        public int executionCounter;
        public int instructionsPerUpdate;

        public void Reset(){
            executionCounter=0;
            instructionsPerUpdate=SafeHouse.Config.InstructionsPerUpdate;
        }

        public Boolean Continue(){
            return instructionsPerUpdate>executionCounter++;
        }
    }


    public class ProcessManager:CPU
    {
        readonly List<KOSProcess> processes = new List<KOSProcess> ();
        internal InstructionCounter GlobalInstructionCounter = new InstructionCounter();

        public ProcessManager(SafeSharedObjects safeSharedObjects):base(safeSharedObjects)
        {
            
        }

        Boolean running = false;
        override internal void ContinueExecution(bool doProfiling)
        {
            Deb.logmisc("ContinueExecution", "Processes", processes.Count,
                         "Program", GetCurrentContext().Program.Count,
                         "Program pointer", GetCurrentContext().InstructionPointer);

            // TODO: this is just "getting started" code
            // it will be replaced later.
            // If there are no processes being ran, stop displaying debug
            // information.
            IfNotActiveStopDebugging();

            for (int i = processes.Count-1;i>= 0;i--) {
                Deb.logmisc("i", i, "total", processes.Count);
                var status = processes[i].Execute();
                Deb.logmisc("From Process Execute. status", status);

                switch (status) {

                case ProcessStatus.FINISHED:
                    Deb.logmisc("Removing process", i);
                    processes.RemoveAt(i);
                    break;

                }
            }
        }

        // Encapsulate a compiled program, then create a process and thread for
        // it, and run it.
        public void RunProgram(Procedure Program){
            // log all the opcodes that were created into the compile.log

            foreach (var opcode in Program.Opcodes) {
                Deb.logcompile(opcode.Label, opcode);
            }
            // Instantiate the Procedures in all the OpcodePushDelegate's
            foreach (var opcode in Program.Opcodes) {
                (opcode as OpcodePushDelegate)?.EncapsulateProcedure(Program.Opcodes);
            }

            Deb.miscIsLogging=true;
            Deb.logmisc("Creating Dummy processes");
            KOSProcess process = new KOSProcess(this);
            processes.Add(process);
            KOSThread thread = new KOSThread(process);
            process.AddThread(thread);
            thread.Call(Program);
            running=true;
        }

        public void IfNotActiveStopDebugging(){
            if (running && processes.Count==0) {
                Deb.logmisc("Resetting program");

                Opcode opcode = new OpcodeEOF();
                running=false;
                Deb.miscIsLogging=false;
                Deb.clearOpcodeFile();
                foreach (var currentOpcode in CPU.OpcodeLogQueue) {
                    Deb.logopcode(currentOpcode.Label, currentOpcode); // evandisoft
                }
                CPU.OpcodeLogQueue.Clear();
            }
        }
    }
}
