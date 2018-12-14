﻿using System;
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
                if (item == null) {
                    sb.Append("null" + ",");
                } else {
                    sb.Append(item + ",");
                }
            }
            return sb.ToString();
        }

        public object Peek(int depth,out bool success) {
            success = false;
            foreach(var item in this) {
                if (depth-- == 0) {
                    success = true;
                    return item;
                }
            }
            return null;
        }

        public void ClearArgs() {
            int numOfPops = 0;
            foreach(var arg in this) {
                numOfPops++;
                if(arg is KOSArgMarkerType) {
                    break;
                }
            }
            for(int i = 0;i < numOfPops;i++) {
                Pop();
            }
        }

        public void AssertArgBottomAndConsume() {
            object shouldBeBottom = Pop();
            if (shouldBeBottom != null && shouldBeBottom.GetType() == OpcodeCall.ArgMarkerType)
                return; // Assert passed.

            throw new Exception("No Arg Marker found.");
        }
    }
}
