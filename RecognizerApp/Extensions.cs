using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Speech.Recognition;

namespace RecognizerApp
{
    /// <summary>
    /// some extension methods 
    /// </summary>
    public static class Extensions
    {
        public static String DebugString(this RecognitionResult result)
        {
            string res = String.Format("{0} [{1}]. {2} ", result.Text, result.Confidence.ToString("F2"), "Words:");
            foreach (var v in result.Words)
            {
                res += String.Format("{0} [{1}], ", v.Text, v.Confidence.ToString("F2"));
            }

            res += String.Format("Semantics: {0} [{1}]", result.Semantics.Value, result.Semantics.Confidence.ToString("F2"));
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

        /// <summary>
        /// C# null termitation style...
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] GetBytes(this String str)
        {
            return Encoding.ASCII.GetBytes(str /*+ Char.MinValue*/);    // the null termination is done elsewhere now
        }

    }
}
