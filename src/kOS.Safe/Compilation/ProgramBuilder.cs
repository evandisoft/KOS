using System;
using System.Collections.Generic;
using System.Linq;
using kOS.Safe.Compilation.KS;
using kOS.Safe.Execution;

namespace kOS.Safe.Compilation
{
    public class ProgramBuilder
    {
        //private evandisoft
        public readonly Dictionary<Guid, ObjectFile> objectFiles = new Dictionary<Guid, ObjectFile>();
        
        public static string BuiltInFakeVolumeId { get { return "[built-in]"; } }

        /// <summary>
        /// Creates a new ObjectFile with the parts provided
        /// </summary>
        /// <param name="parts">Collection of CodeParts generated by the compiler</param>
        /// <returns>Id of the new ObjectFile</returns>
        public Guid AddObjectFile(IEnumerable<CodePart> parts)
        {
            var objectFile = new ObjectFile(parts);

            objectFiles.Add(objectFile.Id, objectFile);
            return objectFile.Id;
        }

        public void AddRange(IEnumerable<CodePart> parts)
        {
            ObjectFile objectFile;
            
            if (objectFiles.Count == 0)
            {
                objectFile = new ObjectFile(parts);
                objectFiles.Add(objectFile.Id, objectFile);
            }
            else
            {
                objectFile = objectFiles.First().Value;
                objectFile.Parts.AddRange(parts);
            }
        }
        
        public List<Opcode> BuildProgram()
        {
            var program = new List<Opcode>();

            foreach (var objectFile in objectFiles.Values)
            {
                var linkedObject = new CodePart();

                // we assume that the first object is the main program and the rest are subprograms/libraries
                bool isMainProgram = (objectFile == objectFiles.Values.First());

                bool firstPart = true;
                foreach (var part in objectFile.Parts)
                {
                    AddInitializationCode(linkedObject, part);

                    // Only do this once on the first pass of the first program:
                    if (isMainProgram && firstPart)
                        linkedObject.FunctionsCode.AddRange(BuildBoilerplateLoader());

                    linkedObject.FunctionsCode.AddRange(part.FunctionsCode);
                    linkedObject.MainCode.AddRange(part.MainCode);
                    firstPart = false;
                }

                // add a jump to the entry point so the execution skips the functions code
                if (isMainProgram)
                    AddJumpToEntryPoint(linkedObject);
                // add an instruction to indicate the end of the program
                AddEndOfProgram(linkedObject, isMainProgram);
                // save the entry point of the object
                objectFile.EntryPointLabel = GetEntryPointLabel(linkedObject);
                // add the linked object to the final program
                program.AddRange(linkedObject.MergeSections());
            }
                       
            // replace all the labels references with the corresponding address
            ReplaceLabels(program);

            return program;
        }
        
        private int labelCounter;
        private string labelFormat;
        private string nextLabel
        {
            get
            {
                return String.Format(labelFormat, labelCounter++);
            }
        }

