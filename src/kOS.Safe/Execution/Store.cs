using System;
using coll = System.Collections.Generic;
using kOS.Safe.Exceptions;

namespace kOS.Safe.Execution {

	// The purpose of this is to manage variable lookup and storage calls
    // that are made in the running of a ProcedureCall.
    //
	// This manages all lookups, and all storage, even up to Global Scope.
    // Opcodes use their ProcedureCall reference in their Execute
    // function to get a reference to this.
	public class Store {
        class Mapping : coll.Dictionary<string, object> {}

        readonly VariableScope globalScope;
        readonly coll.Stack<Mapping> scopeStack = new coll.Stack<Mapping>();

        public Store(VariableScope globalScope){
            Deb.logmisc("Storing reference to globalScope", globalScope);
            this.globalScope=globalScope;
        }

        public void PushNewScope()
        {
            Deb.logmisc("Pushing new scope");
			scopeStack.Push(new Mapping());
        }

        internal void PopScope(short numLevels)
        {
            Deb.logmisc("Popping scope. Numlevels",numLevels);
            for (int i = 0;i<numLevels;i++){
                Deb.logmisc("Popping scope. i", i);
                scopeStack.Pop();
            }
        }

        internal void SetNewLocal(string identifier, object value)
        {
            var lower_identifier = identifier.ToLower();
            Deb.logmisc("Setting new local", lower_identifier,"to",value);
            scopeStack.Peek().Add(lower_identifier, value);
        }


        public object GetValue(object testValue, bool barewordOkay = false)
        {
            Deb.logmisc("GettingValue", testValue,"barewordOkay",barewordOkay);
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


            Variable variable = GetVariable(identifier, barewordOkay);
            Deb.logmisc("Got variable", variable,"value",variable!=null?variable.Value:null);
            return variable.Value;
        }

        internal Variable GetVariable(string identifier, bool barewordOkay)
        {
            Deb.logmisc("GetVariable called for", identifier, "barewordOkay", barewordOkay);
            identifier = identifier.ToLower();
            
            foreach (var level in scopeStack) {
                Deb.logmisc("Checking level", level);
                object value;
                if (level.TryGetValue(identifier, out value)) {
                    return new Variable { Name=identifier, Value=value };
                }
            }
            Deb.logmisc("Attempting to get it in global scope");
            //Variable var;
            if (globalScope.Variables.TryGetValue(identifier, out Variable var)) {
                return var;
            }
            throw new KOSUndefinedIdentifierException(identifier.TrimStart('$'), "");
        }

    }
}
