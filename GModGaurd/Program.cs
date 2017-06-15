using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using GModGaurd.Classes;

namespace GModGaurd
{
    class Program
    {
        private static List<Server> Servers;
        private static ManualResetEvent ResetEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            if (!File.Exists("Servers.json"))
                File.WriteAllText("Servers.json", "[]");
    
            Servers = JsonConvert.DeserializeObject<List<Server>>(File.ReadAllText("Servers.json"));

            foreach (Server v in Servers)
                v.Init();

            ResetEvent.WaitOne();
        }
    }
}