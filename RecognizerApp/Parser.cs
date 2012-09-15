using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceCommand;
using System.Speech.Recognition;
using Configuration;

namespace RecognizerApp
{
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

        // 7. second block length in bytes (including this one)
        // 8. culture code (0 = en-US, 1 = fr-CA)
        // 9. N - number of words 
        // repeated N times:
            // 10. confidence (0-100)
            // 11. word (null-terminated)

        public byte[] ParseResult(RecognitionResult result, byte cultureinfo, bool full)
        {
            Byte[] res = ParseResult(result);
            if (!full)
                return res;

            res[1] = 1; // full format;

            List<byte> block2 = new List<byte>();

            block2.Add(0);     // length of block, not known yet
            block2.Add(cultureinfo);     // length of block, not known yet
            block2.Add((byte)(result.Words.Count));     // number of words 

            foreach (var v in result.Words)
            {
                block2.AddRange(ParseWord(v));
            }
            block2[0] = (byte)block2.Count;

            List<byte> lst = res.ToList();
            lst.AddRange(block2);
            return lst.ToArray();
        }

        private byte[] ParseWord(RecognizedWordUnit word)
        {
            byte[] retval = new byte[word.Text.Length + 2]; // 1 for '/0', 1 for confidence;
            byte[] str = word.Text.GetBytes();
            Array.Copy(str, 0, retval, 1, str.Length);
            retval[0] = (byte)(0.5 + word.Confidence * 100);
            return retval;
        }

        public byte[] ParseResult(RecognitionResult result, System.Globalization.CultureInfo info)
        {
            byte culture = 0;
            if (!info.ToString().Equals(Cfg.Instance.DefaultCulture))
                culture = (byte)1;

            return ParseResult(result, culture, true);
        }

        public VoiceCommandProto ParseResultToCommand(RecognitionResult result)
        {
            String opcode;
            List<int> args;

            VoiceCommandProto res = new VoiceCommandProto();

            // first add the semantics:
            if (result.Semantics == null || result.Semantics.Value == null)
                return res;

            res.words.Add(new VoiceCommandProto.word_result() { word = result.Semantics.Value.ToString(), confidence = result.Semantics.Confidence });

            // then the words:
            foreach (var word in result.Words)
            {
                res.words.Add(new VoiceCommandProto.word_result() { word = word.Text, confidence = word.Confidence });
            }

            if (GetOpcodeArgs(result, out opcode, out args))
            {
                res.opcode = opcode;
                res.args.AddRange(args);
            }

            return res;
        }

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
