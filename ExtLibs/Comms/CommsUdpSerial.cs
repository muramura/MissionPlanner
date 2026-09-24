using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

// dns, ip address
// tcplistner

namespace MissionPlanner.Comms
{
    public class UdpSerial : CommsBase, ICommsSerial, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly List<IPEndPoint> EndPointList = new List<IPEndPoint>();

        private bool _isopen;

        public bool CancelConnect = false;
        /// <summary>
        /// add to EndPointList if need when injecting
        /// </summary>
        public UdpClient client = new UdpClient();

        private readonly ConcurrentQueue<byte[]> _packetQueue = new ConcurrentQueue<byte[]>();
        private byte[] _currentPacket;
        private int _currentPacketOffset;
        private int _queuedBytesCount;
        private readonly object _readLock = new object();
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;

        /// <summary>
        ///     this is the remote endpoint we send messages too. this class does not support multiple remote endpoints.
        /// </summary>
        public IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public string ConfigRef { get; set; } = "";

        private static HashSet<IPAddress> _localAddresses = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
        private static DateTime _lastLocalAddressesUpdate = DateTime.MinValue;
        private static int _isUpdatingAddresses = 0;

        public static IPAddress NormalizeAddress(IPAddress address)
        {
            if (address == null)
                return null;

            try
            {
                if (address.IsIPv4MappedToIPv6)
                    return address.MapToIPv4();
            }
            catch { }

            return address;
        }

        private static void RefreshLocalAddresses()
        {
            try
            {
                var set = new HashSet<IPAddress>
                {
                    IPAddress.Loopback,
                    IPAddress.IPv6Loopback
                };

                // NOTE: NEVER call Dns.GetHostAddresses(Dns.GetHostName()) here!
                // On isolated Wi-Fi (such as StampFly AP without internet access),
                // DNS queries will block for 10-35 seconds waiting for DNS timeout,
                // freezing the MAVLink telemetry receive loop and dropping packets.
                try
                {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up)
                        {
                            var ipProps = ni.GetIPProperties();
                            foreach (var unicast in ipProps.UnicastAddresses)
                            {
                                set.Add(NormalizeAddress(unicast.Address));
                            }
                        }
                    }
                }
                catch { }

