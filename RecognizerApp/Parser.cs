using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Speech.Recognition;
using Configuration;

namespace RecognizerApp
{
    /// <summary>
    /// class used to parse objects to bytes
    /// </summary>
    public class Parser
    {
        // byte protocol (minimal): 

        // 0. message length in bytes (including this one)
        // 1. message format (0 == minimal)
        // 2. message confidence (0-100)
        // 3. opcode
        // 4. arg1 (optional)
        // 5. arg2 (optional)
        // 6. checksum

        /// <summary>
        /// recognition result to byte array - minimal format
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public byte[] ParseResult(RecognitionResult result)
        {
            String opcode;
            List<int> args;
            byte[] res = null;
            int opval;

            if (GetOpcodeArgs(result, out opcode, out args))
            {
                int len = args.Count + 5;   // header, format, confidence, opcode, checksum 
                int k = 0;
                res = new byte[len];
                res[k++] = (byte)len;
                res[k++] = 0;   // minimal format
                res[k++] = (byte)(0.5 + 100 * result.Semantics.Confidence);
                res[k++] = Cfg.Instance.Opcodes.TryGetValue(opcode, out opval) ? (byte)opval : (byte)(0);
                foreach (int i in args)
                    res[k++] = (byte)i;

                res[k] = (byte)((256 - res.Checksum()) % 256);
                return res;
            }
            return new byte[0];
        }

        // byte protocol (full): 

        // 0. first block length in bytes (including this one)
        // 1. message format (0 == minimal)
        // 2. message confidence (0-100)
        // 3. opcode
        // 4. arg1 (optional)
        // 5. arg2 (optional)
        // 6. checksum (of this block)

        // 7. culture code (0 = en-US, 1 = fr-CA)
        // 8. N - number of words 
        // repeated N times:
            // 9. confidence (0-100)
            // 10. word (array of 11 chars, always null-terminated)

        /// <summary>
        /// recognition result to byte array - full format
        /// </summary>
        /// <param name="result"></param>
        /// <param name="cultureinfo"></param>
        /// <param name="full"></param>
        /// <returns></returns>
        public byte[] ParseResult(RecognitionResult result, byte cultureinfo, bool full)
        {
            Byte[] res = ParseResult(result);
            if (!full)
                return res;

            res[1] = 1; // full format;

            List<byte> block2 = new List<byte>();

            block2.Add(cultureinfo);   
            block2.Add((byte)(result.Words.Count));     // number of words 

            foreach (var v in result.Words)
            {
                block2.AddRange(ParseWord(v));
            }

            List<byte> lst = res.ToList();
            lst.AddRange(block2);
            return lst.ToArray();
        }

        /// <summary>
        /// recognized word unit to byte array
        /// </summary>
        /// <param name="word">recognized word</param>
        /// <param name="len">length of output byte array</param>
        /// <returns></returns>
        private byte[] ParseWord(RecognizedWordUnit word, int len = 12)
        {
            byte[] retval = new byte[len]; 
            byte[] str = word.Text.GetBytes();
            Array.Copy(str, 0, retval, 1, Math.Min(str.Length, len - 2));   // yes, if the word has more than 10 letters, it will be truncated!
            retval[0] = (byte)(0.5 + word.Confidence * 100);    // confidence
            return retval;
        }

        /// <summary>
        /// parses recognition result if possible
        /// </summary>
        /// <param name="result"></param>
        /// <param name="info"></param>
        /// <param name="res"></param>
        /// <returns>true if the semantic was in expected format</returns>
        public bool TryParseResult(RecognitionResult result, System.Globalization.CultureInfo info, out byte[] res)
        {
            res = null;
            try
            {
                res = ParseResult(result, info);
                return true;
            }
            catch
            {
                Console.WriteLine("Failed to parse recognition result");
                return false;
            }
        }

        /// <summary>
        /// recognition result to byte array
        /// </summary>
        /// <param name="result"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        public byte[] ParseResult(RecognitionResult result, System.Globalization.CultureInfo info)
        {
            byte culture = 0;
            if (!info.ToString().Equals(Cfg.Instance.DefaultCulture))
                culture = (byte)1;

            return ParseResult(result, culture, true);
        }

        /// <summary>
        /// get the opcode and arguments from the recognized phrase
        /// </summary>
        /// <param name="result"></param>
        /// <param name="opcode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool GetOpcodeArgs(RecognitionResult result, out String opcode, out List<int> args)
        {
            opcode = String.Empty;
            args = new List<int>();

            if (result == null || result.Semantics == null || result.Semantics.Value == null)
                return false;

            String[] res = result.Semantics.Value.ToString().Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (res != null && res.Length > 0)
            {
                opcode = res[0];

                int k;
                char c;
                for (int i = 1; i < res.Length; i++)
                {
                    if (int.TryParse(res[i], out k))
                    {
                        args.Add(k);
                    }
                    else
                    {// parse as 'Letter-number' ("A4")
                        if (res[i].Length > 1)
                        {
                            if (Char.TryParse(res[i].Substring(0, 1), out c) && int.TryParse(res[i].Substring(1, res[i].Length-1), out k))
                            {
                                int arg = 10 * (c - 'A' + 1) + k;
                                args.Add(arg);
                            }
                        }
                    }
                }
            }

            return true;
        }

    }
}
