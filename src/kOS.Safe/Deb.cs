using System;
using System.IO;
//using UnityEngine;
using kOS.Safe.Utilities;
// file added by evandisoft
namespace kOS.Safe
{
    public class Deb
    {
        static public Boolean verbose = false;
        static public void logall(string filename,params object [] args){
                foreach (var arg in args) {
                    if (arg!=null) {
                        File.AppendAllText(filename, arg.ToString());
                    } else {
                        File.AppendAllText(filename, "null");
                    }

                    File.AppendAllText(filename, ", ");
                }

                File.AppendAllText(filename, "\n");
            

        }
        static string opcodesLogname = "Logs/kOS/opcode.log";
        static string miscLogname = "Logs/kOS/misc.log";
        static string compileLogname = "Logs/kOS/compile.log";
        static public void logopcode(params object [] args){
            logall (opcodesLogname, args);
        }
        static public Boolean miscIsLogging = false;
        static public void logmisc(params object [] args){
            if(SafeHouse.Config.DebugEachOpcode && miscIsLogging){
                logall(miscLogname, args);
            }
        }
        static public Boolean compileIsLogging = true;
        static public void logcompile(params object[] args)
        {
            if (SafeHouse.Config.DebugEachOpcode && compileIsLogging) {
                logall(compileLogname, args);
            }
        }
        static public void clearCompileLog(){
            File.WriteAllText(compileLogname, String.Empty);
        }
        static public void clearMiscLog(){
            File.WriteAllText(miscLogname, String.Empty);
        }
        static public void clearOpcodeFile(){
            File.WriteAllText (opcodesLogname, String.Empty);
        }

    }


}
