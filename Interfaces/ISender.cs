using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Interfaces
{
    public interface ISender
    {
        int Send(object o);
        void Connect(Object conn);
        void Close();
        bool IsConnected { get; }
    }

    public class ISender_moq : ISender
    {
        public int Send(object o) { return 0; }
        public void Connect(Object conn) { }
        public void Close() { }
        public bool IsConnected { get { return true; } }
    }
}
