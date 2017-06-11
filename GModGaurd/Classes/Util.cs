using System;
using System.Collections.Generic;
using System.Text;

namespace GModGaurd.Classes
{
    class Util
    {
        public static bool IsValidSourcePacket(byte[] packet)
            => packet.Length > 4 && (packet[0] == 0xFF || packet[0] == 0xFE) && packet[1] == 0xFF && packet[2] == 0xFF && packet[3] == 0xFF;
    }
}
