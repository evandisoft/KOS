﻿using System;
using System.Collections.Generic;

namespace kOS.Safe.Execution
{
    public interface IStack
    {
        void PushArgument(object item);
        object PopArgument();
        object PeekArgument(int digDepth);
        bool PeekCheckArgument(int digDepth, out object item);
        object PeekScope(int digDepth);
        bool PeekCheckScope(int digDepth, out object item);
        void PushScope(object item);
        object PopScope();
        int GetArgumentStackSize();
        void Clear();
        string Dump();
        List<int> GetCallTrace();
        bool HasTriggerContexts();
        bool HasDelayingTriggerContexts();
        VariableScope FindScope(Int16 scopeId);
        VariableScope GetCurrentScope();
        SubroutineContext GetCurrentSubroutineContext();
        List<SubroutineContext> GetTriggerCallContexts(TriggerInfo trigger);
    }
}
