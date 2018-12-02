using System;
using coll = System.Collections.Generic;
using kOS.Safe.Compilation;
using kOS.Safe.Exceptions;
using System.Text;

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

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var item in this){
                sb.Append(item.ToString()+",");
            }
            return sb.ToString();
        }
    }
}
