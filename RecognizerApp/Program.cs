using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Configuration;
using System.Windows.Forms;

namespace RecognizerApp
{
    class Program
    {

        static void Main(string[] args)
        {
            using (Manager _mngr = new Manager())
            {
                try
                {
                    _mngr.Init();
                    _mngr.StartRecognize();
                    Application.Run();  // just to keep the background thread running
                }
                catch (Exception ex)
                {
                    MessageBox.Show(String.Format("Message: {0}\n, Stack: {1}\n InnerException: {2}",
                        ex.Message, ex.StackTrace, ex.InnerException), "Application Error");
                }
            }
        }
    }

}
