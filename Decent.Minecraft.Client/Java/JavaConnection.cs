﻿using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Decent.Minecraft.Client.Java
{
    /// <summary>
    /// A connection to a Minecraft game.
    /// </summary>
    public class JavaConnection : IConnection
    {
        private TcpClient _socket;
        private NetworkStream _stream;
        private StreamReader _streamReader;
        private bool _disposedValue = false; // To detect redundant calls
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public string Address { get; set; } = "localhost";
        public int Port { get; set; } = 4711;

        public JavaConnection()
        {
            _socket = new TcpClient(AddressFamily.InterNetwork);
        }

        public JavaConnection(string address = "localhost", int port = 4711) : this()
        {
            Address = address;
            Port = port;
        }

        public async Task OpenAsync()
        {
            try
            {
                await _socket.ConnectAsync(Address, Port);
                _stream = _socket.GetStream();
                _streamReader = new StreamReader(_stream);
            }
            catch (SocketException e)
            {
                throw new FailedToConnectToMinecraftEngine(e);
            }
        }

        public void Open()
        {
            OpenAsync().Wait();
        }

        public void Close()
        {
            Dispose();
        }

        public async Task SendAsync(string command, IEnumerable data)
        {
            var s = $"{command}({data.FlattenToString()})\n";
            Debug.WriteLineIf(!command.StartsWith("events."), $"Sending: {s}");
            await _semaphore.WaitAsync();
            try
            {
                var buffer = Encoding.ASCII.GetBytes(s);
                await _stream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException) { }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SendAsync(string command, params object[] data)
        {
            await SendAsync(command, (IEnumerable)data);
        }

        public void Send(string command, IEnumerable data)
        {
            SendAsync(command, data).Wait();
        }

        public void Send(string command, params object[] data)
        {
            SendAsync(command, data).Wait();
        }

        public async Task<string> ReceiveAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var response = await _streamReader.ReadLineAsync();
                Debug.WriteLineIf(!string.IsNullOrEmpty(response), $"Received: {response}");
                return response;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public string Receive()
        {
            return ReceiveAsync().Result;
        }

        public async Task<string> SendAndReceiveAsync(string command, IEnumerable data)
        {
            var s = $"{command}({data.FlattenToString()})\n";
            Debug.WriteLineIf(!command.StartsWith("events."), $"Sending: {s}");
            await _semaphore.WaitAsync();
            try
            {
                var buffer = Encoding.ASCII.GetBytes(s);
                await _stream.WriteAsync(buffer, 0, buffer.Length);
                var response = await _streamReader.ReadLineAsync();
                Debug.WriteLineIf(!string.IsNullOrEmpty(response), $"Received: {response}");
                return response;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<string> SendAndReceiveAsync(string command, params object[] data)
        {
            return await SendAndReceiveAsync(command, (IEnumerable)data);
        }

        public string SendAndReceive(string command, IEnumerable data)
        {
            return SendAndReceiveAsync(command, data).Result;
        }

        public string SendAndReceive(string command, params object[] data)
        {
            return SendAndReceiveAsync(command, data).Result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _streamReader.Dispose();
                    _stream.Dispose();
                    _socket.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
