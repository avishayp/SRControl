using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tools;
using System.IO;
using System.Reflection;

namespace Configuration
{
    /// <summary>
    /// configuration class
    /// </summary>
    public class Cfg
    {
        private const string CONFIG_FILE = @"App.cfg";

        public struct Node
        {
            public String OP { set; get; }
            public int NUM { set; get; }
        }

        public struct Config
        {
            public String IP;
            public String port;
            public List<String> GrammarFiles;
            public String AudioInput;
            public int RootConfidenceLevel;
            public List<Node> Opcodes;
         }

        public static Cfg Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Cfg();
                }
                return _instance;
            }
        }

        private void Test()
        {
            _config.IP = "127.0.0.1";
            _config.port = "8000";
            _config.GrammarFiles.Add("en-US=C:\\work\\Grammers\\English.grxml");
            _config.GrammarFiles.Add("fr-FR=C:\\work\\Grammers\\French.grxml");
            _config.Opcodes.Add(new Node() { OP = "MV", NUM = 10 });
            _config.Opcodes.Add(new Node() { OP = "CL", NUM = 20 });

            Serializer.Save(_config, @"C:\temp\test.cfg");
        }

        private void LoadConfig()
        {
            _config = new Config();
            _config.GrammarFiles = new List<string>();
            _config.Opcodes = new List<Node>();

            // Test();

            if (!File.Exists(CONFIG_FILE))
            {
                throw new Exception(String.Format("Failed to load config file from {0}. Please put {0} on your startup directory!", CONFIG_FILE));
            }

            try
            {
                _config = Serializer.Load<Config>(CONFIG_FILE);
                ParseGrammarFiles();
                ParseOpcodes();
            }
            catch 
            {
                throw;
            }
        }

        private Cfg()
        {
            LoadConfig();
        }

        private void ParseGrammarFiles()
        {
            _grammarFiles = new Dictionary<string, string>();
            foreach (var str in _config.GrammarFiles)
            {
                String[] keyval = str.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                _grammarFiles.Add(keyval[0], keyval[1]);
            }
        }

        private void ParseOpcodes()
        {
            Opcodes = new Dictionary<string, int>();
            foreach (var v in _config.Opcodes)
            {
                Opcodes.Add(v.OP, v.NUM);
            }
        }

        private static Cfg _instance;
        private Config _config;
        private Dictionary<String, String> _grammarFiles;

        public String IPAddress { get { return _config.IP; } }
        public String Port { get {return _config.port;} }
        public String DefaultCulture { get { return System.Globalization.CultureInfo.CurrentUICulture.ToString(); } }
        public Dictionary<String,String> GrammarFiles { get { return _grammarFiles; } }
        public int RootConfidenceLevel { get { return _config.RootConfidenceLevel; } }
        public Dictionary<String, int> Opcodes { get; private set; }
        public String AudioInput { get { return _config.AudioInput; } }
    }

}
