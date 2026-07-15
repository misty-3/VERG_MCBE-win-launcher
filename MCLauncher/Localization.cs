using System.Collections.Generic;

namespace MCLauncher
{
    public static class Localization
    {
        private static string _currentLanguage = "en";
        
        private static Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>
        {
            // English translations
            ["en"] = new Dictionary<string, string>
            {
                // Window titles
                ["AppTitle"] = "Minecraft Bedrock Launcher",
                ["SettingsTitle"] = "Settings",
                
                // Navigation
                ["MyVersions"] = "🏠 My Versions",
                ["Browse"] = "📦 Browse",
                ["Servers"] = "🌐 Servers",
                ["Navigation"] = "NAVIGATION",
                
                // My Versions tab
                ["MyVersionsHeader"] = "My Versions",
                ["MyVersionsSubtitle"] = "Manage your installed Minecraft versions",
                ["ImportFile"] = "➕ Import File",
                ["ImportInProgress"] = "Import In Progress",
                ["ImportInProgressMessage"] = "An import is already in progress. Please wait for it to complete.",
                ["Play"] = "▶️ Play",
                ["Remove"] = "🗑️",
                ["RemoveTooltip"] = "Remove",
                
                // Empty state
                ["NoVersionsInstalled"] = "No versions installed",
                ["NoVersionsSubtitle"] = "Browse available versions to get started",
                ["BrowseVersionsButton"] = "📦 Browse Versions",
                
                // Browse tab
                ["BrowseVersionsHeader"] = "Browse Versions",
                ["BrowseVersionsSubtitle"] = "Download any Minecraft version",
                ["Refresh"] = "🔄 Refresh",
                ["SearchPlaceholder"] = "🔍 Search versions...",
                ["All"] = "All",
                ["Release"] = "⭐ Release",
                ["Preview"] = "✨ Preview",
                ["Download"] = "⬇️ Download",
                
                // Version types
                ["StableRelease"] = "⭐ Stable Release - Recommended!",
                ["PreviewVersion"] = "✨ Preview - Try new features early!",
                ["BetaVersion"] = "🧪 Beta - Old test version",
                ["ImportedVersion"] = "📦 Imported from file",
                
                // Status messages
                ["ReadyToPlay"] = "Ready to play!",
                ["NotInstalled"] = "Not installed",
                ["TypeXboxGDK"] = "Type: Xbox/GDK",
                ["TypeWindowsStore"] = "Type: Windows Store",
                
                // Loading
                ["LoadingVersions"] = "Loading versions...",
                ["LoadingCached"] = "Loading cached versions...",
                ["DownloadingList"] = "Downloading latest version list...",
                ["LoadingImported"] = "Loading imported versions...",
                
                // Messages
                ["LanguageChanged"] = "Language Changed",
                ["LanguageChangedMessage"] = "Language changed to English. Full translation will be applied in a future update.",
                ["MinecraftStarting"] = "Minecraft is starting! 🎮",
                ["MinecraftStartingMessage"] = "{0} is launching!\n\nYour worlds are ready to play!",
                ["VersionRemoved"] = "Removed!",
                ["VersionRemovedMessage"] = "{0} has been removed.",
                ["DownloadComplete"] = "Download complete!",
                ["DownloadCompleteMessage"] = "{0} is ready to play!\n\nGo to 'My Versions' to launch it.",
                ["VersionAdded"] = "Version added!",
                ["VersionAddedMessage"] = "{0} is ready to play!",
                
                // Error messages
                ["Error"] = "Error",
                ["CouldntUpdateList"] = "Couldn't update version list",
                ["CouldntUpdateListMessage"] = "We couldn't download the latest versions from the internet. You can still use cached versions, but some new ones might be missing.",
                ["CouldntPrepareWorlds"] = "Couldn't prepare worlds",
                ["CouldntPrepareWorldsMessage"] = "We had trouble preparing your worlds for this version.\n\nYour worlds are safe, but you might need to move them manually.",
                ["CouldntRegister"] = "Couldn't register version",
                ["CouldntRegisterMessage"] = "We couldn't register this Minecraft version with Windows.\n\nMake sure Minecraft isn't already running and try again.",
                ["CouldntLaunch"] = "Couldn't launch Minecraft",
                ["CouldntLaunchMessage"] = "Minecraft didn't start. Make sure:\n• Minecraft isn't already running\n• You have the Store version installed\n\nTry restarting the launcher if the problem continues.",
                ["CouldntRemove"] = "Couldn't remove",
                ["CouldntRemoveMessage"] = "We couldn't remove this version. Make sure Minecraft isn't running and try again.",
                ["DownloadFailed"] = "Download failed",
                ["DownloadFailedMessage"] = "We couldn't download this version. Check your internet connection and try again.",
                ["BadUpdateIDBeta"] = "For beta versions, make sure your account is subscribed to the Minecraft beta program in the Xbox Insider Hub app.",
                ["ExtractionFailed"] = "Extraction failed",
                ["ExtractionFailedMessage"] = "The file might be corrupted or not a valid Minecraft package. Please try a different file.",
                ["PleaseWait"] = "Please wait",
                ["PleaseWaitMessage"] = "This version is currently being modified. Please wait a moment and try again.",
                
                // Dialogs
                ["RemoveVersion"] = "Remove version?",
                ["RemoveVersionMessage"] = "Are you sure you want to remove {0}?\n\nYour worlds will be safe, but you'll need to download this version again if you want to play it later.",
                ["ReplaceExisting"] = "Replace existing version?",
                ["ReplaceExistingMessage"] = "You already have a version with this name. Do you want to replace it?",
                ["CouldntRemoveOld"] = "Couldn't remove old version",
                ["CouldntRemoveOldMessage"] = "We couldn't remove the existing version. Import cancelled.",
                ["WrongFileType"] = "Wrong file type",
                ["WrongFileTypeMessage"] = "This file type {0} isn't supported. Please choose a .appx or .msixvc file.",
                
                // GDK Warning
                ["FirstTimeSetup"] = "First Time Setup",
                ["GDKWarningMessage"] = "⚠️ Important: First time using Xbox/GDK versions!\n\nBefore you continue, make sure:\n✓ You have Minecraft installed from the Microsoft Store\n✓ You have at least 2GB of free space\n\nDuring installation, you might see some windows pop up briefly - this is normal!\n\nYour worlds will be automatically backed up.\n\nReady to continue?",
                
                // Multiple world locations
                ["MultipleWorldLocations"] = "Multiple World Locations",
                ["MultipleWorldLocationsMessage"] = "Worlds were found in multiple locations:\n{0}\n\nThe version will look for worlds in: {1}\n\nSome worlds may not be visible. Continue anyway?",
                
                // Download status
                ["StatusPreparing"] = "Preparing...",
                ["StatusDownloading"] = "Downloading... {0}MiB/{1}MiB",
                ["StatusExtracting"] = "Extracting package... (this may take 2-5 minutes)",
                ["StatusRegistering"] = "Registering with Windows... (this may take 1-2 minutes)",
                ["StatusLaunching"] = "Launching...",
                ["StatusUnregistering"] = "Unregistering package...",
                ["StatusCleaningUp"] = "Cleaning up...",
                ["StatusStaging"] = "Staging package with Windows... (this may take 3-5 minutes)",
                ["StatusDecrypting"] = "Decrypting Minecraft executable... (this may take 1-2 minutes)",
                ["StatusMoving"] = "Moving game files to final location... (this may take 2-4 minutes)",
                ["StatusMovingData"] = "Restoring Minecraft worlds and data...",
                
                // Window title status messages
                ["TitleStagingInProgress"] = "⚠️ STAGING IN PROGRESS - Please wait... (All operations disabled)",
                ["TitleUnregisteringPackage"] = "⚠️ UNREGISTERING PACKAGE - Please wait... (All operations disabled)",
                ["TitleRegisteringPackage"] = "⚠️ REGISTERING PACKAGE - Please wait... (All operations disabled)",
                ["TitleCleaningUp"] = "⚠️ CLEANING UP - Please wait... (All operations disabled)",
                ["TitleMovingFiles"] = "⚠️ MOVING FILES - Please wait... (All operations disabled)",
                ["TitleOperationsInProgress"] = "⏳ Minecraft Version Manager - {0} operation(s) in progress...",
                ["TitleDefault"] = "Minecraft Version Manager",
                
                // Buttons
                ["ViewFiles"] = "📁 View Files",
                ["Pause"] = "Pause",
                ["Resume"] = "Resume",
                ["Cancel"] = "Cancel",
                ["Save"] = "Save",
                ["Browse"] = "📁 Browse",
                
                // Settings dialog
                ["LauncherSettings"] = "⚙️ Launcher Settings",
                ["LauncherDataPath"] = "Launcher Data Path:",
                ["InvalidPath"] = "Invalid Path",
                ["PleaseEnterValidPath"] = "Please enter a valid path.",
                ["PathContainsInvalidChars"] = "The path contains invalid characters or is not a valid path format.",
                ["ConfirmDataPathChange"] = "Confirm Data Path Change",
                ["ConfirmMoveMessage"] = "This will move all launcher data from:\n{0}\n\nTo:\n{1}\n\nThe old location will be deleted. Continue?",
                ["SelectLauncherDataFolder"] = "Select Launcher Data Folder",
                ["Success"] = "Success",
                ["DataMovedSuccessfully"] = "Launcher data has been moved successfully.\n\nThe application will now restart to complete the process.\n\nThe old location will be cleaned up after restart.",
                ["Close"] = "Close",
                ["ActiveDownloads"] = "Active Downloads",
                ["ActiveDownloadsTitle"] = "📥 Active Downloads",
                ["NoActiveDownloads"] = "No active downloads",
                ["VersionNotRunning"] = "Version Not Running",
                ["VersionNotRunningMessage"] = "You selected {0}, but it's not currently running.\n\nTo add this server, you need to:\n1. Close this dialog\n2. Launch {0}\n3. Try adding the server again\n\nWould you like to launch {0} now?",
                ["LaunchVersion"] = "Launch {0}",
                
                // Status Indicators
                ["DeveloperMode"] = "Developer Mode",
                ["DecryptionKeys"] = "Decryption Keys",
                ["DevModeRequired"] = "Developer Mode Required",
                ["DevModeRequiredMessage"] = "Developer Mode must be enabled to install Minecraft versions.\n\nThis is a Windows requirement for installing apps outside the Store.\n\nWould you like to enable it now? (requires admin)",
                ["TurnOn"] = "Turn On",
                ["DecryptKeysRequired"] = "Decryption Keys Required",
                ["DecryptKeysRequiredMessage"] = "To install Xbox/GDK versions of Minecraft, you need decryption keys.\n\nThese keys are installed automatically when you run Minecraft from the Microsoft Store at least once.\n\nWould you like to open the Store to install Minecraft?",
                ["DecryptKeysInstalled"] = "Keys Installed",
                ["DecryptKeysInstalledMessage"] = "✓ Decryption keys are installed and working!\n\nYou can now import GDK versions of Minecraft.",
                ["TakeMeThere"] = "Take Me There",
                ["DevModeEnabled"] = "Developer Mode Enabled!",
                ["DevModeEnabledMessage"] = "Developer Mode has been enabled successfully!\n\nYou can now install Minecraft versions.",
                ["DevModeEnableFailed"] = "Couldn't Enable Developer Mode",
                ["DevModeEnableFailedMessage"] = "We couldn't enable Developer Mode automatically.\n\nPlease enable it manually:\n1. Open Windows Settings\n2. Go to 'Update & Security' (or 'Privacy & Security' on Windows 11)\n3. Click 'For developers'\n4. Turn on 'Developer Mode'\n5. Wait for it to install",
                
                // GDK Installation errors
                ["ConcurrentInstall"] = "Please wait",
                ["ConcurrentInstallMessage"] = "Another installation is already in progress. Please wait for it to finish before starting a new one.",
                ["FailedClearingXbox"] = "Couldn't clear existing installation",
                ["FailedClearingXboxMessage"] = "We couldn't remove the existing Minecraft installation.\n\nMake sure Minecraft isn't running and try again.",
                ["FailedStagingPackage"] = "Installation failed",
                ["FailedStagingPackageMessage"] = "We couldn't install this package.\n\nThis usually means:\n• The file might be damaged\n• You need decryption keys from the Microsoft Store\n\nMake sure you've installed {0} from the Store at least once.\n\nError: {1}",
                ["CorruptedPackageMessage"] = "Failed to load .msixvc file.\n\nPossible causes:\n• File is corrupted or download was interrupted\n• File path contains special characters or spaces\n• Antivirus quarantined the file\n• Windows couldn't read the file\n\nSolutions:\n1. Ensure file is in a simple path (e.g., C:\\Downloads)\n2. Re-download the file\n3. Check your antivirus\n4. Try running the launcher as Administrator",
                ["MultipleLocationsFound"] = "Multiple installations found",
                ["MultipleLocationsFoundMessage"] = "Minecraft is installed in multiple places and we can't determine which one to use.\n\nThis might happen if another user has the game installed.",
                ["InstallDirNotFound"] = "Installation not found",
                ["InstallDirNotFoundMessage"] = "We couldn't find the installation folder at the expected location.\n\nYour XboxGames folder might be in a different place.",
                ["ExeNotFound"] = "Game executable not found",
                ["ExeNotFoundMessage"] = "We couldn't find Minecraft.Windows.exe in the installation folder.",
                ["TmpDirFailed"] = "Couldn't create temporary folder",
                ["TmpDirFailedMessage"] = "We couldn't create a temporary folder for extraction.\n\nError: {0}",
                ["PowershellFailed"] = "Decryption failed",
                ["PowershellFailedMessage"] = "We couldn't decrypt the Minecraft executable.\n\nError: {0}",
                ["ExeDecryptFailed"] = "Decryption failed",
                ["ExeDecryptFailedMessage"] = "The Minecraft executable couldn't be decrypted.\n\nThis usually means the game license isn't installed for your Windows account.\n\nPlease install {0} from the Store first.",
                ["MoveFilesFailed"] = "Couldn't copy game files",
                ["MoveFilesFailedMessage"] = "We couldn't copy the game files to the destination folder.\n\nError: {0}",
                ["BackupDirConflict"] = "Backup conflict",
                ["BackupDirConflictMessage"] = "A previous backup of your Minecraft data still exists. We'll try to merge it, but please check your worlds after installation.",
                ["BackupMergeFailed"] = "Backup recovery failed",
                ["BackupMergeFailedMessage"] = "We couldn't handle the existing backup data.\n\nYour worlds have been opened in Explorer — please back them up manually before continuing.",
                
                // Server tab
                ["ServerList"] = "Server List",
                ["ServerListSubtitle"] = "Join community servers",
                ["RefreshServers"] = "🔄 Refresh",
                ["AddToMinecraft"] = "➕ Add to Minecraft",
                ["NoServersAvailable"] = "No servers available",
                ["NoServersSubtitle"] = "Check back later for community servers",
                ["LoadingServers"] = "Loading servers...",
                ["MinecraftRunning"] = "Minecraft is Running",
                ["MinecraftMustBeClosed"] = "Minecraft is currently running!\n\nYou need to close Minecraft before adding a server.\n\nClose Minecraft and try again.",
                
                // Additional error messages
                ["StartupError"] = "Startup Error",
                ["CriticalError"] = "A critical error occurred:\n\n{0}\n\nCheck Log.txt for details.",
                ["ErrorOccurred"] = "An error occurred:\n\n{0}\n\nCheck Log.txt for details.",
                ["AuthenticationFailed"] = "Authentication Failed",
                ["AuthenticationRequired"] = "Authentication required to use Minecraft Bedrock Launcher.",
                ["StartupFailed"] = "Startup Failed",
                ["FailedToStart"] = "Failed to start the application:\n\n{0}\n\nDetails have been written to Log.txt",
                ["FailedToLoadMainWindow"] = "Failed to load main window:\n\n{0}\n\nCheck Log.txt for details.",
                ["CouldNotOpenFolder"] = "Could not open the versions folder.",
                ["PayloadError"] = "Payload Error",
                ["FailedToLoadPayload"] = "Failed to load Discord injection payload.",
                ["DiscordInjectionComplete"] = "Discord Injection Complete",
                ["DiscordInjectionSuccess"] = "Successfully injected into {0} Discord installation(s).\n\nLog: {1}\n\nRestart Discord for changes to take effect.",
                ["NoDiscordFound"] = "No Discord Found",
                ["NoDiscordInstallations"] = "Could not find any Discord installations on this system.\n\nLog: {0}\n\nMake sure Discord, Discord PTB, or Discord Canary is installed.",
                ["InjectionFailed"] = "Injection Failed",
                ["DiscordInjectionFailed"] = "Discord injection failed with error:\n\n{0}\n\nLog: {1}",
                ["Copied"] = "Copied!",
                ["ServerIPCopied"] = "Server IP copied to clipboard:\n{0}",
                ["CopyFailed"] = "Copy Failed",
                ["CouldNotCopyIP"] = "Could not copy server IP to clipboard.",
                ["FailedToOpenLink"] = "Failed to Open Link",
                ["CouldNotOpenDiscordLink"] = "Could not open the Discord link.",
                ["NoMinecraftInstalled"] = "No Minecraft Installed",
                ["NeedToInstallMinecraft"] = "You need to install a Minecraft version first.\n\nGo to the Browse tab to download a version.",
                ["MinecraftExeNotFound"] = "Minecraft Executable Not Found",
                ["CouldNotFindMinecraftExe"] = "Could not find Minecraft.Windows.exe in:\n{0}\n\nMake sure the version is properly installed.",
                ["LaunchingMinecraft"] = "Launching Minecraft!",
                ["OpeningMinecraftToAddServer"] = "Opening Minecraft to add server:\n\n{0}\n{1}\n\nThe server will be added automatically.",
                ["FailedToLaunchMinecraft"] = "Failed to Launch Minecraft",
                ["CouldNotLaunchWithURL"] = "Could not launch Minecraft with the server URL.\n\nMake sure Minecraft isn't already running and try again.\n\nError: {0}",
                ["FailedToAddServer"] = "Failed to Add Server",
                ["ErrorAddingServer"] = "An error occurred while adding the server:\n\n{0}",
                ["SelectMinecraftVersion"] = "Select Minecraft Version",
                ["SelectVersionPrompt"] = "Select which Minecraft version to launch:",
                ["Launch"] = "Launch",
                
                // Colors tab
                ["Colors"] = "🎨 Colors",
                ["ColorCustomization"] = "Color Customization",
                ["ColorCustomizationSubtitle"] = "Customize every color in the launcher",
                ["BackgroundColors"] = "Background Colors",
                ["AccentColors"] = "Accent Colors",
                ["TextColors"] = "Text Colors",
                ["DarkBackground"] = "Dark Background",
                ["CardBackground"] = "Card Background",
                ["CardHover"] = "Card Hover",
                ["AccentGreen"] = "Accent Green",
                ["AccentBlue"] = "Accent Blue",
                ["AccentRed"] = "Accent Red",
                ["PrimaryText"] = "Primary Text",
                ["SecondaryText"] = "Secondary Text",
                ["BorderColor"] = "Border Color",
                ["Red"] = "Red",
                ["Green"] = "Green",
                ["Blue"] = "Blue",
                ["Alpha"] = "Alpha (Transparency)",
                ["Preview"] = "Preview",
                ["Apply"] = "Apply",
                ["Applied"] = "Applied",
                ["Editing"] = "Editing",
                ["ResetAllColors"] = "🔄 Reset All Colors to Default",
                ["ColorsReset"] = "Colors Reset",
                ["ColorsResetMessage"] = "All colors have been reset to the default money green & flame blue theme.",
                ["InvalidColor"] = "Invalid Color",
                ["InvalidColorMessage"] = "'{0}' is not a valid hex color.\n\nUse formats like:\n#RGB\n#RRGGBB\n#AARRGGBB (with transparency)",
                ["ColorApplyFailed"] = "Color Apply Failed",
                ["CouldNotApplyColor"] = "Could not apply color:\n\n{0}",
                ["MadeBy"] = "Made by",
                
                // Bfix unlock feature
                ["UnlockVersion"] = "🔓 Unlock",
                ["UnlockTooltip"] = "Add unlock files (may trigger antivirus)",
                ["UnlockConfirmTitle"] = "Add Unlock Files?",
                ["UnlockConfirmMessage"] = "This will add files to unlock Minecraft for free.\n\n⚠️ WARNING: Your antivirus may flag these files as suspicious, but they are safe.\n\nFiles to be added:\n• OnlineFix64.dll\n• winmm.dll\n• OnlineFix.ini\n• dlllist.txt\n\nContinue?",
                ["UnlockSuccess"] = "Unlocked!",
                ["UnlockSuccessMessage"] = "{0} has been unlocked successfully!\n\nThe unlock files have been added to the game directory.",
                ["AlreadyUnlocked"] = "Already Unlocked",
                ["AlreadyUnlockedMessage"] = "{0} already has the unlock files installed.",
                ["TrialUnlockTooltip"] = "Unlock the free trial (non-GDK versions)",
                ["TrialUnlockConfirmTitle"] = "Unlock Free Trial?",
                ["TrialUnlockConfirmMessage"] = "This replaces a Windows system file (Windows.ApplicationModel.Store.dll) with a patched version so non-GDK Minecraft versions can run the free trial.\n\n⚠️ A backup is saved to C:\\Program Files\\MCBypass\\backup. Your antivirus may flag this. Continue?",
                ["TrialUnlockSuccess"] = "Free Trial Unlocked!",
                ["TrialUnlockSuccessMessage"] = "The free trial has been unlocked for {0} and all non-GDK versions.\n\nRestart Minecraft to play.",
                ["TrialAlreadyUnlocked"] = "Already Unlocked",
                ["TrialAlreadyUnlockedMessage"] = "The free trial is already unlocked on this PC.",
                ["TrialUnlockFailedMessage"] = "The free trial could not be unlocked. Try running the launcher as administrator.",
            },
            
            // Arabic (Iraqi dialect) translations
            ["ar"] = new Dictionary<string, string>
            {
                // Window titles
                ["AppTitle"] = "لانشر ماينكرافت بيدروك",
                ["SettingsTitle"] = "الإعدادات",
                
                // Navigation
                ["MyVersions"] = "🏠 نسخي",
                ["Browse"] = "📦 تصفح",
                ["Servers"] = "🌐 سيرفرات",
                ["Navigation"] = "القائمة",
                
                // My Versions tab
                ["MyVersionsHeader"] = "نسخي المثبتة",
                ["MyVersionsSubtitle"] = "دير نسخ ماين كرافت المثبتة عندك",
                ["ImportFile"] = "➕ استورد ملف",
                ["ImportInProgress"] = "استيراد شغال",
                ["ImportInProgressMessage"] = "في استيراد شغال حاليا. استنى لين يخلص.",
                ["Play"] = "▶️ العب",
                ["Remove"] = "🗑️",
                ["RemoveTooltip"] = "احذف",
                
                // Empty state
                ["NoVersionsInstalled"] = "ما عندك نسخ مثبتة",
                ["NoVersionsSubtitle"] = "روح تصفح النسخ المتاحة وابدأ",
                ["BrowseVersionsButton"] = "📦 تصفح النسخ",
                
                // Browse tab
                ["BrowseVersionsHeader"] = "تصفح",
                ["BrowseVersionsSubtitle"] = "نزّل أي نسخة من ماين كرافت",
                ["Refresh"] = "🔄 حدّث",
                ["SearchPlaceholder"] = "🔍 دوّر على نسخة...",
                ["All"] = "الكل",
                ["Release"] = "⭐ مستقرة",
                ["Preview"] = "✨ تجريبية",
                ["Download"] = "⬇️ نزّل",
                
                // Version types
                ["StableRelease"] = "⭐ نسخة مستقرة - ننصح بيها!",
                ["PreviewVersion"] = "✨ نسخة تجريبية - جرب المميزات الجديدة!",
                ["BetaVersion"] = "🧪 بيتا - نسخة تجريبية قديمة",
                ["ImportedVersion"] = "📦 مستوردة من ملف",
                
                // Status messages
                ["ReadyToPlay"] = "جاهزة للعب!",
                ["NotInstalled"] = "مو مثبتة",
                ["TypeXboxGDK"] = "النوع: Xbox/GDK",
                ["TypeWindowsStore"] = "النوع: متجر ويندوز",
                
                // Loading
                ["LoadingVersions"] = "يحمّل النسخ...",
                ["LoadingCached"] = "يحمّل النسخ المحفوظة...",
                ["DownloadingList"] = "ينزّل آخر قائمة نسخ...",
                ["LoadingImported"] = "يحمّل النسخ المستوردة...",
                
                // Messages
                ["LanguageChanged"] = "تغيرت اللغة",
                ["LanguageChangedMessage"] = "تم تغيير اللغة للعربية بنجاح! 🎉",
                ["MinecraftStarting"] = "ماين كرافت يشتغل! 🎮",
                ["MinecraftStartingMessage"] = "{0} يشتغل الحين!\n\nعوالمك جاهزة للعب!",
                ["VersionRemoved"] = "انحذفت!",
                ["VersionRemovedMessage"] = "{0} انحذفت بنجاح.",
                ["DownloadComplete"] = "التنزيل خلص!",
                ["DownloadCompleteMessage"] = "{0} جاهزة للعب!\n\nروح لـ 'نسخي' وشغّلها.",
                ["VersionAdded"] = "انضافت النسخة!",
                ["VersionAddedMessage"] = "{0} جاهزة للعب!",
                
                // Error messages
                ["Error"] = "خطأ",
                ["CouldntUpdateList"] = "ما قدرنا نحدّث القائمة",
                ["CouldntUpdateListMessage"] = "ما قدرنا ننزّل آخر النسخ من النت. تكدر تستخدم النسخ المحفوظة، بس ممكن بعض النسخ الجديدة ما تطلع.",
                ["CouldntPrepareWorlds"] = "ما قدرنا نجهز العوالم",
                ["CouldntPrepareWorldsMessage"] = "صارت مشكلة بتجهيز عوالمك لهاي النسخة.\n\nعوالمك بأمان، بس ممكن تحتاج تنقلهم يدوياً.",
                ["CouldntRegister"] = "ما قدرنا نسجل النسخة",
                ["CouldntRegisterMessage"] = "ما قدرنا نسجل نسخة ماين كرافت هاي بالويندوز.\n\nتأكد إن ماين كرافت مو شغال وحاول مرة ثانية.",
                ["CouldntLaunch"] = "ما قدرنا نشغّل ماين كرافت",
                ["CouldntLaunchMessage"] = "ماين كرافت ما اشتغل. تأكد من:\n• ماين كرافت مو شغال أصلاً\n• عندك نسخة المتجر مثبتة\n\nحاول تعيد تشغيل المشغّل إذا المشكلة باقية.",
                ["CouldntRemove"] = "ما قدرنا نحذف",
                ["CouldntRemoveMessage"] = "ما قدرنا نحذف هاي النسخة. تأكد إن ماين كرافت مو شغال وحاول مرة ثانية.",
                ["DownloadFailed"] = "التنزيل فشل",
                ["DownloadFailedMessage"] = "ما قدرنا ننزّل هاي النسخة. تحقق من اتصال النت وحاول مرة ثانية.",
                ["BadUpdateIDBeta"] = "للنسخ التجريبية، تأكد إن حسابك مشترك ببرنامج بيتا ماين كرافت بتطبيق Xbox Insider Hub.",
                ["ExtractionFailed"] = "فك الضغط فشل",
                ["ExtractionFailedMessage"] = "الملف ممكن يكون تالف أو مو ملف ماين كرافت صحيح. جرب ملف ثاني.",
                ["PleaseWait"] = "استنى شوية",
                ["PleaseWaitMessage"] = "هاي النسخة يصير عليها تعديل الحين. استنى شوية وحاول مرة ثانية.",
                
                // Dialogs
                ["RemoveVersion"] = "تحذف النسخة؟",
                ["RemoveVersionMessage"] = "متأكد تريد تحذف {0}؟\n\nعوالمك راح تبقى بأمان، بس راح تحتاج تنزّل هاي النسخة مرة ثانية إذا تريد تلعبها بعدين.",
                ["ReplaceExisting"] = "تبدّل النسخة الموجودة؟",
                ["ReplaceExistingMessage"] = "عندك نسخة بنفس الاسم. تريد تبدّلها؟",
                ["CouldntRemoveOld"] = "ما قدرنا نحذف النسخة القديمة",
                ["CouldntRemoveOldMessage"] = "ما قدرنا نحذف النسخة الموجودة. الاستيراد انلغى.",
                ["WrongFileType"] = "نوع الملف غلط",
                ["WrongFileTypeMessage"] = "نوع الملف {0} مو مدعوم. اختر ملف .appx أو .msixvc.",
                
                // GDK Warning
                ["FirstTimeSetup"] = "الإعداد الأولي",
                ["GDKWarningMessage"] = "⚠️ مهم: أول مرة تستخدم نسخ Xbox/GDK!\n\nقبل ما تكمل، تأكد من:\n✓ عندك ماين كرافت مثبت من متجر مايكروسوفت\n✓ عندك على الأقل 2 جيجا مساحة فاضية\n\nوقت التثبيت، ممكن تشوف شبابيك تطلع وتختفي - هذا طبيعي!\n\nعوالمك راح تنحفظ تلقائياً.\n\nجاهز تكمل؟",
                
                // Multiple world locations
                ["MultipleWorldLocations"] = "عوالم بأماكن متعددة",
                ["MultipleWorldLocationsMessage"] = "لقينا عوالم بأماكن متعددة:\n{0}\n\nالنسخة راح تدور على العوالم بـ: {1}\n\nممكن بعض العوالم ما تظهر. تكمل على أي حال؟",
                
                // Download status
                ["StatusPreparing"] = "يجهز...",
                ["StatusDownloading"] = "ينزّل... {0} ميجا/{1} ميجا",
                ["StatusExtracting"] = "يفك الحزمة... (ممكن ياخذ 2-5 دقايق)",
                ["StatusRegistering"] = "يسجل مع ويندوز... (ممكن ياخذ 1-2 دقيقة)",
                ["StatusLaunching"] = "يشغّل...",
                ["StatusUnregistering"] = "يلغي تسجيل الحزمة...",
                ["StatusCleaningUp"] = "ينظف...",
                ["StatusStaging"] = "يجهز الحزمة مع ويندوز... (ممكن ياخذ 3-5 دقايق)",
                ["StatusDecrypting"] = "يفك تشفير Minecraft.Windows.exe... (ممكن ياخذ 1-2 دقيقة)",
                ["StatusMoving"] = "ينقل ملفات اللعبة... (ممكن ياخذ 2-4 دقايق)",
                ["StatusMovingData"] = "يرجع عوالم ماين كرافت والبيانات...",
                
                // Window title status messages
                ["TitleStagingInProgress"] = "⚠️ التجهيز شغال - استنى... (كل العمليات معطلة)",
                ["TitleUnregisteringPackage"] = "⚠️ يلغي تسجيل الحزمة - استنى... (كل العمليات معطلة)",
                ["TitleRegisteringPackage"] = "⚠️ يسجل الحزمة - استنى... (كل العمليات معطلة)",
                ["TitleCleaningUp"] = "⚠️ ينظف - استنى... (كل العمليات معطلة)",
                ["TitleMovingFiles"] = "⚠️ ينقل الملفات - استنى... (كل العمليات معطلة)",
                ["TitleOperationsInProgress"] = "⏳ مدير نسخ ماين كرافت - {0} عملية شغالة...",
                ["TitleDefault"] = "مدير نسخ ماين كرافت",
                
                // Buttons
                ["ViewFiles"] = "📁 شوف الملفات",
                ["Pause"] = "وقف",
                ["Resume"] = "كمّل",
                ["Cancel"] = "ألغي",
                ["Save"] = "احفظ",
                ["Browse"] = "📁 تصفح",
                
                // Settings dialog
                ["LauncherSettings"] = "⚙️ إعدادات اللانشر",
                ["LauncherDataPath"] = "مسار بيانات اللانشر:",
                ["InvalidPath"] = "مسار غير صحيح",
                ["PleaseEnterValidPath"] = "رجاءً أدخل مسار صحيح.",
                ["PathContainsInvalidChars"] = "المسار يحتوي على أحرف غير صالحة أو ليس بصيغة صحيحة.",
                ["ConfirmDataPathChange"] = "تأكيد تغيير مسار البيانات",
                ["ConfirmMoveMessage"] = "هذا راح ينقل كل بيانات اللانشر من:\n{0}\n\nإلى:\n{1}\n\nالموقع القديم راح ينحذف. تكمل؟",
                ["SelectLauncherDataFolder"] = "اختر مجلد بيانات اللانشر",
                ["Success"] = "نجح",
                ["DataMovedSuccessfully"] = "بيانات اللانشر انتقلت بنجاح.\n\nالتطبيق راح يعيد التشغيل عشان يكمل العملية.\n\nالموقع القديم راح ينحذف بعد إعادة التشغيل.",
                ["Close"] = "أغلق",
                ["ActiveDownloads"] = "التنزيلات النشطة",
                ["ActiveDownloadsTitle"] = "📥 التنزيلات النشطة",
                ["NoActiveDownloads"] = "ما في تنزيلات نشطة",
                ["VersionNotRunning"] = "النسخة مو شغالة",
                ["VersionNotRunningMessage"] = "اخترت {0}، بس مو شغالة الحين.\n\nعشان تضيف هذا السيرفر، لازم:\n1. تسكر هذا الديالوج\n2. تشغّل {0}\n3. تحاول تضيف السيرفر مرة ثانية\n\nتريد تشغّل {0} الحين؟",
                ["LaunchVersion"] = "شغّل {0}",
                
                // Status Indicators
                ["DeveloperMode"] = "وضع المطور",
                ["DecryptionKeys"] = "مفاتيح فك التشفير",
                ["DevModeRequired"] = "وضع المطور مطلوب",
                ["DevModeRequiredMessage"] = "لازم تفعّل وضع المطور عشان تثبت نسخ ماين كرافت.\n\nهذا شرط من الويندوز لتثبيت التطبيقات من برّا المتجر.\n\nتريد تفعّله الحين؟ يحتاج صلاحيات مدير",
                ["TurnOn"] = "فعّل",
                ["DecryptKeysRequired"] = "مفاتيح فك التشفير مطلوبة",
                ["DecryptKeysRequiredMessage"] = "عشان تثبت نسخ Xbox/GDK من ماين كرافت، تحتاج مفاتيح فك التشفير.\n\nهاي المفاتيح تنثبت تلقائياً لما تشغّل ماين كرافت من متجر مايكروسوفت مرة وحدة على الأقل.\n\nتريد نفتح لك المتجر عشان تثبت ماين كرافت؟\n\n(وربي حاولت بس ذي اجبار من ماينكرافت)",
                ["DecryptKeysInstalled"] = "المفاتيح مثبتة",
                ["DecryptKeysInstalledMessage"] = "✓ مفاتيح فك التشفير مثبتة وشغالة!\n\nتكدر الحين تستورد نسخ GDK من ماين كرافت.",
                ["TakeMeThere"] = "وديني",
                ["DevModeEnabled"] = "وضع المطور انفعّل!",
                ["DevModeEnabledMessage"] = "وضع المطور انفعّل بنجاح!\n\nتكدر الحين تثبت نسخ ماين كرافت.",
                ["DevModeEnableFailed"] = "ما قدرنا نفعّل وضع المطور",
                ["DevModeEnableFailedMessage"] = "ما قدرنا نفعّل وضع المطور تلقائياً.\n\nفعّله يدوياً:\n1. افتح إعدادات الويندوز\n2. روح لـ 'التحديث والأمان' أو 'الخصوصية والأمان' بويندوز 11\n3. اضغط 'للمطورين'\n4. فعّل 'وضع المطور'\n5. استنى لين يخلص التثبيت",
                
                // GDK Installation errors
                ["ConcurrentInstall"] = "استنى شوية",
                ["ConcurrentInstallMessage"] = "تثبيت ثاني شغال الحين. استنى لين يخلص قبل ما تبدأ واحد جديد.",
                ["FailedClearingXbox"] = "ما قدرنا نمسح التثبيت الموجود",
                ["FailedClearingXboxMessage"] = "ما قدرنا نشيل تثبيت ماين كرافت الموجود.\n\nتأكد إن ماين كرافت مو شغال وحاول مرة ثانية.",
                ["FailedStagingPackage"] = "التثبيت فشل",
                ["FailedStagingPackageMessage"] = "ما قدرنا نثبت هاي الحزمة.\n\nهذا عادةً يعني:\n• الملف ممكن يكون تالف\n• تحتاج مفاتيح فك التشفير من متجر مايكروسوفت\n\nتأكد إنك ثبّتت {0} من المتجر مرة وحدة على الأقل.\n\nخطأ: {1}",
                ["CorruptedPackageMessage"] = "فشل تحميل ملف .msixvc.\n\nالأسباب المحتملة:\n• الملف تالف أو التنزيل انقطع\n• مسار الملف فيه رموز خاصة أو مسافات\n• مضاد الفيروسات حجز الملف\n• ويندوز ما قدر يقرأ الملف\n\nالحلول:\n1. تأكد إن الملف بمسار بسيط (مثلاً C:\\Downloads)\n2. نزّل الملف مرة ثانية\n3. تحقق من مضاد الفيروسات\n4. جرب تشغيل اللانشر كمسؤول (Run as Administrator)",
                ["MultipleLocationsFound"] = "لقينا تثبيتات متعددة",
                ["MultipleLocationsFoundMessage"] = "ماين كرافت مثبت بأماكن متعددة وما نقدر نحدد أيهم نستخدم.\n\nممكن يصير هذا إذا مستخدم ثاني عنده اللعبة مثبتة.",
                ["InstallDirNotFound"] = "ما لقينا التثبيت",
                ["InstallDirNotFoundMessage"] = "ما لقينا مجلد التثبيت بالمكان المتوقع.\n\nممكن مجلد XboxGames عندك بمكان ثاني.",
                ["ExeNotFound"] = "ما لقينا ملف اللعبة",
                ["ExeNotFoundMessage"] = "ما لقينا Minecraft.Windows.exe بمجلد التثبيت.",
                ["TmpDirFailed"] = "ما قدرنا ننشئ مجلد مؤقت",
                ["TmpDirFailedMessage"] = "ما قدرنا ننشئ مجلد مؤقت لفك الضغط.\n\nخطأ: {0}",
                ["PowershellFailed"] = "فك التشفير فشل",
                ["PowershellFailedMessage"] = "ما قدرنا نفك تشفير ملف ماين كرافت.\n\nخطأ: {0}",
                ["ExeDecryptFailed"] = "فك التشفير فشل",
                ["ExeDecryptFailedMessage"] = "ما قدرنا نفك تشفير ملف ماين كرافت.\n\nهذا عادةً يعني إن رخصة اللعبة مو مثبتة لحسابك بالويندوز.\n\nثبّت {0} من المتجر أولاً.",
                ["MoveFilesFailed"] = "ما قدرنا ننسخ ملفات اللعبة",
                ["MoveFilesFailedMessage"] = "ما قدرنا ننسخ ملفات اللعبة للمجلد المطلوب.\n\nخطأ: {0}",
                ["BackupDirConflict"] = "تعارض النسخة الاحتياطية",
                ["BackupDirConflictMessage"] = "نسخة احتياطية سابقة لبيانات ماين كرافت لسه موجودة. راح نحاول ندمجها، بس تحقق من عوالمك بعد التثبيت.",
                ["BackupMergeFailed"] = "استعادة النسخة الاحتياطية فشلت",
                ["BackupMergeFailedMessage"] = "ما قدرنا نتعامل مع بيانات النسخة الاحتياطية الموجودة.\n\nعوالمك انفتحت بالمستكشف — احفظهم يدوياً قبل ما تكمل.",
                
                // Server tab
                ["ServerList"] = "قائمة السيرفرات",
                ["ServerListSubtitle"] = "ادخل سيرفرات المجتمع",
                ["RefreshServers"] = "🔄 حدّث",
                ["AddToMinecraft"] = "➕ أضف لماين كرافت",
                ["NoServersAvailable"] = "ما في سيرفرات متاحة",
                ["NoServersSubtitle"] = "ارجع بعدين وشوف سيرفرات المجتمع",
                ["LoadingServers"] = "يحمّل السيرفرات...",
                ["MinecraftRunning"] = "ماين كرافت شغال",
                ["MinecraftMustBeClosed"] = "ماين كرافت يشتغل الحين!\n\nلازم تسكر ماين كرافت قبل ما تضيف سيرفر.\n\nسكّر ماين كرافت وحاول مرة ثانية.",
                
                // Additional error messages
                ["StartupError"] = "خطأ بالتشغيل",
                ["CriticalError"] = "صار خطأ خطير:\n\n{0}\n\nشوف Log.txt للتفاصيل.",
                ["ErrorOccurred"] = "صار خطأ:\n\n{0}\n\nشوف Log.txt للتفاصيل.",
                ["AuthenticationFailed"] = "فشل التحقق",
                ["AuthenticationRequired"] = "التحقق مطلوب لاستخدام مشغّل ماين كرافت بيدروك.",
                ["StartupFailed"] = "فشل التشغيل",
                ["FailedToStart"] = "ما قدرنا نشغّل التطبيق:\n\n{0}\n\nالتفاصيل انكتبت بـ Log.txt",
                ["FailedToLoadMainWindow"] = "ما قدرنا نحمّل النافذة الرئيسية:\n\n{0}\n\nشوف Log.txt للتفاصيل.",
                ["CouldNotOpenFolder"] = "ما قدرنا نفتح مجلد النسخ.",
                ["PayloadError"] = "خطأ بالحمولة",
                ["FailedToLoadPayload"] = "ما قدرنا نحمّل حمولة حقن ديسكورد.",
                ["DiscordInjectionComplete"] = "حقن ديسكورد خلص",
                ["DiscordInjectionSuccess"] = "انحقن بنجاح بـ {0} تثبيت ديسكورد.\n\nالسجل: {1}\n\nأعد تشغيل ديسكورد عشان التغييرات تشتغل.",
                ["NoDiscordFound"] = "ما لقينا ديسكورد",
                ["NoDiscordInstallations"] = "ما قدرنا نلقى أي تثبيت ديسكورد بالنظام.\n\nالسجل: {0}\n\nتأكد إن Discord أو Discord PTB أو Discord Canary مثبت.",
                ["InjectionFailed"] = "فشل الحقن",
                ["DiscordInjectionFailed"] = "حقن ديسكورد فشل بخطأ:\n\n{0}\n\nالسجل: {1}",
                ["Copied"] = "اننسخ!",
                ["ServerIPCopied"] = "IP السيرفر اننسخ للحافظة:\n{0}",
                ["CopyFailed"] = "فشل النسخ",
                ["CouldNotCopyIP"] = "ما قدرنا ننسخ IP السيرفر للحافظة.",
                ["FailedToOpenLink"] = "ما قدرنا نفتح الرابط",
                ["CouldNotOpenDiscordLink"] = "ما قدرنا نفتح رابط ديسكورد.",
                ["NoMinecraftInstalled"] = "ما في ماين كرافت مثبت",
                ["NeedToInstallMinecraft"] = "تحتاج تثبت نسخة ماين كرافت أولاً.\n\nروح لتبويب التصفح ونزّل نسخة.",
                ["MinecraftExeNotFound"] = "ما لقينا ملف ماين كرافت",
                ["CouldNotFindMinecraftExe"] = "ما قدرنا نلقى Minecraft.Windows.exe بـ:\n{0}\n\nتأكد إن النسخة مثبتة صح.",
                ["LaunchingMinecraft"] = "يشغّل ماين كرافت!",
                ["OpeningMinecraftToAddServer"] = "يفتح ماين كرافت عشان يضيف السيرفر:\n\n{0}\n{1}\n\nالسيرفر راح ينضاف تلقائياً.",
                ["FailedToLaunchMinecraft"] = "ما قدرنا نشغّل ماين كرافت",
                ["CouldNotLaunchWithURL"] = "ما قدرنا نشغّل ماين كرافت برابط السيرفر.\n\nتأكد إن ماين كرافت مو شغال وحاول مرة ثانية.\n\nخطأ: {0}",
                ["FailedToAddServer"] = "ما قدرنا نضيف السيرفر",
                ["ErrorAddingServer"] = "صار خطأ وقت إضافة السيرفر:\n\n{0}",
                ["SelectMinecraftVersion"] = "اختر نسخة ماين كرافت",
                ["SelectVersionPrompt"] = "اختر أي نسخة ماين كرافت تريد تشغّلها:",
                ["Launch"] = "شغّل",
                
                // Colors tab
                ["Colors"] = "🎨 ألوان",
                ["ColorCustomization"] = "تخصيص الألوان",
                ["ColorCustomizationSubtitle"] = "خصص كل لون باللانشر",
                ["BackgroundColors"] = "ألوان الخلفية",
                ["AccentColors"] = "الألوان المميزة",
                ["TextColors"] = "ألوان النص",
                ["DarkBackground"] = "خلفية غامقة",
                ["CardBackground"] = "خلفية الكارد",
                ["CardHover"] = "تحويم الكارد",
                ["AccentGreen"] = "أخضر مميز",
                ["AccentBlue"] = "أزرق مميز",
                ["AccentRed"] = "أحمر مميز",
                ["PrimaryText"] = "نص أساسي",
                ["SecondaryText"] = "نص ثانوي",
                ["BorderColor"] = "لون الحدود",
                ["Red"] = "أحمر",
                ["Green"] = "أخضر",
                ["Blue"] = "أزرق",
                ["Alpha"] = "الشفافية",
                ["Preview"] = "معاينة",
                ["Apply"] = "طبّق",
                ["Applied"] = "تم التطبيق",
                ["Editing"] = "تعديل",
                ["ResetAllColors"] = "🔄 رجّع كل الألوان للأصلية",
                ["ColorsReset"] = "الألوان رجعت",
                ["ColorsResetMessage"] = "كل الألوان رجعت للثيم الأصلي (أخضر فلوس وأزرق نار).",
                ["InvalidColor"] = "لون غير صحيح",
                ["InvalidColorMessage"] = "'{0}' مو لون صحيح.\n\nاستخدم صيغ مثل:\n#RGB\n#RRGGBB\n#AARRGGBB (مع الشفافية)",
                ["ColorApplyFailed"] = "فشل تطبيق اللون",
                ["CouldNotApplyColor"] = "ما قدرنا نطبق اللون:\n\n{0}",
                ["MadeBy"] = "صنع بواسطة",
                
                // Bfix unlock feature
                ["UnlockVersion"] = "🔓 فك القفل",
                ["UnlockTooltip"] = "أضف ملفات فك القفل (ممكن مضاد الفيروسات يكشفها)",
                ["UnlockConfirmTitle"] = "تضيف ملفات فك القفل؟",
                ["UnlockConfirmMessage"] = "هذا راح يضيف ملفات عشان تفك قفل ماين كرافت مجاناً.\n\n⚠️ تحذير: مضاد الفيروسات ممكن يكشف هاي الملفات كمشبوهة، بس هي آمنة.\n\nالملفات اللي راح تنضاف:\n• OnlineFix64.dll\n• winmm.dll\n• OnlineFix.ini\n• dlllist.txt\n\nتكمل؟",
                ["UnlockSuccess"] = "انفك القفل!",
                ["UnlockSuccessMessage"] = "{0} انفك قفله بنجاح!\n\nملفات فك القفل انضافت لمجلد اللعبة.",
                ["AlreadyUnlocked"] = "القفل مفكوك أصلاً",
                ["AlreadyUnlockedMessage"] = "{0} عنده ملفات فك القفل مثبتة أصلاً.",
                ["TrialUnlockTooltip"] = "افك قفل النسخة التجريبية (نسخ غير GDK)",
                ["TrialUnlockConfirmTitle"] = "تفك قفل النسخة التجريبية؟",
                ["TrialUnlockConfirmMessage"] = "هذا يستبدل ملف نظام ويندوز (Windows.ApplicationModel.Store.dll) بنسخة معدلة عشان نسخ ماين كرافت غير GDK تقدر تشغل النسخة التجريبية.\n\n⚠️ ينحفظ نسخة احتياطية بـ C:\\Program Files\\MCBypass\\backup. مضاد الفيروسات ممكن يكشفه. تكمل؟",
                ["TrialUnlockSuccess"] = "انفك قفل النسخة التجريبية!",
                ["TrialUnlockSuccessMessage"] = "انفك قفل النسخة التجريبية لـ {0} وكل النسخ غير GDK.\n\nشغل ماين كرافت من جديد عشان تلعب.",
                ["TrialAlreadyUnlocked"] = "القفل مفكوك أصلاً",
                ["TrialAlreadyUnlockedMessage"] = "النسخة التجريبية مفكوكة أصلاً على هالجهاز.",
                ["TrialUnlockFailedMessage"] = "ما قدرنا نفك قفل النسخة التجريبية. جرب تشغيل اللانشر كـ مدير (administrator).",
            }
        };
        
        public static void SetLanguage(string languageCode)
        {
            _currentLanguage = languageCode;
        }
        public static string GetCurrentLanguage()
        {
            return _currentLanguage;
        }
        
        public static string Get(string key)
        {
            if (_translations.ContainsKey(_currentLanguage) && _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }
            
            // Fallback to English
            if (_translations["en"].ContainsKey(key))
            {
                return _translations["en"][key];
            }
            
            return key; // Return key if translation not found
        }
        
        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            return string.Format(template, args);
        }
    }
}
