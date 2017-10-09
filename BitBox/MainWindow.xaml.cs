using ConnectionManager;
using MusicLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Timers;
using WinSound;
using NF;
using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace BitBox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window , INotifyPropertyChanged
    {
        #region Recordering And Repeating Properties

        private Player m_Player;
        private Configuration Config = new Configuration();
        private JitterBuffer m_JitterBufferListen;
        private JitterBuffer m_JitterBufferTransmit;
        private MulticastReceiver m_Receiver;
        private MulticastSender m_MulticastSender;
        private Recorder m_Recorder = new WinSound.Recorder();
        private WinSound.EventTimer m_TimerStream = new WinSound.EventTimer();
        private TimeSpan _timeNow;
        private int _songTimerIndex;
        Byte[] m_PartByte;
        private bool m_Loop = false;
        private Byte[] m_FilePayloadBuffer;
        WinSound.WaveFileHeader m_FileHeader = new WinSound.WaveFileHeader();
        private uint m_RecorderFactor = 4;
        private long m_SequenceNumber = 4596;
        private long m_TimeStamp = 0;
        private int m_Version = 2;
        private uint m_JitterBufferLength = 20;
        private bool m_Padding = false;
        private bool m_IsTimerStreamRunning = false;
        private bool m_Extension = false;
        private int m_CSRCCount = 0;
        private bool m_Marker = false;
        private int m_PayloadType = 0;
        private uint m_SourceId = 0;
        private uint m_Milliseconds = 20;
        private int m_CurrentRTPBufferPos = 0;
        private bool UseJitterBuffer
        {
            get
            {
                if (m_JitterBufferListen != null)
                {
                    return m_JitterBufferLength >= 2;
                }
                return false;
            }
        }
        private int m_RTPPartsLength = 0;

        #endregion

        private ObservableCollection<Chat> _chats;
        private MusicData _currentSong;
        private MusicData _selectedSong;
        private Timer _timeOnAir;
        private bool _isListener;
        private bool _isTransmitter;
        private bool _isListening;
        private bool _isMute;
        private bool _isDragging;
        private bool _isNewMessageReceived;
        private bool _isAvailable;
        private string _nextSong;

        public bool IsListener
        {
            get
            {

                return _isListener;
            }

            set
            {

                ShutDown();

                _isListener = value;

                OnPropertyChanged("IsListener");

            }
        }
        public bool IsListening
        {
            get { return _isListening; }
            set 
            { 
                _isListening = value;
                
                OnPropertyChanged("IsListening");
                OnPropertyChanged("IsAlive");
            }
        }
        public bool IsTransmitter
        {
            get
            {

                return _isTransmitter;
            }

            set
            {
                ShutDown();
                _isTransmitter = value;

                OnPropertyChanged("IsTransmitter");
            }
        }
        public bool IsAlive
        {
            get
            {
                if (IsTransmitter)
                    return MusicManager.GetInstance().IsPlaying;

                if (IsListener)
                    return _isListening;

                return false;
            }
        }
        public bool IsMute
        {
            get { return _isMute; }
            set { _isMute = value; OnPropertyChanged("IsMute"); }
        }
        public bool IsDragging
        {
            get { return _isDragging ; }
            set { _isDragging = value; OnPropertyChanged("IsDragging"); }
        }
        public bool IsPlaying
        {
            get {
                return IsAlive && CurrentSong.IsPlaying;
            }
        }
        public bool IsNewMessageReceived
        {
            get { return _isNewMessageReceived; }
            set { _isNewMessageReceived = value; OnPropertyChanged("IsNewMessageReceived"); }
        }
        public bool IsAvailableStatus
        {
            get { return _isAvailable; }
            set { _isAvailable = value; OnPropertyChanged("IsAvailableStatus"); }
        }
        public TimeSpan TotalLong
        {
            get {

                double nTime = 0;

                foreach (MusicData music in PlayList)
                {
                    TimeSpan ts = TimeSpan.Parse(music.TotalDuration);

                    nTime += ts.TotalSeconds;
                }

                return new TimeSpan(0, 0, 0, (int)nTime);
            
            }
        }
        public string NextSong
        {
            get
            {
                if (IsTransmitter)
                    return MusicManager.GetInstance().NextSong;

                return _nextSong;
            }

            set
            {
                _nextSong = value;
                OnPropertyChanged("NextSong");
            }
        }
        public TimeSpan TimeNow
        {
            get
            {
                return _timeNow;
            }
            set
            {

                _timeNow = value;
                OnPropertyChanged("TimeNow");
            }
        }
        public MusicData CurrentSong
        {
            get
            {
                return _currentSong;
            }

            set
            {
                _currentSong = value;
                OnPropertyChanged("CurrentSong");
            }
        }
        public MusicData SelectedSong
        {
            get { return _selectedSong; }
            set { 
                _selectedSong = value; 
                OnPropertyChanged("SelectedSong");
            }
        }
        public ObservableCollection<MusicData> PlayList
        {
            get {
                return new ObservableCollection<MusicData>(MusicManager.GetInstance().PlayList);
            }
        }
        public ObservableCollection<Client> Clients
        {
            get { return new ObservableCollection<Client>(Connection.GetInstance.Clients); }
        }
        public ObservableCollection<Chat> Chats
        {
            get { return _chats; }
            set { _chats = value; OnPropertyChanged("Chats"); }
        }

        #region Ctor

        public MainWindow()
        {
            // Create a temp Music Data
            CurrentSong = new MusicData();

            IsAvailableStatus = true;

            Chats = new ObservableCollection<Chat>();
            
            InitializeComponent();

            this.DataContext = this;

            IsListener = true;
            _songTimerIndex = 0;

            _timeOnAir = new Timer(1000);
            _timeOnAir.Elapsed += _timeOnAir_Elapsed;

            Connection.GetInstance.OnClientRefreshed    += GetInstance_OnClientRefreshed;
            Connection.GetInstance.OnMessagesReceived   += GetInstance_OnMessagesReceived;
            Connection.GetInstance.OnSongInfoChanged    += GetInstance_OnSongInfoChanged;
            Connection.GetInstance.OnMusicPaused        += GetInstance_OnMusicPaused;

            MusicManager.GetInstance().OnSongChanged            += MainWindow_OnSongChanged;
            MusicManager.GetInstance().OnSongDurationChanged    += MainWindow_OnSongDurationChanged;
            

            OnPropertyChanged("PlayList");

            Config.MulticasAddress = "232.2.2.2";
            Config.MulticastPort = 1234;
        }

        #endregion

        #region View Events

        private void imgStop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Stop
            MusicManager.GetInstance().Stop();

            // Shutdown multicast
            ShutDown();

            IsListening = false;

            Refresh();
        }

        private void imgPause_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Pause
            MusicManager.GetInstance().Pause();

            // Puase timer duration
            CurrentSong.Pause();

            Connection.GetInstance.SendPause();

            // Stop multicast
            ShutDown();
        }

        private void imgForward_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MusicManager.GetInstance().Next();
        }

        private void imgMute_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Mute and Unmute
            MusicManager.GetInstance().MuteUnmute();

            IsMute = !IsMute;
        }

        /// <summary>
        /// Start And Play
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void imgPlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsTransmitter)
            {
                //WinSoundServer
                StartSender();

                // Start
                if (!MusicManager.GetInstance().IsInitialized)
                    MusicManager.GetInstance().Start(@"C:\Users\Public\Music\Sample Music"); 

                // Play
                MusicManager.GetInstance().Play();

                // Start multicasting for chat and info
                Connection.GetInstance.JoinToServer(true);

                // Start on-air timer
                _timeOnAir.Start();

                // Refresh properties
                Refresh();
            }
            else
            {
                m_Player = new WinSound.Player();

                StartListener();

                // Send to client that I joined
                Connection.GetInstance.JoinToServer(false);
            }
        }

        private void miConnection_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void miDirectory_Click(object sender, RoutedEventArgs e)
        {
            DirectoryDialog dirDialog = new DirectoryDialog();
            dirDialog.ShowDialog();
        }

        private void miRun_Click(object sender, RoutedEventArgs e)
        {
            //MusicManager.GetInstance().Start();

            //MusicManager.GetInstance().Play();
        }

        private void lstPlaylist_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //MusicManager.GetInstance().Play(lstPlaylist.SelectedItem.ToString());
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                //MusicManager.GetInstance().Play(lstPlaylist.SelectedItem.ToString());
            }
        }

        private void sldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MusicManager.GetInstance().ChangeVolume((int)e.NewValue);
        }

        private void txtUrl_LostFocus(object sender, RoutedEventArgs e)
        {
            if (txtUrl.Text == string.Empty)
            {
                txtUrl.Text = "Add track from URL";
                txtUrl.Foreground = Brushes.Gray;
            }

        }

        private void imgBackward_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MusicManager.GetInstance().Back();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Connection.GetInstance.Close();
            ShutDown();
        }

        #region Drag And Drop

        private void ListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("myFormat") ||
                             sender == e.Source)
            {
                e.Effects = DragDropEffects.None;
            }

            IsDragging = true;
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string file in files)
            {
                if (System.IO.Path.GetExtension(file) == ".mp3")
                {
                    MusicManager.GetInstance().AddSong(file);
                }
            }

            IsDragging = false;

            Refresh();
        }

        private void ListBox_DragLeave(object sender, DragEventArgs e)
        {
            IsDragging = false;
        }

        #endregion

        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSong != null)
            {
                MusicManager.GetInstance().MoveUp(SelectedSong);

                Refresh();
            }
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSong != null)
            {
                if (SelectedSong.Counter == CurrentSong.Counter)
                    return;

                int curSongIndex = SelectedSong.Counter;

                MusicManager.GetInstance().MoveDown(SelectedSong);

                SelectedSong = PlayList[curSongIndex - 1];

                Refresh();

                SendSongInfo();
            }
        }

        private void btnAddTrack_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.Filter = "Music Files (*.mp3)|*.mp3";

            bool? result = openFileDlg.ShowDialog();

            if (result.HasValue &&
                result.Value)
            {
                MusicManager.GetInstance().AddSong(openFileDlg.FileName);

                Refresh();

                SendSongInfo();
            }
        }

        #endregion

        #region Timer Elapsed

        private void _timeOnAir_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeNow = TimeNow.Add(new TimeSpan(0, 0, 1));
        }

        private void OnTimerStream()
        {
            try
            {
                //Wenn noch aktiv
                if (m_IsTimerStreamRunning)
                {
                    if ((m_CurrentRTPBufferPos + m_RTPPartsLength) <= m_FilePayloadBuffer.Length)
                    {
                        //Bytes senden
                        Array.Copy(m_FilePayloadBuffer, m_CurrentRTPBufferPos, m_PartByte, 0, m_RTPPartsLength);
                        m_CurrentRTPBufferPos += m_RTPPartsLength;
                        WinSound.RTPPacket rtp = ToRTPPacket(m_PartByte, m_FileHeader.BitsPerSample, m_FileHeader.Channels);
                        m_MulticastSender.SendBytes(rtp.ToBytes());
                    }
                    else
                    {
                        //Rest-Bytes senden
                        int rest = m_FilePayloadBuffer.Length - m_CurrentRTPBufferPos;
                        Byte[] restBytes = new Byte[m_PartByte.Length];
                        Array.Copy(m_FilePayloadBuffer, m_CurrentRTPBufferPos, restBytes, 0, rest);
                        WinSound.RTPPacket rtp = ToRTPPacket(restBytes, m_FileHeader.BitsPerSample, m_FileHeader.Channels);
                        m_MulticastSender.SendBytes(rtp.ToBytes());

                        if (m_Loop == false)
                        {
                            //QueueTimer beenden
                            StopTimerStream();
                        }
                        else
                        {
                            //Von vorne beginnen
                            m_CurrentRTPBufferPos = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                StopTimerStream();
            }
        }

        #endregion

        #region Music Events

        private void MainWindow_OnSongDurationChanged(int prePos)
        {
            //CurrentPosition = prePos;
        }

        private void MainWindow_OnSongChanged(SongState ss)
        {
            if (ss == SongState.Playing)
            {
                // Get the current song data
                CurrentSong = MusicManager.GetInstance().GetCurrentSong();

                SendSongInfo();
            }
            else if (ss == SongState.Stopped || ss == SongState.Endded || ss == SongState.Changed)
            {
             //   CurrentSong.Start();
            }
            else if (ss == SongState.Ready)
            {
        
            }

            OnPropertyChanged("NextSong");
            OnPropertyChanged("IsPlaying");
        }

        #endregion

        #region Private Methods

        private void ShutDown()
        {
            if (IsAlive)
            {
                Connection.GetInstance.Close(); 
            }

            //Form ist geschlossen
            if (IsListener)
            {
                if (m_Receiver != null)
                {
                    m_Receiver.Disconnect();

                    //Events entfernen
                    m_Receiver.DataReceived2 -= new NF.MulticastReceiver.DelegateDataReceived2(OnDataReceived);
                    m_Receiver.Disconnected -= new NF.MulticastReceiver.DelegateDisconnected(OnDisconnected);
                }
                if (m_Player != null)
                {
                    m_Player.Close();
                    if (m_JitterBufferListen != null)
                    {
                        m_JitterBufferListen.Stop(); 
                    }
                }

                _isListening = false;
            }
            else
            {

                if (m_MulticastSender != null)
                {
                    m_MulticastSender.Close();
                }
                m_MulticastSender = null;
                m_Recorder.Stop();
            }

            if (_timeOnAir != null)
            {
                _timeOnAir.Stop(); 
            }
        }

        private void StartSender()
        {
            if (m_Recorder.Started == false)
            {
                //Starten
                m_MulticastSender = new MulticastSender(Config.MulticasAddress, Config.MulticastPort, 10);

                InitTimerStream();

                InitJitterBufferTransmit();

                StartRecording();
            }
            else
            {
                //Schliessen
                m_MulticastSender.Close();
                m_MulticastSender = null;
                m_Recorder.Stop();

                //Wenn JitterBuffer
                if (Config.UseJitterBuffer)
                {
                    //m_TimerProgressBarJitterBuffer.Stop();
                    m_JitterBufferTransmit.Stop();
                }

                //Warten bis Aufnahme beendet
                while (m_Recorder.Started)
                {
                    //Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
            }
        }

        private void StartRecording()
        {
            try
            {
                m_Recorder.DataRecorded += OnDataReceivedFromSoundcard;
                m_Recorder.RecordingStopped += OnRecordingStopped;

                //Buffer Grösse je nach JitterBuffer berechnen
                int bufferSize = 0;
                if (Config.UseJitterBuffer)
                {
                    bufferSize = WinSound.Utils.GetBytesPerInterval((uint)Config.SamplesPerSecond, Config.BitsPerSample, Config.Channels) * (int)m_RecorderFactor;
                }
                else
                {
                    bufferSize = WinSound.Utils.GetBytesPerInterval((uint)Config.SamplesPerSecond, Config.BitsPerSample, Config.Channels);
                }

                if (bufferSize > 0)
                {
                    if (m_Recorder.Start(Config.SoundDeviceName, Config.SamplesPerSecond, Config.BitsPerSample, Config.Channels, Config.BufferCount, bufferSize))
                    {
                        //Wenn JitterBuffer
                        if (Config.UseJitterBuffer)
                        {
                            m_JitterBufferTransmit.Start();
                        }
                    }
                    else
                    {
                        //ShowError();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(String.Format("BufferSize must be greater than 0. BufferSize: {0}", bufferSize));
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Refresh()
        {
            OnPropertyChanged("IsAlive");
            OnPropertyChanged("PlayList");
            OnPropertyChanged("NextSong");
            OnPropertyChanged("TotalLong");
        }

        private void InitJitterBufferListen()
        {
            //Wenn vorhanden
            if (m_JitterBufferListen != null)
            {
                m_JitterBufferListen.DataAvailable -= new WinSound.JitterBuffer.DelegateDataAvailable(OnDataAvailableListen);
            }

            //Neu erstellen
            m_JitterBufferListen = new WinSound.JitterBuffer(null, 8, 20);
            m_JitterBufferListen.DataAvailable += new WinSound.JitterBuffer.DelegateDataAvailable(OnDataAvailableListen);
        }

        private void InitJitterBufferTransmit()
        {
            //Wenn vorhanden
            if (m_JitterBufferTransmit != null)
            {
                m_JitterBufferTransmit.DataAvailable -= new WinSound.JitterBuffer.DelegateDataAvailable(OnDataAvailableTransmit);
            }

            //Neu erstellen
            m_JitterBufferTransmit = new WinSound.JitterBuffer(null, 20, 20);
            m_JitterBufferTransmit.DataAvailable += new WinSound.JitterBuffer.DelegateDataAvailable(OnDataAvailableTransmit);
        }

        private void StartListener()
        {
            IsListening = true;

            //MulticastReceiver
            m_Receiver = new NF.MulticastReceiver(Config.PacketSize);
            m_Receiver.DataReceived2    += OnDataReceived;
            m_Receiver.Disconnected     += OnDisconnected;
            m_Receiver.Connect(Config.MulticasAddress, Config.MulticastPort);

            //WinSound Player öffnen
            m_Player.Open(Config.SoundDeviceName, Config.SamplesPerSecond, Config.BitsPerSample, Config.Channels, Config.BufferCount);
            
            InitJitterBufferListen();
            m_JitterBufferListen.Start();
        }

        private Byte[] ToRTPData(Byte[] data, int bitsPerSample, int channels)
        {
            //Neues RTP Packet erstellen
            WinSound.RTPPacket rtp = ToRTPPacket(data, bitsPerSample, channels);
            //RTPHeader in Bytes erstellen
            Byte[] rtpBytes = rtp.ToBytes();
            //Fertig
            return rtpBytes;
        }

        private WinSound.RTPPacket ToRTPPacket(Byte[] linearData, int bitsPerSample, int channels)
        {
            //Daten Nach MuLaw umwandeln
            Byte[] mulaws = WinSound.Utils.LinearToMulaw(linearData, bitsPerSample, channels);

            //Neues RTP Packet erstellen
            WinSound.RTPPacket rtp = new WinSound.RTPPacket();

            //Werte übernehmen
            rtp.Data = mulaws;
            rtp.SourceId = m_SourceId;
            rtp.CSRCCount = m_CSRCCount;
            rtp.Extension = m_Extension;
            rtp.HeaderLength = WinSound.RTPPacket.MinHeaderLength;
            rtp.Marker = m_Marker;
            rtp.Padding = m_Padding;
            rtp.PayloadType = m_PayloadType;
            rtp.Version = m_Version;

            //RTP Header aktualisieren
            try
            {
                rtp.SequenceNumber = Convert.ToUInt16(m_SequenceNumber);
                m_SequenceNumber++;
            }
            catch (Exception)
            {
                m_SequenceNumber = 0;
            }
            try
            {
                rtp.Timestamp = Convert.ToUInt32(m_TimeStamp);
                m_TimeStamp += mulaws.Length;
            }
            catch (Exception)
            {
                m_TimeStamp = 0;
            }

            //Fertig
            return rtp;
        }

        private void InitTimerStream()
        {
      //      m_TimerStream.TimerTick += new WinSound.EventTimer.DelegateTimerTick(OnTimerStream);
        //    StartTimerStream();
        }

        private void StopTimerStream()
        {
            if (m_TimerStream.IsRunning)
            {
                //QueueTimer beenden
                m_TimerStream.Stop();

                //Variablen setzen
                m_IsTimerStreamRunning = m_TimerStream.IsRunning;
                m_MulticastSender.Close();
                m_MulticastSender = null;
                m_CurrentRTPBufferPos = 0;
                OnFileStreamingEnd();
            }
        }

        private void OnFileStreamingEnd()
        {
            
        }

        private void StartTimerStream()
        {
            //WaveFile Header
            m_FileHeader = WinSound.WaveFile.Read(Config.FileName);
            //Buffer erzeugen
            FillRTPBufferWithPayloadData(m_FileHeader);
            //Bytes für die einzelnen Datenpakete
            m_PartByte = new Byte[m_RTPPartsLength];
            //Buffer Position
            m_CurrentRTPBufferPos = 0;

      
            //Timer starten
            m_TimerStream.Start(m_Milliseconds, 0);
            m_IsTimerStreamRunning = m_TimerStream.IsRunning;
        }

        private void FillRTPBufferWithPayloadData(WinSound.WaveFileHeader header)
        {
            m_RTPPartsLength = WinSound.Utils.GetBytesPerInterval(header.SamplesPerSecond, header.BitsPerSample, header.Channels);
            m_FilePayloadBuffer = header.Payload;
        }

        private void SongChanged(string[] packets)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                CurrentSong = new MusicData()
                {
                    Name = packets[1],
                    ArtistName = packets[2],
                    TotalDuration = packets[4],
                };

                CurrentSong.SetPosition(int.Parse(packets[5]));

                NextSong = packets[3];

                CurrentSong.Start();

                OnPropertyChanged("CurrentSong");
            }));
        }

        private void GetInstance_OnSongInfoChanged(MusicData music)
        {
            if (IsTransmitter)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                CurrentSong = music;

                NextSong = music.NextSong;

                CurrentSong.Start();
            }));
        }

        #endregion

        #region On Property Changed

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string properyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(properyName));
        }
        
        #endregion

        #region Events Handler

        private void OnDataReceivedFromSoundcard(Byte[] data)
        {
            try
            {
                lock (this)
                {
                    if (m_MulticastSender != null)
                    {
                        //Wenn JitterBuffer
                        if (Config.UseJitterBuffer)
                        {
                            //Sounddaten in kleinere Einzelteile zerlegen
                            int bytesPerInterval = WinSound.Utils.GetBytesPerInterval((uint)Config.SamplesPerSecond, Config.BitsPerSample, Config.Channels);
                            int count = data.Length / bytesPerInterval;
                            int currentPos = 0;
                            for (int i = 0; i < count; i++)
                            {
                                //Teilstück in RTP Packet umwandeln
                                Byte[] partBytes = new Byte[bytesPerInterval];
                                Array.Copy(data, currentPos, partBytes, 0, bytesPerInterval);
                                currentPos += bytesPerInterval;
                                WinSound.RTPPacket rtp = ToRTPPacket(partBytes, Config.BitsPerSample, Config.Channels);
                                //In Buffer legen
                                m_JitterBufferTransmit.AddData(rtp);
                            }
                        }
                        else
                        {
                            //Alles in RTP Packet umwandeln
                            Byte[] rtp = ToRTPData(data, Config.BitsPerSample, Config.Channels);
                            //Absenden
                            m_MulticastSender.SendBytes(rtp);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void OnRecordingStopped()
        {
            m_JitterBufferTransmit.Stop();
        }

        private void OnDataReceived(NF.MulticastReceiver mc, Byte[] bytes)
        {
            try
            {
                //Wenn der Player gestartet wurde
                if (m_Player.Opened && m_Receiver.Connected)
                {
                    //RTP Header auslesen
                    WinSound.RTPPacket rtp = new WinSound.RTPPacket(bytes);

                    string strStringValue = Encoding.GetEncoding("Windows-1255").GetString(bytes);
                    string[] packets = strStringValue.Split('@');

                    if (packets.Length > 0)
                    {
                        if (packets[0] == "HEADER")
                        {
                            if (packets.Length == 5)
                            {
                                SongChanged(packets);

                                return;
                            }
                        }
                    }
                    //Wenn Header korrekt
                    if (rtp.Data != null)
                    {
                        //Wenn JitterBuffer verwendet werden soll
                        if (UseJitterBuffer)
                        {
                            m_JitterBufferListen.AddData(rtp);
                        }
                        else
                        {
                            //Nach Linear umwandeln
                            Byte[] linearBytes = WinSound.Utils.MuLawToLinear(rtp.Data, Config.BitsPerSample, Config.Channels);
                            //Abspielen
                            m_Player.PlayData(linearBytes, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(String.Format("FormMain.cs | OnDataReceived() | {0}", ex.Message));
            }
        }

        private void OnDisconnected(string reason)
        {
            try
            {
                m_Player.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(String.Format("FormMain.cs | OnDisconnected() | {0}", ex.Message));
            }
        }

        private void OnDataAvailableListen(Object sender, WinSound.RTPPacket rtp)
        {
            //Nach Linear umwandeln
            Byte[] linearBytes = WinSound.Utils.MuLawToLinear(rtp.Data, Config.BitsPerSample, Config.Channels);
            //Abspielen
            m_Player.PlayData(linearBytes, false);
        }

        private void OnDataAvailableTransmit(Object sender, WinSound.RTPPacket rtp)
        {
            try
            {
                if (m_MulticastSender != null && IsPlaying)
                {
                    
                    //RTP Packet in Bytes umwandeln
                    Byte[] rtpBytes = rtp.ToBytes();
                    //Absenden
                    m_MulticastSender.SendBytes(rtpBytes);
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtUrl.Text == "Add track from URL")
            {
                txtUrl.Foreground = Brushes.Black;
                TextBox textbox = (TextBox)sender;

                textbox.Text = string.Empty; 
            }
        }
        
        private void GetInstance_OnClientRefreshed()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OnPropertyChanged("Clients");
            }));

            if (IsTransmitter)
            {
                SendSongInfo();
            }
        }

        private void SendSongInfo()
        {
            //m_MulticastSender.SendText("HEADER@" + MusicManager.GetInstance().CurrentSong + "@" +
            //                                       MusicManager.GetInstance().ArtistName + "@" +
            //                                       MusicManager.GetInstance().NextSong + "@" +
            //                                       CurrentSong.TotalDuration + "@" +
            //                                       CurrentSong.CurrentPosition.TotalSeconds);

            Connection.GetInstance.SendInfo("*", CurrentSong.Name,
                                            CurrentSong.ArtistName,
                                            MusicManager.GetInstance().NextSong,
                                            CurrentSong.TotalDuration,
                                            CurrentSong.CurrentPosition.TotalSeconds.ToString());
        }

        private void GetInstance_OnMessagesReceived(Chat mesage)
        {
              Dispatcher.BeginInvoke(new Action(() =>
              {
                  Chats.Add(mesage);

                  if(!(bool)gbChat.IsChecked)
                    IsNewMessageReceived = true;

              }));

              OnPropertyChanged("Chats");
        }

        private void GetInstance_OnMusicPaused()
        {
            if (IsListener)
            {
                CurrentSong.Pause();
            }
        }

        #endregion

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox textBox = (TextBox)sender;

                string text = textBox.Text;

                Connection.GetInstance.SendChat(text);

                textBox.Text = ""; 
            }
        }

        private void Grid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                txtChat.Focus();
                IsNewMessageReceived = false;
            }
        }
      
    }

    public class Configuration
    {
        /// <summary>
        /// Config
        /// </summary>
        public Configuration()
        {

        }

        //Attribute
        public String MulticasAddress = "";
        public String SoundDeviceName = "";
        public int MulticastPort = 0;
        public int SamplesPerSecond = 8000;
        public short BitsPerSample = 16;
        public short Channels = 2;
        public Int32 BufferCount = 8;
        public String FileName = "";
        public bool Loop = false;
        public bool UseJitterBuffer = true;
        public Int32 PacketSize = 4096;
    }
}
