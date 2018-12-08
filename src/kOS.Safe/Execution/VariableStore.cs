using System;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;
using kOS.Safe.Encapsulation;

namespace kOS.Safe.Execution {
    public class Mapping : coll.Dictionary<string, Variable> { }
    // The purpose of this is to manage variable lookup and storage calls
    // that are made in the running of a ProcedureCall.
    //
    // This manages all lookups, and all storage, even up to Global Scope.
    // Opcodes use their ProcedureCall reference in their Execute
    // function to get a reference to this.
    public class VariableStore {
        readonly VariableScope globalVariables;
        public readonly coll.Stack<Mapping> scopeStack = new coll.Stack<Mapping>();

        public VariableStore(VariableScope globalVariables){
            Deb.EnqueueExec("Storing reference to globalScope", globalVariables);
            this.globalVariables=globalVariables;
        }

        public void AddClosure(coll.List<Mapping> closure){
            if (closure==null) return;
            coll.Stack<Mapping> reverser = new coll.Stack<Mapping>();
            foreach(var level in closure){
                reverser.Push(level);
            }
            foreach(var level in reverser){
                scopeStack.Push(level);
            }
        }

        public void PushNewScope()
        {
            Deb.EnqueueExec("Pushing new scope");
			scopeStack.Push(new Mapping());
        }

        internal void PopScope(short numLevels)
        {
            Deb.EnqueueExec("Popping scope. Numlevels",numLevels);
            for (int i = 0;i<numLevels;i++){
                Deb.EnqueueExec("Popping scope. i", i);
                scopeStack.Pop();
            }
        }

        internal void SetNewLocal(string identifier, object value)
        {
            var lower_identifier = identifier.ToLower();
            Deb.EnqueueExec("Setting new local", lower_identifier,"to",value);
            var local = scopeStack.Peek();
            local[lower_identifier]=new Variable { Name=lower_identifier, Value=value };
        }


        public object GetValue(object testValue, bool barewordOkay = false)
        {
            Deb.EnqueueExec("GettingValue", testValue, "barewordOkay",barewordOkay);
            // $cos     cos named variable
            // cos()    cos trigonometric function
            // cos      string literal "cos"

            // If it's a variable, meaning it starts with "$" but
            // does NOT have a value like $<.....>, which are special
            // flags used internally:
            var identifier = testValue as string;
            if (identifier == null ||
                identifier.Length <= 1 ||
                identifier[0] != '$' ||
                identifier[1] == '<') {
                return testValue;
            }
            //evandisoft TODO: just hardwiring this in there to get it to not
            // treat the Procedure as an identifier
            Deb.EnqueueExec("the type of testvalue is", testValue.GetType());
            if (testValue.GetType()==typeof(Procedure)){
                Deb.EnqueueExec("returning the procedure");
                return testValue; 
            }


            Variable variable = GetVariable(identifier, barewordOkay);
            Deb.EnqueueExec("Got variable", variable,"value",variable!=null?variable.Value : "null");
            return variable.Value;
        }

        internal Variable GetVariable(string identifier, bool barewordOkay)
        {
            Deb.EnqueueExec("GetVariable called for", identifier, "barewordOkay", barewordOkay);
            identifier = identifier.ToLower();

            foreach (var level in scopeStack) {
                Deb.EnqueueExec("Checking level", level,"total levels",scopeStack.Count);
                if (level.TryGetValue(identifier, out Variable variable)) {
                    return variable;
                }
            }
            Deb.EnqueueExec("Attempting to get it in global scope");
            //Variable var;
            if (globalVariables.Variables.TryGetValue(identifier, out Variable var)) {
                return var;
            }
            throw new KOSUndefinedIdentifierException(identifier.TrimStart('$'), "");
        }
        public void SetGlobal(string identifier, object value)
        {
            // Attempt to get it as a global.  Make a new one if it's not found.
            // This preserves the "bound-ness" of the variable if it's a
            // BoundVariable, whereas unconditionally making a new Variable wouldn't:
            Variable variable = globalVariables.GetLocal(identifier);
            if (variable == null) {
                variable = new Variable { Name = identifier };
                globalVariables.Add(identifier, variable);
            }
            variable.Value = value;
        }
        public void SetValue(string identifier, object value)
        {
            Variable variable;
            Deb.EnqueueExec("value is of type", value.GetType());
            identifier = identifier.ToLower();
            Deb.EnqueueExec("Attempting to find a place to set it in local scope");
            foreach (var level in scopeStack){
                if (level.TryGetValue(identifier, out variable)) {
                    variable.Value=value; 
                    return;
                }
            }
            Deb.EnqueueExec("Attempting to find a place to set it in global scope");
            if (globalVariables.Variables.TryGetValue(identifier, out variable)) {
                variable.Value=value; 
                return;
            }

            Deb.EnqueueExec("Setting it in new global variable");
            SetGlobal(identifier, value);
        }

        public override string ToString(){
            string retval = "";
            foreach(var level in scopeStack){
                foreach(var item in level){
                    retval+=item.Key+","+item.Value.Value+";";
                }
                retval+="\n";
            }
            return retval;
        }

        internal void RemoveVariable(string variableName)
        {
            Variable variable;
            foreach(var level in scopeStack){
                if(level.TryGetValue(variableName,out variable)){
                    level.Remove(variableName);
                    variable.Value=null;
                    return;
                }
            }
            if(globalVariables.Variables.TryGetValue(variableName,out variable)){
                globalVariables.Variables.Remove(variableName);
                variable.Value=null;
                return;
            }
        }
    }
}
