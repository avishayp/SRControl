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
            Manager mngr = new Manager();

            mngr.Init();
            mngr.StartRecognize();
            Application.Run();  // just to keep the background thread running
        }

    }

}
