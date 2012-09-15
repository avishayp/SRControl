/*************************
 * 
 * 
 * 
 * *******************************/
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
    class Manager : IDisposable
    {
        public Manager()
        {
            InternalInit();
        }

        public void StartRecognize()
        {
            _worker.RunWorkerAsync();

            if (IsDemo)
                PlaySound();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recognizer"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private bool HandleRecognized(SpeechRecognitionEngine recognizer, RecognitionResult result)
        {
            if (!IsConfident(result))
                return false;

            _count++;   // successful recognitions counter, this is just for debugging

            // for debug
            PrintResult(result);
            SaveAudioStream(result);

            VoiceCommandProto cmd = _parser.ParseResultToCommand(result);
            byte[] datagram = _parser.ParseResult(result, recognizer.RecognizerInfo.Culture);

            // protobuf - not used
            //cmd.culture = recognizer.RecognizerInfo.Culture.ToString();
            //SendCommand(cmd);

            SendBytes(datagram);
            return true;
        }

        public void StopRecognize()
        {
            if (_worker != null && _worker.IsBusy)
                _worker.CancelAsync();

            if (ActiveRecognizer != null)
                ActiveRecognizer.RecognizeAsyncStop();

            StopSound();

            if (File.Exists(TempFileName))
                File.Delete(TempFileName);
        }

        public void Abort()
        {
            StopRecognize();

            for (int i = 0; i < _recognizers.Count; i++)
                _recognizers[i].Dispose();
        }

        /// <summary>
        /// here the recognition event is handled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {            
            RecognitionResult res = e.UserState as RecognitionResult;
            if (!HandleRecognized(res))
                ReportRejectedRecognition(res);
        }

        /// <summary>
        /// here the actual recognition loop is executed 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecognitionLoop(object sender, DoWorkEventArgs e)
        {
            RecognitionResult result;
            while (true)
            {
                result = ActiveRecognizer.Recognize();      // synchronic recognition

                if (result != null)
                {
                    ((BackgroundWorker)sender).ReportProgress(0, result);      // this event is (mis)used to handle the recognition result on the main app thread
                }
            }
        }

        private SpeechRecognitionEngine InitRecognizer(String culture, String grammarfile)
        {
            Console.WriteLine(String.Format("New recognizer: culture={0}, grammar={1}", culture, grammarfile));

            if (!File.Exists(grammarfile))
            {
                throw new Exception(String.Format("Grammar file {0} for culture {1} was not found.", grammarfile, culture.ToString()));
            }

            SpeechRecognitionEngine recognizer = new SpeechRecognitionEngine(new CultureInfo(culture));

            if (String.IsNullOrEmpty(Cfg.Instance.AudioInput))
            {
                try
                {
                    recognizer.SetInputToDefaultAudioDevice();
                    Console.WriteLine("Voice is recorded to " + _wavFile);
                }
                catch (InvalidOperationException)
                {
                    throw new Exception("Failed to set audio input device - make sure microphone is connected and working!");
                }
            }
            else
            {
                if (File.Exists(Cfg.Instance.AudioInput))
                {                    
                    recognizer.SetInputToWaveFile(Cfg.Instance.AudioInput);
                }
                else
                {
                    throw new Exception("Speech recognizer init with wav file input failed because the file " + Cfg.Instance.AudioInput +
                        " was not found.\nLeave AudioInput configuration field empty to use microphone the audio input device!");
                }
            }

            try
            {
                recognizer.LoadGrammar(new Grammar(grammarfile));
            }
            catch (NotSupportedException ex)
            {
                recognizer.LoadGrammar(new DictationGrammar());
                Console.WriteLine("Grammar file " + grammarfile + " is not supported - operating in dictation mode.");
            }

            return recognizer;
        }        

        private void PrintResult(RecognitionResult result)
        {
            string res = String.Format("res {0}: {1}", _count, result.DebugString());
            Console.WriteLine(res);
        }

        private void PrintCommand(VoiceCommandProto cmd)
        {
            Console.WriteLine(String.Format("cmd {0}: {1}", _count, cmd.DebugString()));
        }

        private void PrintBytes(byte[] data)
        {
            Console.WriteLine("bytes: " + String.Join(" ", Array.ConvertAll(data, Convert.ToString)));
        }

        private bool HandleRecognized(RecognitionResult result)
        {
            return HandleRecognized(ActiveRecognizer, result);
        }

        private bool SendBytes(byte[] data)
        {
            // for debug
            PrintBytes(data);
            Console.WriteLine();

            return _sender.Send(data) > 0;
        }

        private void PlaySound()
        {
            if (_player == null)
            {
                _player = new System.Media.SoundPlayer(Cfg.Instance.AudioInput);
            }

            _player.Stop();
            _player.Play();
        }

        private void StopSound()
        {
            if (_player != null)
                _player.Stop();
        }

        private void SaveAudioStream(RecognitionResult res)
        {
            if (IsDemo)
                return;

            if (!File.Exists(_wavFile))
            {
                using (Stream stream = File.Create(_wavFile))
                {
                    res.Audio.WriteToWaveStream(stream);
                }
            }
            else
            {
                using (Stream stream = File.Create(TempFileName))
                {
                    res.Audio.WriteToWaveStream(stream);
                }

                AudioHelper.AudioMixer.Concatenate(_wavFile, TempFileName, 0.5);
                File.Delete(TempFileName);
            }
        }

        private String TempFileName
        {
            get { return _wavFile + ".temp"; }
        }

        private String GetWavFileName()
        {
            int k = 1;

            String dir = @"c:\temp\sounds";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            String file = String.Format("audio_{0}.wav", k);

            while (File.Exists(Path.Combine(dir, file)))
            {
                file = String.Format("audio_{0}.wav", ++k);
            }
            return Path.Combine(dir, file);
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

                // for debug
                PrintBytes(data);
                Console.WriteLine();

                return _sender.Send(data) > 0;
            }
        }

        private void ReportRejectedRecognition(RecognitionResult res)
        {
            Console.WriteLine(String.Format("Speech recognition rejected: Root confidence = {0}, threshold = {1}", res.Words[0].Confidence.ToString("F2"), (Cfg.Instance.RootConfidenceLevel / 100.0).ToString("F2") ));
            PrintResult(res);
        }

        private bool IsConfident(RecognitionResult result)
        {
            return 100 * result.Words[0].Confidence > Cfg.Instance.RootConfidenceLevel;
        }

        private bool IsDemo { get { return File.Exists(Cfg.Instance.AudioInput); } }

        #region /////// I N I T ///////

        public bool IsInit { get; private set; }

        public void Init()
        {
            ActiveCulture = new CultureInfo(Cfg.Instance.DefaultCulture);

            InitWorker();
            IsInit = InitClient();
            InitGrammars();
            RegisterRecognitionEvents();
            SetActiveRecognizer();
        }

        private void RegisterRecognitionEvents()
        {
            foreach (var v in _recognizers)
            {
                v.SpeechDetected += new EventHandler<SpeechDetectedEventArgs>(SpeechDetected);
            }
        }

        private void SpeechDetected(object sender, SpeechDetectedEventArgs e)
        {
            // Console.WriteLine("Detecting speech...");
        }

        private void SetActiveRecognizer()
        {
            foreach (var v in _recognizers)
            {
                if (ActiveCulture.Equals(v.RecognizerInfo.Culture))
                {
                    ActiveRecognizer = v;
                    Console.WriteLine("Active recognizer: " + ActiveCulture.ToString());
                    if (IsDemo)
                        Console.WriteLine("Running from wav file: " + Cfg.Instance.AudioInput);
                }
            }
        }

        private void InternalInit()
        {
            _parser = new Parser();
            _sender = new ByteSender();
            _recognizers = new List<SpeechRecognitionEngine>();            
            _worker = new BackgroundWorker();
            IsInit = false;
            _wavFile = GetWavFileName();
        }

        private void InitGrammars()
        {
            try
            {
                foreach (var v in Cfg.Instance.GrammarFiles)
                {
                    _recognizers.Add(InitRecognizer(v.Key, v.Value));
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }            
        }

        private void InitWorker()
        {
            _worker.DoWork += new DoWorkEventHandler(RecognitionLoop);
            _worker.ProgressChanged += new ProgressChangedEventHandler(_worker_ProgressChanged);
            _worker.WorkerReportsProgress = true;
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_worker_RunWorkerCompleted);
            _worker.WorkerSupportsCancellation = true;
        }

        void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                StopRecognize();
                ActiveRecognizer.SetInputToNull();
                Console.WriteLine("Reached end of input file.");
            }

            if (e.Cancelled)
                Console.WriteLine("Recognition stopped!");
            else
                Console.WriteLine("Recognition completed.");
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
        String _wavFile;
        System.Media.SoundPlayer _player;
        private static int _count = 0;

        #endregion

        public void Dispose()
        {
            Abort();
        }
    }
}
