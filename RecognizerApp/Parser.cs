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
        // byte protocol: 
        // 1. message length in bytes (including this one)
        // 2. culture code (0 = en-US, 1 = fr-CA)
        // 3. message confidence (0-100)
        // 4. opcode
        // 5. arg1 (optional)
        // 6. arg2 (optional)
        // 7. checksum

        public byte[] ParseResult(RecognitionResult result, byte cultureinfo)
        {
            String opcode;
            List<int> args;
            byte[] res = null;
            int opval;

            if (GetOpcodeArgs(result, out opcode, out args))
            {
                int len = args.Count + 5;   // header, culture, confidence, opcode, checksum 
                int k = 0;
                res = new byte[len];
                res[k++] = (byte)len;
                res[k++] = cultureinfo;
                res[k++] = (byte) (100 * result.Semantics.Confidence);
                res[k++] = Cfg.Instance.Opcodes.TryGetValue(opcode, out opval) ? (byte)opval : (byte)(0);
                foreach (int i in args)
                    res[k++] = (byte)i;

                res[k] = (byte) ((256 - res.Checksum()) % 256);
                return res;
            }
            return new byte[0];
        }

        public VoiceCommandProto ParseResultToCommand(RecognitionResult result)
        {
            String opcode;
            List<int> args;

            VoiceCommandProto res = new VoiceCommandProto();

            // first add the semantics:
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

            try
            {
                String[] res = result.Semantics.Value.ToString().Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
               
                if (res != null && res.Length > 0)
                {
                    opcode = res[0];
                    for (int i = 1; i < res.Length; i++)
                    {
                        args.Add(int.Parse(res[i]));
                    }
                }

                return true;
            }
            catch { }
            return false;
        }

    }
}
