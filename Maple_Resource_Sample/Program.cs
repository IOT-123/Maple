/*
 * 2018-07-11 Nic Roche: add to repository
 * 
 * 
*/

using Maple;
using Netduino.Foundation.Network;
using System.Threading;

namespace Maple_Resource_Sample
{
    public class Program
    {
        public static void Main()
        {
            MapleServer server = new MapleServer();
            Initializer.NetworkConnected += (s, e) =>
            {
                // start maple server and send name broadcast address
                server.Start("assimilate server", Initializer.CurrentNetworkInterface.IPAddress);
            };
            Initializer.InitializeNetwork();
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
