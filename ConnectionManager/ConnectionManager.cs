using MusicLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinSound;

namespace ConnectionManager
{
    public class Connection 
    {

        #region Static Properties

        public static string MulticastChatGroup = "232.5.5.5";
        public static int MulticastChatPort = 5555; 

        #endregion

        private List<Client> _clients;
        private System.Timers.Timer _tmSendToClients;
        private Socket _socket;
        private byte[] _bytes;
        private bool _clientsChanged;

        public List<Client> Clients
        {
            get { return _clients; }
        }

        private static Connection _instance;
        public static Connection GetInstance
        {
            get {
                if (_instance == null)
                    _instance = new Connection();

                return _instance;
            }
        }

        #region Delegates And Events

        public delegate void OnClientsRefreshedDelegate();
        public event OnClientsRefreshedDelegate OnClientRefreshed;

        public delegate void OnMessagesReceivedDelegate(Chat mesage);
        public event OnMessagesReceivedDelegate OnMessagesReceived;

        public delegate void OnSongInfoChangedDelegate(MusicData music);
        public event OnSongInfoChangedDelegate OnSongInfoChanged;

        public delegate void OnMusicPausedDelegate();
        public event OnMusicPausedDelegate OnMusicPaused;

        #endregion

        public Connection()
        {
            _bytes = new byte[1024];

            _clients = new List<Client>();

            _tmSendToClients = new System.Timers.Timer(5000);
            _tmSendToClients.Elapsed += _tmSendToClients_Elapsed;
        }

        /// <summary>
        /// Start the server 
        /// </summary>
        public void StartServer()
        {
            IPAddress destAddr = IPAddress.Parse(MulticastChatGroup);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);

            IPEndPoint m_EndPoint = new IPEndPoint(destAddr, MulticastChatPort);

            Read();
        }

        /// <summary>
        /// Join to the server
        /// </summary>
        public void JoinToServer(bool bAsTransmitter)
        {
            IPAddress ipAddress = IPAddress.Parse(MulticastChatGroup);
            
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); // Multicast Socket
            
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, MulticastChatPort);

            _socket.Bind(ipEndPoint);

            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(ipAddress, IPAddress.Any));

            if (!bAsTransmitter)
                Send("@", Environment.MachineName);
            else
            {
                Send("#", Environment.MachineName);

                _tmSendToClients.Start();
            }
            
            Read();
        }

        public void SendChat(string message)
        {
            Send("$", Environment.MachineName, message);
        }

        public void SendInfo(string prefix, params string[] data)
        {
            Send(prefix, data);
        }

        public void SendPause()
        {
            Send("&");
        }

        public void Close()
        {
            Send("^", Environment.MachineName);

            if (_socket != null)
                _socket.Close();

            _tmSendToClients.Stop();
        }

        #region Private Methods

        private void Read()
        {
            _socket.BeginReceive(_bytes, 0, _bytes.Length, SocketFlags.None, new AsyncCallback(OnDataReceived), _socket);
        }

        private void Send(string prefix, params string[] machineName)
        {
            byte[] data = new byte[0];
            string strValue = prefix;

            foreach (string item in machineName)
            {
                strValue += item + prefix;
            }

            if (strValue.Any())
            {
                strValue = strValue.Remove(strValue.Length - 1);
            }

            data = Encoding.GetEncoding("Windows-1255").GetBytes(strValue);

            _socket.SendTo(data, 0, data.Length, SocketFlags.None, new IPEndPoint(IPAddress.Parse(MulticastChatGroup), MulticastChatPort));
        }

        private void OnDataReceived(IAsyncResult ar)
        {
            try
            {
                int read = _socket.EndReceive(ar);

                byte[] data = new byte[read];

                // Get the string data
                string dataString = Encoding.GetEncoding("Windows-1255").GetString(_bytes.Take(read).ToArray());

                if (dataString.StartsWith("@"))
                {
                    // Get the parts of the data
                    string[] packets = dataString.Split('@');

                    foreach (string part in packets)
                    {
                        if(!string.IsNullOrEmpty(part))
                            if (part != Environment.MachineName)
                                if (!_clients.Select(t => t.MachineName).Contains(part))
                                {
                                    _clients.Add(new Client(true, part));
                                    _clientsChanged = true;
                                }
                    }
                }
                else if (dataString.StartsWith("#"))
                {
                    // Get the parts of the data
                    string[] packets = dataString.Split('#');

                    if (packets[1] != Environment.MachineName)
                        if (!_clients.Select(t => t.MachineName).Contains(packets[1]))
                        {
                            _clients.Add(new Client(false, packets[1]));
                            _clientsChanged = true;
                        }
                }
                else if (dataString.StartsWith("$"))
                {
                    // Get the parts of the data
                    string[] packets = dataString.Split('$');

                    if (OnMessagesReceived != null)
                        OnMessagesReceived(new Chat() { Sender = packets[1], Text = packets[2] });
                }
                else if (dataString.StartsWith("*"))
                {
                    // Get the parts of the data
                    string[] packets = dataString.Split('*');

                    if (OnSongInfoChanged != null)
                    {
                        MusicData music = new MusicData() { Name = packets[1], ArtistName = packets[2], TotalDuration = packets[3], NextSong = packets[3] };
                        music.SetPosition(int.Parse(packets[5]));
                        OnSongInfoChanged(music);
                    }
                }
                else if (dataString.StartsWith("^"))
                {
                    // Get the parts of the data
                    string[] packets = dataString.Split('*');

                    Client client = _clients.FirstOrDefault(t => t.MachineName == packets[1]);

                    _clients.Remove(client);

                    _clientsChanged = true;
                }
                else if (dataString.StartsWith("&"))
                {
                   
                    // Get the parts of the data
                    if (OnMusicPaused != null)
                        OnMusicPaused();
                }

                if(_clientsChanged == true)
                    if (OnClientRefreshed != null)
                        OnClientRefreshed();

                _clientsChanged = false;

                _socket.BeginReceive(_bytes, 0, _bytes.Length, SocketFlags.None, new AsyncCallback(OnDataReceived), null);
            }
            catch (Exception ex)
            {
                
            }
        }

        private void SendClientToAll()
        {
            string data = "";

            foreach (Client client in Clients)
            {
                data += client.MachineName + "@";
            }

            if (data.Any())
            {
                data = data.Remove(data.Length - 1);
            }

            Send("@", data);

            Send("#", Environment.MachineName); 
        }

        private void _tmSendToClients_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SendClientToAll();
        }

        #endregion

    }
}
