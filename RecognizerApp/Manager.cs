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
using System.ComponentModel;
using System.Threading;

namespace RecognizerApp
{
    /// <summary>
    /// this class that does all the work
    /// </summary>
    class Manager : IDisposable
    {
        #region ///////   P U B L I C   M E T H O D S   ///////

        public Manager()
        {
            InternalInit();
        }

        /// <summary>
        /// start recognition loop thread
        /// </summary>
        public void StartRecognize()
        {
            _worker.RunWorkerAsync();

            if (IsDemo)
                PlaySound();
        }

        /// <summary>
        /// handles speech recognition event
        /// </summary>
        /// <param name="recognizer">the active recognizer</param>
        /// <param name="result">recognition result</param>
        /// <returns></returns>
        private bool HandleRecognized(SpeechRecognitionEngine recognizer, RecognitionResult result)
        {
            if (!IsConfident(result))
                return false;

            _count++;   // successful recognitions counter, this is just for debugging

            // for debug
            _logger.PrintResult(result, _count);

            if (!IsDemo)
                _srMngr.SaveAudioStream(result);

            byte[] datagram = null;

            if (_parser.TryParseResult(result, recognizer.RecognizerInfo.Culture, out datagram))
            {
                SendBytes(datagram);
                return true;
            }
            return false;
        }

        /// <summary>
        /// stop recognition thread
        /// </summary>
        public void StopRecognize()
        {
            if (_worker != null && _worker.IsBusy)
                _worker.CancelAsync();

            if (ActiveRecognizer != null)
                ActiveRecognizer.RecognizeAsyncStop();

            StopSound();
        }

        /// <summary>
        /// stop recognition thread and dispose the recognizers
        /// </summary>
        public void Abort()
        {
            StopRecognize();

            for (int i = 0; i < _recognizers.Count; i++)
                _recognizers[i].Dispose();
        }

        /// <summary>
        /// set the matching recognizer as active
        /// </summary>
        /// <param name="info"></param>
        public void ChangeLanguage(CultureInfo info)
        {
            if (!ActiveCulture.Equals(info))
            {
                Console.WriteLine(String.Format("Switching culture from {0} to {1}.", ActiveCulture, info));
                StopRecognize();
                ActiveCulture = info;
                SetActiveRecognizer();
                StartRecognize();
            }
        }

        /// <summary>
        /// this method is called when an instance of this class is garbage-collected
        /// </summary>
        public void Dispose()
        {
            Abort();
        }

        #endregion

        #region ///////   P R I V A T E   M E T H O D S   ///////

        /// <summary>
        /// here the recognition event is handled
        /// </summary>
        /// <param name="sender">the backgounrd worker that raised the event</param>
        /// <param name="e">event argument</param>
        private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {            
            RecognitionResult res = e.UserState as RecognitionResult;
            if (!HandleRecognized(res))
                _logger.ReportRejectedRecognition(res, _count);
        }

        /// <summary>
        /// here the actual recognition loop is executed 
        /// </summary>
        /// <param name="sender">the background worker</param>
        /// <param name="e">not used</param>
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

                // in demo mode, we want to slow the rate of recognition output:
                if (IsDemo)
                    Thread.Sleep(4000);
            }
        }

        /// <summary>
        /// handle speech recognition event
        /// </summary>
        /// <param name="result"></param>
        /// <returns>true if recgnition is confident, and semantic was parsed</returns>
        private bool HandleRecognized(RecognitionResult result)
        {
            return HandleRecognized(ActiveRecognizer, result);
        }

        /// <summary>
        /// sends byte array on udp
        /// </summary>
        /// <param name="data">the datagram</param>
        /// <returns>tru if all data was sent</returns>
        private bool SendBytes(byte[] data)
        {
            // for debug
            _logger.PrintBytes(data);

            return _sender.Send(data) > 0;
        }

        /// <summary>
        /// used to switch recognizers (for different languages)
        /// </summary>
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

        /// <summary>
        /// this event handler only executed when the recognition loop throws an exception.
        /// this happens in demo mode, when the input wav file reaches end of file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecognitionLoopCompleted(object sender, RunWorkerCompletedEventArgs e)
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

        /// <summary>
        /// plays the input wav file, if exists (demo mode)
        /// </summary>
        private void PlaySound()
        {
            if (_player == null)
            {
                _player = new System.Media.SoundPlayer(Cfg.Instance.AudioInput);
            }

            _player.Stop();
            _player.Play();
        }

        /// <summary>
        /// stop the wav play (demo modeS)
        /// </summary>
        private void StopSound()
        {
            if (_player != null)
                _player.Stop();
        }

        /// <summary>
        /// recognition confidence level
        /// </summary>
        /// <param name="result">recognition result</param>
        /// <returns>true if the overall confidence is higher than threshold</returns>
        private bool IsConfident(RecognitionResult result)
        {
            return 100 * result.Words[0].Confidence > Cfg.Instance.RootConfidenceLevel;
        }

        private bool IsDemo { get { return File.Exists(Cfg.Instance.AudioInput); } }

        #endregion

        #region /////// I N I T ///////

        public bool IsInit { get; private set; }

        /// <summary>
        /// initiating members
        /// </summary>
        public void Init()
        {
            ActiveCulture = new CultureInfo(Cfg.Instance.DefaultCulture);

            InitWorker();
            IsInit = InitClient();
            InitGrammars();
            SetActiveRecognizer();
        }
        
        /// <summary>
        /// creating instances of members
        /// </summary>
        private void InternalInit()
        {
            _parser = new Parser();
            _sender = new ByteSender();
            _recognizers = new List<SpeechRecognitionEngine>();            
            _worker = new BackgroundWorker();
            _srMngr = new RecognitionManager();
            _logger = new Logger();

            IsInit = false;
        }

        /// <summary>
        /// creates recognizers and loads grammar files
        /// </summary>
        private void InitGrammars()
        {
            try
            {
                foreach (var v in Cfg.Instance.GrammarFiles)
                {
                    _recognizers.Add(_srMngr.CreateSpeechRecognizer(v.Key, v.Value));
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }            
        }

        /// <summary>
        /// the backgroundworker runs the recognition thread
        /// </summary>
        private void InitWorker()
        {
            _worker.DoWork += new DoWorkEventHandler(RecognitionLoop);
            _worker.ProgressChanged += new ProgressChangedEventHandler(_worker_ProgressChanged);
            _worker.WorkerReportsProgress = true;
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RecognitionLoopCompleted);
            _worker.WorkerSupportsCancellation = true;
        }

        /// <summary>
        /// udp client
        /// </summary>
        /// <returns>true if connection succeeded</returns>
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

        #region ///////   P R I V A T E     M E M B E R S   ///////

        private Parser _parser;
        private ISender _sender;
        private List<SpeechRecognitionEngine> _recognizers;
        BackgroundWorker _worker;
        System.Media.SoundPlayer _player;
        private RecognitionManager _srMngr;
        private ILogger _logger;

        private CultureInfo ActiveCulture { get; set; }
        private SpeechRecognitionEngine ActiveRecognizer { get; set; }
        private static int _count = 0;

        #endregion
    }
}
