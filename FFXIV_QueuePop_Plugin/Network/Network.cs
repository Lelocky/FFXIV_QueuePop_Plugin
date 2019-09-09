//ORIGINAL SOURCE: https://github.com/devunt/DFAssist
using FFXIV_QueuePop_Plugin.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NetFwTypeLib;
using System.Threading;

namespace FFXIV_QueuePop_Plugin
{
    internal partial class Network
    {
        private static class NativeMethods
        {
            [DllImport("Iphlpapi.dll", SetLastError = true)]
            public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int tcpTableLength, bool sort, AddressFamily ipVersion, int tcpTableType, int reserved);
        }

        public const int TCP_TABLE_OWNER_PID_CONNECTIONS = 4;
        public readonly byte[] RCVALL_IPLEVEL = new byte[4] { 3, 0, 0, 0 };

        [StructLayout(LayoutKind.Sequential)]
        public struct TcpTable
        {
            public uint length;
            public TcpRow row;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TcpRow
        {
            public TcpState state;
            public uint localAddr;
            public uint localPort;
            public uint remoteAddr;
            public uint remotePort;
            public uint owningPid;
        }

        private List<Connection> connections = new List<Connection>();
        private string exePath;
        private Socket socket;
        private byte[] recvBuffer = new byte[0x20000];
        internal bool IsRunning { get; private set; } = false;
        private object lockAnalyse = new object();

        internal void StartCapture(Process process, CancellationToken cancellationToken)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log.Write(LogType.Info, "l-network-starting");

                        if (IsRunning)
                        {
                            Log.Write(LogType.Info, "l-network-error-already-started");
                            return;
                        }

                        UpdateGameConnections(process);

                        if (connections.Count < 2)
                        {
                            Log.Write(LogType.Info, "l-network-error-no-connection");
                            return;
                        }

                        var localAddress = connections[0].localEndPoint.Address;


                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
                        socket.Bind(new IPEndPoint(localAddress, 0));
                        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AcceptConnection, true);
                        socket.IOControl(IOControlCode.ReceiveAll, RCVALL_IPLEVEL, null);
                        socket.ReceiveBufferSize = recvBuffer.Length * 4;

                        socket.BeginReceive(recvBuffer, 0, recvBuffer.Length, 0, new AsyncCallback(OnReceive), null);
                        IsRunning = true;

