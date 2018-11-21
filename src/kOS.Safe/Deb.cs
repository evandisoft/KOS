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
        static string opcodesfilename = "Logs/kOS/opcode.log";
        static string generalLogname = "Logs/kOS/misc.log";
        static public void logopcode(params object [] args){
            logall (opcodesfilename, args);
        }
        static public Boolean miscIsLogging = false;
        static public void logmisc(params object [] args){
            if(SafeHouse.Config.DebugEachOpcode && miscIsLogging){
                logall(generalLogname, args);
            }

        }
        static public void clearMiscLog(){
            File.WriteAllText(generalLogname, String.Empty);
        }
        static public void clearOpcodeFile(){
            File.WriteAllText (opcodesfilename, String.Empty);
        }

    }


}
