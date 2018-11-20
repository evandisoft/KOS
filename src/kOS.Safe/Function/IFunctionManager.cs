using coll = System.Collections.Generic;
using System;

namespace kOS.Safe.Function
{
    public interface IFunctionManager
    {
        void Load();
        object CallFunction(string functionName,coll.Stack<object> args);

        [Obsolete("Calling functions without args is being phased out.")]
        object CallFunction(string functionName);
        bool Exists(string functionName);
    }
}