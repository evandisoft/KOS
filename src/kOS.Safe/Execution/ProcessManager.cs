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

        /// <summary>
        /// Resets the instructionsPerUpdate to 
        /// SafeHouse.Config.InstructionsPerUpdate and resets the
        /// executionCounter to 0.
        /// </summary>
        public void Reset(){
            executionCounter=0;
            instructionsPerUpdate=SafeHouse.Config.InstructionsPerUpdate;
        }


        /// <summary>
        /// Returns true if there are more instructions to execute this
        /// update and increments the counter.
        /// </summary>
        public Boolean Continue(){
            return instructionsPerUpdate>executionCounter++;
        }
    }

   

    /// <summary>
    /// Process manager. - Replacement for the CPU
    /// TODO: This is not really implemented fully yet.
    /// Currently, threads in a particular process will
    /// eat up all the rest of the GLOBAL_INSTRUCTION_LIMIT and prevent
    /// triggers in other processes from running on each update. But currently
    /// we are only using one process at a time, so this is not currently a
    /// problem.
    /// </summary>
    public class ProcessManager:CPU
    {
        readonly Stack<KOSProcess> processes = new Stack<KOSProcess> ();
        internal InstructionCounter GlobalInstructionCounter = new InstructionCounter();
        internal KOSProcess CurrentProcess => processes.Peek();
        internal KOSProcess InterpreterProcess;

        public ProcessManager(SafeSharedObjects safeSharedObjects):base(safeSharedObjects)
        {
            var newProcess = new KOSProcess(this);
            processes.Push(newProcess);
            InterpreterProcess = newProcess;
        }


        public bool debugging = false;
        override internal void ContinueExecution(bool doProfiling)
        {
            Deb.EnqueueExec("ContinueExecution", "Processes", processes.Count);


            // If there are no processes being ran, stop displaying debug
            // information.
            IfNotActiveStopDebugging();

            Deb.EnqueueExec("Executing Process", CurrentProcess.ID);
            CurrentProcess.Execute();

            switch (CurrentProcess.Status) {
            case ProcessStatus.FINISHED:
                PopIfCurrentProcessNotInterpreter();
                return;
            case ProcessStatus.ERROR:
                PopIfCurrentProcessNotInterpreter();
                return;
            case ProcessStatus.WAIT:
                return;
            case ProcessStatus.GLOBAL_INSTRUCTION_LIMIT:
                return;
            }


        }

        void PopIfCurrentProcessNotInterpreter() {
            if (!InterpreterIsCurrent()) {
                Deb.EnqueueExec("Removing process", CurrentProcess.ID);
                processes.Pop();
            }
        }

        public void RunInInterpreter(Procedure Program, List<object> args = null) {
            KOSThread thread = new KOSThread(InterpreterProcess);
            InterpreterProcess.AddThread(thread);
            thread.CallWithArgs(Program, args);

            debugging = true;
        }

        public void RunInNewProcess(Procedure Program, List<object> args = null) {
            KOSProcess process = new KOSProcess(this);
            processes.Push(process);
            KOSThread thread = new KOSThread(process);
            process.AddThread(thread);
            thread.CallWithArgs(Program, args);

            debugging = true;
        }

        public bool InterpreterIsCurrent() {
            return InterpreterProcess == CurrentProcess;
        }

        public bool ExecutionIsActive() {
            if (!InterpreterIsCurrent()) {
                return true;
            }
            switch (CurrentProcess.Status) {
            case ProcessStatus.OK:
            case ProcessStatus.WAIT:
                return true;
            }
            return false;
        }



        public void IfNotActiveStopDebugging(){
            if (debugging && CurrentProcess.Status!=ProcessStatus.OK) {
                Deb.RawLog("Stopping debugging");
                Deb.LogQueues();
                debugging=false;
                Deb.DisableLogging();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="manual">If set to <c>true</c> manual.</param>
        public override void BreakExecution(bool manual) {
            processes.Clear();
            if (debugging) {
                Deb.LogQueues();
            }

            debugging = false;
            Deb.DisableLogging();

        }

        public void Boot() {
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
            } else {
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

    }
}
