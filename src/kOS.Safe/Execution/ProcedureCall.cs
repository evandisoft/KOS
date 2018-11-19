using System;
using System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Encapsulation;
using kOS.Safe.Execution;

namespace kOS.Safe
{
    // for speed (assuming that would improve speed) give procedurecall a reference
    // to the thread, and perhaps also the process

    public enum ProcedureCallStatus
    {
        OK,
        FINISHED
    }


    public class ProcedureCall:ICpu
    {
        private List<Opcode> Opcodes { get; set; }
        int instructionPointer = 0;

	    // TODO: Add closures to this, and possibly permanent  references
        // to the thread and process
	    public ProcedureCall(List<Opcode> Opcodes)
        {
            this.Opcodes = Opcodes;
        }


        public ProcedureCallStatus Execute(KOSThread kOSThread)
        {
            Opcode opcode = Opcodes[instructionPointer];
            opcode.Execute(this);
            int delta = opcode.DeltaInstructionPointer;
            instructionPointer += delta;

            if(instructionPointer==Opcodes.Count){
                return ProcedureCallStatus.FINISHED;
            }else if(instructionPointer<Opcodes.Count){
		        return ProcedureCallStatus.OK;
            }else{
                throw new Exception ("Instruction way out of bounds!");
            }
        }


        public int InstructionPointer { 
            get => instructionPointer; 
            set => throw new NotImplementedException (); 
        }

        public double SessionTime => throw new NotImplementedException ();

        public List<string> ProfileResult => throw new NotImplementedException ();

        public int NextTriggerInstanceId => throw new NotImplementedException ();

        public int InstructionsThisUpdate => throw new NotImplementedException ();

        public bool IsPoppingContext => throw new NotImplementedException ();





        public void PushArgumentStack (object item)
        {
            throw new NotImplementedException ();
        }

        public object PopArgumentStack ()
        {
            throw new NotImplementedException ();
        }

        public void PushNewScope (short scopeId, short parentScopeId)
        {
            throw new NotImplementedException ();
        }

        public void PushScopeStack (object thing)
        {
            throw new NotImplementedException ();
        }

        public object PopScopeStack (int howMany)
        {
            throw new NotImplementedException ();
        }

        public List<VariableScope> GetCurrentClosure ()
        {
            throw new NotImplementedException ();
        }

        public IUserDelegate MakeUserDelegate (int entryPoint, bool withClosure)
        {
            throw new NotImplementedException ();
        }

        public void AssertValidDelegateCall (IUserDelegate userDelegate)
        {
            throw new NotImplementedException ();
        }

        public object GetValue (object testValue, bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public object PopValueArgument (bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public object PeekValueArgument (int digDepth, bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public object PeekRawArgument (int digDepth, out bool checkOkay)
        {
            throw new NotImplementedException ();
        }

        public object PeekRawScope (int digDepth, out bool checkOkay)
        {
            throw new NotImplementedException ();
        }

        public object PopValueEncapsulatedArgument (bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public object PeekValueEncapsulatedArgument (int digDepth, bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public Structure GetStructureEncapsulatedArgument (Structure testValue, bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public Structure PopStructureEncapsulatedArgument (bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public Structure PeekStructureEncapsulatedArgument (int digDepth, bool barewordOkay = false)
        {
            throw new NotImplementedException ();
        }

        public int GetArgumentStackSize ()
        {
            throw new NotImplementedException ();
        }

        public void SetValue (string identifier, object value)
        {
            throw new NotImplementedException ();
        }

        public void SetValueExists (string identifier, object value)
        {
            throw new NotImplementedException ();
        }

        public void SetNewLocal (string identifier, object value)
        {
            throw new NotImplementedException ();
        }

        public void SetGlobal (string identifier, object value)
        {
            throw new NotImplementedException ();
        }

        public bool IdentifierExistsInScope (string identifier)
        {
            throw new NotImplementedException ();
        }

        public string DumpVariables ()
        {
            throw new NotImplementedException ();
        }

        public string DumpStack ()
        {
            throw new NotImplementedException ();
        }

        public void RemoveVariable (string identifier)
        {
            throw new NotImplementedException ();
        }

        public TriggerInfo AddTrigger (int triggerFunctionPointer, InterruptPriority priority, int instanceId, bool immediate, List<VariableScope> closure)
        {
            throw new NotImplementedException ();
        }

        public TriggerInfo AddTrigger (TriggerInfo trigger, bool immediate)
        {
            throw new NotImplementedException ();
        }

        public TriggerInfo AddTrigger (UserDelegate del, InterruptPriority priority, int instanceId, bool immediate, List<Structure> args)
        {
            throw new NotImplementedException ();
        }

        public TriggerInfo AddTrigger (UserDelegate del, InterruptPriority priority, int instanceId, bool immediate, params Structure [] args)
        {
            throw new NotImplementedException ();
        }

        public void RemoveTrigger (int triggerFunctionPointer, int instanceId)
        {
            throw new NotImplementedException ();
        }

        public void RemoveTrigger (TriggerInfo trigger)
        {
            throw new NotImplementedException ();
        }

        public void CancelCalledTriggers (int triggerFunctionPointer, int instanceId)
        {
            throw new NotImplementedException ();
        }

        public void CancelCalledTriggers (TriggerInfo trigger)
        {
            throw new NotImplementedException ();
        }

        public void CallBuiltinFunction (string functionName)
        {
            throw new NotImplementedException ();
        }

        public bool BuiltInExists (string functionName)
        {
            throw new NotImplementedException ();
        }

        public void BreakExecution (bool manual)
        {
            throw new NotImplementedException ();
        }

        public void YieldProgram (YieldFinishedDetector yieldTracker)
        {
            throw new NotImplementedException ();
        }

        public void AddVariable (Variable variable, string identifier, bool local, bool overwrite = false)
        {
            throw new NotImplementedException ();
        }

        public IProgramContext GetCurrentContext ()
        {
            throw new NotImplementedException ();
        }

        public SubroutineContext GetCurrentSubroutineContext ()
        {
            throw new NotImplementedException ();
        }

        public void AddPopContextNotifyee (IPopContextNotifyee notifyee)
        {
            throw new NotImplementedException ();
        }

        public void RemovePopContextNotifyee (IPopContextNotifyee notifyee)
        {
            throw new NotImplementedException ();
        }

        public Opcode GetCurrentOpcode ()
        {
            throw new NotImplementedException ();
        }

        public Opcode GetOpcodeAt (int instructionPtr)
        {
            throw new NotImplementedException ();
        }

        public void Boot ()
        {
            throw new NotImplementedException ();
        }

        public void StartCompileStopwatch ()
        {
            throw new NotImplementedException ();
        }

        public void StopCompileStopwatch ()
        {
            throw new NotImplementedException ();
        }

        public IProgramContext SwitchToProgramContext ()
        {
            throw new NotImplementedException ();
        }

        public List<int> GetCallTrace ()
        {
            throw new NotImplementedException ();
        }

        public List<string> GetCodeFragment (int contextLines)
        {
            throw new NotImplementedException ();
        }

        public void RunProgram (List<Opcode> program)
        {
            throw new NotImplementedException ();
        }

        public void KOSFixedUpdate (double deltaTime)
        {
            throw new NotImplementedException ();
        }

        public void Dispose ()
        {
            throw new NotImplementedException ();
        }
    }
}
