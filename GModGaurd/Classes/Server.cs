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

        private IPEndPoint ServerEndpoint;
        private UdpClient ServerSocket = new UdpClient();

        public void Init()
        {
           IPAddress.TryParse(ServerIP, out _ServerIP);
           IPAddress.TryParse(BindIP, out _BindIP);

            Cache = new A2SCache()
            {
                Hostname = _ServerIP,
                Port = ServerPort
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

        private async void A2S_Info(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_INFO");
            if (Cache.Info != null)
                await ServerSocket.SendAsync(Cache.Info, Cache.Info.Length, result.RemoteEndPoint);
        }

        private async void A2S_Player(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_PLAYER");
            if (Cache.Players != null)
                foreach (byte[] v in Cache.Players)
                    await ServerSocket.SendAsync(v, v.Length, result.RemoteEndPoint);
                    
        }

        private async void A2S_Rules(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_RULES");
            if (Cache.Rules != null)
                foreach (byte[] v in Cache.Rules)
                    await ServerSocket.SendAsync(v, v.Length, result.RemoteEndPoint);
        }

        private async void A2S_GetChallenge(UdpReceiveResult result)
        {
            Console.WriteLine("A2S_GETCHALLENGE");

            //if (Cache.Challenge != null)
                // await ServerSocket.SendAsync();
        }

        private async void HandlePacket(UdpReceiveResult result)
        {
            Console.WriteLine("\n-----------------------------------------------------------");
            Console.WriteLine("Packet: " + Encoding.UTF8.GetString(result.Buffer));
            Console.WriteLine("Len: " + result.Buffer.Length);
            Console.WriteLine("Source: " + result.RemoteEndPoint.Address);

            if (Util.IsValidSourcePacket(result.Buffer))
            {
                switch (result.Buffer[4])
                {
                    case 0x54:
                        A2S_Info(result);
                        break;

                    case 0x55:
                        A2S_Player(result);
                        break;

                    case 0x56:
                        A2S_Rules(result);
                        break;

                    case 0x57:
                        A2S_GetChallenge(result);
                        break;
                }
            }
        }
    }
}