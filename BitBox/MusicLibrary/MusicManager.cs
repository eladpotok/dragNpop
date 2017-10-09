using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WMPLib;

namespace MusicLibrary
{
    public class MusicManager
    {
        private WindowsMediaPlayer _player;
        private string _libraryPath;
        private int _songIndex;

        public string CurrentSong
        {
            get
            {
                if (_player.currentMedia == null)
                    return "";

                return _player.currentMedia.sourceURL;
            }
        }
        public string NextSong { get {

            if (_player == null)
                return string.Empty;

            if(_player.currentPlaylist.count > _songIndex+1)
                return _player.currentPlaylist.get_Item(_songIndex+1).name;

            return "";
        
        }  }
        public string ArtistName
        {
            get
            {
                if (_player.currentMedia == null)
                    return "";

                return _player.currentMedia.getItemInfoByAtom(_player.mediaCollection.getMediaAtom("WM/AlbumArtist"));
            }
        }
        public bool IsPlaying
        {
            get
            {
                return _player != null && (_player.playState == WMPPlayState.wmppsPlaying || _player.playState == WMPPlayState.wmppsTransitioning);
            }
        }
        public TimeSpan TotalPosition
        {
            get
            {
                if (_player.currentMedia == null)
                    return new TimeSpan();

                return new TimeSpan(0, 0, 0, (int)_player.currentMedia.duration);
            }
        }
        public string LibraryPath
        {
            get { return _libraryPath; }
            set { _libraryPath = value; }
        }
        public bool IsMute { get {
            if (_player == null)
                return false;
            return _player.settings.mute;

        } }
        public List<MusicData> PlayList;
        public bool IsInitialized { get; set; }

        private static MusicManager _instance;
        public static MusicManager GetInstance()
        {
            if (_instance == null)
                _instance = new MusicManager();

            return _instance;
        }

        public delegate void OnSongChangedDelegate(SongState songState);
        public event OnSongChangedDelegate OnSongChanged;

        public delegate void OnSongDurationChangedDelegate(int prePos);
        public event OnSongDurationChangedDelegate OnSongDurationChanged;

        private MusicManager()
        {
            _player = new WindowsMediaPlayer();
            _player.PlayStateChange += _player_PlayStateChange;
            
            PlayList = new List<MusicData>();
        }

        private void InitializePlayList()
        {
            PlayList = new List<MusicData>();

            _player.currentPlaylist = _player.playlistCollection.newPlaylist("BixBox");

            // Get all songs in the library
            string[] songs = Directory.GetFiles(_libraryPath, "*.mp3");

            int index = 1;

            foreach (string song in songs)
            {
                IWMPMedia media = _player.newMedia(song);

                _player.currentPlaylist.appendItem(media);

                PlayList.Add(new MusicData()
                {
                    Counter = index++,
                    Name = Path.GetFileNameWithoutExtension(song),
                    TotalDuration = (new TimeSpan(0, 0, 0, (int)media.duration).ToString()),
                    ArtistName = media.getItemInfoByAtom(_player.mediaCollection.getMediaAtom("WM/AlbumArtist"))
                });
            }

            IsInitialized = true;
        }

        #region Public Methods

        public void Start(string libraryPath = null)
        {
            if(libraryPath != null )
                _libraryPath = libraryPath;

            InitializePlayList();
        }

        public void Play()
        {
            _player.controls.play();
        }
        
        public void Play(string songName)
        {
            _player.controls.stop();

            _player = new WindowsMediaPlayer();
            _player.PlayStateChange += _player_PlayStateChange;

            //_player.currentMedia = _player.

            _player.URL = songName;

            _player.controls.play();
        }

        public void Stop()
        {
            _player.controls.stop();

            // Clear Playlist
            PlayList.Clear();

            _player.close();

            IsInitialized = false;
        }

        public void Pause()
        {
            _player.controls.pause();

            GetCurrentSong().Pause();
        }

        public void ChangeVolume(int value)
        {
            _player.settings.volume = value;
        }

        public void MuteUnmute()
        {
            _player.settings.mute = !_player.settings.mute;
        }

        public void Next()
        {
            MusicData music = GetCurrentSong();

            if (music != null)
            {
                music.IsPlaying = false;
            }

            music.Stop();

            _songIndex++;

            if (_player.currentPlaylist.count <= _songIndex)
                _songIndex = 0;

            _player.controls.next();
        }

        public void AddSong(string file)
        {
            IWMPMedia media = _player.newMedia(file);

            _player.currentPlaylist.appendItem(media);

            PlayList.Add(new MusicData()
            {
                Counter = _player.currentPlaylist.count ,
                Name = Path.GetFileNameWithoutExtension(file),
                TotalDuration = (new TimeSpan(0, 0, 0, (int)media.duration).ToString()),
                ArtistName = media.getItemInfoByAtom(_player.mediaCollection.getMediaAtom("WM/AlbumArtist"))
            });
        }

