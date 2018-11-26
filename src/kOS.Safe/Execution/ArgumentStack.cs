using System;
using coll = System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Exceptions;

namespace kOS.Safe.Execution {
    public class ArgumentStack:coll.Stack<object> {

        public int CountArgs(){
            int i = 0;
            foreach(var arg in this){
                if(arg.GetType() == OpcodeCall.ArgMarkerType){
                    return i;
                }
                i++;
            }
            throw new Exception("There is no arg marker!");
        }
    }
}
