using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GModGaurd.Classes
{
    class A2SCache
    {
        public IPAddress Hostname;
        public int Port;
        public IPEndPoint EndPoint;

        public int RefreshRate;

        public TimeSpan LastCache = TimeSpan.MinValue;

        public byte[] Info;
        public byte[] Challenge;
        public byte[][] Players;
        public byte[][] Rules;


        // https://developer.valvesoftware.com/wiki/Server_queries
        private readonly byte[] InfoRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 };
        private readonly byte InfoResponseHeader = 0x49;
        private readonly byte InfoRequestChallenge = 0x41;

        private readonly byte[] ChallengeRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF }; // GetChallenge returns the same challenge for all types so we'll just request a player challenge and cache it
        private readonly byte ChallengeResponseHeader = 0x41;

        private readonly byte[] PlayersRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55 };
        private readonly byte PlayersResponseHeader = 0x44;

        private readonly byte[] RulesRequest = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x56 };
        private readonly byte RulesResponseHeader = 0x45;


        public A2SCache()
            => new Thread(Poll).Start();

        public async Task Refresh()
        {
           
            EndPoint ??= new IPEndPoint(Hostname, Port);

            using (UdpClient client = new()) // SendTimeout is currently non functional on UDPClients, this works
            {
                try
                {
                    Task task1 = _Refresh(client);
                    Task task2 = Task.Delay(1000);
                    Task finished = await Task.WhenAny(task1, task2);

                    if (!task1.IsCompleted)
                        throw new Exception("A2SCache.Refresh timed out!");
                }
                catch (Exception ex)
                {
                    if (LastCache.Seconds > 10) // Stop responding if the server fails to respond for 10 seconds
                    {
                        Info = null;
                        Players = null;
                        Rules = null;
                        Challenge = null;
                    }
         
                    Console.WriteLine("Failed to cache: " + ex.Message);
                    client.Dispose();
                }
            }
        }

        private async Task _Refresh(UdpClient client)
        {
            Info = await RequestInfo(client);
            Console.WriteLine("A2SCache -> RequestInfo");

            Challenge = await RequestChallenge(client);
            Console.WriteLine("A2SCache -> RequestChallenge");

            Players = await RequestPlayers(client);
            Console.WriteLine("A2SCache -> RequestPlayer");

            Rules = await RequestRules(client);
            Console.WriteLine("A2SCache -> RequestRules");

            Console.WriteLine("A2SCache -> Finished refresh!");
        }

        private async Task<byte[]> WaitForResponse(UdpClient client, byte header, bool validate = true)
        {
            UdpReceiveResult result = await client.ReceiveAsync();

            if ((!Util.IsValidSourcePacket(result.Buffer) || result.Buffer[4] != header) && validate)
                throw new Exception("Invalid source packet!");

            return result.Buffer;
        }

        private async Task<byte[][]> WaitForMultiPacketResponse(UdpClient client, byte header)
        {
            byte[] firstresp = await WaitForResponse(client, header, false);
            byte[][] response;

            if (firstresp[0] == 0xFE)
            {
                int packets = Convert.ToInt32(firstresp[8]);
                response = new byte[packets][];
                for (int i = 1; i < packets; i++)
                    response[i] = await WaitForResponse(client, header, false);
            }
            else
                response = new byte[1][];

            response[0] = firstresp;

            return response;
        }

        private async Task<byte[]> RequestInfo(UdpClient client)
        {
            await client.SendAsync(InfoRequest, InfoRequest.Length, EndPoint);

            UdpReceiveResult result = await client.ReceiveAsync();

            if (Util.IsValidSourcePacket(result.Buffer))
            {
                if (result.Buffer[4] == InfoResponseHeader)
                {
                    return result.Buffer;
                }
                else if (result.Buffer[4] == InfoRequestChallenge)
                {
                    int len = InfoRequest.Length + 4;
                    byte[] infoRequestChallenged = new byte[len];

                    for (int i = 0; i < InfoRequest.Length; i++)
                        infoRequestChallenged[i] = InfoRequest[i];

                    for (int i = 0; i < 4; i++)
                        infoRequestChallenged[InfoRequest.Length + i] = result.Buffer[i + 5];


                    await client.SendAsync(infoRequestChallenged, infoRequestChallenged.Length, EndPoint);
                    return await WaitForResponse(client, InfoResponseHeader);

                }
            }

            throw new Exception("Invalid source packet!");
        }

        private async Task<byte[]> RequestChallenge(UdpClient client)
        {
            await client.SendAsync(ChallengeRequest, ChallengeRequest.Length, EndPoint);
            return await WaitForResponse(client, ChallengeResponseHeader);
        }

        private async Task<byte[][]> RequestPlayers(UdpClient client)
        {
            byte[] request = new byte[PlayersRequest.Length + 4];
            Array.Copy(PlayersRequest, 0, request, 0, PlayersRequest.Length);
            Array.Copy(new byte[] { Challenge[5], Challenge[6], Challenge[7], Challenge[8] }, 0, request, RulesRequest.Length, 4);

            await client.SendAsync(request, request.Length, EndPoint);

            return await WaitForMultiPacketResponse(client, PlayersResponseHeader);
        }

       private async Task<byte[][]> RequestRules(UdpClient client)
       {
            byte[] request = new byte[RulesRequest.Length + 4];
            Array.Copy(RulesRequest, 0, request, 0, RulesRequest.Length);
            Array.Copy(new byte[] { Challenge[5], Challenge[6], Challenge[7], Challenge[8] }, 0, request, RulesRequest.Length, 4);

            await client.SendAsync(request, request.Length, EndPoint);

            return await WaitForMultiPacketResponse(client, RulesResponseHeader);
       }

        private async void Poll()
        {
            while (true)
            {
                Thread.Sleep(RefreshRate * 1000);
                await Refresh();
            }
        }
    }
}
