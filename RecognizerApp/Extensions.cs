using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Speech.Recognition;
using VoiceCommand;

namespace RecognizerApp
{
    /// <summary>
    /// some extension methods 
    /// </summary>
    public static class Extensions
    {
        public static String DebugString(this RecognitionResult result)
        {
            string res = String.Format("{0}. {1} ", result.Text, "Words:");
            foreach (var v in result.Words)
            {
                res += String.Format("{0} [{1}], ", v.Text, v.Confidence.ToString("F2"));
            }

            res += String.Format("Semantics: {0} [{1}]", result.Semantics.Value, result.Semantics.Confidence.ToString("F2"));
            return res;
        }

        public static string DebugString(this VoiceCommandProto cmd)
        {
            string res = cmd.opcode + " ";
            foreach (var v in cmd.args)
                res += v + " ";

            return res;
        }

        public static byte Checksum(this byte[] arr)
        {
            int ret = 0;
            for (int i = 0; i < arr.Length - 1; i++)
            {
                ret += arr[i];
            }

            return (byte) (ret % 256);
        }
    }
}
