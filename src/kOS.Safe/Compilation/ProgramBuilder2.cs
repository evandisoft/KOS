using System;
using System.Collections.Generic;
using kOS.Safe.Encapsulation;
using kOS.Safe.DataStructures;

namespace kOS.Safe.Compilation{
    public class ProgramBuilder2 {
        static public Procedure BuildProgram(List<CodePart> parts){
            PrintCodeParts("Before Relocate and Jump Labels", parts);

            // A map of destinationlabels to pushdelegate's containing
            // their proper opcodes.
            // This will be used to replace all the pushdelecaterelocatelater
            // opcodes with the proper pushdelegate opcodes.
            var pushDelegatesMap = CreatePushDelegatesMap(parts);

            List<Opcode> mainProgram = null;
            foreach (var part in parts) {
                if (part.MainCode.Count>0) {
                    if (mainProgram!=null) {
                        throw new Exception("More than one MainCode section!");
                    }
                    mainProgram=part.MainCode;
                }
            }
            if (mainProgram==null) {
                throw new Exception("There was no MainCode section!");
            }
            mainProgram.Add(new OpcodeEOP());

            var procedureOpcodes = new OpcodeList();
            procedureOpcodes.AddRange(mainProgram);
            ReplaceRelocateAndJumpLabels(procedureOpcodes, pushDelegatesMap);
            var programProcedure = new Procedure(procedureOpcodes);

            Deb.EnqueueBuild("Opcodes for main program");
            foreach (var opcode in procedureOpcodes) {
                Deb.EnqueueBuild(opcode);
            }
            foreach (var pushDelegate in pushDelegatesMap.Values){
                ReplaceRelocateAndJumpLabels(
                    pushDelegate.procedureOpcodes, pushDelegatesMap);
                Deb.EnqueueBuild("Opcodes for", pushDelegate.DestinationLabel);
                foreach(var opcode in pushDelegate.procedureOpcodes){
                    Deb.EnqueueBuild(opcode);
                }
            }

            return programProcedure;
        }

        static public void ReplaceRelocateAndJumpLabels(
            OpcodeList opcodes,
            Dictionary<string, OpcodePushDelegate> pushDelegateMap
        )
        {
            ReplaceRelocateDelegateOpcodes(opcodes,pushDelegateMap);
            ReplaceJumpLabels(opcodes);
        }


        static bool IsEOP(Opcode opcode)
        {
            return opcode is OpcodeEOP;
        }
        static OpcodeList GetDelegateOpcodes(IEnumerator<Opcode> opcodesEnumerator)
        {
            OpcodeList delegateOpcodes = new OpcodeList();
            do {
                delegateOpcodes.Add(opcodesEnumerator.Current);
            } while (!IsEOP(opcodesEnumerator.Current)&&opcodesEnumerator.MoveNext());
            return delegateOpcodes;
        }
        /// <summary>
        /// Gets all delegate destination labels so we know what functions are
        /// defined.
        /// </summary>
        /// <returns>The all delegate destination labels.</returns>
        /// <param name="parts">Parts.</param>
        static Dictionary<string, bool>
        GetAllDelegateDestinationLabels(List<CodePart> parts)
        {
            var delegateDestinationLabels =
                new Dictionary<string, bool>();
            foreach (var part in parts) {
                foreach (var opcode in part.AllOpcodes) {
                    if (opcode.Code==ByteCode.PUSHDELEGATERELOCATELATER) {
                        var relopcode = opcode as OpcodePushDelegateRelocateLater;
                        delegateDestinationLabels.Add(opcode.DestinationLabel, relopcode.WithClosure);
                    }
                }
            }
            return delegateDestinationLabels;
        }
        /// <summary>
        /// Using the dictionary with all the delegate labels, create
        /// OpcodePushDelegates with the proper lists of opcodes each.
        /// </summary>
        /// <returns>The push delegates map.</returns>
        /// <param name="parts">Parts.</param>
        static Dictionary<string, OpcodePushDelegate>
        CreatePushDelegatesMap(List<CodePart> parts)
        {
            var delegateDestinationLabels = GetAllDelegateDestinationLabels(parts);
            var pushDelegatesMap = new Dictionary<string, OpcodePushDelegate>();
            foreach (var part in parts) {
                var opcodesEnumerator = part.AllOpcodes.GetEnumerator();
                while (opcodesEnumerator.MoveNext()) {
                    if (delegateDestinationLabels.TryGetValue(
                        opcodesEnumerator.Current.Label, out bool withClosure)) {
                        var destLabel = opcodesEnumerator.Current.Label;
                        var delegateOpcodes = GetDelegateOpcodes(opcodesEnumerator);
                        pushDelegatesMap.Add(destLabel,
                            new OpcodePushDelegate(
                                delegateOpcodes, withClosure));
                    }
                }
            }
            return pushDelegatesMap;
        }
        static void
        ReplaceRelocateDelegateOpcodes(
            OpcodeList opcodes,
            Dictionary<string, OpcodePushDelegate> pushDelegatesMap
        )
        {
            for (int i = 0;i<opcodes.Count;i++) {
                if (opcodes[i].Code==ByteCode.PUSHDELEGATERELOCATELATER) {
                    if (pushDelegatesMap.TryGetValue(
                        opcodes[i].DestinationLabel,
                        out OpcodePushDelegate pushDelegate
                    )) {
                        pushDelegate.Label=opcodes[i].Label;
                        opcodes[i]=pushDelegate;
                    } else {
                        throw new Exception(
                            "No OpcodePushDelegate found for label "+opcodes[i].DestinationLabel);
                    }
                }
            }
        }


        static public void PrintCodeParts(string message, List<CodePart> parts)
        {
            foreach (var part in parts) {
                Deb.EnqueueCompile("Function opcodes");
                foreach (var opcode in part.FunctionsCode) {
                    Deb.EnqueueCompile(opcode);
                }
                Deb.EnqueueCompile("Initialization opcodes");
                foreach (var opcode in part.InitializationCode) {
                    Deb.EnqueueCompile(opcode);
                }
                Deb.EnqueueCompile("Mainprogram code");
                foreach (var opcode in part.MainCode) {
                    Deb.EnqueueCompile(opcode);
                }
            }
        }

        static void ReplaceJumpLabels(List<Opcode> opcodes){
            var labelIndexMap = LabelIndexMap(opcodes);
            for (int i = 0;i<opcodes.Count;i++){
                var branchOpcode = opcodes[i] as BranchOpcode;
                if(branchOpcode!=null && branchOpcode.Distance==0){
                    if(labelIndexMap.TryGetValue(branchOpcode.DestinationLabel,out int destIndex)){
                        branchOpcode.Distance=destIndex-i;
                    } else{
                        throw new Exception("Index not found for label "+branchOpcode.DestinationLabel+
                                            " at opcode "+branchOpcode+" of type "+ branchOpcode.Code);
                    }
                }
            }
        }

        static Dictionary<string, int> LabelIndexMap(List<Opcode> opcodes){
            var labelIndexMap = new Dictionary<string, int>();
            for (int i = 0;i<opcodes.Count;i++){
                if(opcodes[i].Label!=String.Empty){
                    labelIndexMap.Add(opcodes[i].Label, i);
                }
            }
            return labelIndexMap;
        }
    }
}