        protected IEnumerable<Opcode> BuildBoilerplateLoader()
        {
            List<Opcode> boilerplate = new List<Opcode>();
            
            InternalPath path = new BuiltInPath();
            
            // First label of the load/runner will be called "@LR00", which is important because that's
            // what we hardcode all the compiler-built VisitRunStatement's to call out to:
            labelCounter = 0;
            labelFormat = "@LR{0:D2}";
            boilerplate.Add(new OpcodePushScope(-999,0) {Label = nextLabel, SourcePath = path});

            // High level kerboscript function calls flip the argument orders for us, but
            // low level kRISC does not so the parameters have to be read in stack order:
            
            // store parameter 2 in a local name:
            boilerplate.Add(new OpcodeStoreLocal("$runonce") {Label = nextLabel, SourcePath = path});

            // store parameter 1 in a local name:
            boilerplate.Add(new OpcodeStoreLocal("$filename") {Label = nextLabel, SourcePath = path});
            
            // Unconditionally call load() no matter what.  load() will abort and return
            // early if the program was already compiled, and tell us that on the stack:
            boilerplate.Add(new OpcodePush(new kOS.Safe.Execution.KOSArgMarkerType()) {Label = nextLabel, SourcePath = path});
            boilerplate.Add(new OpcodePush("$filename") {Label = nextLabel, SourcePath = path});
            boilerplate.Add(new OpcodeEval() {Label = nextLabel, SourcePath = path});
            boilerplate.Add(new OpcodePush(true) {Label = nextLabel, SourcePath = path}); // the flag that tells load() to abort early if it's already loaded:
            boilerplate.Add(new OpcodePush(null) {Label = nextLabel, SourcePath = path});
            boilerplate.Add(new OpcodeCall("load()") {Label = nextLabel, SourcePath = path});

            // Stack now has the 2 return values of load():
            //    Topmost value is a boolean flag for whether or not the program was already loaded.
            //    Second-from-top value is the entry point into the program.

            // If load() didn't claim the program was already loaded, or if we aren't operating
            // in "once" mode, then jump to the part where we call the loaded program, else
            // fall through to a dummy do-nothing return for the "run once, but it already ran" case:
            Opcode branchFromOne = new OpcodeBranchIfFalse() {Label = nextLabel, SourcePath = path};
            boilerplate.Add(branchFromOne);
            boilerplate.Add(new OpcodePush("$runonce") {Label = nextLabel, SourcePath = path});
            Opcode branchFromTwo = new OpcodeBranchIfFalse() {Label = nextLabel, SourcePath = path};
            boilerplate.Add(branchFromTwo);
            boilerplate.Add(new OpcodePop() {Label = nextLabel, SourcePath = path}); // onsume the entry point that load() returned. We won't be calling it.
            boilerplate.Add(new OpcodePush(0) {Label = nextLabel, SourcePath = path});   // ---+-- The dummy do-nothing return.
            boilerplate.Add(new OpcodeReturn(1) {Label = nextLabel, SourcePath = path}); // ---'
            
            // Actually call the Program from its entry Point, which is now the thing left on top
            // of the stack from the second return value of load():
            Opcode branchTo = new OpcodeStoreLocal("$entrypoint") {Label = nextLabel, SourcePath = path};
            boilerplate.Add(branchTo);
            boilerplate.Add(new OpcodeCall("$entrypoint") {Label = nextLabel, SourcePath = path});
            
            boilerplate.Add(new OpcodePop() {Label = nextLabel, SourcePath = path});
            boilerplate.Add(new OpcodePush(0) {Label = nextLabel, SourcePath = path});
            boilerplate.Add(new OpcodeReturn(1) {Label = nextLabel, SourcePath = path});

            branchFromOne.DestinationLabel = branchTo.Label;
            branchFromTwo.DestinationLabel = branchTo.Label;

            return boilerplate;
        }
        
        protected class BuiltInPath : InternalPath
        {
            private string command;

            public BuiltInPath() : base(BuiltInFakeVolumeId)
            {

            }

            public override string Line(int line)
            {
                return command;
            }

            public override string ToString()
            {
                return "[built-in]";
            }
        }
        
        protected virtual void AddInitializationCode(CodePart linkedObject, CodePart part)
        {
            linkedObject.InitializationCode.AddRange(part.InitializationCode);
        }

        private void AddJumpToEntryPoint(CodePart linkedObject)
        {
            if (linkedObject.MainCode.Count <= 0) return;

            var jumpOpcode = new OpcodeBranchJump
            {
                DestinationLabel = GetEntryPointLabel(linkedObject)
            };

            linkedObject.FunctionsCode.Insert(0, jumpOpcode);
        }

        private string GetEntryPointLabel(CodePart linkedObject)
        {
            List<Opcode> codeSection = linkedObject.InitializationCode.Count > 0 ? linkedObject.InitializationCode : linkedObject.MainCode;
            return codeSection[0].Label;
        }

        protected virtual void AddEndOfProgram(CodePart linkedObject, bool isMainProgram)
        {
            // possible refactor: this logic needs to be moved into the compiler
            // itself eventually, so that we can make an "exit" statement.  As it stands,
            // the fact that the final exit code is only dealt with here outside the
            // compiler, and the fact that it changes depending on if it's called from
            // the interpreter or from another program (the interpreter doesn't expect an exit
            // code, and won't pop it, which is the reason for this if/else below), is
            // what makes that non-trivial.
            if (isMainProgram)
            {
                linkedObject.MainCode.Add(new OpcodePop()); // to consume the argbottom mark.
                linkedObject.MainCode.Add(new OpcodeEOP());
            }
            else
            {
                linkedObject.MainCode.Add(new OpcodePush(0)); // all Returns now need a dummy return value on them.
                linkedObject.MainCode.Add(new OpcodeReturn(0));
            }
        }

