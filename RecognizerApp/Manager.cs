using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Interfaces;
using System.Speech.Recognition;
using Client;
using System.Globalization;
using Configuration;
using System.Net;
using System.IO;
using System.Speech.AudioFormat;
using VoiceCommand;
using ProtoBuf;
using System.ComponentModel;

namespace RecognizerApp
{
    /// <summary>
    /// monster class that does all the work
    /// </summary>
    class Manager
    {
        public Manager()
        {
            InternalInit();
        }

        public void StartRecognize()
        {
            _worker.RunWorkerAsync();
        }

        private bool HandleRecognized(SpeechRecognitionEngine recognizer, RecognitionResult result)
        {
            if (!IsConfident(result))
                return false;

            // for debug
            PrintResult(result);

            VoiceCommandProto cmd = _parser.ParseResultToCommand(result);
            cmd.culture = recognizer.RecognizerInfo.Culture.ToString();
            SendCommand(cmd);

            return true;
        }

        public void StopRecognize()
        {
            _worker.CancelAsync();
            ActiveRecognizer.RecognizeAsyncStop();
        }

        public void DisposeRecognizers()
        {
            for (int i = 0; i < _recognizers.Count; i++)
                _recognizers[i].Dispose();
        }

        private bool IsConfident(RecognitionResult result)
        {
            return 100 * result.Words[0].Confidence > Cfg.Instance.RootConfidenceLevel;
        }

        /// <summary>
        /// here the recognition event is handled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _count++;   // recognitions counter, this is just for debugging
            RecognitionResult res = e.UserState as RecognitionResult;
            if (!HandleRecognized(res))
                ReportRejectedRecognition(res);
        }

        /// <summary>
        /// here the actual recognition loop is executed 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            RecognitionResult result;
            while (true)
            {
                result = ActiveRecognizer.Recognize();      // synchronic recognition
                if (result != null)
                {
                    _worker.ReportProgress(0, result);      // this event is (mis)used to handle the recognition result on the main app thread
                }
            }
        }

        private SpeechRecognitionEngine InitRecognizer(String culture, String grammarfile)
        {
            SpeechRecognitionEngine recognizer = new SpeechRecognitionEngine(new CultureInfo(culture));
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.LoadGrammar(new Grammar(grammarfile));
            return recognizer;
        }

        private static int _count = 0;

        private void PrintResult(RecognitionResult result)
        {
            string res = String.Format("res {0}: {1}", _count, result.DebugString());
            Console.WriteLine(res);
        }

        private void PrintCommand(VoiceCommandProto cmd)
        {
            Console.WriteLine(String.Format("cmd {0}: {1}", _count, cmd.DebugString()));
        }

        private bool HandleRecognized(RecognitionResult result)
        {
            return HandleRecognized(ActiveRecognizer, result);
        }

        private bool SendCommand(VoiceCommandProto cmd)
        {
            // for debug
            PrintCommand(cmd);

            using (Stream stream = new MemoryStream())
            {
                Serializer.Serialize(stream, cmd);
                byte[] data = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(data, 0, data.Length);
                return _sender.Send(data) > 0;
            }
        }

        private void ReportRejectedRecognition(RecognitionResult res)
        {
            Console.WriteLine(String.Format("Speech recognition rejected: Root confidence = {0}, threshold = {1}", res.Words[0].Confidence.ToString("F2"), (Cfg.Instance.RootConfidenceLevel / 100.0).ToString("F2") ));
        }

        #region /////// I N I T ///////

        public bool IsInit { get; private set; }

        public void Init()
        {
            InitWorker();
            IsInit = InitClient();
            IsInit &= InitGrammars();
            SetActiveRecognizer();
        }

        private void SetActiveRecognizer()
        {
            foreach (var v in _recognizers)
            {
                if (ActiveCulture.Equals(v.RecognizerInfo.Culture))
                {
                    ActiveRecognizer = v;
                }
            }
        }

        private void InternalInit()
        {
            _parser = new Parser();
            _sender = new ByteSender();
            _recognizers = new List<SpeechRecognitionEngine>();
            ActiveCulture = new CultureInfo(Cfg.Instance.DefaultCulture);
            _worker = new BackgroundWorker();
            IsInit = false;
        }

        private bool InitGrammars()
        {
            try
            {
                foreach (var v in Cfg.Instance.GrammarFiles)
                {
                    _recognizers.Add(InitRecognizer(v.Key, v.Value));
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void InitWorker()
        {
            _worker.DoWork += new DoWorkEventHandler(_worker_DoWork);
            _worker.ProgressChanged += new ProgressChangedEventHandler(_worker_ProgressChanged);
            _worker.WorkerReportsProgress = true;
        }

        private bool InitClient()
        {
            IPAddress ipaddress = null;
            int port = 0;

            if (IPAddress.TryParse(Cfg.Instance.IPAddress, out ipaddress) && int.TryParse(Cfg.Instance.Port, out port))
            {
                IPEndPoint ipendpoint = new IPEndPoint(ipaddress, port);
                _sender.Connect(ipendpoint);
            }
            return _sender.IsConnected;
        }

        #endregion

        #region /////// P U B L I C   ///////

        public CultureInfo ActiveCulture { get; private set; }
        public SpeechRecognitionEngine ActiveRecognizer { get; private set; }

        #endregion

        #region /////// P R I V A T E ///////

        private Parser _parser;
        private ISender _sender;
        private List<SpeechRecognitionEngine> _recognizers;
        BackgroundWorker _worker;

        #endregion
    }
}
