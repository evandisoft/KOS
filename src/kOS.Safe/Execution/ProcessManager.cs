using System;
using kOS.Safe.Execution;
using kOS.Safe.Compilation;
using coll = System.Collections.Generic;
using System.Collections.Generic;
using kOS.Safe.Encapsulation;
using kOS.Safe.Utilities;
using kOS.Safe.Binding;
using kOS.Safe.Callback;
using kOS.Safe.Exceptions;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Debug = kOS.Safe.Utilities.Debug;
using kOS.Safe.Persistence;

namespace kOS.Safe
{
    public class InstructionCounter {
        public int executionCounter;
        public int instructionsPerUpdate;
        public InstructionCounter(){
            Reset();
        }

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

        public  void Boot(){
            globalVariables.Clear();
            //if (shared.GameEventDispatchManager != null) shared.GameEventDispatchManager.Clear();

            PushInterpreterContext();
            currentTime = 0;
            // clear stack (which also orphans all local variables so they can get garbage collected)
            stack.Clear();
            if (shared.Interpreter != null) shared.Interpreter.Reset();
            // load functions
            if (shared.FunctionManager != null) shared.FunctionManager.Load();
            // load bindings
            if (shared.BindingMgr != null) shared.BindingMgr.Load();
            if (shared.Screen != null) {
                shared.Screen.ClearScreen();
                string bootMessage = string.Format("kOS Operating System\n" + "KerboScript v{0}\n(manual at {1})\n \n" + "Proceed.\n",
                                                   SafeHouse.Version, SafeHouse.DocumentationURL);
                shared.Screen.Print(bootMessage);
            }
            if (!shared.Processor.CheckCanBoot()) return;
            VolumePath path = shared.Processor.BootFilePath;
            // Check to make sure the boot file name is valid, and then that the boot file exists.
            if (path == null) {
                SafeHouse.Logger.Log("Boot file name is empty, skipping boot script");
            } 
            else {
                // Boot is only called once right after turning the processor on,
                // the volume cannot yet have been changed from that set based on
                // Config.StartOnArchive, and Processor.CheckCanBoot() has already
                // handled the range check for the archive.
                Volume sourceVolume = shared.VolumeMgr.CurrentVolume;
                var file = shared.VolumeMgr.CurrentVolume.Open(path);
                if (file == null) {
                    SafeHouse.Logger.Log(string.Format("Boot file \"{0}\" is missing, skipping boot script", path));
                }

                //shared.VolumeMgr.SwitchTo(shared.VolumeMgr.GetVolume(0));
                //else {
                //    var bootContext = "program";
                //    shared.ScriptHandler.ClearContext(bootContext);
                //    IProgramContext programContext = SwitchToProgramContext();
                //    programContext.Silent = true;

                //    string bootCommand = string.Format("run \"{0}\".", file.Path);

                //    var options = new CompilerOptions {
                //        LoadProgramsInSameAddressSpace = true,
                //        FuncManager = shared.FunctionManager,
                //        IsCalledFromRun = false
                //    };

                //    YieldProgram(YieldFinishedCompile.RunScript(new BootGlobalPath(bootCommand), 1, bootCommand, bootContext, options));

                //}
            }
        }

        Boolean debugging = false;
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
            debugging=true;
        }

        public void IfNotActiveStopDebugging(){
            if (debugging && processes.Count==0) {
                Deb.logmisc("Resetting program");

                Opcode opcode = new OpcodeEOF();
                debugging=false;
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
