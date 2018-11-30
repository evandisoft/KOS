using System.Collections.Generic;
using System;
using kOS.Safe.Persistence;
using System.Collections;

namespace kOS.Safe.Compilation
{
    public class CodePart
    {
        public CodePart()
        {
            FunctionsCode = new List<Opcode>(); 
            InitializationCode = new List<Opcode>();
            MainCode = new List<Opcode>();
            AllOpcodes=new OpcodeEnumerator(this);
        }

        public OpcodeEnumerator AllOpcodes { get; }

        public class OpcodeEnumerator : IEnumerable<Opcode> {
            CodePart codePart;
            public OpcodeEnumerator(CodePart codePart){
                this.codePart=codePart;
            }

            public IEnumerator<Opcode> GetEnumerator()
            {
                foreach (var opcode in codePart.FunctionsCode) {
                    yield return opcode;
                }
                foreach (var opcode in codePart.InitializationCode) {
                    yield return opcode;
                }
                foreach (var opcode in codePart.MainCode) {
                    yield return opcode;
                }

            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

        }
       


        public List<Opcode> FunctionsCode { get; set; }
        public List<Opcode> InitializationCode { get; set; }
        public List<Opcode> MainCode { get; set; }

        public List<Opcode> MergeSections()
        {
            var mergedCode = new List<Opcode>();
            mergedCode.AddRange(FunctionsCode);
            mergedCode.AddRange(InitializationCode);
            mergedCode.AddRange(MainCode);
            return mergedCode;
        }

        public void AssignSourceName(GlobalPath filePath)
        {
            AssignSourcePathToSection(filePath, FunctionsCode);
            AssignSourcePathToSection(filePath, InitializationCode);
            AssignSourcePathToSection(filePath, MainCode);
        }

        private void AssignSourcePathToSection(GlobalPath filePath, IEnumerable<Opcode> section)
        {
            foreach (Opcode opcode in section)
            {
                opcode.SourcePath = filePath;
            }
        }


    }
}
