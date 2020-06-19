using System;
using System.Windows.Forms;

namespace hotrun {
    static class Program {
        [STAThread]
        static void Main() {
            Application.Run(new HkWatcher());
        }
    }
}
