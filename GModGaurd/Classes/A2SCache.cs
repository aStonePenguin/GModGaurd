using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GModGaurd.Classes
{
    class A2SCache
    {
        public IPAddress Hostname;
        public int Port = 27015;
        public IPEndPoint EndPoint;

        public int RefreshRate = 5;

        public DateTime LastCache;

        public byte[] Info;

        public byte[] PlayersChallenge;
        public byte[][] Players;

        public byte[] RulesChallenge;
        public byte[][] Rules;

        private readonly byte[] InfoRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 };
        private readonly byte InfoResponseHeader = 0x49;
        private readonly byte ChallengeResponseHeader = 0x41;

        private readonly byte[] PlayersChallengeRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF };
        private readonly byte[] PlayersRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55 };
        private readonly byte PlayersResponseHeader = 0x44;

        private readonly byte[] RulesChallengeRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x56, 0xFF, 0xFF, 0xFF, 0xFF };
        private readonly byte[] RulesRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x56 };
        private readonly byte RulesResponseHeader = 0x45;

        private readonly byte[] Challenge = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x41 }; // how do we want to do this one HMM

        public A2SCache()
        {
            new Thread(Poll).Start();
        }

        public async Task Refresh()
        {
            LastCache = DateTime.Now;

            if (EndPoint == null)
                EndPoint = new IPEndPoint(Hostname, Port);

            using (UdpClient client = new UdpClient()) // SendTimeout is currently non functional on UDPClients, this works
            {
                try
                {
                    var finished = await Task.WhenAny(_Refresh(client), Task.Delay(1000));
                    if (!finished.IsCompleted)
                        throw new Exception("A2SCache.Refresh timed out!");
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to cache!");
                    client.Dispose();
                }
            }
        }

        private async Task _Refresh(UdpClient client)
        {
            Info = await RequestInfo(client);
            Console.WriteLine("RequestInfo");

            Players = await RequestPlayers(client);
            Console.WriteLine("RequestPlayer");

            Rules = await RequestRules(client);
            Console.WriteLine("RequestRules");

            Console.WriteLine("Finished refresh!");
        }

        private async Task<byte[]> WaitForResponse(UdpClient client, byte header)
        {
            UdpReceiveResult result = await client.ReceiveAsync();

            Console.WriteLine(Encoding.UTF8.GetString(result.Buffer));
            
            //if (!Util.IsValidSourcePacket(result.Buffer) || result.Buffer[4] != header)
            //    throw new Exception("Invalid source packet!");

            return result.Buffer;
        }

        private async Task<byte[][]> WaitForMultiPacketResponse(UdpClient client, byte header)
        {
            byte[] firstresp = await WaitForResponse(client, header);
            byte[][] response;

            if (firstresp[0] == 0xFE)
            {
                int packets = Convert.ToInt32(firstresp[8]);
                response = new byte[packets][];
                for (int i = 1; i < packets; i++)
                    response[i] = await WaitForResponse(client, header);
            }
            else
                response = new byte[1][];

            response[0] = firstresp;

            return response;
        }

        private async Task<byte[]> RequestInfo(UdpClient client)
        {
            await client.SendAsync(InfoRequest, InfoRequest.Length, EndPoint);
            return await WaitForResponse(client, InfoResponseHeader);
        }

        private async Task<byte[]> RequestPlayersChallenge(UdpClient client)
        {
            await client.SendAsync(PlayersChallengeRequest, PlayersChallengeRequest.Length, EndPoint);
            byte[] response = new byte[4];
            Array.Copy(await WaitForResponse(client, ChallengeResponseHeader), 5, response, 0, 4);
            return response;
        }

        private async Task<byte[][]> RequestPlayers(UdpClient client)
        {
            PlayersChallenge = await RequestPlayersChallenge(client);

            byte[] request = new byte[PlayersChallenge.Length + PlayersRequest.Length];
            Array.Copy(PlayersRequest, 0, request, 0, PlayersRequest.Length);
            Array.Copy(PlayersChallenge, 0, request, PlayersRequest.Length, PlayersChallenge.Length);
            await client.SendAsync(request, request.Length, EndPoint);

            return await WaitForMultiPacketResponse(client, PlayersResponseHeader);  
        }

        private async Task<byte[]> RequestRulesChallenge(UdpClient client)
        {
            await client.SendAsync(RulesChallengeRequest, RulesChallengeRequest.Length, EndPoint);
            byte[] response = new byte[4];
            Array.Copy(await WaitForResponse(client, ChallengeResponseHeader), 5, response, 0, 4);
            return response;
        }

        private async Task<byte[][]> RequestRules(UdpClient client)
        {
            RulesChallenge = await RequestRulesChallenge(client);

            byte[] request = new byte[RulesChallenge.Length + RulesRequest.Length];

            Array.Copy(RulesRequest, 0, request, 0, RulesRequest.Length);
            Array.Copy(RulesChallenge, 0, request, RulesRequest.Length, RulesChallenge.Length);

            await client.SendAsync(request, request.Length, EndPoint);

            return await WaitForMultiPacketResponse(client, RulesResponseHeader);
        }

        private async void Poll()
        {
            Thread.Sleep(RefreshRate * 1000);
            await Refresh();
        }
    }
}
