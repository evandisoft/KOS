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
            Deb.miscIsLogging=true;

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
            var procedureOpcodes = new OpcodeList();
            procedureOpcodes.Add(new OpcodeNOP()); // adding sentinal opcode
            procedureOpcodes.AddRange(mainProgram);
            ReplaceRelocateAndJumpLabels(
                procedureOpcodes, pushDelegatesMap);
            var programProcedure = new Procedure(procedureOpcodes);



            Deb.logcompile("Opcodes for main program");
            foreach (var opcode in procedureOpcodes) {
                Deb.logcompile(opcode.Label, opcode);
            }
            foreach (var pushDelegate in pushDelegatesMap.Values){
                ReplaceRelocateAndJumpLabels(
                    pushDelegate.procedureOpcodes, pushDelegatesMap);
                Deb.logcompile("Opcodes for", pushDelegate.DestinationLabel);
                foreach(var opcode in pushDelegate.procedureOpcodes){
                    Deb.logcompile(opcode.Label, opcode);
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

        // Get all the labels so that we know what functions are defined.
        // (Anonymous functions are defined in maincode sections, unlike
        // regular functions, which each have their own codepart
        // and in that codepart they are placed in a FunctionCode section.)
        // So we cannot look for the functions first. We can only find
        // the functions after we've seen all the PushDelecateRelocateLater
        // opcodes
        static Boolean IsEOP(Opcode opcode)
        {
            return opcode is OpcodeEOP;
        }
        static OpcodeList GetDelegateOpcodes(IEnumerator<Opcode> opcodesEnumerator)
        {
            OpcodeList delegateOpcodes = new OpcodeList();
            delegateOpcodes.Add(new OpcodeNOP());
            do {
                delegateOpcodes.Add(opcodesEnumerator.Current);
            } while (!IsEOP(opcodesEnumerator.Current)&&opcodesEnumerator.MoveNext());
            return delegateOpcodes;
        }
        static Dictionary<string, bool>
        GetAllDelegateDestinationLabels(List<CodePart> parts)
        {
            var delegateDestinationLabels =
                new Dictionary<string, bool>();
            foreach (var part in parts) {
                foreach (var opcode in part.AllOpcodes) {
                    //Deb.logcompile("GetAllLabels,Perusing opcode", opcode);
                    if (opcode.Code==ByteCode.PUSHDELEGATERELOCATELATER) {
                        //Deb.logcompile(
                            //"IsRelocate. DestLabel is ",opcode.DestinationLabel,
                            //"opcode label is ",opcode.Label);
                        var relopcode = opcode as OpcodePushDelegateRelocateLater;
                        delegateDestinationLabels.Add(opcode.DestinationLabel, relopcode.WithClosure);
                    }
                }
            }
            return delegateDestinationLabels;
        }
        static Dictionary<string, OpcodePushDelegate>
        CreatePushDelegatesMap(List<CodePart> parts)
        {
            var delegateDestinationLabels = GetAllDelegateDestinationLabels(parts);
            var pushDelegatesMap = new Dictionary<string, OpcodePushDelegate>();
            foreach (var part in parts) {
                var opcodesEnumerator = part.AllOpcodes.GetEnumerator();
                while (opcodesEnumerator.MoveNext()) {
                    //Deb.logcompile(
                        //"CreatePushDelegatesMap.",
                        //"opcode label is ", opcodesEnumerator.Current.Label);
                    if (delegateDestinationLabels.TryGetValue(
                        opcodesEnumerator.Current.Label, out bool withClosure)) {
                        var destLabel = opcodesEnumerator.Current.Label;
                        //Deb.logcompile(
                            //"Found dest label",opcodesEnumerator.Current.Label);
                        var delegateOpcodes = GetDelegateOpcodes(opcodesEnumerator);
                        pushDelegatesMap.Add(
                            destLabel,
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
