﻿#region

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using API;
using API.Server;
using API.Session;
using log4net;
using SilkroadSecurityAPI;

#endregion

namespace DuckSoup.Library.Session;

public sealed class Session : ISession
{
    private readonly byte[] _clientBuffer = new byte[4096];
    private readonly Security _clientSecurity = new();

    private readonly ILog _logger = Global.Logger;
    private readonly byte[] _serverBuffer = new byte[4096];
    private readonly Security _serverSecurity = new();
    private TcpClient _clientTcpClient;
    private bool _exit;
    private TcpClient _serverTcpClient;


    public Session(TcpClient clientTcpClient, IAsyncServer asyncServer)
    {
        SessionData = new SessionData();
        AsyncServer = asyncServer;
        _clientTcpClient = clientTcpClient;

        if (SharedObjects.DebugLevel >= DebugLevel.Connections)
            _logger.InfoFormat("{0} - Preparing Session..", asyncServer.Service.Name);


        // generates a "unique" id from the address and port hashcode and safes ip
        if (!(_clientTcpClient.Client.RemoteEndPoint is IPEndPoint ep)) return;
        ClientId = ep.Address.GetHashCode() + ep.Port.GetHashCode();
        ClientIp = ep.Address.ToString();
    }

    public IAsyncServer AsyncServer { get; init; }

    public int ClientId { get; set; }
    public string ClientIp { get; set; }

    public void Dispose()
    {
        Dispose("Unknown reason");
    }
    
    public void Dispose(string reason)
    {
        if (SharedObjects.DebugLevel >= DebugLevel.Connections)
            _logger.InfoFormat("{0} - Stop Session - {1} ({2}) - {3}",
                AsyncServer.Service.Name, ClientId,
                ClientIp,
                reason);

        // double socket close prevention
        if (_exit) return;
        _exit = true;

        _clientTcpClient?.Close();
        _clientTcpClient = null;
        _serverTcpClient?.Close();
        _serverTcpClient = null;
        SessionData?.Dispose();
        // removes the session from the session list - the function has a contains check
        AsyncServer.RemoveSession(this);
    }

    public async Task SendToClient(Packet packet)
    {
        if (_clientTcpClient == null || _serverTcpClient == null || _exit)
            return;

        try
        {
            _clientSecurity.Send(packet);
            // probably not needed tho since we're in a permanent circle anyways
            // might be the reason login and so on is FUCKING slow
            await TransferToClient();
        }
        catch (Exception)
        {
            Dispose("send to client");
        }
    }

    public async Task SendToServer(Packet packet)
    {
        if (_clientTcpClient == null || _serverTcpClient == null || _exit)
            return;

        try
        {
            _serverSecurity.Send(packet);
            // probably not needed tho since we're in a permanent circle anyways
            // might be the reason login and so on is FUCKING slow
            await TransferToServer();
        }
        catch (Exception)
        {
            Dispose("send to server");
        }
    }

    public async Task SendNotice(string message)
    {
        var notice = new Packet(0x3026, false, false);
        notice.WriteByte(7);
        notice.WriteAscii(message);
        await SendToClient(notice);
    }

