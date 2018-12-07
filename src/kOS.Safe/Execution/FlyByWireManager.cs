using System;
using System.Collections.Generic;
using kOS.Safe.Binding;

namespace kOS.Safe.Execution {
    public class FlyByWireManager {
        Dictionary<string, bool> flyByWire = new Dictionary<string, bool>();
        IBindingManager bindingManager;

        public FlyByWireManager(IBindingManager bindingManager) {
            this.bindingManager = bindingManager;
        }

        public void EnableActiveFlyByWire() {
            foreach (KeyValuePair<string, bool> kvp in flyByWire) {
                bindingManager.ToggleFlyByWire(kvp.Key, kvp.Value);
            }
        }

        public void DisableActiveFlyByWire() {
            foreach (KeyValuePair<string, bool> kvp in flyByWire) {
                if (kvp.Value) {
                    try {
                        bindingManager.ToggleFlyByWire(kvp.Key, false);
                    } catch (Exception ex) // intentionally catch any exception thrown so we don't crash in the middle of breaking execution
                      {
                        // log the exception only when "super verbose" is enabled
                        Utilities.SafeHouse.Logger.SuperVerbose(string.Format("Excepton in ProgramContext.DisableActiveFlyByWire\r\n{0}", ex));
                    }
                }
            }
        }

        public void ToggleFlyByWire(string paramName, bool enabled) {
            Deb.EnqueueExec("Binding manager toggleflybywire null?", bindingManager == null, "enabled?", enabled);
            if (bindingManager == null) return;

            bindingManager.ToggleFlyByWire(paramName, enabled);
            flyByWire[paramName] = enabled;
        }
    }
}