                        Log.Write(LogType.Info, "l-network-started");
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(LogType.Error, "l-network-error-starting", ex);
                }
            });
        }

        internal void StopCapture()
        {
            try {
                if (!IsRunning)
                {
                    Log.Write(LogType.Info, "l-network-error-already-stopped");
                    return;
                }

                socket.Close();
                connections.Clear();


                Log.Write(LogType.Info, "l-network-stopping");
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-network-error-stopping", ex);
            }
        }

        internal void UpdateGameConnections(Process process)
        {
            var update = connections.Count < 2;
            var currentConnections = GetConnections(process);

            foreach (var connection in connections)
            {
                if (!currentConnections.Contains(connection))
                {
                    update = true;
                    Log.Write(LogType.Info, "l-network-detected-connection-closing");
                    break;
                }
            }

            if (update)
            {
                var lobbyEndPoint = GetLobbyEndPoint(process);

                connections = currentConnections.Where(x => !x.remoteEndPoint.Equals(lobbyEndPoint)).ToList();

                foreach (var connection in connections)
                {
                    Log.Write(LogType.Info, "l-network-detected-connection " + connection.ToString());
                }
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                var length = socket.EndReceive(ar);
                var buffer = recvBuffer.Take(length).ToArray();
                socket.BeginReceive(recvBuffer, 0, recvBuffer.Length, 0, new AsyncCallback(OnReceive), null);

                FilterAndProcessPacket(buffer);
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is NullReferenceException)
            {
                IsRunning = false;
                socket = null;
                Log.Write(LogType.Error, "l-network-stopped", ex);
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-network-error-receiving-packet", ex);
            }
        }

        private void FilterAndProcessPacket(byte[] buffer)
        {
            try {
                var ipPacket = new IPPacket(buffer);

                if (ipPacket.IsValid && ipPacket.Protocol == ProtocolType.Tcp)
                {
                    var tcpPacket = new TCPPacket(ipPacket.Data);

                    if (!tcpPacket.IsValid)
                    {
                        return;
                    }

                    if (!tcpPacket.Flags.HasFlag(TCPFlags.ACK | TCPFlags.PSH))
                    {
                        return;
                    }

                    var sourceEndPoint = new IPEndPoint(ipPacket.SourceIPAddress, tcpPacket.SourcePort);
                    var destinationEndPoint = new IPEndPoint(ipPacket.DestinationIPAddress, tcpPacket.DestinationPort);
                    var connection = new Connection() { localEndPoint = sourceEndPoint, remoteEndPoint = destinationEndPoint };
                    var reverseConnection = new Connection() { localEndPoint = destinationEndPoint, remoteEndPoint = sourceEndPoint };

                    if (!(connections.Contains(connection) || connections.Contains(reverseConnection)))
                    {
                        return;
                    }

                    if (!connections.Contains(reverseConnection))
                    { 
                        return;
                    }

                    lock (lockAnalyse)
                    {
                        AnalyseFFXIVPacket(tcpPacket.Payload);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-network-error-filtering-packet", ex);
            }
        }

        private T GetInstance<T>(string typeName)
        {
            return (T)Activator.CreateInstance(Type.GetTypeFromProgID(typeName));
        }

        private void RegisterToFirewall()
        {
            try
            {
                var netFwMgr = GetInstance<INetFwMgr>("HNetCfg.FwMgr");
                var netAuthApps = netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications;

                var isExists = false;
                foreach (var netAuthAppObject in netAuthApps)
                {
                    var netAuthApp = netAuthAppObject as INetFwAuthorizedApplication;
                    if (netAuthApp != null && netAuthApp.ProcessImageFileName == exePath && netAuthApp.Enabled)
                    {
                        isExists = true;
                    }
                }

                if (!isExists)
                {
                    var netAuthApp = GetInstance<INetFwAuthorizedApplication>("HNetCfg.FwAuthorizedApplication");

                    netAuthApp.Enabled = true;
                    netAuthApp.Name = "FFXIV_QUEUEPOP_PLUGIN";
                    netAuthApp.ProcessImageFileName = exePath;
                    netAuthApp.Scope = NET_FW_SCOPE_.NET_FW_SCOPE_ALL;

                    netAuthApps.Add(netAuthApp);

                    Log.Write(LogType.Info, "l-firewall-registered");
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-firewall-error", ex);
            }
        }

        private IPEndPoint GetLobbyEndPoint(Process process)
        {
            IPEndPoint ipep = null;
            string lobbyHost = null;
            var lobbyPort = 0;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
                {
                    foreach (var @object in searcher.Get())
                    {
                        var commandline = @object["CommandLine"].ToString();
                        var argv = commandline.Split(' ');

                        foreach (var arg in argv)
                        {
                            var splitted = arg.Split('=');
                            if (splitted.Length == 2)
                            {
                                if (splitted[0] == "DEV.LobbyHost01")
                                {
                                    lobbyHost = splitted[1];
                                }
                                else if (splitted[0] == "DEV.LobbyPort01")
                                {
                                    lobbyPort = int.Parse(splitted[1]);
                                }
                            }
                        }
                    }
                }

                if (lobbyHost != null && lobbyPort > 0)
                {
                    var address = Dns.GetHostAddresses(lobbyHost)[0];
                    ipep = new IPEndPoint(address, lobbyPort);
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-network-error-finding-lobby", ex);
            }

            return ipep;
        }

        private List<Connection> GetConnections(Process process)
        {
            var connections = new List<Connection>();

            var tcpTable = IntPtr.Zero;
            var tcpTableLength = 0;

            if (NativeMethods.GetExtendedTcpTable(tcpTable, ref tcpTableLength, false, AddressFamily.InterNetwork, TCP_TABLE_OWNER_PID_CONNECTIONS, 0) != 0)
            {
                try
                {
                    tcpTable = Marshal.AllocHGlobal(tcpTableLength);
                    if (NativeMethods.GetExtendedTcpTable(tcpTable, ref tcpTableLength, false, AddressFamily.InterNetwork, TCP_TABLE_OWNER_PID_CONNECTIONS, 0) == 0)
                    {
                        var table = (TcpTable)Marshal.PtrToStructure(tcpTable, typeof(TcpTable));

                        var rowPtr = new IntPtr(tcpTable.ToInt64() + Marshal.SizeOf(typeof(uint)));
                        for (var i = 0; i < table.length; i++)
                        {
                            var row = (TcpRow)Marshal.PtrToStructure(rowPtr, typeof(TcpRow));

                            if (row.owningPid == process.Id)
                            {
                                var local = new IPEndPoint(row.localAddr, (ushort)IPAddress.NetworkToHostOrder((short)row.localPort));
                                var remote = new IPEndPoint(row.remoteAddr, (ushort)IPAddress.NetworkToHostOrder((short)row.remotePort));

                                connections.Add(new Connection() { localEndPoint = local, remoteEndPoint = remote });
                            }

                            rowPtr = new IntPtr(rowPtr.ToInt64() + Marshal.SizeOf(typeof(TcpRow)));
                        }
                    }
                }
                finally
                {
                    if (tcpTable != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(tcpTable);
                    }
                }
            }

            return connections;
        }

        private class Connection
        {
            public IPEndPoint localEndPoint { get; set; }
            public IPEndPoint remoteEndPoint { get; set; }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                var connection = obj as Connection;

                return localEndPoint.Equals(connection.localEndPoint) && remoteEndPoint.Equals(connection.remoteEndPoint);
            }

            public override int GetHashCode()
            {
                return (localEndPoint.GetHashCode() + 0x0609) ^ remoteEndPoint.GetHashCode();
            }

            public override string ToString()
            {
                return $"{localEndPoint.ToString()} -> {remoteEndPoint.ToString()}";
            }
        }

    }
}
