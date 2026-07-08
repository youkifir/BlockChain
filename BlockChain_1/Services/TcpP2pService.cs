using BlockChain_1.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockChain_1.Services
{
    public class TcpP2pService
    {
        public readonly TcpListener _listener;
        private readonly ConcurrentBag<TcpClient> _clients = new ConcurrentBag<TcpClient>();
        private readonly BlockChainService _blockChainService;
        public TcpP2pService(BlockChainService blockChainService, int port)
        {
            _blockChainService = blockChainService;
            _listener = new TcpListener(System.Net.IPAddress.Any, port);
        }
        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"P2P server started on port {((System.Net.IPEndPoint)_listener.LocalEndpoint).Port}");
            Task.Run(() => AcceptClientsAsync());
        }
        private async Task AcceptClientsAsync()
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _clients.Add(client);
                Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                Task.Run(() => HandleClientAsync(client));
            }
        }
        private async Task HandleClientAsync(TcpClient client)
        {

            using var stream = client.GetStream();
            using var reader = new System.IO.BinaryReader(stream, Encoding.UTF8);

            while (client.Connected)
            {
                try
                {
                    var messageLengthBytes = reader.ReadInt32();

                    var messageBytes = reader.ReadBytes(messageLengthBytes);
                    var messageJson = Encoding.UTF8.GetString(messageBytes);
                    if (messageJson != null)
                    {
                        Console.WriteLine($"Received message from {client.Client.RemoteEndPoint}: {messageJson}");
                        // Handle the received message (e.g., process transactions, blocks, etc.)
                        ProcessMessage(messageJson);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                    break;
                }
            }
        }
        private void ProcessMessage(string messageJson)
        {
            var message = JsonSerializer.Deserialize<P2pMessage>(messageJson);
            if (message == null) return;

            switch (message.Type)
            {
                case MessageType.NewBlock:
                    var newBlock = JsonSerializer.Deserialize<Block>(message.Data);
                    if (newBlock == null) return;

                    var lastBlock = _blockChainService.Chain.Last();
                    if (newBlock.Index == lastBlock.Index + 1 && newBlock.PreviousHash == lastBlock.Hash)
                    {
                        _blockChainService.Chain.Add(newBlock);
                        Console.WriteLine($"New block added to the chain: {newBlock.Index}");
                    }
                    else
                    {
                        Console.WriteLine($"Received invalid block: {newBlock.Index}");
                    }
                    break;
                case MessageType.SyncChain:
                    var receivedChain = JsonSerializer.Deserialize<List<Block>>(message.Data);
                    if (receivedChain == null) return;
                    var consensusChain = _blockChainService.ResolveConflicts(receivedChain);
                    if (consensusChain)
                    {
                        _blockChainService.Chain = receivedChain;
                        Console.WriteLine($"Blockchain synchronized with received chain. New length: {receivedChain.Count}");
                    }
                    else
                    {
                        BroadcastSync(); // Request the correct chain from peers
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown message type: {message.Type}");
                    break;
            }

        }
        private void BroadcastSync()
        {
            var message = new P2pMessage(MessageType.SyncChain, JsonSerializer.Serialize(_blockChainService.Chain));
            BroadcastMessage(message);
        }
        private void BroadcastMessage(P2pMessage message)
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);
            foreach (var client in _clients)
            {
                if (client.Connected)
                {
                    try
                    {
                        var stream = client.GetStream();
                        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                        writer.Write(messageLengthBytes, 0, messageLengthBytes.Length);
                        writer.Write(messageBytes, 0, messageBytes.Length);
                        Console.WriteLine($"Broadcasted message to client {client.Client.RemoteEndPoint}: {messageJson}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error broadcasting to client {client.Client.RemoteEndPoint}: {ex.Message}");
                    }
                }
            }
        }
        public void BroadCastNewBlock(Block newBlock)
        {
            var message = new P2pMessage(MessageType.NewBlock, JsonSerializer.Serialize(newBlock));
            BroadcastMessage(message);
        }
        public async Task ConnectToPeerAsync(string ipAddress, int port)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);
                _clients.Add(client);
                Console.WriteLine($"Connected to peer: {ipAddress}:{port}");
                BroadcastSync(); // Send the current blockchain to the newly connected peer
                await Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to peer {ipAddress}:{port} - {ex.Message}");
            }
        }
    }
}
