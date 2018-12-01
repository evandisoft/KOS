using coll = System.Collections.Generic;
using System;
using kOS.Safe.Execution;

namespace kOS.Safe.Function
{
    public interface IFunctionManager
    {
        void Load();
        void CallFunction(string functionName,IExec exec);

        [Obsolete("Calling functions without args is being phased out.")]
        void CallFunction(string functionName);
        bool Exists(string functionName);
    }
}