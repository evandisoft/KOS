using System;
using kOS.Safe.Execution;
using kOS.Safe.Compilation;
using System.Collections.Generic;
using kOS.Safe.Encapsulation;
using kOS.Safe.Utilities;
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
        public bool Continue(){
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

        public ProcessManager(SafeSharedObjects safeSharedObjects) : base(safeSharedObjects) {
            Init();
        }

        public void Init() {
            processes.Clear();
            var newProcess = new InterpreterProcess(this);
            processes.Push(newProcess);
            InterpreterProcess = newProcess;
        }

        public bool debugging = false;
        override internal void ContinueExecution(bool doProfiling)
        {
            Deb.EnqueueExec("ContinueExecution", "Processes", processes.Count);

            EnableOrDisableDebugging();

            Deb.EnqueueExec("Executing Process", CurrentProcess.ID);
            CurrentProcess.Execute();
            Deb.EnqueueExec("Process has status", CurrentProcess.Status);

            switch (CurrentProcess.Status) {
            case ProcessStatus.FINISHED:
            case ProcessStatus.ERROR:
                CurrentProcess.FlyByWire.DisableActiveFlyByWire();
                PopIfCurrentProcessNotInterpreter();
                break;
            case ProcessStatus.GLOBAL_INSTRUCTION_LIMIT:
                CurrentProcess.Status = ProcessStatus.OK;
                break;
            }

            Deb.EnqueueExec("Process has status", CurrentProcess.Status);
        }

        void PopIfCurrentProcessNotInterpreter() {
            if (!InterpreterIsCurrent()) {
                CurrentProcess.FlyByWire.DisableActiveFlyByWire();
                Deb.EnqueueExec("Removing process", CurrentProcess.ID);
                processes.Pop();
            }
            CurrentProcess.FlyByWire.EnableActiveFlyByWire();
        }

        public void RunInInterpreter(Procedure Program, List<object> args) {
            KOSThread thread = new KOSThread(InterpreterProcess);
            InterpreterProcess.AddThread(thread);
            thread.CallWithArgs(Program, args);
            InterpreterProcess.Status = ProcessStatus.OK;
        }

        public void RunInNewProcess(Procedure Program, List<object> args) {
            KOSProcess process = new KOSProcess(this);
            CurrentProcess.FlyByWire.DisableActiveFlyByWire();
            processes.Push(process);
            KOSThread thread = new KOSThread(process);
            process.AddThread(thread);
            thread.CallWithArgs(Program, args);
        }


        public bool InterpreterIsCurrent() {
            return InterpreterProcess == CurrentProcess;
        }

        /// <summary>
        /// Enables or disables debugging based on whether ExecutionIsActive
        /// </summary>
        public void EnableOrDisableDebugging() {
            if (!debugging && CurrentProcess.Status==ProcessStatus.OK) {
                Deb.RawLog("Starting debugging");
                Deb.EnableLogging();
                debugging = true;
            } 
            else if(debugging && CurrentProcess.Status != ProcessStatus.OK) {
                Deb.RawLog("Stopping debugging");
                Deb.DisableLogging();
                Deb.LogQueues();
                debugging = false;
            }
        }

        /// <summary>
        /// Doesn't yet handle removing toggleflybywire properly.
        /// </summary>
        /// <param name="manual">If set to <c>true</c> manual.</param>
        public override void BreakExecution(bool manual) {
            if (debugging) {
                Deb.LogQueues();
            }
            debugging = false;
            CurrentProcess.FlyByWire.DisableActiveFlyByWire();
            InterpreterProcess.FlyByWire.DisableActiveFlyByWire();
            Init();
        }

        public override void Boot() {
            Init();
            globalVariables.Clear();
            if (shared.GameEventDispatchManager != null) shared.GameEventDispatchManager.Clear();

            PushInterpreterContext();
            currentTime = 0;
            // clear stack (which also orphans all local variables so they can get garbage collected)
            //stack.Clear();
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

                shared.VolumeMgr.SwitchTo(shared.VolumeMgr.GetVolume(0));
                if(file==null)
                {
                    SafeHouse.Logger.Log(string.Format("Boot file \"{0}\" is missing, skipping boot script", path));
                }
                else {
                    var bootContext = "program";
                    shared.ScriptHandler.ClearContext(bootContext);
                    //IProgramContext programContext = SwitchToProgramContext();
                    //programContext.Silent = true;

                    string bootCommand = string.Format("run \"{0}\".", file.Path);

                    var options = new CompilerOptions {
                        LoadProgramsInSameAddressSpace = false,
                        FuncManager = shared.FunctionManager,
                        IsCalledFromRun = false
                    };

                    List<CodePart> commandParts =
                        shared.ScriptHandler.Compile(
                        new BootGlobalPath(bootCommand), 1, bootCommand, "program", options);
                    debugging = true;
                    
                    RunInInterpreter(ProgramBuilder2.BuildProgram(commandParts),new List<object>());
                    //YieldProgram(YieldFinishedCompile.RunScript(new BootGlobalPath(bootCommand), 1, bootCommand, bootContext, options));

                }
            }
        }

    }
}