                _localAddresses = set;
                _lastLocalAddressesUpdate = DateTime.Now;
            }
            catch
            {
            }
            finally
            {
                Interlocked.Exchange(ref _isUpdatingAddresses, 0);
            }
        }

        public static bool IsLocalAddress(IPAddress address)
        {
            if (address == null)
                return false;

            address = NormalizeAddress(address);

            if (IPAddress.IsLoopback(address))
                return true;

            // Fast-path: StampFly / ESP32 AP gateway is never local
            if (address.ToString() == "192.168.4.1")
                return false;

            try
            {
                // Update in background if stale, never block the receive thread!
                if ((DateTime.Now - _lastLocalAddressesUpdate).TotalSeconds > 10)
                {
                    if (Interlocked.CompareExchange(ref _isUpdatingAddresses, 1, 0) == 0)
                    {
                        ThreadPool.QueueUserWorkItem(_ => RefreshLocalAddresses());
                    }
                }

                return _localAddresses != null && _localAddresses.Contains(address);
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidRemoteEndPoint(IPEndPoint ep)
        {
            if (ep == null || ep.Address == null || ep.Port <= 0)
                return false;

            var addr = NormalizeAddress(ep.Address);

            // Ignore Any, Broadcast, None (e.g. 255.255.255.255 or 0.0.0.0)
            if (addr.Equals(IPAddress.Any) || addr.Equals(IPAddress.Broadcast) || addr.Equals(IPAddress.None) || addr.ToString() == "255.255.255.255" || addr.ToString() == "0.0.0.0")
                return false;

            // Ignore self/local loopback packets
            if (IsSelfPacket(ep))
                return false;

            return true;
        }

        private void AddEndPoint(IPEndPoint ep)
        {
            if (ep == null || ep.Address == null)
                return;

            var normEp = new IPEndPoint(NormalizeAddress(ep.Address), ep.Port);
            if (!IsValidRemoteEndPoint(normEp))
                return;

            lock (EndPointList)
            {
                foreach (var existing in EndPointList)
                {
                    if (NormalizeAddress(existing.Address).Equals(normEp.Address) && existing.Port == normEp.Port)
                        return;
                }

                EndPointList.Add(normEp);
                log.InfoFormat("UDPSerial: Added unique remote endpoint {0}", normEp);
            }
        }

        private bool IsSelfPacket(IPEndPoint endPoint)
        {
            if (endPoint == null)
                return false;

            // Fast-path: StampFly vehicle address is never self
            if (endPoint.Address != null && endPoint.Address.ToString() == "192.168.4.1")
                return false;

            try
            {
                int localPort = 0;
                if (client?.Client != null && client.Client.LocalEndPoint is IPEndPoint lep)
                {
                    localPort = lep.Port;
                }
                else if (int.TryParse(Port, out int p))
                {
                    localPort = p;
                }

                // If sent from our own local listening port and from a local address, it's our own loopback packet
                if (localPort != 0 && endPoint.Port == localPort && IsLocalAddress(endPoint.Address))
                    return true;

                // If sent from one of our non-loopback local interface addresses (e.g. Wi-Fi IP),
                // it is definitely sent from this device, not from an external vehicle!
                if (!IPAddress.IsLoopback(endPoint.Address) && IsLocalAddress(endPoint.Address))
                    return true;
            }
            catch
            {
            }

            return false;
        }



        public UdpSerial()
        {
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            //System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            Port = "14550";
            ReadTimeout = 500;
        }

        private void ConfigureSocketBuffers(UdpClient c)
        {
            if (c?.Client == null) return;
            try
            {
                c.Client.ReceiveBufferSize = 2 * 1024 * 1024; // 2MB to prevent OS packet drops
                c.Client.SendBufferSize = 512 * 1024;
            }
            catch (Exception ex)
            {
                log.WarnFormat("UDPSerial: Failed to set socket buffer sizes: {0}", ex.Message);
            }
        }

        public UdpSerial(UdpClient client)
        {
            this.client = client;
            ConfigureSocketBuffers(this.client);
            _isopen = true;
            ReadTimeout = 500;
            StartReceiveWorker();
        }

        public string Port { get; set; }

        public int WriteBufferSize { get; set; }
        public int WriteTimeout { get; set; }
        public bool RtsEnable { get; set; }
        public Stream BaseStream => new UDPStream(this);

        public void toggleDTR()
        {
        }

        public int ReadTimeout
        {
            get; // { return client.ReceiveTimeout; }
            set; // { client.ReceiveTimeout = value; }
        }

        public int ReadBufferSize { get; set; }

        public int BaudRate { get; set; }

        public int DataBits { get; set; }

        public string PortName
        {
            get => "UDP" + Port;
            set { }
        }

        public int BytesToRead
        {
            get
            {
                lock (_readLock)
                {
                    int currentLeft = (_currentPacket != null) ? (_currentPacket.Length - _currentPacketOffset) : 0;
                    return currentLeft + _queuedBytesCount;
                }
            }
        }

        public int BytesToWrite => 0;

        public bool IsOpen
        {
            get
            {
                if (client?.Client == null) return false;
                return _isopen;
            }
            set => _isopen = value;
        }

        public bool DtrEnable { get; set; }

        public void Open()
        {
            if (client.Client.Connected || IsOpen)
            {
                log.Info("UDPSerial socket already open");
                return;
            }

            client.Close();

            var dest = Port;

            dest = OnSettings("UDP_port" + ConfigRef, dest);

            if (inputboxreturn.Cancel == OnInputBoxShow("Listern Port",
                    "Enter Local port (ensure remote end is already sending)", ref dest)) return;
            Port = dest;

            OnSettings("UDP_port" + ConfigRef, Port, true);

            //######################################

            try
            {
                if (client != null) client.Close();
            }
            catch
            {
            }

            client = new UdpClient(int.Parse(Port));
            ConfigureSocketBuffers(client);

            while (true)
            {
                Thread.Sleep(500);

                if (CancelConnect)
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }

                    return;
                }

                if (client.Available > 0 || BytesToRead > 0)
                    break;
            }

            if (client.Available == 0 && BytesToRead == 0)
                return;

            try
            {
                // reset any previous connection
                RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (true)
                {
                    var firstPacket = client.Receive(ref RemoteIpEndPoint);
                    if (IsSelfPacket(RemoteIpEndPoint))
                    {
                        log.DebugFormat("UDPSerial Open: Discarding self packet from {0}", RemoteIpEndPoint);
                        if (client.Available > 0)
                            continue;

                        while (client.Available == 0 && !CancelConnect)
                        {
                            Thread.Sleep(100);
                        }
                        if (CancelConnect) return;
                        continue;
                    }

                    log.InfoFormat("UDPSerial connecting to {0} : {1}", RemoteIpEndPoint.Address, RemoteIpEndPoint.Port);
                    AddEndPoint(RemoteIpEndPoint);
                    _isopen = true;

                    if (firstPacket != null && firstPacket.Length > 0)
                    {
                        _packetQueue.Enqueue(firstPacket);
                        Interlocked.Add(ref _queuedBytesCount, firstPacket.Length);
                    }

                    StartReceiveWorker();
                    break;
                }
            }
            catch (Exception ex)
            {
                if (client != null && client.Client.Connected) client.Close();
                log.Info(ex.ToString());
                //CustomMessageBox.Show("Please check your Firewall settings\nPlease try running this command\n1.    Run the following command in an elevated command prompt to disable Windows Firewall temporarily:\n    \nNetsh advfirewall set allprofiles state off\n    \nNote: This is just for test; please turn it back on with the command 'Netsh advfirewall set allprofiles state on'.\n", "Error");
                throw new Exception("The socket/UDPSerial is closed " + ex);
            }
        }

        private void StartReceiveWorker()
        {
            lock (_readLock)
            {
                if (_receiveTask != null && !_receiveTask.IsCompleted)
                    return;

                _receiveCts = new CancellationTokenSource();
                var token = _receiveCts.Token;

                _receiveTask = Task.Factory.StartNew(() => ReceiveWorkerLoop(token),
                    token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
        }

        private void StopReceiveWorker()
        {
            try
            {
                _receiveCts?.Cancel();
                lock (_readLock)
                {
                    Monitor.PulseAll(_readLock);
                }
                _receiveTask?.Wait(200);
            }
            catch { }
            finally
            {
                _receiveCts?.Dispose();
                _receiveCts = null;
                _receiveTask = null;
            }

            lock (_readLock)
            {
                while (_packetQueue.TryDequeue(out _)) { }
                _currentPacket = null;
                _currentPacketOffset = 0;
                _queuedBytesCount = 0;
            }
        }

        private void ReceiveWorkerLoop(CancellationToken token)
        {
            log.Info("UDPSerial: Background receive worker thread started");
            while (!token.IsCancellationRequested && _isopen && client?.Client != null)
            {
                try
                {
                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = client.Receive(ref remoteEp);

                    if (data == null || data.Length == 0)
                        continue;

                    if (IsSelfPacket(remoteEp))
                        continue;

                    AddEndPoint(remoteEp);

                    if (_packetQueue.Count > 2000)
                    {
                        if (_packetQueue.TryDequeue(out var dropped))
                        {
                            Interlocked.Add(ref _queuedBytesCount, -dropped.Length);
                        }
                    }

                    _packetQueue.Enqueue(data);
                    Interlocked.Add(ref _queuedBytesCount, data.Length);

                    lock (_readLock)
                    {
                        Monitor.Pulse(_readLock);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (token.IsCancellationRequested || !_isopen)
                        break;

                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested || !_isopen)
                        break;

                    log.DebugFormat("UDPSerial receive worker exception: {0}", ex.Message);
                    Thread.Sleep(5);
                }
            }
            log.Info("UDPSerial: Background receive worker thread ended");
        }

        public int Read(byte[] readto, int offset, int length)
        {
            VerifyConnected();
            if (length < 1) return 0;

            int totalRead = 0;
            var deadline = DateTime.Now.AddMilliseconds(ReadTimeout);

            lock (_readLock)
            {
                while (totalRead < length && DateTime.Now < deadline)
                {
                    if (_currentPacket != null && _currentPacketOffset < _currentPacket.Length)
                    {
                        int available = _currentPacket.Length - _currentPacketOffset;
                        int toCopy = Math.Min(available, length - totalRead);
                        Buffer.BlockCopy(_currentPacket, _currentPacketOffset, readto, offset + totalRead, toCopy);
                        _currentPacketOffset += toCopy;
                        totalRead += toCopy;

                        if (_currentPacketOffset >= _currentPacket.Length)
                        {
                            _currentPacket = null;
                            _currentPacketOffset = 0;
                        }

                        if (totalRead == length)
                            return totalRead;

                        continue;
                    }

                    if (_packetQueue.TryDequeue(out var nextPacket))
                    {
                        Interlocked.Add(ref _queuedBytesCount, -nextPacket.Length);
                        _currentPacket = nextPacket;
                        _currentPacketOffset = 0;
                        continue;
                    }

                    if (totalRead > 0)
                        return totalRead;

                    int remainingMs = (int)(deadline - DateTime.Now).TotalMilliseconds;
                    if (remainingMs <= 0)
                        break;

                    Monitor.Wait(_readLock, Math.Min(remainingMs, 10));
                }
            }

            return totalRead;
        }

        public int ReadByte()
        {
            VerifyConnected();
            var count = 0;
            while (BytesToRead == 0)
            {
                Thread.Sleep(1);
                if (count > ReadTimeout)
                    throw new Exception("NetSerial Timeout on read");
                count++;
            }

            var buffer = new byte[1];
            Read(buffer, 0, 1);
            return buffer[0];
        }

        public int ReadChar()
        {
            return ReadByte();
        }

        public string ReadExisting()
        {
            VerifyConnected();
            var data = new byte[client.Available];
            if (data.Length > 0)
                Read(data, 0, data.Length);

            var line = Encoding.ASCII.GetString(data, 0, data.Length);

            return line;
        }

        public void WriteLine(string line)
        {
            VerifyConnected();
            line = line + "\n";
            Write(line);
        }

        public void Write(string line)
        {
            VerifyConnected();
            var data = new ASCIIEncoding().GetBytes(line);
            Write(data, 0, data.Length);
        }

        public void Write(byte[] write, int offset, int length)
        {
            VerifyConnected();
            byte[] data = write;
            if (offset != 0 || length != write.Length)
            {
                data = new byte[length];
                Buffer.BlockCopy(write, offset, data, 0, length);
            }

            if (EndPointList.Count == 0)
            {
                AddEndPoint(new IPEndPoint(IPAddress.Parse("192.168.4.1"), 14550));
            }

            IPEndPoint[] targets;
            lock (EndPointList)
            {
                targets = EndPointList.ToArray();
            }

            foreach (var ipEndPoint in targets)
            {
                if (!IsValidRemoteEndPoint(ipEndPoint))
                {
                    lock (EndPointList)
                    {
                        EndPointList.Remove(ipEndPoint);
                    }
                    continue;
                }

                try
                {
                    int sent = client.Send(data, length, ipEndPoint);
                }
                catch (Exception ex)
                {
                    log.WarnFormat("[UDP-OUT] Error to {0}: {1}", ipEndPoint, ex.Message);
                }
            }
        }

        public void DiscardInBuffer()
        {
            VerifyConnected();
            lock (_readLock)
            {
                while (_packetQueue.TryDequeue(out _)) { }
                _currentPacket = null;
                _currentPacketOffset = 0;
                _queuedBytesCount = 0;
            }
            log.Info("UdpSerial DiscardInBuffer completed");
        }

        public string ReadLine()
        {
            var temp = new byte[4000];
            var count = 0;
            var timeout = 0;

            while (timeout <= 100)
            {
                if (!IsOpen) break;
                if (BytesToRead > 0)
                {
                    var letter = (byte) ReadByte();

                    temp[count] = letter;

                    if (letter == '\n') // normal line
                        break;

                    count++;
                    if (count == temp.Length)
                        break;
                    timeout = 0;
                }
                else
                {
                    timeout++;
                    Thread.Sleep(5);
                }
            }

            Array.Resize(ref temp, count + 1);

            return Encoding.ASCII.GetString(temp, 0, temp.Length);
        }

        public void Close()
        {
            _isopen = false;
            try
            {
                if (client != null) client.Close();
            }
            catch { }

            StopReceiveWorker();

            lock (EndPointList)
            {
                EndPointList.Clear();
            }

            client = new UdpClient();
            ConfigureSocketBuffers(client);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void VerifyConnected()
        {
            if (client == null || !IsOpen)
            {
                Close();
                throw new Exception("The socket/serialproxy is closed");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                Close();
                client = null;
            }

            // free native resources
        }
    }
}