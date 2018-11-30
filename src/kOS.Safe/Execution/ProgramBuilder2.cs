using System;
using System.Collections.Generic;
using kOS.Safe.Encapsulation;
using kOS.Safe.Compilation;

namespace kOS.Safe.Execution {
    public class ProgramBuilder2 {
        static public Procedure BuildProgram(List<CodePart> parts){
            PrintCodeParts("Before Relocate", parts);
            ReplaceRelocateDelegateOpcodes(parts);
            PrintCodeParts("After Relocate", parts);
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
            return new Procedure(mainProgram);
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
        static List<Opcode> GetDelegateOpcodes(IEnumerator<Opcode> opcodesEnumerator)
        {
            List<Opcode> delegateOpcodes = new List<Opcode>();
            do {
                delegateOpcodes.Add(opcodesEnumerator.Current);
            } while (!IsEndReturn(opcodesEnumerator.Current)&&opcodesEnumerator.MoveNext());
            return delegateOpcodes;
        }
        static Dictionary<string, bool>
        GetAllDelegateLabels(List<CodePart> parts)
        {
            var delegateLabels =
                new Dictionary<string, bool>();
            foreach (var part in parts) {
                foreach (var opcode in part.AllOpcodes) {
                    if (opcode.Code==ByteCode.PUSHDELEGATERELOCATELATER) {
                        var relopcode = opcode as OpcodePushDelegateRelocateLater;
                        delegateLabels.Add(opcode.DestinationLabel, relopcode.WithClosure);
                    }
                }
            }
            return delegateLabels;
        }
        static Dictionary<string, OpcodePushDelegate>
        CreatePushDelegatesMap(List<CodePart> parts)
        {
            var delegateLabels = GetAllDelegateLabels(parts);
            var pushDelegatesMap = new Dictionary<string, OpcodePushDelegate>();
            foreach (var part in parts) {
                var opcodesEnumerator = part.AllOpcodes.GetEnumerator();
                while (opcodesEnumerator.MoveNext()) {
                    if (delegateLabels.TryGetValue(
                        opcodesEnumerator.Current.Label, out bool withClosure)) {

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
            for (int i = 0;i<opcodes.Count;i++) {
                if (opcodes[i].Code==ByteCode.PUSHDELEGATERELOCATELATER) {
                    if (pushDelegatesMap.TryGetValue(
                        opcodes[i].Label,
                        out OpcodePushDelegate pushDelegate
                    )) {
                        opcodes[i]=pushDelegate;
                    } else {
                        throw new Exception(
                            "No OpcodePushDelegate found for label "+opcodes[i].Label);
                    }
                }
            }
        }

        static void
        ReplaceRelocateDelegateOpcodes(List<CodePart> parts)
        {
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
    }
}
