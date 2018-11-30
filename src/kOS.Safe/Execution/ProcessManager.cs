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
            Deb.logmisc("ContinueExecution", "Processes", processes.Count);

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



        // Get all the labels so that we know what functions are defined.
        // (Anonymous functions are defined in maincode sections, unlike
        // regular functions, which each have their own codepart
        // and in that codepart they are placed in a FunctionCode section.)
        // So we cannot look for the functions first. We can only find
        // the functions after we've seen all the PushDelecateRelocateLater
        // opcodes
        static Boolean IsEndReturn(Opcode opcode)
        {
            return opcode.GetType()==typeof(OpcodeReturn)&&((OpcodeReturn)opcode).Depth==0;
        }
        static List<Opcode> GetDelegateOpcodes(IEnumerator<Opcode> opcodesEnumerator){
            List<Opcode> delegateOpcodes = new List<Opcode>();
            do {
                delegateOpcodes.Add(opcodesEnumerator.Current);
            } while (!IsEndReturn(opcodesEnumerator.Current)&&opcodesEnumerator.MoveNext());
            return delegateOpcodes;
        }
        static Dictionary<string,bool> 
        GetAllDelegateLabels(List<CodePart> parts)
        {
            var delegateLabels = 
                new Dictionary<string, bool>();
            foreach(var part in parts){
                foreach(var opcode in part.AllOpcodes){
                    if(opcode.Code==ByteCode.PUSHDELEGATERELOCATELATER){
                        var relopcode = opcode as OpcodePushDelegateRelocateLater;
                        delegateLabels.Add(opcode.DestinationLabel,relopcode.WithClosure);
                    }
                }
            }
            return delegateLabels;
        }
        static Dictionary<string,OpcodePushDelegate> 
        CreatePushDelegatesMap(List<CodePart> parts)
        {
            var delegateLabels = GetAllDelegateLabels(parts);
            var pushDelegatesMap = new Dictionary<string, OpcodePushDelegate>();
            foreach (var part in parts) {
                var opcodesEnumerator = part.AllOpcodes.GetEnumerator();
                while (opcodesEnumerator.MoveNext()) {
                    if(delegateLabels.TryGetValue(
                        opcodesEnumerator.Current.Label, out bool withClosure)){

                        var delegateOpcodes = GetDelegateOpcodes(opcodesEnumerator);
                        pushDelegatesMap.Add(
                            opcodesEnumerator.Current.Label,
                            new OpcodePushDelegate(
                                delegateOpcodes, withClosure));
                    }
                }
            }
            return pushDelegatesMap;
        }
        static void
        ReplaceRelocateDelegateOpcodes(
            List<Opcode> opcodes,
            Dictionary<string, OpcodePushDelegate> pushDelegatesMap
        )
        {
            for (int i = 0;i<opcodes.Count;i++){
                if(opcodes[i].Code==ByteCode.PUSHDELEGATERELOCATELATER){
                    if(pushDelegatesMap.TryGetValue(
                        opcodes[i].Label,
                        out OpcodePushDelegate pushDelegate
                    )){
                        opcodes[i]=pushDelegate;
                    } else{
                        throw new Exception(
                            "No OpcodePushDelegate found for label "+opcodes[i].Label);
                    }
                }
            }
        }

        static void
        ReplaceRelocateDelegateOpcodes(List<CodePart> parts){
            var pushDelegatesMap = CreatePushDelegatesMap(parts);

            var opcodePushDelegates = new List<OpcodePushDelegate>();

            foreach (var part in parts) {
                Deb.logcompile("Relocating Function opcodes");
                ReplaceRelocateDelegateOpcodes(part.FunctionsCode, pushDelegatesMap);
                Deb.logcompile("Relocating Initialization opcodes");
                ReplaceRelocateDelegateOpcodes(part.InitializationCode, pushDelegatesMap);
                Deb.logcompile("Relocating Mainprogram code");
                ReplaceRelocateDelegateOpcodes(part.MainCode, pushDelegatesMap);
            }
        }

        static public void PrintCodeParts(string message,List<CodePart> parts)
        {
            Deb.logcompile("Examining codeparts "+message);
            foreach (var part in parts) {
                Deb.logcompile("Function opcodes");
                foreach (var opcode in part.FunctionsCode) {
                    Deb.logcompile(opcode.Label, opcode);
                }
                Deb.logcompile("Initialization opcodes");
                foreach (var opcode in part.InitializationCode) {
                    Deb.logcompile(opcode.Label, opcode);
                }
                Deb.logcompile("Mainprogram code");
                foreach (var opcode in part.MainCode) {
                    Deb.logcompile(opcode.Label, opcode);
                }
            }
            Deb.logcompile("End of Codeparts "+message);
        }
        // This is only here temporarily. Can be put somewhere else later
        static public Procedure CreateProgramProcedure(List<CodePart> parts){
            PrintCodeParts("Before Relocate",parts);
            ReplaceRelocateDelegateOpcodes(parts);
            PrintCodeParts("After Relocate", parts);
            Deb.miscIsLogging=true;

            List<Opcode> mainProgram=null;
            foreach(var part in parts){
                if(part.MainCode.Count>0){
                    if(mainProgram!=null){
                        throw new Exception("More than one MainCode section!");
                    }
                    mainProgram=part.MainCode;
                }
            }
            if(mainProgram==null){
                throw new Exception("There was no MainCode section!");
            }
            //Deb.logmisc("creating new builder");
            //ProgramBuilder builder = new ProgramBuilder();
            //Deb.logmisc("adding parts");
            //builder.AddRange(parts);
            //Deb.logmisc("building program");
            //List<Opcode> newProgram = builder.BuildProgram();
            //Deb.logmisc("running program");

            //foreach (var opcode in newProgram) {
            //    (opcode as OpcodePushDelegate)?.EncapsulateProcedure(newProgram);
            //}
            //foreach (var opcode in newProgram) {
            //    Deb.logcompile(opcode.Label, opcode);
            //}
            return new Procedure(mainProgram);
        }

        // Encapsulate a compiled program, then create a process and thread for
        // it, and run it.
        public void RunProgram(Procedure Program,List<object> args=null){
            // log all the opcodes that were created into the compile.log

            // Instantiate the Procedures in all the OpcodePushDelegate's


            Deb.miscIsLogging=true;
            Deb.logmisc("Creating Dummy processes");
            KOSProcess process = new KOSProcess(this);
            processes.Add(process);
            KOSThread thread = new KOSThread(process);
            process.AddThread(thread);
            thread.CallWithArgs(Program,args);

            debugging=true;
        }

        public void IfNotActiveStopDebugging(){
            if (debugging && processes.Count==0) {
                Deb.logmisc("Resetting program");

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
