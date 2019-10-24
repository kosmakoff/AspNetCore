// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.MsQuic.Internal
{
    /// <summary>
    /// Listens for new Quic Connections
    /// </summary>
    internal class MsQuicConnectionListener : IConnectionListener, IAsyncDisposable
    {
        private QuicRegistration _registration;
        private QuicSecConfig _secConfig;
        private QuicSession _session;
        private QuicListener _listener;
        private IAsyncEnumerator<MsQuicConnection> _acceptEnumerator;
        private bool _disposed;
        private bool _stopped;

        private readonly Channel<MsQuicConnection> _acceptConnectionQueue = Channel.CreateUnbounded<MsQuicConnection>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        public MsQuicConnectionListener(MsQuicTransportContext transportContext, EndPoint endpoint)
        {
            _registration = new QuicRegistration();
            TransportContext = transportContext;
            EndPoint = endpoint;
        }

        public MsQuicTransportContext TransportContext { get; }
        public EndPoint EndPoint { get; set; }

        public IHostApplicationLifetime AppLifetime => TransportContext.AppLifetime;
        public IMsQuicTrace Log => TransportContext.Log;

        public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (await _acceptEnumerator.MoveNextAsync())
            {
                return _acceptEnumerator.Current;
            }

            return null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            StopAcceptingConnections();

            await UnbindAsync().ConfigureAwait(false);

            // TODO do something with _listener;

            //await StopThreadsAsync().ConfigureAwait(false);
        }

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;

            await DisposeAsync();
        }

        internal async Task BindAsync()
        {
            // TODO make this configurable
            await StartAsync();

            _acceptEnumerator = AcceptConnectionsAsync();
        }

        private async IAsyncEnumerator<MsQuicConnection> AcceptConnectionsAsync()
        {
            while (true)
            {
                while (await _acceptConnectionQueue.Reader.WaitToReadAsync())
                {
                    while (_acceptConnectionQueue.Reader.TryRead(out var connection))
                    {
                        yield return connection;
                    }
                }

                yield return null;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _registration.RegistrationOpen(Encoding.ASCII.GetBytes(TransportContext.Options.RegistrationName));

            _secConfig = await _registration.CreateSecurityConfig(TransportContext.Options.Certificate);

            _session = _registration.SessionOpen(TransportContext.Options.Alpn);

            _listener = _session.ListenerOpen(ListenerCallbackHandler);
            _session.SetIdleTimeout(TransportContext.Options.IdleTimeout);
            _session.SetPeerBiDirectionalStreamCount(TransportContext.Options.MaxBidirectionalStreamCount);
            _session.SetPeerUnidirectionalStreamCount(TransportContext.Options.MaxBidirectionalStreamCount);

            _listener.Start(EndPoint as IPEndPoint);
        }

        private QUIC_STATUS ListenerCallbackHandler(
            ref NativeMethods.ListenerEvent evt)
        {
            switch (evt.Type)
            {
                case QUIC_LISTENER_EVENT.NEW_CONNECTION:
                    {
                        var connection = new QuicConnection(_registration, evt.Data.NewConnection.Connection, false);

                        evt.Data.NewConnection.SecurityConfig = _secConfig.NativeObjPtr;
                        var msQuicConnection = new MsQuicConnection(connection);
                        _acceptConnectionQueue.Writer.TryWrite(msQuicConnection);
                    }
                    break;
                default:
                    return QUIC_STATUS.INTERNAL_ERROR;
            }
            Console.WriteLine("Woah?");

            return QUIC_STATUS.SUCCESS;
        }

        protected void StopAcceptingConnections()
        {
            _acceptConnectionQueue.Writer.TryComplete();
        }
    }
}