        private void ReplaceLabels(List<Opcode> program)
        {            
            var labels = new Dictionary<string, int>();

            // get the index of every label
            for (int index = 0; index < program.Count; index++)
            {
                if (program[index].Label != string.Empty)
                {
                    if (labels.ContainsKey(program[index].Label))
                    {
                        if (program[index].Label.EndsWith("-default"))
                            continue;
                        // This is one of those "should never happen" errors that if it happens
                        // it means kOS devs screwed up - so dump the partially relabeled program
                        // to the log just to help in diagnosing the bug report that may happen:
                        //
                        Utilities.SafeHouse.Logger.LogError("=====Relabeled Program so far is: =========");
                        Utilities.SafeHouse.Logger.LogError(Utilities.Debug.GetCodeFragment(program));

                        throw new Exceptions.KOSCompileException(LineCol.Unknown(), string.Format(
                            "ProgramBuilder.ReplaceLabels: Cannot add label {0}, label already exists.  Opcode: {1}", program[index].Label, program[index].ToString()));
                    }
                    labels.Add(program[index].Label, index);
                }
            }
 
            // replace destination labels with the corresponding index
            for (int index = 0; index < program.Count; index++)
            {
                Opcode opcode = program[index];
                if (string.IsNullOrEmpty(opcode.DestinationLabel)) continue;

                if (!labels.ContainsKey(opcode.DestinationLabel))
                {
                    Utilities.SafeHouse.Logger.LogError("=====Relabeled Program so far is: =========");
                    Utilities.SafeHouse.Logger.LogError(Utilities.Debug.GetCodeFragment(program));

                    throw new Exceptions.KOSCompileException(LineCol.Unknown(), string.Format(
                        "ProgramBuilder.ReplaceLabels: Cannot find label {0}.  Opcode: {1}", opcode.DestinationLabel, opcode.ToString()));

                }
                int destinationIndex = labels[opcode.DestinationLabel];
                if (opcode is BranchOpcode)
                {
                    ((BranchOpcode)opcode).Distance = destinationIndex - index;
                }
                else if (opcode is OpcodePushRelocateLater)
                {
                    // Replace the OpcodePushRelocateLater with the proper OpcodePush:
                    Opcode newOp;
                    if (opcode is OpcodePushDelegateRelocateLater)
                    {
                        newOp = new OpcodePushDelegate(destinationIndex, ((OpcodePushDelegateRelocateLater)opcode).WithClosure);
                    }
                    else
                        newOp = new OpcodePush(destinationIndex);
                    newOp.SourcePath = opcode.SourcePath;
                    newOp.SourceLine = opcode.SourceLine;
                    newOp.SourceColumn = opcode.SourceColumn;
                    newOp.Label = opcode.Label;
                    program[index] = newOp;
                }
                else if (opcode is OpcodeCall)
                {
                    ((OpcodeCall)opcode).Destination = destinationIndex;
                }
            }

            // complete the entry point address of all the objects
            foreach (var objectFile in objectFiles.Values)
            {
                if (objectFile.EntryPointLabel != string.Empty)
                    objectFile.EntryPointAddress = labels[objectFile.EntryPointLabel];
            }
        }

        // private evandisoft
        public int GetObjectFileEntryPointAddress(Guid objectFileId)
        {
            return objectFiles.ContainsKey(objectFileId) ? objectFiles[objectFileId].EntryPointAddress : 0;
        }

        //private evandisoft
       public class ObjectFile
        {
            public Guid Id { get; private set; }
            public List<CodePart> Parts { get; private set; }
            public string EntryPointLabel { get; set; }
            public int EntryPointAddress { get; set; }

            public ObjectFile(IEnumerable<CodePart> parts)
            {
                Id = Guid.NewGuid();
                
                Parts = parts.ToList();

            }
        }

    }
}
