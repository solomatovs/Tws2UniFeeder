using System;
using System.Collections.Generic;
using System.Text;

namespace Tws2UniFeeder
{
    public interface ITwsProcess
    {
        bool TwsProcessIsRunning();
        bool RestartTwsProcess();
    }
}