    public async Task Start()
    {
        if (SharedObjects.DebugLevel >= DebugLevel.Connections)
            _logger.InfoFormat("{0} - Starting Session..", AsyncServer.Service.Name);

        _clientSecurity.GenerateSecurity(true, true, true);
        // creates a new server socket and connects it according to the remote addr. and port 
        _serverTcpClient = new TcpClient();
        await _serverTcpClient.ConnectAsync(AsyncServer.RemoteEndPoint.Address, AsyncServer.RemoteEndPoint.Port);

        // Just making sure to disconnect clients that have fucked up
        _ = Task.Factory.StartNew(() =>
        {
            Thread.Sleep(500);
            if (_serverTcpClient is not {Connected: true} || _clientTcpClient is not {Connected: true})
                Dispose("500 ms");

            return Task.CompletedTask;
        });

        // starts receiving loop from server + client - if a destroy was called the loop brakes
        _ = Task.Factory.StartNew(async () =>
        {
            while (!_exit)
                await DoReceiveFromServer();

            _serverTcpClient?.Close();
            _serverTcpClient = null;
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        _ = Task.Factory.StartNew(async () =>
        {
            while (!_exit)
                await DoReceiveFromClient();

            _clientTcpClient?.Close();
            _clientTcpClient = null;
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private async Task DoReceiveFromClient()
    {
        try
        {
            // receive stuff
            var clientBufferMemory = new Memory<byte>(_clientBuffer);
            var recvCount = await _clientTcpClient.GetStream().ReadAsync(clientBufferMemory);

            if (recvCount == 0)
            {
                Dispose("receive count 0");
                return;
            }

            // starts receiving again
            _clientSecurity.Recv(_clientBuffer, 0, recvCount);

            // transfers the incoming packets to a list
            var receivedPackets = _clientSecurity.TransferIncoming();

            // if there are no packets start receiving again
            if (receivedPackets == null)
            {
                await TransferToServer();
                return;
            }


            // loop through all received packets
            foreach (var packet in receivedPackets)
            {
                // ignore handshake
                if (packet.Opcode == 0x9000 || packet.Opcode == 0x5000 || packet.Opcode == 0x2001)
                    continue;

                #region Protection

                // Packet Modifying
                PacketLength = packet.GetBytes().Length;

                // Packet Flooding
                // calc the last checktime
                var lastCheckDiff = (DateTime.Now - _lastPacketReset).TotalSeconds;
                // lastcheckdiff * bytelimitation for exact bytes per time - don't need to round it to one second
                var maxBytesPerTime = lastCheckDiff * AsyncServer.Service.ByteLimitation;
                // if the packetsize exceeded the calculated value and it was measured over 2 second (to prevent super random lag dcs)
                if (_packetSize > maxBytesPerTime && lastCheckDiff > 2.0)
                {
                    if (SharedObjects.DebugLevel >= DebugLevel.Warning)
                        _logger.WarnFormat(
                            "{0} - Client {1}({2}) exceedet the byte limit: {3} (maximum: {4} - Last check {5} seconds ago)",
                            AsyncServer.Service.Name, ClientId, ClientIp,
                            _packetSize, maxBytesPerTime, lastCheckDiff);
                    Dispose("byte limit");
                }
                else if (lastCheckDiff > 1)
                {
                    // else reset
                    _lastPacketReset = DateTime.Now;
                    _packetSize = 0;
                }

                #endregion

                var packetResult = await AsyncServer.PacketHandler.HandleClient(packet, this);

                // debug
                if (SharedObjects.DebugLevel >= DebugLevel.Debug)
                    _logger.DebugFormat("{0} - DoRecvFromClient Packet: 0x{1:X} - {2} ({3}) - Status: {4} ",
                        AsyncServer.Service.Name, packet.Opcode, ClientId, ClientIp,
                        packetResult.PacketResultType);

                switch (packetResult.PacketResultType)
                {
                    case PacketResultType.Override:
                        _serverSecurity.Send(packetResult.OverridePacket);
                        break;
                    case PacketResultType.Block:
                        break;
                    case PacketResultType.Disconnect:
                        Dispose("receive from client disconnect");
                        break;
                    case PacketResultType.Nothing:
                        _serverSecurity.Send(packet);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // transfers packets to the server and starts receiving from client again
            await TransferToServer();
        }
        catch (Exception e)
        {
            Dispose("receive from client catch " + e?.Message +  "\n"+ e?.InnerException?.Message);
        }
    }

    private async Task DoReceiveFromServer()
    {
        try
        {
            // receive stuff
            var serverBufferMemory = new Memory<byte>(_serverBuffer);
            var recvCount = await _serverTcpClient.GetStream().ReadAsync(serverBufferMemory);
            if (recvCount == 0)
            {
                Dispose("receive from server disconnect");
                return;
            }

            // starts receiving again
            _serverSecurity.Recv(_serverBuffer, 0, recvCount);

            // transfers the incoming packets to a list
            var receivedPackets = _serverSecurity.TransferIncoming();

            // if there are no packets start receiving again
            if (receivedPackets == null)
            {
                await TransferToClient();
                return;
            }

            // loop through all received packets
            foreach (var packet in receivedPackets)
            {
                // ignore handshake
                if (packet.Opcode == 0x5000 || packet.Opcode == 0x9000)
                    continue;

                // debug
                if (SharedObjects.DebugLevel >= DebugLevel.Debug)
                    Global.Logger.DebugFormat("{0} - DoRecvFromServer Packet: 0x{1:X} - {2} ({3})",
                        AsyncServer.Service.Name, packet.Opcode, ClientId, ClientIp);

                var packetResult = await AsyncServer.PacketHandler.HandleServer(packet, this);

                switch (packetResult.PacketResultType)
                {
                    case PacketResultType.Override:
                        _clientSecurity.Send(packetResult.OverridePacket);
                        break;
                    case PacketResultType.Block:
                        break;
                    case PacketResultType.Disconnect:
                        Dispose("receive from server disconnect");
                        break;
                    case PacketResultType.Nothing:
                        _clientSecurity.Send(packet);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // transfers packets to the client and starts receiving from server again
            await TransferToClient();
        }
        catch (Exception e)
        {
            Dispose("receive from server catch " + e?.Message +  "\n"+ e?.InnerException?.Message);
        }
    }

    private async Task TransferToClient()
    {
        if (_exit) return;
        try
        {
            var kvp = _clientSecurity.TransferOutgoing();

            if (kvp == null) return;

            foreach (var t in kvp)
                //transfers the client packets to the client
                await _clientTcpClient.GetStream().WriteAsync(t.Key.Buffer, 0, t.Key.Buffer.Length);
        }
        catch (Exception)
        {
            Dispose("transfer to client catch");
        }
    }

    private async Task TransferToServer()
    {
        if (_exit) return;

        try
        {
            var kvp = _serverSecurity.TransferOutgoing();
            if (kvp == null) return;
            foreach (var t in kvp)
            {
                _packetSize += t.Key.Buffer.Length;
                //transfers the server packets to the server
                await _serverTcpClient.GetStream().WriteAsync(t.Key.Buffer, 0, t.Key.Buffer.Length);
            }
        }
        catch (Exception)
        {
            Dispose("transfer to server catch");
        }
    }


    #region Features

    public string Hwid { get; set; }
    public bool CharacterGameReady { get; set; } = false;
    public bool FirstSpawn { get; set; } = false;
    public ISessionData SessionData { get; init; }

    #endregion

    #region Protection

    // Packet Modification
    public int PacketLength { get; set; }

    // Packet Flooding
    private int _packetSize;
    private DateTime _lastPacketReset;

    // False Packets
    public bool CharnameSent { get; set; } = false;
    public bool CharScreen { get; set; } = false;
    public bool UserLoggedIn { get; set; } = false;

    #endregion
}