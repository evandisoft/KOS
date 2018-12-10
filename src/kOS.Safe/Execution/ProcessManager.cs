using System;
using kOS.Safe.Execution;
using kOS.Safe.Compilation;
using System.Collections.Generic;
using kOS.Safe.Encapsulation;
using kOS.Safe.Utilities;
using kOS.Safe.Persistence;
using System.Linq;

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
    /// </summary>
    public class ProcessManager:CPU
    {
        readonly Dictionary<string, Procedure> compiledPrograms = new Dictionary<string, Procedure>();
        public readonly Dictionary<GlobalPath, bool> ranPrograms = new Dictionary<GlobalPath, bool>();
        readonly Stack<KOSProcess> processes = new Stack<KOSProcess>();
        internal InstructionCounter GlobalInstructionCounter = new InstructionCounter();
        internal KOSProcess CurrentProcess => processes.Peek();
        internal KOSProcess InterpreterProcess;

        public ProcessManager(SafeSharedObjects safeSharedObjects) : base(safeSharedObjects) {
            Init();
        }

        public void Init() {
            Deb.RawLog("Initializing ProcessManager");
            foreach (var process in processes) {
                process.PrepareForDisposal();
            }
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
            case ProcessStatus.SHUTDOWN:
                Init();
                return;
            case ProcessStatus.FINISHED:
            case ProcessStatus.ERROR:
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
                CurrentProcess.PrepareForDisposal();
                Deb.EnqueueExec("Removing process", CurrentProcess.ID);
                processes.Pop();
                CurrentProcess.FlyByWire.EnableActiveFlyByWire();
            } 
        }

        /// <summary>
        /// Creates a new thread, puts the Program into the thread, and
        /// puts the thread into the interpreter process.
        /// </summary>
        /// <param name="Program">Program.</param>
        /// <param name="args">Arguments.</param>
        public void RunInInterpreter(Procedure Program, List<object> args) {
            KOSThread thread = new KOSThread(InterpreterProcess);
            InterpreterProcess.AddThread(thread);
            thread.CallWithArgs(Program, args);
            InterpreterProcess.Status = ProcessStatus.OK;
        }

        /// <summary>
        /// Creates a new process, a new thread, and places the Program into
        /// the thread, and the thread into the process.
        /// </summary>
        /// <param name="Program">Program.</param>
        /// <param name="args">Arguments.</param>
        public void RunInNewProcess(Procedure Program, List<object> args) {
            // Making the behavior same as the old version.
            SaveAndClearPointers();
            KOSProcess process = new KOSProcess(this);
            CurrentProcess.FlyByWire.DisableActiveFlyByWire();
            processes.Push(process);
            KOSThread thread = new KOSThread(process);
            process.AddThread(thread);
            thread.CallWithArgs(Program, args);
        }

        private void SaveAndClearPointers() {
            // Any global variable that ends in an asterisk (*) is a system pointer
            // that shouldn't be inherited by other program contexts.  These sorts of
            // variables should only exist for the current program context.
            // This method stashes all such variables in a storage area for the program
            // context, then clears them.  The stash can be used later by RestorePointers()
            // to bring them back into existence when coming back to this program context again.
            // Pointer variables include:
            //   IP jump location for subprograms.
            //   IP jump location for functions.
            savedPointers = new VariableScope(0, null);
            var pointers = new List<KeyValuePair<string, Variable>>(globalVariables.Locals.Where(entry => StringUtil.EndsWith(entry.Key, "*")));

            foreach (var entry in pointers) {
                savedPointers.Add(entry.Key, entry.Value);
                globalVariables.Remove(entry.Key);
            }
            SafeHouse.Logger.Log(string.Format("Saving and removing {0} pointers", pointers.Count));
        }

        public bool InterpreterIsCurrent() {
            return InterpreterProcess == CurrentProcess;
        }

        /// <summary>
        /// Enables or disables debugging based on whether execution is active.
        /// If it is not active, we don't want to store meaningless debug info.
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
        /// Breaks the Execution.
        /// </summary>
        /// <param name="manual">If set to <c>true</c> manual.</param>
        public override void BreakExecution(bool manual) {
            Deb.RawLog("Execution Broken");
            Init();
            if (manual) {
                Deb.RawLog("Manual Break.");

            } 
            if (debugging) {
                Deb.LogQueues();
            }
            debugging = false;
        }

        public override void KOSFixedUpdate(double deltaTime) {
            bool showStatistics = SafeHouse.Config.ShowStatistics;

            currentTime = shared.UpdateHandler.CurrentFixedTime;

            try {
                PreUpdateBindings();

                ContinueExecution(showStatistics);

                PostUpdateBindings();
            } catch (Exception e) {
                if (shared.Logger != null) {
                    shared.Logger.Log(e);
                    SafeHouse.Logger.Log(stack.Dump());
                }
                if (shared.SoundMaker != null) {
                    // Stop all voices any time there is an error, both at the interpreter and in a program
                    shared.SoundMaker.StopAllVoices();
                }

                BreakExecution(false);
            }
        }

        public override void Boot() {
            Init();
            globalVariables.Clear();
            if (shared.GameEventDispatchManager != null) shared.GameEventDispatchManager.Clear();

            //PushInterpreterContext();
            currentTime = 0;
            // clear stack (which also orphans all local variables so they can get garbage collected)

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

                //shared.VolumeMgr.SwitchTo(shared.VolumeMgr.GetVolume(0));
                if(file==null)
                {
                    SafeHouse.Logger.Log(string.Format("Boot file \"{0}\" is missing, skipping boot script", path));
                }
                else {
                    var bootContext = "program";
                    shared.ScriptHandler.ClearContext(bootContext);

                    string bootCommand = string.Format("run \"{0}\".", file.Path);

                    var options = new CompilerOptions {
                        LoadProgramsInSameAddressSpace = false,
                        FuncManager = shared.FunctionManager,
                        IsCalledFromRun = false
                    };

                    Procedure program =
                        shared.ScriptHandler.Compile(
                        new BootGlobalPath(bootCommand), 1, bootCommand, "program", options);
                    //debugging = true;

                    RunInInterpreter(program,new List<object>());
                }
            }
        }
    }
}
