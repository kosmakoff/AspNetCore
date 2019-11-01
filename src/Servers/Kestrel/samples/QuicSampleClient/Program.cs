using System;
using System.Buffers;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Transport.MsQuic;

namespace QuicSampleClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var transportOptions = new MsQuicTransportOptions()
            {
                Alpn = "QuicTest",
                RegistrationName = "Quic AspNetCore client",
                Certificate = CertificateLoader.LoadFromStoreCert("localhost", StoreName.My.ToString(), StoreLocation.CurrentUser, true),
                IdleTimeout = TimeSpan.FromHours(1),
            };

            var transportContext = new MsQuicTransportContext(null, null, transportOptions);
            var factory = new MsQuicConnectionFactory(transportContext);
            var connectionContext = await factory.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5555));
            var streamFeature = connectionContext.Features.Get<IStreamListener>();
            var streamContext = await streamFeature.StartBidirectionalStreamAsync();
            await streamContext.Transport.Output.WriteAsync(Encoding.ASCII.GetBytes("Test"));
            var readResult = await streamContext.Transport.Input.ReadAsync();
            if (readResult.Buffer.Length > 0)
            {
                Console.WriteLine(Encoding.ASCII.GetString(readResult.Buffer.ToArray()));
            }
        }
    }
}