        public void Back()
        {
            MusicData music = GetCurrentSong();

            if (music != null)
            {
                music.IsPlaying = false;
            }

            music.Stop();

            _songIndex--;

            if (_songIndex < 0)
                _songIndex = _player.currentPlaylist.count-1;

            _player.controls.previous();
        }

        #endregion

        #region Events

        private void _player_PlayStateChange(int NewState)
        {
            SongState ss = SongState.Changed;
            MusicData music = GetCurrentSong();

            if (NewState == (int)WMPPlayState.wmppsPlaying)
            {
                ss = SongState.Playing;

                if (music != null)
                {
                    music.IsPlaying = true;
                    music.Start();
                }
            }

            if (NewState == (int)WMPPlayState.wmppsStopped)
            {
                ss = SongState.Stopped;
                music.Stop();
            }
                

            if (NewState == (int)WMPPlayState.wmppsMediaEnded)
            {
                _songIndex++;
                ss = SongState.Endded;

                music.Stop();

                if (music != null)
                {
                    music.IsPlaying = false;
                }
            }
            if (NewState == (int)WMPPlayState.wmppsTransitioning)
            {
          
            }
            if (NewState == (int)WMPPlayState.wmppsReady)
            {
                ss = SongState.Ready;
                if (music !=null)
                {
                    music.Start(); 
                }
            }


            if (OnSongChanged != null)
                OnSongChanged(ss);
        }

        private void _player_DurationUnitChange(int NewDurationUnit)
        {
            if (OnSongDurationChanged != null)
                OnSongDurationChanged(NewDurationUnit);
        }

        #endregion

        public MusicData GetCurrentSong()
        {
            if (PlayList != null && PlayList.Any())
                return PlayList[_songIndex];

            return null;
        }

        public void MoveUp(MusicData SelectedSong)
        {
            int nCounter = SelectedSong.Counter;

            IWMPMedia selectedMedia = _player.currentPlaylist.get_Item(nCounter-1);

            // Check if it's the first song
            if (nCounter == 1 )
            {
               
            }
            else
            {
                _player.currentPlaylist.removeItem(selectedMedia);
                _player.currentPlaylist.insertItem(nCounter - 2, selectedMedia);

                PlayList.Remove(SelectedSong);
                PlayList.Insert(nCounter - 2, SelectedSong);

                SelectedSong.Counter = nCounter - 1;
                PlayList[nCounter-1].Counter = nCounter;
            }
            
        }

        public void MoveDown(MusicData SelectedSong)
        {
            int nCounter = SelectedSong.Counter;

            IWMPMedia selectedMedia = _player.currentPlaylist.get_Item(nCounter - 1);

            // Check if it's the first song
            if (nCounter == _player.currentPlaylist.count)
            {

            }
            else
            {
                _player.currentPlaylist.removeItem(selectedMedia);
                _player.currentPlaylist.insertItem(nCounter , selectedMedia);

                SelectedSong.Counter++;
                PlayList[nCounter].Counter--;

                PlayList.Remove(SelectedSong);
                PlayList.Insert(nCounter, SelectedSong);
            }

        }
    }

    public class MusicData : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string ArtistName { get; set; }
        public string TotalDuration { get; set; }
        public string NextSong { get; set; }

        private TimeSpan _currPosition;
        public TimeSpan CurrentPosition
        {
            get { return _currPosition; }
            set { _currPosition = value; OnPropertyChanged("CurrentPosition"); }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {

            get { return _isPlaying; }
            set { _isPlaying = value; OnPropertyChanged("IsPlaying"); }
        }

        private int _counter;
        public int Counter
        {
            get { return _counter; }
            set { _counter = value; OnPropertyChanged("Counter"); }
        }

        private int _timerIndex;
        private System.Timers.Timer _playTimer;

        public MusicData()
        {
            _playTimer = new System.Timers.Timer(1000);
            _playTimer.Elapsed += _playTimer_Elapsed;

            _timerIndex = 0;
        }

        #region On Property Changed

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string properyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(properyName));
        }

        #endregion

        private void _playTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CurrentPosition = new TimeSpan(0, 0, ++_timerIndex);
        }

        public void Stop()
        {
            _playTimer.Elapsed -= _playTimer_Elapsed;

            _playTimer.Stop();

            _timerIndex = 0;

            CurrentPosition = new TimeSpan();
        }

        public void Pause()
        {
            _playTimer.Stop();
        }

        public void Start()
        {
            _playTimer.Start();
        }

        public void SetPosition(int nCurrPosition)
        {
            _timerIndex = nCurrPosition;
        }
    }

    public enum SongState
    { 
        Playing,
        Changed,
        Stopped,
        Endded,
        Ready
    }
}
