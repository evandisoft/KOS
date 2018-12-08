using coll = System.Collections.Generic;
using System;
using kOS.Safe.Execution;

namespace kOS.Safe.Function
{
    public interface IFunctionManager
    {
        void Load();
        void CallFunction(string functionName,IExec exec);

        void CallFunction(string functionName);
        bool Exists(string functionName);
    }
}