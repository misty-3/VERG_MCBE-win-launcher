using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using MCLauncher;

namespace MCLauncher
{
    struct MinecraftPackageFamilies
    {
        public const string MINECRAFT = "Microsoft.MinecraftUWP_8wekyb3d8bbwe";
        public const string MINECRAFT_PREVIEW = "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe";
    }
}

namespace MCLauncher.WPFDataTypes
{
    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }

    public interface ICommonVersionCommands
    {
        ICommand LaunchCommand { get; }

        ICommand DownloadCommand { get; }

        ICommand RemoveCommand { get; }

        ICommand PauseResumeCommand { get; }

        ICommand UnlockCommand { get; }

        ICommand FreeTrialUnlockCommand { get; }
    }

    public enum VersionType : int
    {
        Release = 0,
        Beta = 1,
        Preview = 2,
        Imported = 100
    }

    public enum PackageType
    {
        UWP,
        GDK
    }

    public class Version : NotifyPropertyChangedBase
    {
        public static readonly string UNKNOWN_UUID = "UNKNOWN";

        public Version(string uuid, string name, VersionType versionType, bool isNew, ICommonVersionCommands commands, PackageType packageType, List<string> downloadUrls)
        {
            this.UUID = uuid;
            this.Name = name;
            this.VersionType = versionType;
            this.IsNew = isNew;
            this.DownloadCommand = commands.DownloadCommand;
            this.LaunchCommand = commands.LaunchCommand;
            this.RemoveCommand = commands.RemoveCommand;
            this.GameDirectory = Path.Combine("Versions", (versionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + Name);
            this.PackageType = packageType;
            this.DownloadURLs = downloadUrls ?? new List<string>();
        }
        public Version(string name, string directory, ICommonVersionCommands commands, PackageType packageType)
        {
            this.UUID = UNKNOWN_UUID;
            this.Name = name;
            this.VersionType = VersionType.Imported;
            this.DownloadCommand = commands.DownloadCommand;
            this.LaunchCommand = commands.LaunchCommand;
            this.RemoveCommand = commands.RemoveCommand;
            this.GameDirectory = directory;
            this.PackageType = packageType;
        }

        public string UUID { get; set; }
        public string Name { get; set; }
        public VersionType VersionType { get; set; }
        public bool IsNew
        {
            get { return _isNew; }
            set
            {
                _isNew = value;
                OnPropertyChanged("IsNew");
            }
        }
        public bool IsImported
        {
            get => VersionType == VersionType.Imported;
        }

        public string GameDirectory { get; set; }

        public PackageType PackageType { get; set; }

        public List<string> DownloadURLs { get; set; }

        public string GamePackageFamily
        {
            get => VersionType == VersionType.Preview ? MinecraftPackageFamilies.MINECRAFT_PREVIEW : MinecraftPackageFamilies.MINECRAFT;
        }

        public bool IsInstalled => Directory.Exists(GameDirectory);

        public string DisplayName
        {
            get
            {
                string typeTag = "";
                if (VersionType == VersionType.Beta)
                    typeTag = "(beta)";
                else if (VersionType == VersionType.Preview)
                    typeTag = "(preview)";
                string packageTypeTag = "";
                if (PackageType == PackageType.GDK)
                {
                    packageTypeTag += "GDK";
                }
                else if (PackageType == PackageType.UWP)
                {
                    packageTypeTag += "UWP";
                }

                return Name + " - " + packageTypeTag + (typeTag.Length > 0 ? " " + typeTag : "") + (IsNew ? " (NEW!)" : "");
            }
        }
        public string DisplayInstallStatus
        {
            get
            {
                return IsInstalled ? "Installed" : "Not installed";
            }
        }

        public ICommand LaunchCommand { get; set; }
        public ICommand DownloadCommand { get; set; }
        public ICommand RemoveCommand { get; set; }

        private VersionStateChangeInfo _stateChangeInfo;
        private bool _isNew = false;
        public VersionStateChangeInfo StateChangeInfo
        {
            get { return _stateChangeInfo; }
            set { _stateChangeInfo = value; OnPropertyChanged("StateChangeInfo"); OnPropertyChanged("IsStateChanging"); }
        }

        public bool IsStateChanging => StateChangeInfo != null;

        public void UpdateInstallStatus()
        {
            OnPropertyChanged("IsInstalled");
        }
    }

    public enum VersionState
    {
        Initializing,
        Downloading,
        Extracting,
        Registering,
        Launching,
        Unregistering,
        CleaningUp,
        Staging,
        Decrypting,
        Moving,
        MovingData
    };

    public class VersionStateChangeInfo : NotifyPropertyChangedBase
    {
        private VersionState _versionState;

        private long _progress = 0;
        private long _maxProgress = 0;
        private bool _isPaused = false;

        private DateTime _startTime = DateTime.Now;
        private long _lastProgress = 0;
        private DateTime _lastUpdateTime = DateTime.Now;
        private double _averageSpeed = 0;

        public VersionStateChangeInfo(VersionState versionState)
        {
            _versionState = versionState;
            _startTime = DateTime.Now;
            _lastUpdateTime = DateTime.Now;
        }

        public VersionState VersionState
        {
            get { return _versionState; }
            set
            {
                _versionState = value;
                Progress = 0;
                MaxProgress = 0;
                _startTime = DateTime.Now;
                _lastUpdateTime = DateTime.Now;
                _lastProgress = 0;
                _averageSpeed = 0;
                OnPropertyChanged("VersionState");
                OnPropertyChanged("IsProgressIndeterminate");
                OnPropertyChanged("DisplayStatus");
            }
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            set { _isPaused = value; OnPropertyChanged("IsPaused"); }
        }

        public bool IsProgressIndeterminate
        {
            get
            {
                return _maxProgress == 0;
            }
        }

        public long Progress
        {
            get { return _progress; }
            set
            {
                DateTime now = DateTime.Now;
                double timeDiff = (now - _lastUpdateTime).TotalSeconds;

                if (timeDiff > 0.5)
                {
                    long progressDiff = value - _lastProgress;
                    double instantSpeed = progressDiff / timeDiff;

                    if (_averageSpeed == 0)
                    {
                        _averageSpeed = instantSpeed;
                    }
                    else
                    {
                        _averageSpeed = (_averageSpeed * 0.7) + (instantSpeed * 0.3);
                    }

                    _lastProgress = value;
                    _lastUpdateTime = now;
                }

                _progress = value;
                OnPropertyChanged("Progress");
                OnPropertyChanged("DisplayStatus");
            }
        }

        public long MaxProgress
        {
            get { return _maxProgress; }
            set { _maxProgress = value; OnPropertyChanged("MaxProgress"); OnPropertyChanged("IsProgressIndeterminate"); }
        }

        private string GetTimeRemaining()
        {
            if (_maxProgress == 0 || _progress == 0 || _averageSpeed <= 0)
            {
                return "";
            }

            long remaining = _maxProgress - _progress;
            double secondsRemaining = remaining / _averageSpeed;

            if (secondsRemaining < 0 || double.IsInfinity(secondsRemaining) || double.IsNaN(secondsRemaining))
            {
                return "";
            }

            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsRemaining);

            if (timeSpan.TotalHours >= 1)
            {
                return string.Format(" - {0:0}h {1:0}m remaining", timeSpan.TotalHours, timeSpan.Minutes);
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return string.Format(" - {0:0}m {1:0}s remaining", timeSpan.TotalMinutes, timeSpan.Seconds);
            }
            else
            {
                return string.Format(" - {0:0}s remaining", timeSpan.TotalSeconds);
            }
        }

        public string DisplayStatus
        {
            get
            {
                switch (_versionState)
                {
                    case VersionState.Initializing:
                        return Localization.Get("StatusPreparing");
                    case VersionState.Downloading:
                        string timeRemaining = GetTimeRemaining();
                        return string.Format(Localization.Get("StatusDownloading"),
                            Progress / 1024 / 1024,
                            MaxProgress / 1024 / 1024) + timeRemaining;
                    case VersionState.Extracting:
                        return Localization.Get("StatusExtracting");
                    case VersionState.Registering:
                        return Localization.Get("StatusRegistering");
                    case VersionState.Launching:
                        return Localization.Get("StatusLaunching");
                    case VersionState.Unregistering:
                        return Localization.Get("StatusUnregistering");
                    case VersionState.CleaningUp:
                        return Localization.Get("StatusCleaningUp");
                    case VersionState.Staging:
                        return Localization.Get("StatusStaging");
                    case VersionState.Decrypting:
                        return Localization.Get("StatusDecrypting");
                    case VersionState.Moving:
                        return Localization.Get("StatusMoving");
                    case VersionState.MovingData:
                        return Localization.Get("StatusMovingData");
                    default:
                        return "...";
                }
            }
        }

        public ICommand CancelCommand { get; set; }
    }
}
