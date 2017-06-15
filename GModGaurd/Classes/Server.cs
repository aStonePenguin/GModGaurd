using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GModGaurd.Classes
{
    class Server
    {
        private A2SCache Cache;

        [JsonProperty]
        public string ServerIP = "127.0.0.0";
        public IPAddress _ServerIP;

        [JsonProperty]
        public int ServerPort = 27015;

        [JsonProperty]
        public string BindIP = "127.0.0.0";
        public IPAddress _BindIP;

        [JsonProperty]
        public int BindPort = 27010;

        [JsonProperty]
        public int RefreshRate = 5;

        private IPEndPoint ServerEndpoint;
        private UdpClient ServerSocket = new UdpClient();

        public void Init()
        {
           IPAddress.TryParse(ServerIP, out _ServerIP);
           IPAddress.TryParse(BindIP, out _BindIP);

            Cache = new A2SCache()
            {
                Hostname = _ServerIP,
                Port = ServerPort,
                RefreshRate = RefreshRate
            };

            Cache.Refresh().Wait();

            ServerEndpoint = new IPEndPoint(_BindIP, BindPort);
            ServerSocket.Client.Bind(ServerEndpoint);

            new Thread(Poll).Start();
        }

        private async void Poll()
        {
            while (true)
                HandlePacket(await ServerSocket.ReceiveAsync());
        }

        private async Task A2S_Info(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_INFO");
            if (Cache.Info != null)
                await ServerSocket.SendAsync(Cache.Info, Cache.Info.Length, result.RemoteEndPoint);
        }

        private async Task A2S_GetChallenge(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_GETCHALLENGE");
            if (Cache.Challenge != null)
                await ServerSocket.SendAsync(Cache.Challenge, Cache.Challenge.Length, result.RemoteEndPoint);
        }

        private async Task A2S_Players(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_PLAYER");
            if (Cache.Players != null && Cache.Challenge != null && result.Buffer[5] == Cache.Challenge[5] && result.Buffer[6] == Cache.Challenge[6] && result.Buffer[7] == Cache.Challenge[7] && result.Buffer[8] == Cache.Challenge[8])
                foreach (byte[] v in Cache.Players)
                    await ServerSocket.SendAsync(v, v.Length, result.RemoteEndPoint);
        }

        private async Task A2S_Rules(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_RULES");
            if (Cache.Rules != null && Cache.Challenge != null && result.Buffer[5] == Cache.Challenge[5] && result.Buffer[6] == Cache.Challenge[6] && result.Buffer[7] == Cache.Challenge[7] && result.Buffer[8] == Cache.Challenge[8])
                foreach (byte[] v in Cache.Rules)
                    await ServerSocket.SendAsync(v, v.Length, result.RemoteEndPoint);
        }

        private async void HandlePacket(UdpReceiveResult result)
        {
#if DEBUG
            Console.WriteLine("\n-----------------------------------------------------------");
            Console.WriteLine("Packet: " + Encoding.UTF8.GetString(result.Buffer));
            Console.WriteLine("Len: " + result.Buffer.Length);
            Console.WriteLine("Source: " + result.RemoteEndPoint.Address);
# endif
            if (Util.IsValidSourcePacket(result.Buffer))
            {
                //foreach (var v in result.Buffer)
                    //Console.WriteLine(v);

                if (result.Buffer.Length == 9 && ((result.Buffer[5] == 0xFF && result.Buffer[6] == 0xFF && result.Buffer[7] == 0xFF && result.Buffer[8] == 0xFF) || (result.Buffer[5] == 0x00 && result.Buffer[6] == 0x00 && result.Buffer[7] == 0x00 && result.Buffer[8] == 0x00))) // Steam sends 0's, everything else sends FF.
                    await A2S_GetChallenge(result);
                else
                {
                    switch (result.Buffer[4])
                    {
                        case 0x54:
                            await A2S_Info(result);
                            break;

                        case 0x55:
                            await A2S_Players(result);
                            break;

                        case 0x56:
                            await A2S_Rules(result);
                            break;
                    }
                }
            }
        }
    }
}