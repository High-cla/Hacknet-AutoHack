# API 索引

> 自动生成，勿手改；重新生成：`node tools/api-index.ts`
>
> 数据源为 `decompiled/` 反编译源码。游戏与框架均**不带 XML 文档**，
> 故本索引只到**签名级**（类型 / 成员 / 所在 `文件:行号`），没有作者说明文字；
> 需要实现语义时按行号直接读 `decompiled/`。

## 怎么用

- 找符号：`rg 'SearchGraph' docs/API.md`
- 列全部类型：`rg '^`.*\((class|struct|interface|enum|delegate)\)' docs/API.md`
- 列全部 public 成员：`rg '^    public' docs/API.md`
- 定位实现：命中行的 `文件:行号` 即 `decompiled/` 下的真实位置，可直接打开

## 覆盖

| 程序集 | public 类型 | public 成员 |
| --- | ---: | ---: |
| Hacknet（游戏本体） | 410 | 5004 |
| PathfinderAPI（框架） | 116 | 511 |

---
## Hacknet（游戏本体）

### (global)

`(global).TextureUpdatedDelegate` (delegate) — decompiled/game-proj/XNAWebRenderer.cs:6

`(global).XNAWebRenderer` (class) — decompiled/game-proj/XNAWebRenderer.cs:4
    public const string nativeLibName = "XNAWebRenderer.dll";
    public static extern void XNAWR_Initialize([MarshalAs(UnmanagedType.LPStr)] string initialURL, TextureUpdatedDelegate callback, int width, int height);
    public static extern void XNAWR_Shutdown();
    public static extern void XNAWR_Update();
    public static extern void XNAWR_LoadURL([MarshalAs(UnmanagedType.LPStr)] string URL);
    public static extern void XNAWR_SetViewport(int width, int height);

### Hacknet

`Hacknet.AcademicDatabaseDaemon` (class) — decompiled/game-proj/Hacknet/AcademicDatabaseDaemon.cs:10
    public const string ROOT_FOLDERNAME = "academic_data";
    public const string ENTRIES_FOLDERNAME = "entry_cache";
    public const string CONFIG_FILENAME = "config.sys";
    public const string INFO_FILENAME = "info.txt";
    public const float SEARCH_TIME = 3.6f;
    public const float MULTI_MATCH_SEARCH_TIME = 0.7f;
    public const string DEGREE_SPLIT_DELIM = "--------------------";
    public ADDState state = ADDState.Welcome;
    public Folder root;
    public Folder entries;
    public Color themeColor;
    public Color backThemeColor;
    public Color darkThemeColor;
    public Texture2D loadingCircle;
    public string searchedName;
    public string foundFileName;
    public float searchStartTime = 0f;
    public List<Degree> searchedDegrees;
    public List<string> searchResultsNames;
    public string infoText;
    public bool needsDeletionConfirmation = false;
    public int editedIndex = 0;
    public ADDEditField editedField;
    public List<Vector2> backBars;
    public List<Vector2> topBars;
    public AcademicDatabaseDaemon(Computer c, string serviceName, OS os)
    public void init()
    public override void initFiles()
    public override void loadInit()
    public void initFilesFromPeople(List<Person> people = null)
    public void addFileForPerson(Person p)
    public FileEntry getFileForPerson(Person p)
    public string convertNameToFileNameStart(string name)
    public FileEntry findFileForName(string name)
    public void setDegreesFromFileEntryData(string file)
    public bool doesDegreeExist(string owner_name, string degree_name, string uni_name, float gpaMin)
    public bool hasDegrees(string owner_name)
    public void doPreEntryViewSearch()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void drawSearchState(Rectangle bounds, SpriteBatch sb)
    public void drawMultipleEntriesState(Rectangle bounds, SpriteBatch sb)
    public void drawEntryState(Rectangle bounds, SpriteBatch sb)
    public void drawEditDegreeState(Rectangle bounds, SpriteBatch sb)
    public bool doEditField()
    public void setEditedFieldValue(string value)
    public void drawTitle(Rectangle bounds, SpriteBatch sb)
    public void drawSideBar(Rectangle bounds, SpriteBatch sb)
    public void updateSideBar()
    public void saveChangesToEntry()
    public override void navigatedTo()
    public override void userAdded(string name, string pass, byte type)
    public override string getSaveString()

`Hacknet.AcademicDatabaseDaemon.ADDEditField` (enum) — decompiled/game-proj/Hacknet/AcademicDatabaseDaemon.cs:12

`Hacknet.AcademicDatabaseDaemon.ADDState` (enum) — decompiled/game-proj/Hacknet/AcademicDatabaseDaemon.cs:20

`Hacknet.Account` (class) — decompiled/game-proj/Hacknet/Account.cs:3
    public string ID;
    public string Cash;
    public string Bank;
    public string Apartments;
    public string Vehicles;
    public string PegasusVehicles;
    public string Rank;
    public string RP;
    public string Kills;
    public override string ToString()

`Hacknet.AchievementsManager` (class) — decompiled/game-proj/Hacknet/AchievementsManager.cs:5
    public static bool Unlock(string name, bool recordAndCheckFlag = false)

`Hacknet.ActionDelayer` (class) — decompiled/game-proj/Hacknet/ActionDelayer.cs:6
    public List<Pair> pairs = new List<Pair>();
    public List<Pair> nextPairs = new List<Pair>();
    public DateTime Time { get; set; }
    public void Pump()
    public void RunAllDelayedActions()
    public void Post(Condition condition, Action action)
    public void PostAnimation(IEnumerator<Condition> animation)
    public static Condition WaitUntil(DateTime time)
    public static Condition Wait(double time)
    public static Condition NextTick()
    public static Condition FileDeleted(Folder f, string filename)

`Hacknet.ActionDelayer.Pair` (struct) — decompiled/game-proj/Hacknet/ActionDelayer.cs:10
    public Condition Condition;
    public Action Action;

`Hacknet.ActiveMission` (class) — decompiled/game-proj/Hacknet/ActiveMission.cs:9
    public List<MisisonGoal> goals;
    public string[] delims = new string[1] { "#%#" };
    public string nextMission;
    public bool activeCheck = false;
    public MailServer.EMailData email;
    public bool hasFinished = false;
    public int endFunctionValue;
    public string endFunctionName;
    public int startFunctionValue;
    public string startFunctionName;
    public string postingTitle;
    public string postingBody;
    public string[] postingAcceptFlagRequirements = null;
    public int requiredRank = 0;
    public int difficulty = 0;
    public bool ShouldIgnoreSenderVerification = false;
    public string reloadGoalsSourceFile;
    public bool wasAutoGenerated = false;
    public string genTarget;
    public string genPath;
    public string genFile;
    public string genTargetName;
    public string genOther;
    public bool willSendEmail = true;
    public string client;
    public string target;
    public Dictionary<string, string> generationKeys;
    public ActiveMission(List<MisisonGoal> _goals, string next, MailServer.EMailData _email)
    public void Update(float t)
    public string getSaveString()
    public static object load(XmlReader reader)
    public void addEndFunction(int val, string name)
    public void addStartFunction(int val, string name)
    public void ActivateSuppressedStartFunctionIfPresent()
    public bool isComplete(List<string> additionalDetails = null)
    public void finish()
    public void sendEmail(OS os)

`Hacknet.AddEmailDaemon` (class) — decompiled/game-proj/Hacknet/AddEmailDaemon.cs:11
    public const string SEND_SCRIPT_URL = "http://www.tijital-games.com/hacknet/SendVictoryEmail.php?mail=[EMAIL]";
    public const int WAITING = 0;
    public const int ENTERING = 1;
    public const int CONFIRM = 2;
    public const int ERROR = 3;
    public static string lastSentEmail;
    public int state;
    public string email;
    public AddEmailDaemon(Computer computer, string serviceName, OS opSystem)
    public void saveEmail()
    public void sendEmail()
    public override string getSaveString()
    public void makeWebRequest()
    public override void draw(Rectangle bounds, SpriteBatch sb)

`Hacknet.Administrator` (class) — decompiled/game-proj/Hacknet/Administrator.cs:3
    public bool ResetsPassword = false;
    public bool IsSuper = false;
    public virtual void disconnectionDetected(Computer c, OS os)
    public virtual void traceEjectionDetected(Computer c, OS os)

`Hacknet.AdvancedTutorial` (class) — decompiled/game-proj/Hacknet/AdvancedTutorial.cs:12
    public static List<string> commandSequence;
    public static List<string[]> altCommandequence;
    public static List<string> feedbackSequence;
    public static string loadedLocale = "en-us";
    public int state;
    public string lastCommand;
    public string[] renderText;
    public string[] hintButtonText = null;
    public float hintTextFadeTimer = 0f;
    public string[] hintButtonDelimiter = new string[1] { "|" };
    public float flashTimer;
    public SoundEffect startSound;
    public SoundEffect advanceFlash;
    public List<Action> stepCompletionSequence;
    public NodeBounceEffect bounceEffect;
    public bool CanActivateFirstStep = true;
    public AdvancedTutorial(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public void parseCommand()
    public void advanceState()
    public void printCurrentCommandToTerminal()
    public void getRenderText()
    public override void Killed()
    public override void Draw(float t)
    public Vector2 RenderText(string[] stringData, Vector2 dpos, float charHeight, float opacityMod = 1f)
    public Vector2 RenderTextOld(string[] stringData, Vector2 dpos, float charHeight, float opacityMod = 1f)

`Hacknet.AgentDetails` (class) — decompiled/game-proj/Hacknet/AgentDetails.cs:3
    public string Codename;
    public string RealName;
    public string IP;
    public string SpecialNotes;
    public override string ToString()

`Hacknet.AircraftDaemon` (class) — decompiled/game-proj/Hacknet/AircraftDaemon.cs:10
    public const float FlightHoursPerLengthUnit = 0.06855416f;
    public const float ReloadFirmwareTime = 6f;
    public const string CriticalFilename = "747FlightOps.dll";
    public const float RoughTotalFallTimeSeconds = 135f;
    public const float StartingAltitude = 38000f;
    public Texture2D WorldMap;
    public Texture2D Circle;
    public Texture2D Plane;
    public Texture2D CautionIcon;
    public Texture2D StatusOKIcon;
    public Texture2D CircleOutline;
    public Vector2 mapOrigin;
    public Vector2 mapDest;
    public float FlightProgress;
    public double CurrentAltitude = 37900.0;
    public float currentAirspeed = 460f;
    public float rateOfClimb = 0.073f;
    public Color ThemeColor = Color.CornflowerBlue;
    public Folder MainFolder;
    public bool PilotAlerted = false;
    public bool IsReloadingFirmware = false;
    public float firmwareReloadProgress = 0f;
    public float timeFallingFor = 0f;
    public float timeSinceLastDataUpdate = 0f;
    public bool IsSubscribedForUpdates = false;
    public bool IsInCriticalFirmwareFailure = false;
    public bool AircraftFallStartsImmediatley = true;
    public Action CrashAction;
    public AircraftDaemon(Computer c, OS os, string name, Vector2 mapOrigin, Vector2 mapDest, float progress)
    public override void initFiles()
    public override void loadInit()
    public override void navigatedTo()
    public void StartUpdating()
    public void UnsubscribeFromUpdates()
    public void Update(float t)
    public void CrashAircraft()
    public void StartReloadFirmware()
    public void FinishReloadingFirmware()
    public bool IsInCriticalDescent()
    public override string getSaveString()
    public string VSString(string name, string result)
    public string VSString(string name, float result)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawHeadings(Rectangle bounds, SpriteBatch sb)
    public void DrawFieldDisplay(Rectangle dest, SpriteBatch sb, string title, string value, byte status)
    public void DrawMap(Rectangle dest, SpriteBatch sb)

`Hacknet.AlignmentX` (enum) — decompiled/game-proj/Hacknet/AlignmentX.cs:3

`Hacknet.AlignmentY` (enum) — decompiled/game-proj/Hacknet/AlignmentY.cs:3

`Hacknet.AuthenticatingDaemon` (class) — decompiled/game-proj/Hacknet/AuthenticatingDaemon.cs:9
    public UserDetail user;
    public AuthenticatingDaemon(Computer computer, string serviceName, OS opSystem)
    public virtual void loginGoBack()
    public virtual void userLoggedIn()
    public void startLogin()
    public void doLoginDisplay(Rectangle bounds, SpriteBatch sb)
    public new static bool validUser(byte type)
    public void forceLogin(string username, string pass)

`Hacknet.AvconDemoEndDaemon` (class) — decompiled/game-proj/Hacknet/AvconDemoEndDaemon.cs:7
    public bool confirmed = false;
    public AvconDemoEndDaemon(Computer c, string name, OS os)
    public override void navigatedTo()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()
    public void endDemo()

`Hacknet.BasicAdministrator` (class) — decompiled/game-proj/Hacknet/BasicAdministrator.cs:3
    public override void disconnectionDetected(Computer c, OS os)

`Hacknet.BoatMail` (class) — decompiled/game-proj/Hacknet/BoatMail.cs:7
    public Texture2D logo;
    public static string JunkEmail = "HOr$e Exp@nding R0Lexxxx corp\n\nHello Sir/Madam,\nI am but a humble nigerian prince who is crippled by the instability in my country, and require a transfer of 5000 united states dollar in order to rid my country of the scourge of the musclebeasts which roam our plains and ravage our villages\nPlease send these funds to real_nigerian_prince_the_third@boatmail.com\nYours in jegus,\nNigerian Prince";
    public BoatMail(Computer c, string name, OS os)
    public override void drawBackingGradient(Rectangle boundsTo, SpriteBatch sb)
    public override void doInboxHeader(Rectangle bounds, SpriteBatch sb)
    public override void drawTopBar(Rectangle bounds, SpriteBatch sb)

`Hacknet.BootCrashAssistanceModule` (class) — decompiled/game-proj/Hacknet/BootCrashAssistanceModule.cs:10
    public const float TimePerBlock = 9.2f;
    public const float TimeSubForEarlyLines = -8.3f;
    public const int NumberOfFastBlocks = 11;
    public bool IsActive = false;
    public float elapsedTime = 0f;
    public List<string> SequenceBlocks = new List<string>();
    public int blocksComplete = 0;
    public bool AwaitingInput = false;
    public bool ShouldSkipDialogueTypeout = false;
    public BootCrashAssistanceModule(Rectangle location, OS operatingSystem)
    public override void Update(float t)
    public override void Draw(float t)
    public void DrawMonospace(string text, SpriteFont font, Vector2 pos, Color c, float charWidth)

`Hacknet.CAROData` (class) — decompiled/game-proj/Hacknet/CAROData.cs:3
    public string UserID;
    public string Headshots;
    public string Kills;
    public string Rank;
    public string Crowbars;
    public string InventoryID;
    public string BanStatus;
    public override string ToString()

`Hacknet.Clock2Exe` (class) — decompiled/game-proj/Hacknet/Clock2Exe.cs:8
    public Texture2D triangle;
    public Texture2D arc;
    public Texture2D arcThin;
    public Rectangle arcClip;
    public Rectangle arcClipSmaller;
    public bool isLargeMode = false;
    public Clock2Exe(Rectangle location, OS operatingSystem, string[] p)
    public override void Draw(float t)
    public void DrawRadialPointer(float rotationPercent, Rectangle fullArea, float radius, Color c, bool small = false)

`Hacknet.ClockExe` (class) — decompiled/game-proj/Hacknet/ClockExe.cs:7
    public ClockExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Draw(float t)

`Hacknet.Computer` (class) — decompiled/game-proj/Hacknet/Computer.cs:11
    public const byte CORPORATE = 1;
    public const byte HOME = 2;
    public const byte SERVER = 3;
    public const byte EMPTY = 4;
    public const byte EOS = 5;
    public static float BASE_BOOT_TIME = (Settings.isConventionDemo ? 15f : 25.5f);
    public static float BASE_REBOOT_TIME = 10.5f;
    public static float BASE_PROXY_TICKS = 30f;
    public static float BASE_TRACE_TIME = 15f;
    public string name;
    public string idName;
    public string ip;
    public Vector2 location;
    public FileSystem files;
    public int securityLevel;
    public float traceTime;
    public int portsNeededForCrack = 0;
    public string adminIP;
    public List<UserDetail> users;
    public List<int> links;
    public List<int> ports;
    public List<byte> portsOpen;
    public bool silent = false;
    public bool disabled = false;
    public float bootTimer = 0f;
    public bool userLoggedIn = false;
    public UserDetail currentUser;
    public Dictionary<int, int> PortRemapping = null;
    public string icon = null;
    public float timeLastPinged;
    public float highlightFlashTime = 0f;
    public byte type;
    public string adminPass;
    public List<Daemon> daemons;
    public bool AllowsDefaultBootModule = true;
    public bool hasProxy = false;
    public float proxyOverloadTicks = 0f;
    public float startingOverloadTicks = -1f;
    public bool proxyActive = false;
    public ShellExe reportingShell = null;
    public ExternalCounterpart externalCounterpart = null;
    public string attatchedDeviceIDs = null;
    public Firewall firewall = null;
    public bool firewallAnalysisInProgress = false;
    public bool HasTracker = false;
    public Administrator admin = null;
    public OS os;
    public MemoryContents Memory;
    public Computer(string compName, string compIP, Vector2 compLocation, int seclevel, byte compType, OS opSystem)
    public void initDaemons()
    public FileSystem generateRandomFileSystem()
    public void openPortsForSecurityLevel(int security)
    public void openPorts(int n)
    public void addProxy(float time)
    public void addFirewall(int level)
    public void addFirewall(int level, string solution)
    public void addFirewall(int level, string solution, float additionalTime)
    public void addMultiplayerTargetFile()
    public void sendNetworkMessage(string s)
    public void tryExternalCounterpartDisconnect()
    public void hostileActionTaken()
    public void bootupTick(float t)
    public void log(string message)
    public string generateFolderName(int seed)
    public string generateFileName(int seed)
    public string generateFileData(int seed)
    public bool connect(string ipFrom)
    public void addNewUser(string ipFrom, string name, string pass, byte type)
    public void addNewUser(string ipFrom, UserDetail usr)
    public void crash(string ipFrom)
    public void reboot(string ipFrom)
    public bool canReadFile(string ipFrom, FileEntry f, int index)
    public bool canCopyFile(string ipFrom, string name)
    public bool deleteFile(string ipFrom, string name, List<int> folderPath)
    public bool moveFile(string ipFrom, string name, string newName, List<int> folderPath, List<int> destFolderPath)
    public bool makeFile(string ipFrom, string name, string data, List<int> folderPath, bool isUpload = false)
    public bool makeFolder(string ipFrom, string name, List<int> folderPath)
    public void disconnecting(string ipFrom, bool externalDisconnectToo = true)
    public void giveAdmin(string ipFrom)
    public void openPort(int portNum, string ipFrom)
    public void closePort(int portNum, string ipFrom)
    public bool isPortOpen(int portNum)
    public void openCDTray(string ipFrom)
    public void closeCDTray(string ipFrom)
    public void forkBombClients(string ipFrom)
    public virtual int login(string username, string password, byte type = 1)
    public int GetDisplayPortNumberFromCodePort(int codePort)
    public int GetCodePortNumberFromDisplayPort(int displayPort)
    public void setAdminPassword(string newPass)
    public string getSaveString()
    public static Computer load(XmlReader reader, OS os)
    public string getTooltipString()
    public Vector2 getScreenSpacePosition()
    public Daemon getDaemon(Type t)
    public static string generateBinaryString(int length)
    public static string generateBinaryString(int length, MSRandom rng)
    public static Folder getFolderAtDepth(Computer c, int depth, List<int> path)
    public override string ToString()
    public static Computer loadFromFile(string filename)
    public Folder getFolderFromPath(string path, bool createFoldersThatDontExist = false)
    public List<int> getFolderPath(string path, bool createFoldersThatDontExist = false)
    public bool PlayerHasAdminPermissions()

`Hacknet.ComputerLoader` (class) — decompiled/game-proj/Hacknet/ComputerLoader.cs:13
    public static Action MissionPreLoadComplete;
    public static Action postAllLoadedActions;
    public static OS os;
    public static void init(object opsys)
    public static object loadComputer(string filename, bool preventAddingToNetmap = false, bool preventInitDaemons = false)
    public static void DLCCheck(string name)
    public static void loadPortsIntoComputer(string portsList, object computer_obj)
    public static object readMission(string filename)
    public static void loadMission(string filename, bool PreventEmail = false)
    public static string filter(string s)
    public static Computer findComp(string target)
    public static object findComputer(string target)

`Hacknet.ComputerTypeInfo` (class) — decompiled/game-proj/Hacknet/ComputerTypeInfo.cs:3
    public static string getDefaultBootDaemonFilename(object c)

`Hacknet.Condition` (delegate) — decompiled/game-proj/Hacknet/ActionDelayer.cs:8

`Hacknet.ConnectedNodeEffect` (class) — decompiled/game-proj/Hacknet/ConnectedNodeEffect.cs:8
    public const int NUMBER_OF_SEGMENTS = 7;
    public const float MIN_DISTANCE = 18f;
    public const float MAX_DISTANCE = 30f;
    public OS os;
    public static List<Texture2D> textures;
    public Color color;
    public bool Intense = false;
    public float ScaleFactor = 1f;
    public int[] tex;
    public float[] distance;
    public float[] offset;
    public float[] timescale;
    public Vector2 origin;
    public ConnectedNodeEffect(OS os)
    public ConnectedNodeEffect(OS os, bool intense)
    public void init(bool intesne = false)
    public void reset()
    public void draw(SpriteBatch sb, Vector2 pos)

`Hacknet.CoreModule` (class) — decompiled/game-proj/Hacknet/CoreModule.cs:6
    public static Texture2D LockSprite;
    public bool inputLocked = false;
    public bool guiInputLockStatus = false;
    public override void LoadContent()
    public override void PreDrawStep()
    public override void PostDrawStep()

`Hacknet.Corporation` (class) — decompiled/game-proj/Hacknet/Corporation.cs:7
    public static float COMPUTER_SEPERATION = 0.066f;
    public static float COMPUTER_SEPERATION_ADD_PER_CYCLE = 0.04f;
    public static float Y_ASPECT_RATIO_BIAS = 1.9f;
    public static List<Vector2> TestedPositions = new List<Vector2>();
    public OS os;
    public List<Computer> servers;
    public string name;
    public string postfix;
    public string ipSubstring;
    public string baseID;
    public Computer mainframe;
    public Computer mailServer;
    public Computer webServer;
    public Computer internalServices;
    public Computer fileServer;
    public Computer backupServer;
    public Vector2 basePosition;
    public int baseSecurityLevel;
    public int serverCount;
    public bool altRotation;
    public Corporation(OS _os)
    public void generate()
    public void generateName()
    public void generateServers()
    public void generateMainframe()
    public void generateMailServer()
    public void generateWebServer()
    public void generateInternalServices()
    public void generateFileServer()
    public void generateBackupMachine()
    public void linkServers()
    public void addServersToInternet()
    public string getAddress()
    public Vector2 getLocation()
    public static Vector2 GetOffsetPositionFromCycle(int pos, int total, float ExtraDistance = 0f)
    public static bool GeneratedPositionIsValid(Vector2 position, NetworkMap netMap, bool ignoreNetmap = false)
    public static Vector2 getNearbyNodeOffset(Vector2 basePos, int positionNumber, int total, NetworkMap map, float extraDistance = 0f, bool forceUseThisPosition = false)
    public static Vector2 getNearbyNodeOffsetOld(Vector2 basePos, int positionNumber, int total, NetworkMap map, float ExtraSeperationDistance = 0f)
    public bool locationCollides(Vector2 loc)
    public string getName()
    public string getFullName()

`Hacknet.CrashModule` (class) — decompiled/game-proj/Hacknet/CrashModule.cs:9
    public const float BOOT_FAIL_CRASH_TIME = 15f;
    public static float BLUESCREEN_TIME = 8f;
    public static float BOOT_TIME = (Settings.isConventionDemo ? 5f : (Settings.FastBootText ? 1.2f : 14.5f));
    public static float BLACK_TIME = 2f;
    public static float POST_BLACK_TIME = 1f;
    public string BootLoadErrors = "";
    public float elapsedTime = 0f;
    public int state = 0;
    public Color bluescreenBlue = new Color(0, 0, 170);
    public Color bluescreenGrey = new Color(167, 167, 167);
    public Color textColor = new Color(0, 0, 255);
    public SpriteFont bsodFont;
    public static SoundEffect beep;
    public string bsodText = "";
    public string originalBootText;
    public string[] bootText;
    public int bootTextCount = 0;
    public float bootTextDelay = 1f;
    public float bootTextTimer = 0f;
    public float bootTextErrorDelay = 0f;
    public bool graphicsErrorsDetected = false;
    public bool hasPlayedBeep = false;
    public bool IsInHostileFileCrash = false;
    public int extraErrors = 0;
    public CrashModule(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public void loadBootText()
    public override void Update(float t)
    public override void Draw(float t)
    public Vector2 drawString(string text, Vector2 dpos, SpriteFont font)
    public string checkOSBootFiles(string bootString)
    public void reset()
    public void completeReboot()

`Hacknet.CustomConnectDisplayDaemon` (class) — decompiled/game-proj/Hacknet/CustomConnectDisplayDaemon.cs:9
    public MovingBarsEffect topEffect;
    public MovingBarsEffect botEffect;
    public bool HasBeenAdminBefore = false;
    public float timeInThisState = 0f;
    public CustomConnectDisplayDaemon(Computer c, OS os)
    public CustomConnectDisplayDaemon(Computer c, string name, OS os)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public virtual void DrawAdminDisplay(Rectangle bounds, SpriteBatch sb, Computer c)
    public virtual void DrawConnectButtons(Rectangle bounds, SpriteBatch sb, Computer c, int margin, int y, AlignmentX ButtonAlignment = AlignmentX.Middle)
    public virtual void DrawCautionLinedMessage(Rectangle dest, int stripHeight, Color color, string Message, SpriteBatch sb, Texture2D stripTexture = null, int textOffsetY = 0)
    public virtual void DrawNonAdminDisplay(Rectangle dest, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.CustomConnectDisplayOverride` (class) — decompiled/game-proj/Hacknet/CustomConnectDisplayOverride.cs:3
    public CustomConnectDisplayOverride(Computer c, string name, OS os)

`Hacknet.CustomTheme` (class) — decompiled/game-proj/Hacknet/CustomTheme.cs:7
    public Color defaultHighlightColor = new Color(0, 139, 199, 255);
    public Color defaultTopBarColor = new Color(130, 65, 27);
    public Color warningColor = Color.Red;
    public Color subtleTextColor = new Color(90, 90, 90);
    public Color darkBackgroundColor = new Color(8, 8, 8);
    public Color indentBackgroundColor = new Color(12, 12, 12);
    public Color outlineColor = new Color(68, 68, 68);
    public Color lockedColor = new Color(65, 16, 16, 200);
    public Color brightLockedColor = new Color(160, 0, 0);
    public Color brightUnlockedColor = new Color(0, 160, 0);
    public Color unlockedColor = new Color(39, 65, 36);
    public Color lightGray = new Color(180, 180, 180);
    public Color shellColor = new Color(222, 201, 24);
    public Color shellButtonColor = new Color(105, 167, 188);
    public Color moduleColorSolidDefault = new Color(50, 59, 90, 255);
    public Color moduleColorStrong = new Color(14, 28, 40, 80);
    public Color moduleColorBacking = new Color(5, 6, 7, 10);
    public Color semiTransText = new Color(120, 120, 120, 0);
    public Color terminalTextColor = new Color(213, 245, 255);
    public Color topBarTextColor = new Color(126, 126, 126, 100);
    public Color superLightWhite = new Color(2, 2, 2, 30);
    public Color connectedNodeHighlight = new Color(222, 0, 0, 195);
    public Color exeModuleTopBar = new Color(130, 65, 27, 80);
    public Color exeModuleTitleText = new Color(155, 85, 37, 0);
    public Color netmapToolTipColor = new Color(213, 245, 255, 0);
    public Color netmapToolTipBackground = new Color(0, 0, 0, 70);
    public Color topBarIconsColor = Color.White;
    public Color AFX_KeyboardMiddle = new Color(0, 120, 255);
    public Color AFX_KeyboardOuter = new Color(255, 150, 0);
    public Color AFX_WordLogo = new Color(0, 120, 255);
    public Color AFX_Other = new Color(0, 100, 255);
    public Color thisComputerNode = new Color(95, 220, 83);
    public Color scanlinesColor = new Color(255, 255, 255, 15);
    public string themeLayoutName = null;
    public string backgroundImagePath = null;
    public Color BackgroundImageFillColor = Color.Black;
    public bool UseAspectPreserveBackgroundScaling = false;
    public static CustomTheme Deserialize(string filepath)
    public string GetSaveString()
    public void LoadIntoOS(object os_obj)
    public OSTheme GetThemeForLayout()

`Hacknet.Daemon` (class) — decompiled/game-proj/Hacknet/Daemon.cs:6
    public string name;
    public bool isListed;
    public Computer comp;
    public OS os;
    public Daemon(Computer computer, string serviceName, OS opSystem)
    public virtual void initFiles()
    public virtual void draw(Rectangle bounds, SpriteBatch sb)
    public virtual void navigatedTo()
    public virtual void userAdded(string name, string pass, byte type)
    public virtual string getSaveString()
    public virtual void loadInit()
    public static bool validUser(byte type)
    public void registerAsDefaultBootDaemon()

`Hacknet.DatabaseDaemon` (class) — decompiled/game-proj/Hacknet/DatabaseDaemon.cs:11
    public MovingBarsEffect sideEffect;
    public DatabasePermissions Permissions;
    public DatabaseState State;
    public Folder DatasetFolder;
    public Type DataType;
    public string DataTypeIdentifier;
    public bool FilenameIsPersonName = false;
    public FileEntry ActiveFile = null;
    public string errorMessage = "None";
    public object DeserializedFile = null;
    public string Foldername;
    public List<object> Dataset = null;
    public Color ThemeColor;
    public bool HadThemeColorApplied = true;
    public Vector2 BlockStartTopLeft;
    public float blockSize;
    public int blocksWide;
    public int blocksHigh;
    public bool HasSpecialCaseDraw = false;
    public Texture2D PlaceholderSprite;
    public Texture2D Triangle;
    public Dictionary<object, Texture2D> WildcardAssets = new Dictionary<object, Texture2D>();
    public ScrollableSectionedPanel ScrollPanel;
    public ScrollableTextRegion TextRegion;
    public string passwordResetHelperString = null;
    public string adminResetEmailHostID = null;
    public string adminResetPassEmailAccount = null;
    public bool AdminEmailResetStarted = false;
    public bool AdminEmailResetHasHappened = false;
    public string passwordResetMessage = "";
    public static DatabasePermissions GetDatabasePermissionsFromString(string data)
    public DatabaseDaemon(Computer c, OS os, string name, DatabasePermissions permissions, string DataTypeIdentifier, string Foldername = null, Color? ThemeColor = null)
    public DatabaseDaemon(Computer c, OS os, string name, string permissions, string DataTypeIdentifier, string Foldername = null, Color? ThemeColor = null)
    public void Init(Computer c, OS os, string name, DatabasePermissions permissions, string DataTypeIdentifier, string Foldername, Color themeColor)
    public override void initFiles()
    public override void loadInit()
    public void InitDataset()
    public override void navigatedTo()
    public object GetObjectForRecordName(string recordName)
    public void ResetAccessPassword()
    public string CleanXMLForFile(string data)
    public string DeCleanXMLForFile(string data)
    public string GetFilenameForPersonName(string firstname, string lastname)
    public string GetFilenameForObject(object obj)
    public override string getSaveString()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawWelcome(Rectangle dest, SpriteBatch sb)
    public void DrawError(Rectangle dest, SpriteBatch sb)
    public void DrawBrowse(Rectangle dest, SpriteBatch spriteBatch)
    public void DrawEntry(Rectangle dest, SpriteBatch spriteBatch)
    public string GetAnnounceNameForFileName(string filename)
    public Rectangle GetDrawRectForRow(int row, int inset)
    public void DrawBackground(Rectangle dest, SpriteBatch sb, int desiredNumOfBlocks)
    public void DrawSpecialCase(Vector2 currentPos, Rectangle totalArea, Type objType, string drawnValue, SpriteBatch sb)

`Hacknet.DatabaseDaemon.DatabasePermissions` (enum) — decompiled/game-proj/Hacknet/DatabaseDaemon.cs:13

`Hacknet.DatabaseDaemon.DatabaseState` (enum) — decompiled/game-proj/Hacknet/DatabaseDaemon.cs:19

`Hacknet.DeathRowDatabaseDaemon` (class) — decompiled/game-proj/Hacknet/DeathRowDatabaseDaemon.cs:8
    public const string ROOT_FOLDERNAME = "dr_database";
    public const string RECORDS_FOLDERNAME = "records";
    public const string SERVER_INFO_FILENAME = "ServerDetails.txt";
    public static Texture2D Logo;
    public static Texture2D Circle;
    public Folder root;
    public Folder records;
    public int SelectedIndex = -1;
    public Color themeColor = new Color(207, 44, 19);
    public Vector2 recordScrollPosition = Vector2.Zero;
    public DeathRowDatabaseDaemon(Computer c, string serviceName, OS os)
    public override void initFiles()
    public void LoadRecords(string data = null)
    public DeathRowEntry ConvertStringToRecord(string data)
    public override void loadInit()
    public override string getSaveString()
    public string ConvertFilesToOutput()
    public void TestStringConversion()
    public bool ContainsRecordForName(string fName, string lName)
    public DeathRowEntry GetRecordForName(string fName, string lName)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawTitleScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawRecord(Rectangle bounds, SpriteBatch sb, DeathRowEntry entry)
    public Vector2 DrawCompactLabel(string label, string value, Vector2 drawPos, int margin, int seperatorHeight, int textWidth)

`Hacknet.DeathRowDatabaseDaemon.DeathRowEntry` (struct) — decompiled/game-proj/Hacknet/DeathRowDatabaseDaemon.cs:10
    public string FName;
    public string LName;
    public string RecordNumber;
    public string Age;
    public string Date;
    public string Country;
    public string PriorRecord;
    public string IncidentReport;
    public string Statement;

`Hacknet.DebugLog` (class) — decompiled/game-proj/Hacknet/DebugLog.cs:5
    public static char[] delimiters = new char[2] { '\n', '\r' };
    public static List<string> data = new List<string>(64);
    public static void add(string s)
    public static string GetDump()

`Hacknet.DecypherExe` (class) — decompiled/game-proj/Hacknet/DecypherExe.cs:7
    public const float LOADING_TIME = 3.5f;
    public const float WORKING_TIME = 10f;
    public const float COMPLETE_TIME = 3f;
    public const float ERROR_TIME = 6f;
    public Computer targetComputer;
    public Folder destFolder;
    public FileEntry targetFile;
    public string targetFilename;
    public string destFilename;
    public string password = "";
    public DecypherStatus status = DecypherStatus.Loading;
    public float timeOnThisPhase = 0f;
    public float percentComplete = 0f;
    public string errorMessage = "Unknown Error";
    public string displayHeader = "Unknown";
    public string displayIP = "Unknown";
    public string writtenFilename = "Unknown";
    public List<int> rowsActive = new List<int>();
    public List<int> columnsActive = new List<int>();
    public float lastLockedPercentage = 0f;
    public int rowsDrawn = 10;
    public int columnsDrawn = 10;
    public int lcgSeed = 1;
    public DecypherExe(Rectangle location, OS operatingSystem, string[] p)
    public void InitializeFiles(string filename)
    public override void Update(float t)
    public bool CompleteLoading()
    public void CompleteWorking()
    public Rectangle DrawLoadingMessage(string message, float startPoint, Rectangle dest, bool showLoading = true)
    public override void Draw(float t)

`Hacknet.DecypherExe.DecypherStatus` (enum) — decompiled/game-proj/Hacknet/DecypherExe.cs:9

`Hacknet.DecypherTrackExe` (class) — decompiled/game-proj/Hacknet/DecypherTrackExe.cs:6
    public const float LOADING_TIME = 3.5f;
    public const float COMPLETE_TIME = 10f;
    public const float ERROR_TIME = 6f;
    public Computer targetComputer;
    public Folder destFolder;
    public FileEntry targetFile;
    public string targetFilename;
    public string destFilename;
    public DecHeadStatus status = DecHeadStatus.Loading;
    public float timeOnThisPhase = 0f;
    public float percentComplete = 0f;
    public string errorMessage = "Unknown Error";
    public string displayHeader = "Unknown";
    public string displayIP = "Unknown";
    public static Color LoadingBarColorRed = new Color(196, 29, 60, 80);
    public static Color LoadingBarColorBlue = new Color(29, 113, 196, 80);
    public DecypherTrackExe(Rectangle location, OS operatingSystem, string[] p)
    public void InitializeFiles(string filename)
    public override void Update(float t)
    public bool CompleteLoading()
    public void GetHeaders()
    public Rectangle DrawLoadingMessage(string message, float startPoint, Rectangle dest, bool showLoading = true, bool highlight = false)
    public override void Draw(float t)

`Hacknet.DecypherTrackExe.DecHeadStatus` (enum) — decompiled/game-proj/Hacknet/DecypherTrackExe.cs:8

`Hacknet.Degree` (class) — decompiled/game-proj/Hacknet/Degree.cs:3
    public string name;
    public string uni;
    public float GPA;
    public Degree()
    public Degree(string degreeName, string uniName, float degreeGPA)
    public override string ToString()

`Hacknet.DelayableActionSystem` (class) — decompiled/game-proj/Hacknet/DelayableActionSystem.cs:9
    public static string EncryptionPass = "dasencrypt";
    public Folder folder;
    public DelayableActionSystem(Folder sourceFolder, object osObj)
    public DelayableActionSystem()
    public void Update(float t, object osObj)
    public virtual void InstantlyResolveAllActions(object osObj)
    public virtual void AddAction(SerializableAction action, float delay)
    public string GetNextSeqNumber()
    public static DelayableActionSystem FindDelayableActionSystemOnComputer(Computer c)

`Hacknet.DelayedInput` (struct) — decompiled/game-proj/Hacknet/DelayedInput.cs:3
    public double Delay { get; set; }
    public InputMap inputs { get; set; }
    public float xPos { get; set; }
    public float yPos { get; set; }

`Hacknet.DemoEndScreen` (class) — decompiled/game-proj/Hacknet/DemoEndScreen.cs:9
    public Rectangle Fullscreen;
    public bool StopsMusic = true;
    public bool IsDLCDemoScreen = false;
    public double timeOnThisScreen = 0.0;
    public PointGatherEffect pointEffect = new PointGatherEffect();
    public HexGridBackground HexBackground;
    public override void LoadContent()
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public override void Draw(GameTime gameTime)

`Hacknet.DisplayModule` (class) — decompiled/game-proj/Hacknet/DisplayModule.cs:14
    public const int MAX_DISPLAY_STRING_LENGTH = 6000;
    public string command = "";
    public string[] commandArgs;
    public Folder LastDisplayedFileFolder = null;
    public string LastDisplayedFileSourceIP = null;
    public int x;
    public int y;
    public new Rectangle tmpRect;
    public List<Texture2D> computers;
    public Dictionary<string, Texture2D> compAltIcons;
    public Texture2D defaultComputer;
    public Texture2D lockSprite;
    public Texture2D openLockSprite;
    public Texture2D fancyCornerSprite;
    public Texture2D fancyPanelSprite;
    public DisplayModuleLSHelper lsModuleHelper = new DisplayModuleLSHelper();
    public ScrollableTextRegion catTextRegion;
    public Vector2 scroll;
    public Vector2 catScroll;
    public int errorCount = 0;
    public bool hasSentErrorEmail = false;
    public string invioableSecurityCacheString = null;
    public float invioabilityCharChangeTimer = 0f;
    public string loginDetailsCache = null;
    public bool lockLoginDisplayCache = false;
    public DisplayModule(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public void typeChanged()
    public void doCommandModule()
    public void doDaemonDisplay()
    public void doLoginDisplay()
    public void forceLogin(string username, string pass)
    public Texture2D GetComputerImage(Computer comp)
    public void doConnectHeader()
    public void doConnectDisplay()
    public void doDisconnectDisplay()
    public void doDisconnectForcedDisplay()
    public void doCatDisplay()
    public void doProbeDisplay()
    public void DrawInvioabilityEffect(Rectangle dest)
    public void doLsDisplay()
    public void doFolderGui(int width, int height, int indexOffset, Folder f, int recItteration)
    public static string splitForWidth(string s, int width)
    public static string splitForWidth(string s, int width, bool correct)
    public static string cleanSplitForWidth(string s, int width)

`Hacknet.DLC1SessionUpgrader` (class) — decompiled/game-proj/Hacknet/DLC1SessionUpgrader.cs:9
    public static bool HasDLC1Installed = false;
    public static void CheckForDLCFiles()
    public static void UpgradeSession(object osobj, bool needsNodeInjection)
    public static void EndDLCSection(object osobj)
    public static void ReDsicoverAllVisibleNodesInOSCache(object osobj)

`Hacknet.DLCCreditsDaemon` (class) — decompiled/game-proj/Hacknet/DLCCreditsDaemon.cs:10
    public const float ResetSequenceTime = 5f;
    public string[] CreditsData;
    public bool showingCredits = false;
    public bool isInResetSequence = false;
    public float timeInReset = 0f;
    public float timeInCredits = 0f;
    public SoundEffect spindown;
    public SoundEffect spindownImpact;
    public SoundEffect buildup;
    public bool hasCuedBuildup = false;
    public bool hasCuedFinaleSong = false;
    public string OverrideTitle = null;
    public string OverrideButtonText = null;
    public string ConditionalActionsToLoadOnButtonPress = null;
    public DLCCreditsDaemon(Computer c, OS os)
    public DLCCreditsDaemon(Computer c, OS os, string overrideTitle, string overrideButtonText)
    public void LoadSounds()
    public override void navigatedTo()
    public void EndDLC()
    public void AddRadialMailLine()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.DLCHubServer` (class) — decompiled/game-proj/Hacknet/DLCHubServer.cs:15
    public const string ROOT_FOLDERNAME = "HomeBase";
    public const string MISSION_FOLDERNAME = "contracts";
    public const string ARCHIVE_FOLDERNAME = "archive";
    public const string ACTIONS_FOLDERNAME = "runtime";
    public const string CONFIG_FILENAME = "dhs_config.sys";
    public List<string> Agents = new List<string>();
    public Color themeColor = Color.MediumVioletRed;
    public string groupName = "Bibliotheca";
    public bool AddsFactionPointForMissionCompleteion = true;
    public bool AutoClearMissionsOnSingleComplete = true;
    public bool AllowContractAbbandon = false;
    public DelayableActionSystem DelayedActions;
    public Folder rootFolder;
    public Folder missionFolder;
    public Folder archivesFolder;
    public Folder actionsFolder;
    public DHSState State = DHSState.Welcome;
    public ClaimableMission SelectedMission = null;
    public bool isAddingTextResponse = false;
    public List<string> MissionTextResponses = new List<string>();
    public string inProgressTextResponse = null;
    public float BaseWelcomeFadeoutTime = 4f;
    public float WelcomeFadeoutTimerLeft = 4f;
    public IRCSystem IRCSystem;
    public Dictionary<string, Color> HighlightedWords = new Dictionary<string, Color>();
    public SoundEffect ButtonPressSound;
    public SoundEffect WooshBuildup;
    public SoundEffect USIntro;
    public List<ClaimableMission> ActiveMissions = new List<ClaimableMission>(3);
    public Texture2D MissionAvaliableIcon;
    public Texture2D MissionTakenIcon;
    public Texture2D MissionPlayersIcon;
    public Texture2D LoadingSpinner;
    public int UIButtonOffset = 0;
    public ScrollableTextRegion ScrollableTextPanel;
    public bool ShouldShowMissionIncompleteMessage = false;
    public bool AbandonMissionShowConfirmation = false;
    public HexGridBackground HexBackground;
    public float timeSpentInLoading = 0f;
    public bool HasStartedWoosh = false;
    public DLCHubServer(Computer c, string serviceName, string group, OS _os)
    public override void initFiles()
    public void InitDefaults()
    public void initFilesystem()
    public void InitTests()
    public override void navigatedTo()
    public void SubscribeToAlertActionFroNewMessage(Action<string, string> act)
    public void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act)
    public string GetName()
    public void ReGenerateConfigFile()
    public FileEntry generateConfigFile()
    public void loadFromConfigFileData(string config)
    public string getDataFromConfigLine(string line, string sentinel = "= ")
    public string GetSerializedWordHighlightList()
    public void DeserializeWordHighlightList(string input)
    public override void loadInit()
    public override string getSaveString()
    public void AddAgent(string AgentName, string agentPassword, Color color)
    public void AddMission(string missionPath, string AgentClaimName = null, bool startsComplete = false)
    public void AddMission(ActiveMission mission, string AgentClaimName = null, bool startsComplete = false)
    public void RemoveMission(string missionPath)
    public void ReadActiveMissions()
    public void ReSerializeActiveMissions()
    public string GetFilenameForMission(ActiveMission mission)
    public bool PlayerHasClaimedMission()
    public void PlayerAcceptMission(ClaimableMission mission)
    public void PlayerAbandonedMission(ClaimableMission mission)
    public bool PlayerAttemptCompleteMission(ClaimableMission mission, bool ForceComplete = false)
    public void CompleteAndArchiveMissionSet(List<ClaimableMission> missionsToArchive)
    public void ClearAllActiveMissions()
    public void Update()
    public bool ShouldDisplayNotifications()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DoLoadingPlayerInScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawOptionsPanel(Rectangle bounds, SpriteBatch sb)
    public void DrawHomeScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawMissionSelectView(Rectangle bounds, SpriteBatch sb)
    public void DrawMissionPanel(Rectangle bounds, SpriteBatch sb, ClaimableMission mission)
    public void DrawMissionDetailsPanel(Rectangle bounds, SpriteBatch sb, ClaimableMission mission)
    public void DrawAdditionalDetailsSection(Rectangle bounds, SpriteBatch sb)
    public void DrawHeaderLine(Rectangle dest, string header, SpriteBatch sb)

`Hacknet.DLCHubServer.ClaimableMission` (class) — decompiled/game-proj/Hacknet/DLCHubServer.cs:27
    public string AgentClaim;
    public bool IsComplete;
    public ActiveMission Mission;
    public float UITextScrollDown;

`Hacknet.DLCHubServer.DHSState` (enum) — decompiled/game-proj/Hacknet/DLCHubServer.cs:17

`Hacknet.DLCIntroExe` (class) — decompiled/game-proj/Hacknet/DLCIntroExe.cs:11
    public const bool combineNodeExplodeAndMailBurst = false;
    public static float SpinUpTime = 13.8f;
    public static float FlickeringTime = 10f;
    public static float MailIconFlickerOutTime = 3.82f;
    public static float AssignMission1Time = 16f;
    public static float AssignMission2Time = 16f;
    public static float WindDownTimeAfterCompletingMission = 0.8f;
    public static float NodeImpactEffectTransOutTime = 3f;
    public static float NodeImpactEffectTransInTime = 2f;
    public Color themeColor = new Color(38, 201, 155, 220);
    public IntroState State = IntroState.NotStarted;
    public float TimeInThisState = 0f;
    public float percentageThroughThisState = 0f;
    public Texture2D circle;
    public Texture2D circleOutline;
    public OSTheme originalTheme = OSTheme.HacknetBlue;
    public List<TraceKillExe.PointImpactEffect> ImpactEffects = new List<TraceKillExe.PointImpactEffect>();
    public float timeBetweenNodeRemovals = 1f;
    public float timeSinceNodeRemoved = 0f;
    public Color originalTopBarIconsColor;
    public string assignment1MissionPath = "Content/DLC/Missions/Intro/KaguyaTrialMission1.xml";
    public string assignment2MissionPath = "Content/DLC/Missions/Intro/KaguyaTrialMission2.xml";
    public string Assignment1Text = "";
    public string Assignment2Text = "";
    public string AssignmentsCompleteText = "";
    public int charsRenderedSoFar = 0;
    public ActiveMission LoadedMission;
    public bool IsOnAssignment1 = true;
    public bool AllAssignmentsComplete = false;
    public bool MissionIsComplete = false;
    public string PhaseTitle = "";
    public string PhaseSubtitle = "";
    public HexGridBackground BackgroundEffect;
    public bool OSTraceTimerOverrideActive = false;
    public SoundEffect GlowSound;
    public SoundEffect BreakSound;
    public ExplodingUIElementEffect explosion = new ExplodingUIElementEffect();
    public DLCIntroExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Killed()
    public void UpdateState(float t)
    public void PlayerLostToTraceTimer()
    public void CompleteExecution()
    public void UpdateMailePhaseOut(float t)
    public void CompleteMailPhaseOut()
    public void PrepareForUIBreakdown()
    public void AddRadialMailLine()
    public void UpdateUIFlickerIn()
    public void UpdateUIBreaking(float t)
    public void DrawAssignmentPhase(float t)
    public void StartAssignment()
    public void CheckProgressOfCurrentAssignment()
    public void MissionWasCompleted()
    public override void Draw(float t)
    public void DrawPhaseTitle(float t, Rectangle dest)
    public void UpdateImpactEffects(float t)
    public void DrawImpactEffects(List<TraceKillExe.PointImpactEffect> Effects)

`Hacknet.DLCIntroExe.IntroState` (enum) — decompiled/game-proj/Hacknet/DLCIntroExe.cs:13

`Hacknet.DLCTraceSlower` (class) — decompiled/game-proj/Hacknet/DLCTraceSlower.cs:9
    public const int TargetRamUse = 600;
    public const float RAM_CHANGE_PS = 200f;
    public const int RAM_STARTING = 50;
    public string ActiveConnectedCompIP = null;
    public DepthDotGridEffect dotEffect;
    public DLCTraceSlower(Rectangle location, OS operatingSystem)
    public static DLCTraceSlower GenerateInstanceOrNullFromArguments(string[] args, Rectangle location, object osObj, Computer target)
    public override void Update(float t)
    public override void Killed()
    public override void Completed()
    public override void Draw(float t)

`Hacknet.EndingSequenceModule` (class) — decompiled/game-proj/Hacknet/EndingSequenceModule.cs:11
    public const float SpeechTextHashDelay = 1f;
    public const float SpeechTextPercDelay = 0.5f;
    public const float SpeechTextCharDelay = 0.05f;
    public bool IsActive = false;
    public float elapsedTime = 0f;
    public float HacknetTitleFreezeTime = 10f;
    public float creditsPixelsScrollPerSecond = 65f;
    public SoundEffect speech;
    public SoundEffectInstance speechinstance;
    public WaveformRenderer waveRender;
    public bool IsInCredits = false;
    public float creditsScroll = 0f;
    public string[] CreditsData;
    public SoundEffect spinUpEffect;
    public SoundEffect traceDownEffect;
    public string BitSpeechText;
    public int SpeechTextIndex = 0;
    public float SpeechTextTimer = 0f;
    public EndingSequenceModule(Rectangle location, OS operatingSystem)
    public override void Update(float t)
    public void RollCredits()
    public override void Draw(float t)
    public void DrawCredits()
    public void CompleteAndReturnToMenu()

`Hacknet.EOSAppGenerator` (class) — decompiled/game-proj/Hacknet/EOSAppGenerator.cs:5
    public static string[] Name1 = new string[18]
    public static string[] Name2 = new string[21]
    public static string[] Name3 = new string[18]
    public static string[] Postfix = new string[16]
    public static string[] SaveData1 = new string[22]
    public static string[] SaveData2 = new string[30]
    public static string[] SaveData3 = new string[30]
    public static string[] SaveDataWildcards = new string[13]
    public static string[] GenerateNames()
    public static string GenerateName()
    public static string GenerateAppSaveLine()
    public static Folder GetAppFolder()

`Hacknet.EOSComp` (class) — decompiled/game-proj/Hacknet/EOSComp.cs:6
    public static void AddEOSComp(XmlReader rdr, Computer compAttatchedTo, object osObj)
    public static Folder GenerateEOSFolder()
    public static void GenerateEOSFilesystem(Computer device)

`Hacknet.EOSDeviceScannerExe` (class) — decompiled/game-proj/Hacknet/EOSDeviceScannerExe.cs:9
    public const float TOTAL_TIME = 8f;
    public const float SHORTCUT_TIME = 3.5f;
    public const float timeBetweenBounces = 0.07f;
    public List<Vector2> locations = new List<Vector2>();
    public float timeToNextBounce = 0f;
    public List<string> ResultTitles = new List<string>();
    public List<string> ResultBodies = new List<string>();
    public float timer = 0f;
    public Computer targetComp;
    public bool IsComplete = false;
    public int devicesFound = 0;
    public bool isError = false;
    public string errorMessage = null;
    public EOSDeviceScannerExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.ExeModule` (class) — decompiled/game-proj/Hacknet/ExeModule.cs:7
    public static float FADEOUT_RATE = 0.5f;
    public static float MOVE_UP_RATE = 350f;
    public static int DEFAULT_RAM_COST = 246;
    public int PID = 0;
    public float fade = 1f;
    public bool isExiting = false;
    public bool needsRemoval = false;
    public float moveUpBy = 0f;
    public int ramCost = DEFAULT_RAM_COST;
    public int baseRamCost = 0;
    public string targetIP = "";
    public bool needsProxyAccess = false;
    public string IdentifierName = "UNKNOWN";
    public ExeModule(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public virtual void Completed()
    public virtual void Killed()
    public virtual void drawOutline()
    public virtual void drawTarget(string typeName = "app:")
    public Rectangle GetContentAreaDest()

`Hacknet.ExtensionSequencerExe` (class) — decompiled/game-proj/Hacknet/ExtensionSequencerExe.cs:11
    public static int ACTIVATING_RAM_COST = 170;
    public static int BASE_RAM_COST = 60;
    public static float RAM_CHANGE_PS = 100f;
    public static double Song_Length = 186.0;
    public static float TimeBetweenBeats = 1.832061f;
    public MovingBarsEffect bars = new MovingBarsEffect();
    public string targetID;
    public string flagForProgressionName;
    public string oldSongName = null;
    public int targetRamUse = ACTIVATING_RAM_COST;
    public float stateTimer = 0f;
    public float beatHits = 0.15f;
    public double beatDropTime = 16.64;
    public List<ConnectedNodeEffect> nodeeffects = new List<ConnectedNodeEffect>();
    public SequencerExeState state = SequencerExeState.Unavaliable;
    public Computer targetComp;
    public bool HasBeenKilled = false;
    public ExtensionSequencerExe(Rectangle location, OS operatingSystem, string[] p)
    public override void LoadContent()
    public override void Update(float t)
    public void ActiveStateUpdate(float t)
    public void UpdateRamCost(float t)
    public override void Killed()
    public void MoveToActiveState()
    public override void Draw(float t)
    public void DrawActiveState()

`Hacknet.ExtensionSequencerExe.SequencerExeState` (enum) — decompiled/game-proj/Hacknet/ExtensionSequencerExe.cs:13

`Hacknet.ExternalCounterpart` (class) — decompiled/game-proj/Hacknet/ExternalCounterpart.cs:10
    public static Dictionary<string, string> networkIPList;
    public static ASCIIEncoding encoder;
    public string idName;
    public string connectionIP;
    public bool isConnected = false;
    public TcpClient connection;
    public byte[] buffer;
    public static string getIPForServerName(string serverName)
    public static void loadNetIPList()
    public ExternalCounterpart(string idName, string ipEndpoint)
    public void sendMessage(string message)
    public void disconnect()
    public void testConnection()
    public void establishConnection()
    public void DoTcpConnectionCallback(IAsyncResult ar)
    public void writeMessage(string message)
    public void TCPWriteMessageCallback(IAsyncResult ar)

`Hacknet.Faction` (class) — decompiled/game-proj/Hacknet/Faction.cs:7
    public int playerValue;
    public int neededValue;
    public string name = "unknown";
    public string idName = "";
    public bool playerHasPassedValue = false;
    public bool PlayerLosesValueOnAbandon = false;
    public Faction(string _name, int _neededValue)
    public virtual void addValue(int value, object os)
    public void contractAbbandoned(object osIn)
    public int getRank()
    public virtual string getSaveString()
    public int getMaxRank()
    public virtual void playerPassedValue(object os)
    public static Faction loadFromSave(XmlReader xmlRdr)
    public bool valuePassedPoint(int oldValue, int neededValue)

`Hacknet.FastActionHost` (class) — decompiled/game-proj/Hacknet/FastActionHost.cs:7
    public FastDelayableActionSystem DelayedActions;
    public bool RequiresLogin = false;
    public Folder folder;
    public FastActionHost(Computer c, OS os, string name)
    public override void initFiles()
    public override void loadInit()
    public string GetName()
    public override void navigatedTo()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.FastBasicAdministrator` (class) — decompiled/game-proj/Hacknet/FastBasicAdministrator.cs:5
    public override void disconnectionDetected(Computer c, OS os)

`Hacknet.FastDelayableActionSystem` (class) — decompiled/game-proj/Hacknet/FastDelayableActionSystem.cs:9
    public new static string EncryptionPass = "dasencrypt";
    public new Folder folder;
    public List<KeyValuePair<float, SerializableAction>> Actions = new List<KeyValuePair<float, SerializableAction>>();
    public int SeqNum = 0;
    public FastDelayableActionSystem(Folder sourceFolder, object osObj)
    public new void Update(float t, object osObj)
    public override void InstantlyResolveAllActions(object osObj)
    public override void AddAction(SerializableAction action, float delay)
    public FileEntry EncryptAction(SerializableAction action, float delay)
    public List<FileEntry> GetAllFilesForActions()
    public void DeserializeActions(List<FileEntry> files)

`Hacknet.FastProgressOnlyAdministrator` (class) — decompiled/game-proj/Hacknet/FastProgressOnlyAdministrator.cs:3
    public override void traceEjectionDetected(Computer c, OS os)
    public override void disconnectionDetected(Computer c, OS os)

`Hacknet.FileEncrypter` (class) — decompiled/game-proj/Hacknet/FileEncrypter.cs:6
    public static string[] HeaderSplitDelimiters = new string[1] { "::" };
    public static string EncryptString(string data, string header, string ipLink, string pass = "", string fileExtension = null)
    public static ushort GetPassCodeFromString(string code)
    public static string Encrypt(string data, ushort passcode)
    public static string Decrypt(string data, ushort passcode)
    public static string[] DecryptString(string data, string pass = "")
    public static string[] TestingDecryptString(string data, ushort pass)
    public static string[] DecryptHeaders(string data, string pass = "")
    public static int FileIsEncrypted(string data, string pass = "")
    public static string MakeReplacementsForDisplay(string input)

`Hacknet.FileEntry` (class) — decompiled/game-proj/Hacknet/FileEntry.cs:8
    public static List<string> filenames;
    public static List<string> fileData;
    public string name;
    public string data;
    public int size;
    public int secondCreatedAt;
    public FileEntry()
    public FileEntry(string dataEntry, string nameEntry)
    public string head()
    public string getName()
    public static void init(ContentManager content)

`Hacknet.FileSanitiser` (class) — decompiled/game-proj/Hacknet/FileSanitiser.cs:5
    public static string purifyStringForDisplay(string data)
    public static void purifyVehicleFile(string path)
    public static string replaceChar(string data, int index, char replacer)
    public static void purifyNameFile(string path)
    public static void purifyLocationFile(string path)

`Hacknet.FileSystem` (class) — decompiled/game-proj/Hacknet/FileSystem.cs:6
    public Folder root;
    public FileSystem(bool empty)
    public FileSystem()
    public void generateSystemFiles()
    public string getSaveString()
    public static FileSystem load(XmlReader reader)
    public string TestEquals(object obj)
    public override int GetHashCode()

`Hacknet.FileType` (interface) — decompiled/game-proj/Hacknet/FileType.cs:3

`Hacknet.Firewall` (class) — decompiled/game-proj/Hacknet/Firewall.cs:8
    public const int MIN_SOLUTION_LENGTH = 6;
    public const int OUTPUT_LINE_WIDTH = 20;
    public const int CHARS_SOLVED_PER_PASS = 3;
    public const string SOLVED_CHAR = "0";
    public int solutionLength = 6;
    public string solution;
    public bool solved = false;
    public int complexity = 1;
    public int analysisPasses = 0;
    public float additionalDelay = 0f;
    public Firewall()
    public Firewall(int complexity)
    public Firewall(int complexity, string solution)
    public Firewall(int complexity, string solution, float additionalTime)
    public void generateRandomSolution()
    public static Firewall load(XmlReader reader)
    public string getSaveString()
    public void resetSolutionProgress()
    public bool attemptSolve(string attempt, object os)
    public void writeAnalyzePass(object os_object, object target_object)
    public IEnumerator<ActionDelayer.Condition> generateOutputPass(int pass, OS os, Computer target)
    public string generateOutputLine(int location)
    public override bool Equals(object obj)
    public override int GetHashCode()
    public override string ToString()

`Hacknet.Folder` (class) — decompiled/game-proj/Hacknet/Folder.cs:6
    public static string ampersandReplacer = "|##AMP##|";
    public static string backslashReplacer = "|##BS##|";
    public static string rightSBReplacer = "|##RSB##|";
    public static string leftSBReplacer = "|##LSB##|";
    public static string rightABReplacer = "|##RAB##|";
    public static string leftABReplacer = "|##LAB##|";
    public static string quoteReplacer = "|##QOT##|";
    public static string singlequoteReplacer = "|##SIQ##|";
    public List<FileEntry> files = new List<FileEntry>();
    public List<Folder> folders = new List<Folder>();
    public string name;
    public Folder(string foldername)
    public string getName()
    public bool containsFile(string name, string data)
    public bool containsFileWithData(string data)
    public bool containsFile(string name)
    public Folder searchForFolder(string folderName)
    public FileEntry searchForFile(string fileName)
    public string getSaveString()
    public static string Filter(string s)
    public static string deFilter(string s)
    public static Folder load(XmlReader reader)
    public void load(string data)
    public string TestEqualsFolder(Folder f)

`Hacknet.ForkBombExe` (class) — decompiled/game-proj/Hacknet/ForkBombExe.cs:5
    public static float RAM_CHANGE_PS = 150f;
    public int targetRamUse = 999999999;
    public string runnerIP = "";
    public static string binary = "";
    public int binaryScroll = 0;
    public int charsWide = 0;
    public bool frameSwitch = false;
    public ForkBombExe(Rectangle location, OS operatingSystem)
    public ForkBombExe(Rectangle location, OS operatingSystem, string ipFrom)
    public override void LoadContent()
    public override void Killed()
    public override void Update(float t)
    public override void Draw(float t)
    public override void Completed()

`Hacknet.FTPBounceExe` (class) — decompiled/game-proj/Hacknet/FTPBounceExe.cs:6
    public static float DURATION = 15f;
    public static float SCROLL_RATE = 0.08f;
    public string binary;
    public byte[] acceptedBinary1;
    public byte[] acceptedBinary2;
    public int binaryChars = 0;
    public float progress = 0f;
    public float timeLeft = DURATION;
    public float binaryScrollTimer = SCROLL_RATE;
    public int binaryIndex = 0;
    public bool complete = false;
    public int unlockedChars1 = 0;
    public int unlockedChars2 = 0;
    public int[] unlockOrder;
    public FTPBounceExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public override void Completed()

`Hacknet.FTPFastExe` (class) — decompiled/game-proj/Hacknet/FTPFastExe.cs:7
    public const float RUN_TIME = 7f;
    public const float IDLE_TIME = 7.5f;
    public float elapsedTime = 0f;
    public PointGatherEffect points = new PointGatherEffect();
    public FTPFastExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.Game1` (class) — decompiled/game-proj/Hacknet/Game1.cs:17
    public static Game1 singleton;
    public static bool threadsExiting;
    public static CultureInfo culture;
    public static CultureInfo OriginalCultureInfo;
    public GraphicsDeviceManager graphics;
    public GraphicsDeviceInformation graphicsInfo;
    public SpriteBatch spriteBatch;
    public ScreenManager sman;
    public bool resolutionSet = true;
    public bool IsDrawing = false;
    public bool CanLoadContent = false;
    public bool HasLoadedContent = false;
    public bool NeedsSettingsLocaleActivation = false;
    public EventHandler<PreparingDeviceSettingsEventArgs> graphicsPreparedHandler;
    public static string AutoLoadExtensionPath = null;
    public Game1()
    public void GraphicsDevice_DeviceLost(object sender, EventArgs e)
    public void graphics_DeviceReset(object sender, EventArgs e)
    public void graphics_DeviceResetting(object sender, EventArgs e)
    public void graphics_DeviceDisposing(object sender, EventArgs e)
    public void graphics_PreparingDeviceSettingsForAltMonitor(object sender, PreparingDeviceSettingsEventArgs e)
    public void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
    public void setNewGraphics()
    public void CheckAndFixWindowPosition()
    public void setWindowPosition(Vector2 pos)
    public override void Initialize()
    public void handleExit(object sender, EventArgs e)
    public void LoadRegenSafeContent()
    public override void LoadContent()
    public void LoadGraphicsContent()
    public void LoadInitialScreens()
    public override void UnloadContent()
    public override void Update(GameTime gameTime)
    public override void Draw(GameTime gameTime)
    public static Game1 getSingleton()

`Hacknet.GameSaver` (class) — decompiled/game-proj/Hacknet/GameSaver.cs:3
    public static string compSplitter = "@*7@632(27@&(*@)@&*#@HD(*@H$J(";
    public static void save(object osObj)

`Hacknet.GameScreen` (class) — decompiled/game-proj/Hacknet/GameScreen.cs:6
    public bool isPopup = false;
    public TimeSpan transitionOnTime = TimeSpan.Zero;
    public TimeSpan transitionOffTime = TimeSpan.Zero;
    public float transitionPosition = 1f;
    public ScreenState screenState = ScreenState.TransitionOn;
    public bool isExiting = false;
    public bool otherScreenHasFocus;
    public ScreenManager screenManager;
    public PlayerIndex? controllingPlayer;
    public bool IsPopup
    public TimeSpan TransitionOnTime
    public TimeSpan TransitionOffTime
    public float TransitionPosition
    public byte TransitionAlpha => (byte)(255f - TransitionPosition * 255f);
    public ScreenState ScreenState
    public bool IsExiting
    public bool IsActive => !otherScreenHasFocus && (screenState == ScreenState.TransitionOn || screenState == ScreenState.Active);
    public ScreenManager ScreenManager
    public PlayerIndex? ControllingPlayer
    public virtual void LoadContent()
    public virtual void UnloadContent()
    public virtual void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public bool UpdateTransition(GameTime gameTime, TimeSpan time, int direction)
    public virtual void HandleInput(InputState input)
    public virtual void Draw(GameTime gameTime)
    public void ExitScreen()
    public virtual void inputMethodChanged(bool usingGamePad)
    public GameScreen()

`Hacknet.GenerationStatics` (class) — decompiled/game-proj/Hacknet/GenerationStatics.cs:3
    public static int CorportationsGenerated = 0;

`Hacknet.GitCommitEntry` (class) — decompiled/game-proj/Hacknet/GitCommitEntry.cs:5
    public int EntryNumber = 0;
    public List<string> ChangedFiles = new List<string>();
    public string Message;
    public string UserName;
    public string SourceIP;
    public override string ToString()

`Hacknet.GuiData` (class) — decompiled/game-proj/Hacknet/GuiData.cs:11
    public static Vector2 temp = default(Vector2);
    public static Point tmpPoint = default(Point);
    public static Color tmpColor = default(Color);
    public static Rectangle tmpRect = default(Rectangle);
    public static MouseState lastMouse;
    public static MouseState mouse;
    public static InputState lastInput;
    public static Color Default_Selected_Color = new Color(0, 166, 235);
    public static Color Default_Unselected_Color = new Color(255, 128, 0);
    public static Color Default_Backing_Color = new Color(30, 30, 50, 100);
    public static Color Default_Light_Backing_Color = new Color(80, 80, 100, 255);
    public static Color Default_Lit_Backing_Color = new Color(255, 199, 41, 100);
    public static Color Default_Dark_Neutral_Color = new Color(10, 10, 15, 200);
    public static Color Default_Dark_Background_Color = new Color(40, 40, 45, 180);
    public static Color Default_Trans_Grey = new Color(30, 30, 30, 100);
    public static Color Default_Trans_Grey_Bright = new Color(60, 60, 60, 100);
    public static Color Default_Trans_Grey_Dark = new Color(20, 20, 20, 200);
    public static Color Default_Trans_Grey_Strong = new Color(80, 80, 80, 100);
    public static Color Default_Trans_Grey_Solid = new Color(100, 100, 100, 255);
    public static int lastMouseWheelPos = -1;
    public static int lastMouseScroll = 0;
    public static int hot = -1;
    public static int active = -1;
    public static int enganged = -1;
    public static SpriteBatch spriteBatch;
    public static SpriteFont font;
    public static SpriteFont titlefont;
    public static SpriteFont smallfont;
    public static SpriteFont tinyfont;
    public static SpriteFont UITinyfont;
    public static SpriteFont UISmallfont;
    public static SpriteFont detailfont;
    public static bool blockingInput = false;
    public static bool blockingTextInput = false;
    public static bool willBlockTextInput = false;
    public static Vector2 scrollOffset = Vector2.Zero;
    public static float lastTimeStep = 0.016f;
    public static bool initialized = false;
    public static TextInputHook TextInputHook;
    public static List<FontCongifOption> FontConfigs = new List<FontCongifOption>();
    public static Dictionary<string, List<FontCongifOption>> LocaleFontConfigs = new Dictionary<string, List<FontCongifOption>>();
    public static FontCongifOption ActiveFontConfig = default(FontCongifOption);
    public static void InitFontOptions(ContentManager content)
    public static void ActivateFontConfig(string configName)
    public static void ActivateFontConfig(FontCongifOption config)
    public static void init(GameWindow window)
    public static void doInput()
    public static void doInput(InputState input)
    public static void setTimeStep(float t)
    public static KeyboardState getKeyboadState()
    public static KeyboardState getLastKeyboadState()
    public static Vector2 getMousePos()
    public static Point getMousePoint()
    public static float getMouseWheelScroll()
    public static bool isMouseLeftDown()
    public static bool mouseLeftUp()
    public static bool mouseWasPressed()
    public static void startDraw()
    public static void endDraw()
    public static char[] getFilteredKeys()

`Hacknet.GuiData.FontCongifOption` (struct) — decompiled/game-proj/Hacknet/GuiData.cs:13
    public SpriteFont smallFont;
    public SpriteFont tinyFont;
    public SpriteFont bigFont;
    public SpriteFont detailFont;
    public string name;
    public float tinyFontCharHeight;

`Hacknet.HackerScriptExecuter` (class) — decompiled/game-proj/Hacknet/HackerScriptExecuter.cs:11
    public const string splitDelimiter = " $#%#$\r\n";
    public static SoundEffect MusicStopSFX;
    public static void runScript(string scriptName, object os, string sourceCompReplacer = null, string targetCompReplacer = null)
    public static void executeThreadedScript(string[] script, OS os)
    public static string getBasicNetworkCommand(string targetCommand, Computer target, Computer source)
    public static string getPathString(string fPath, OS os, Folder f)

`Hacknet.HeartMonitorDaemon` (class) — decompiled/game-proj/Hacknet/HeartMonitorDaemon.cs:12
    public const string ActiveFirmwareFilename = "LiveFirmware.dll";
    public const string FolderName = "KBT_Pacemaker";
    public const string LiveFolderName = "Active";
    public const string SubLoginUsername = "EAdmin";
    public const string SubLoginPass = "tens86";
    public const float MinFirmwareLoadTime = 10f;
    public const float TimeToDeathInDanger = 21f;
    public const float DyingDangerTime = 6f;
    public const float DeadBeepSustainFadeOut = 16f;
    public const float DeadBeepSustainFadeoutStartDelay = 5f;
    public Texture2D Heart;
    public Texture2D OxyIcon;
    public Texture2D WarnIcon;
    public Effect blufEffect;
    public RenderTarget2D bloomTarget;
    public RenderTarget2D priorTarget;
    public RenderTarget2D secondaryBloomTarget;
    public List<Action<int, int, SpriteBatch>> PostBloomDrawCalls = new List<Action<int, int, SpriteBatch>>();
    public SpriteBatch BlurContentSpritebatch;
    public Color bloomColor = new Color(10, 10, 10, 0);
    public Color heartColor = new Color(247, 237, 125);
    public bool HasSecondaryLogin = false;
    public string PatientID = "UNKNOWN";
    public bool PatientInCardiacArrest = false;
    public bool PatientDead = false;
    public float PatientTimeInDanger = 0f;
    public float timeDead = 0f;
    public float timeTillNextHeartbeat = 0f;
    public float timeBetweenHeartbeats = 0.88235295f;
    public float beatTime = 0.18f;
    public SoundEffect beepSound;
    public SoundEffectInstance beepSustainSound;
    public float volume = 0.4f;
    public float opTransition = 0f;
    public bool opOpening = false;
    public string loginUsername = null;
    public string loginPass = null;
    public float firmwareLoadTime = 0f;
    public int selectedFirmwareIndex = 0;
    public bool isConfirmingSelection = false;
    public string selectedFirmwareName = "";
    public string selectedFirmwareData = "";
    public BasicMedicalMonitor HeartMonitor;
    public BasicMedicalMonitor BPMonitor;
    public BasicMedicalMonitor SPMonitor;
    public List<IMedicalMonitor> Monitors = new List<IMedicalMonitor>();
    public float projectionFowardsTime = 0.3f;
    public float timeSinceLastHeartBeat = float.MaxValue;
    public int HeartRate = 0;
    public float currentSPO2 = 0f;
    public float averageSPO2 = 0f;
    public int reportedSP02 = 95;
    public float timeSinceNormalHeartRate = 0f;
    public float alarmHeartOKTimer = 0f;
    public HeartMonitorState State = HeartMonitorState.Welcome;
    public float timeThisState = 0f;
    public HeartMonitorDaemon(Computer c, OS os)
    public override string getSaveString()
    public override void initFiles()
    public void SetUpMonitors()
    public void UpdateReports(float dt)
    public void UpdateReportsForHeartbeat()
    public void ChangeState(HeartMonitorState newState)
    public override void navigatedTo()
    public void UpdateStates(float dt)
    public void ForceStopBeepSustainSound()
    public void Update(float dt)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawStates(Rectangle bounds, SpriteBatch sb)
    public void StartBloomDraw(SpriteBatch sb)
    public void EndBloomDraw(Rectangle bounds, Rectangle zeroedBounds, SpriteBatch mainSB, SpriteBatch bloomContentSpritebatch)
    public void DrawSegments(Rectangle bounds, SpriteBatch sb)
    public void DrawMonitorNumericalDisplay(Rectangle bounds, string display, string value, SpriteBatch sb, Color col, Texture2D icon = null)
    public void DrawMonitorStatusPanelDisplay(Rectangle bounds, string display, string value, SpriteBatch sb, Color col, Texture2D icon = null)
    public void DrawGraph(IMedicalMonitor monitor, Rectangle dest, Color col, SpriteBatch sb, bool drawUnderline = true)
    public void EnactFirmwareChange()
    public void DrawWelcomeScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawLinedMessage(string message, Color col, Rectangle dest, SpriteBatch sb)
    public void DrawOptionsPanel(Rectangle bounds, SpriteBatch spritebatch)
    public Rectangle DrawOptionsPanelHeaders(Rectangle bounds, SpriteBatch sb, float contentFade)
    public void DrawOptionsPanelContent(Rectangle bounds, SpriteBatch sb, float contentFade)
    public void DrawOptionsPanelFirmwareContent(Rectangle bounds, SpriteBatch sb, float contentFade)
    public bool DrawSelectedFirmwareFileDetails(Rectangle bounds, SpriteBatch sb, string data, string filename)
    public bool IsValidFirmwareData(string data)
    public void DrawOptionsPanelLoginContent(Rectangle bounds, SpriteBatch sb, float contentFade)
    public bool ButtonFlashForContentFade(float contentFade)

`Hacknet.HeartMonitorDaemon.HeartMonitorState` (enum) — decompiled/game-proj/Hacknet/HeartMonitorDaemon.cs:14

`Hacknet.Helpfile` (class) — decompiled/game-proj/Hacknet/Helpfile.cs:6
    public static int ITEMS_PER_PAGE = 10;
    public static List<string> help;
    public static string prefix = "---------------------------------\n" + LocaleTerms.Loc("Command List - Page [PAGENUM] of [TOTALPAGES]") + ":\n";
    public static string postfix = "help [PAGE NUMBER]\n " + LocaleTerms.Loc("Displays the specified page of commands.") + "\n---------------------------------\n";
    public static string LoadedLanguage = "en-us";
    public static void init()
    public static void writeHelp(OS os, int page = 0)
    public static int getNumberOfPages()

`Hacknet.HexClockExe` (class) — decompiled/game-proj/Hacknet/HexClockExe.cs:7
    public OSTheme theme;
    public bool stopUIChange = false;
    public HexClockExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Killed()
    public override void Draw(float t)
    public void AutoUpdateTheme(Color c)

`Hacknet.HostileHackerBreakinSequence` (class) — decompiled/game-proj/Hacknet/HostileHackerBreakinSequence.cs:11
    public static readonly string BaseDirectory = GetBaseDirectory();
    public static readonly string HelpFilePath = Path.Combine(BaseDirectory, "VM_Recovery_Guide.txt");
    public static readonly string Win32_BatchFilePath = Path.Combine(BaseDirectory, "OpenCMD.bat");
    public static readonly string HostileDirectory = Path.Combine(BaseDirectory, "Libs", "Injected");
    public static readonly string HostileFilePath = Path.Combine(HostileDirectory, "VMBootloaderTrap.dll");
    public static string GetBaseDirectory()
    public static void Execute(object osobj, Computer source, Computer target)
    public static bool IsFirstSuccessfulBootAfterBlockingState(object osobj)
    public static void ReactToFirstSuccesfulBoot(object osobj)
    public static bool IsInBlockingHostileFileState(object osobj)
    public static void CopyHostileFileToLocalSystem()
    public static string GetHelpText()
    public static void CopyHelpFile()
    public static void OpenWindowsHelpDocument()
    public static string OpenTerminal()
    public static void CrashProgram()
    public static void LockFileToPreventDeletionWin32(string filepath)
    public static void LockFileToPreventDeletionUnix(string dir, string file)
    public static void MinimizeWindow(IntPtr handle)
    public static void MinimizeAllOpenWindows()

`Hacknet.HTTPExploitExe` (class) — decompiled/game-proj/Hacknet/HTTPExploitExe.cs:8
    public static float DURATION = 14f;
    public static float AFTER_COMPLETION_STALL = 1f;
    public static float GRAPH_MOVEMENT = 22f;
    public float progress = 0f;
    public bool hasCompleted = false;
    public float sucsessTimer = AFTER_COMPLETION_STALL;
    public float tAccum = 0f;
    public float fastTimeAccum = 0f;
    public float heightRange = 0f;
    public List<Vector2> graphPoints;
    public List<Vector2> backGraphPoints;
    public HTTPExploitExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public void drawLine(Vector2 origin, Vector2 dest, Color c)
    public override void Completed()

`Hacknet.HubServerAlertsIcon` (class) — decompiled/game-proj/Hacknet/HubServerAlertsIcon.cs:11
    public bool IsEnabled = true;
    public Texture2D icon;
    public Texture2D circle;
    public SoundEffect alertSound;
    public string syncServerName;
    public IMonitorableDaemon HubServer;
    public Computer HubServerComp;
    public string[] TagsToAlertFor;
    public OS os;
    public bool PendingAlerts = false;
    public List<float> RadiusCircles = new List<float>();
    public HubServerAlertsIcon(ContentManager content, string serverToSyncTo, string[] tagsToAlertFor)
    public void Init(object OSobj)
    public void UpdateTarget(object monitor, object comp)
    public void ProcessNewLog(string Author, string Message)
    public void SendAlert()
    public void Update(float dt)
    public void ConnectToServer()
    public void Draw(Rectangle dest, SpriteBatch sb)

`Hacknet.IMonitorableDaemon` (interface) — decompiled/game-proj/Hacknet/IMonitorableDaemon.cs:5

`Hacknet.InputMap` (struct) — decompiled/game-proj/Hacknet/InputMap.cs:3
    public InputStates now = now;
    public InputStates last = last;
    public static InputMap e = default(InputMap);
    public static InputMap Empty
    public static bool operator ==(InputMap self, InputMap other)
    public static bool operator !=(InputMap self, InputMap other)
    public override bool Equals(object obj)
    public override int GetHashCode()

`Hacknet.InputMapping` (class) — decompiled/game-proj/Hacknet/InputMapping.cs:5
    public static InputStates ret;
    public static InputMap map;
    public static InputStates lastCalculatedState;
    public static InputStates getStatesFromKeys(KeyboardState keys, GamePadState pad, GamePadThumbSticks sticks)
    public static InputMap getMapFromKeys(KeyboardState keys, GamePadState pad)

`Hacknet.InputState` (class) — decompiled/game-proj/Hacknet/InputState.cs:6
    public const int MaxInputs = 4;
    public KeyboardState[] CurrentKeyboardStates;
    public GamePadState[] CurrentGamePadStates;
    public KeyboardState[] LastKeyboardStates;
    public GamePadState[] LastGamePadStates;
    public readonly bool[] GamePadWasConnected;
    public static InputState Empty = new InputState();
    public InputState()
    public void Update()
    public bool IsNewKeyPress(Keys key, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    public bool IsNewButtonPress(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    public bool IsMenuSelect(PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    public bool IsMenuCancel(PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    public bool IsMenuUp(PlayerIndex? controllingPlayer)
    public bool IsMenuDown(PlayerIndex? controllingPlayer)
    public bool IsPauseGame(PlayerIndex? controllingPlayer)

`Hacknet.InputStates` (struct) — decompiled/game-proj/Hacknet/InputStates.cs:3
    public float movement;
    public bool jumping;
    public bool useItem;
    public float wmovement;
    public bool wjumping;
    public bool wuseItem;
    public static bool operator ==(InputStates self, InputStates other)
    public static bool operator !=(InputStates self, InputStates other)
    public override bool Equals(object obj)
    public override int GetHashCode()

`Hacknet.IntroTextModule` (class) — decompiled/game-proj/Hacknet/IntroTextModule.cs:11
    public static float FLASH_TIME = 3.5f;
    public static float STAY_ONSCREEN_TIME = 3f;
    public static float MODULE_FLASH_TIME = 2f;
    public static float CHAR_TIME = 0.048f;
    public static float LINE_TIME = 0.455f;
    public static float DELAY_FROM_START_MUSIC_TIMER = (Settings.isConventionDemo ? 6.2f : 15.06f);
    public static float DEMO_DELAY_FROM_START_MUSIC_TIMER = 5.18f;
    public float timer;
    public float charTimer;
    public float lineTimer;
    public bool complete;
    public bool finishedText;
    public string[] text;
    public int textIndex;
    public int charIndex = 0;
    public static string[] delims = new string[1] { "\r\n" };
    public Rectangle fullscreen;
    public IntroTextModule(Rectangle location, OS operatingSystem)
    public override void Update(float t)
    public override void Draw(float t)
    public string GetScreensizeSplitVersionOfString(string input, Vector2 dpos)

`Hacknet.IRCDaemon` (class) — decompiled/game-proj/Hacknet/IRCDaemon.cs:10
    public IRCSystem System;
    public List<KeyValuePair<string, string>> StartingMessages = new List<KeyValuePair<string, string>>();
    public Dictionary<string, Color> UserColors = new Dictionary<string, Color>();
    public Color ThemeColor;
    public DelayableActionSystem DelayedActions;
    public bool RequiresLogin = false;
    public IRCDaemon(Computer c, OS os, string name)
    public override void initFiles()
    public override void loadInit()
    public void SubscribeToAlertActionFroNewMessage(Action<string, string> act)
    public void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act)
    public bool ShouldDisplayNotifications()
    public string GetName()
    public void ReloadUserColors()
    public override void navigatedTo()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.ISPDaemon` (class) — decompiled/game-proj/Hacknet/ISPDaemon.cs:9
    public const float MAX_WIDTH = 7f;
    public const float MAX_RATE = 16f;
    public const float EFFECT_TIMER = 30f;
    public const float SEARCH_TIME = 2f;
    public const string ABOUT_MESSAGE_FILE = "ISP_About_Message.txt";
    public List<ExpandingRectangleData> outlineEffectEntries = new List<ExpandingRectangleData>();
    public float lastTimer = 0f;
    public float timeEnteredLoadingScreen = 0f;
    public ISPDaemonState state = ISPDaemonState.Welcome;
    public string ipSearch = null;
    public Computer scannedComputer = null;
    public bool inspectionFlagged = false;
    public override void initFiles()
    public override void navigatedTo()
    public override string getSaveString()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawIPEntryScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawLoadingScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawEnterIPScreen(Rectangle bounds, SpriteBatch sb)
    public void drawBackButton(Rectangle bounds, ISPDaemonState stateTo = ISPDaemonState.Welcome)
    public void DrawAdminOnlyError(Rectangle bounds, SpriteBatch sb)
    public void DrawNotFoundError(Rectangle bounds, SpriteBatch sb)
    public void DrawAboutScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawWelcomeScreen(Rectangle bounds, SpriteBatch sb, Rectangle fullBounds)
    public void drawOutlineEffect(Rectangle bounds, SpriteBatch sb)
    public void drawRect(Rectangle rect, SpriteBatch sb, int thickness, float opacity, float blackLerp)
    public void addNewOutlineEffect()

`Hacknet.ISPDaemon.ExpandingRectangleData` (struct) — decompiled/game-proj/Hacknet/ISPDaemon.cs:11
    public float scaleIn;
    public float rate;
    public float thickness;
    public float blackLerp;

`Hacknet.ISPDaemon.ISPDaemonState` (enum) — decompiled/game-proj/Hacknet/ISPDaemon.cs:22

`Hacknet.KeyboardAndGamePad` (struct) — decompiled/game-proj/Hacknet/KeyboardAndGamePad.cs:5
    public KeyboardState keyboard;
    public GamePadState gamepad;

`Hacknet.LevelType` (struct) — decompiled/game-proj/Hacknet/LevelType.cs:3
    public int NumOfPuzzles = puzzles;
    public int NumOfBackgrounds = bgs;
    public string name = lvlname;

`Hacknet.LoadedTexture` (struct) — decompiled/game-proj/Hacknet/LoadedTexture.cs:5
    public string path { get; set; }
    public Texture2D tex { get; set; }
    public int retainCount { get; set; }

`Hacknet.LocaleTerms` (class) — decompiled/game-proj/Hacknet/LocaleTerms.cs:7
    public const string NewlineReplacer = "[%\\n%]";
    public static Dictionary<string, string> ActiveTerms = new Dictionary<string, string>();
    public static void ReadInTerms(string termsFilepath, bool clearPreviouslyLoadedTerms = true)
    public static string RemoveQuotes(string input)
    public static void ClearForEnUS()
    public static string Loc(string input)

`Hacknet.LocalizedFileLoader` (class) — decompiled/game-proj/Hacknet/LocalizedFileLoader.cs:7
    public static string Read(string filepath)
    public static string GetLocalizedFilepath(string filepath)
    public static string SafeFilterString(string data)
    public static string FilterStringForLocalization(string data)

`Hacknet.LogoCustomConnectDisplayDaemon` (class) — decompiled/game-proj/Hacknet/LogoCustomConnectDisplayDaemon.cs:8
    public const int ButtonLowerHeight = 80;
    public AlignmentX ButtonsAlignment;
    public string logoImageName;
    public string titleImageName;
    public string buttonAlignmentName;
    public Texture2D LogoImage;
    public Texture2D TitleImage;
    public bool LogoShouldClipOverdraw = true;
    public LogoCustomConnectDisplayDaemon(Computer c, OS os, string logoImageName, string titleImageName, bool logoShouldClipoverdraw, string buttonAlignment)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override void DrawAdminDisplay(Rectangle bounds, SpriteBatch sb, Computer c)
    public override void DrawNonAdminDisplay(Rectangle dest, SpriteBatch sb)
    public void DrawLogoAndTitle(Rectangle dest, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.LogoDaemon` (class) — decompiled/game-proj/Hacknet/LogoDaemon.cs:8
    public const string FileName = "DisplayText.txt";
    public string LogoImagePath;
    public bool showsTitle;
    public string BodyText;
    public Color TextColor = Color.White;
    public Texture2D LoadedLogo;
    public bool hasTriedToLoadLogo = false;
    public TrailLoadingSpinnerEffect spinner;
    public float timeOnPage = 0f;
    public LogoDaemon(Computer c, OS os, string name, bool showsTitle, string LogoImagePath)
    public override void initFiles()
    public override void navigatedTo()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.MailIcon` (class) — decompiled/game-proj/Hacknet/MailIcon.cs:9
    public static float ALERT_TIME = 2.4f;
    public static float SCALE = 1f;
    public bool isEnabled = true;
    public Texture2D tex;
    public Texture2D texBig;
    public float bigTexScaleMod = 1f;
    public float firstEverAlertMod = 2.7f;
    public Vector2 pos;
    public SoundEffect newMailSound;
    public float alertTimer;
    public bool mailUnchecked;
    public static Color uncheckedMailPulseColor = new Color(110, 110, 110, 0);
    public OS os;
    public MailServer targetServer = null;
    public MailIcon(OS operatingSystem, Vector2 position)
    public void UpdateTargetServer(MailServer server)
    public void Update(float t)
    public void Draw()
    public int getWidth()
    public void connectToMail()
    public void mailSent(string mail, string userTo)
    public void mailReceived(string mail, string userTo)

`Hacknet.MailResponder` (interface) — decompiled/game-proj/Hacknet/MailResponder.cs:3

`Hacknet.MailServer` (class) — decompiled/game-proj/Hacknet/MailServer.cs:13
    public const int TYPE = 0;
    public const int SENDER = 1;
    public const int SUBJECT = 2;
    public const int BODY = 3;
    public const int ATTACHMENT = 4;
    public const int UNREAD = 0;
    public const int READ = 1;
    public const float MISSION_COMPLETE_FLASH_TIME = 3f;
    public static bool shouldGenerateJunk = true;
    public Folder root;
    public Folder accounts;
    public Folder userFolder;
    public List<int> rootPath;
    public List<int> accountsPath;
    public UserDetail user;
    public FileEntry selectedEmail;
    public int state;
    public int inboxPage = 0;
    public int totalPagesDetected = -1;
    public static string emailSplitDelimiter = "@*&^#%@)_!_)*#^@!&*)(#^&\n";
    public static string[] emailSplitDelims = new string[1] { emailSplitDelimiter };
    public static string[] spaceDelim = new string[1] { "#%#" };
    public Color themeColor = new Color(125, 5, 6);
    public Color textColor;
    public string[] emailData;
    public bool shouldGenerateJunkEmails = true;
    public Color evenLine;
    public Color oddLine;
    public Color senderDarkeningColor;
    public Color seperatorLineColor;
    public List<MailResponder> responders;
    public Texture2D panel;
    public Texture2D corner;
    public Texture2D unopenedIcon;
    public Rectangle panelRect;
    public bool missionIncompleteReply;
    public static SoundEffect buttonSound;
    public ScrollableSectionedPanel sectionedPanel;
    public Action setupComplete;
    public List<string> emailReplyStrings = new List<string>();
    public bool addingNewReplyString = false;
    public MailServer(Computer c, string name, OS os)
    public override void initFiles()
    public override void loadInit()
    public void addJunkEmails(Folder f)
    public void initFilesystem()
    public void setThemeColor(Color newThemeColor)
    public void addResponder(MailResponder resp)
    public void removeResponder(MailResponder resp)
    public override void navigatedTo()
    public void viewInbox(UserDetail newUser)
    public void addMail(string mail, string userTo)
    public bool MailWithSubjectExists(string userName, string mailSubject)
    public override void userAdded(string name, string pass, byte type)
    public override string getSaveString()
    public virtual void drawTopBar(Rectangle bounds, SpriteBatch sb)
    public virtual void drawBackingGradient(Rectangle boundsTo, SpriteBatch sb)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public int GetRenderTextHeight()
    public int DrawMailMessageText(Rectangle textBounds, SpriteBatch sb, string[] text)
    public void doEmailViewerDisplay(Rectangle bounds, SpriteBatch sb)
    public void DrawButtonGlow(Vector2 dpos, Vector2 labelSize)
    public virtual void doInboxHeader(Rectangle bounds, SpriteBatch sb)
    public void doInboxDisplay(Rectangle bounds, SpriteBatch sb)
    public void doRespondDisplay(Rectangle bounds, SpriteBatch sb)
    public void AddSentEmailRecordFileForMissionCompletion(ActiveMission mission, List<string> additionalDetails)
    public bool attemptCompleteMission(ActiveMission mission)
    public void doLoginDisplay(Rectangle bounds, SpriteBatch sb)
    public void forceLogin(string username, string pass)
    public new static bool validUser(byte type)
    public static string generateEmail(string subject, string body, string sender)
    public static string generateEmail(string subject, string body, string sender, List<string> attachments)
    public static string getSubject(string mail)
    public static string cleanString(string s)
    public static string minimalCleanString(string s)

`Hacknet.MailServer.EMailData` (struct) — decompiled/game-proj/Hacknet/MailServer.cs:15
    public string sender = sendr;
    public string body = bod;
    public string subject = subj;
    public List<string> attachments = _attachments;

`Hacknet.MainDisplayOverrideEXE` (interface) — decompiled/game-proj/Hacknet/MainDisplayOverrideEXE.cs:6

`Hacknet.MainMenu` (class) — decompiled/game-proj/Hacknet/MainMenu.cs:17
    public const int CharCountForTestPassedMessage = 950;
    public static string OSVersion = "v5.069";
    public static string AccumErrors = "";
    public static Color buttonColor;
    public static Color exitButtonColor;
    public SpriteFont titleFont;
    public Color titleColor;
    public bool canLoad = false;
    public bool hasSentErrorEmail = false;
    public int framecount = 0;
    public HexGridBackground hexBackground;
    public string testSuiteResult = null;
    public SavefileLoginScreen loginScreen = new SavefileLoginScreen();
    public MainMenuState State = MainMenuState.Normal;
    public AttractModeMenuScreen attractModeScreen = new AttractModeMenuScreen();
    public ExtensionsMenuScreen extensionsScreen = new ExtensionsMenuScreen();
    public bool NextStartedGameShouldBeDLCAccelerated = false;
    public MainMenu()
    public override void LoadContent()
    public void HookUpCreationEvents()
    public static void CreateNewAccountForExtensionAndStart(string username, string pass, ScreenManager sman, GameScreen currentScreen = null, ExtensionsMenuScreen extensionsScreen = null)
    public void UpdateUIForSaveCorruption(string saveName, Exception ex)
    public void UpdateUIForSaveMissing(string saveName, Exception ex)
    public void UpdateUIForSaveCreationFailed(Exception ex)
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public override void HandleInput(InputState input)
    public static void resetOS()
    public override void Draw(GameTime gameTime)
    public bool DrawBackgroundAndTitle()
    public void DrawLoginScreen(Rectangle dest, bool needsNewUser = false)
    public void drawDemoModeButtons(bool canRun)
    public void drawTestingMainMenuButtons(bool canRun)
    public void StartFullDLCTest()
    public void drawMainMenuButtons(bool canRun)

`Hacknet.MainMenu.MainMenuState` (enum) — decompiled/game-proj/Hacknet/MainMenu.cs:19

`Hacknet.MarkovTextDaemon` (class) — decompiled/game-proj/Hacknet/MarkovTextDaemon.cs:11
    public Corpus corpus;
    public bool CorpusBeingLoaded = false;
    public bool SentenceBeingGenerated = false;
    public bool LastSentenceWasError = false;
    public string corpusFolderPath;
    public float loadProgress = 0f;
    public int DisplayWidth = 300;
    public List<string> GeneratedSentences = new List<string>();
    public ScrollableTextRegion TextDisplay;
    public MarkovTextDaemon(Computer c, OS os, string name, string corpusLoadPath)
    public override void navigatedTo()
    public void CorpusLoaded(Corpus c)
    public void LoadProgressUpdated(float progress, string progressMessage)
    public void AddNewSentence(string sentence)
    public void PrepateToClearCorpusOnNavigateAway()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawLoading(Rectangle dest, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.MedicalDatabaseDaemon` (class) — decompiled/game-proj/Hacknet/MedicalDatabaseDaemon.cs:12
    public const string FOLDER_NAME = "Medical";
    public const float SEARCH_TIME = 1.6f;
    public Folder recordsFolder;
    public MedicalDatabaseState state = MedicalDatabaseState.MainMenu;
    public float totalTimeThisState = 1f;
    public float elapsedTimeThisState = 0f;
    public string searchName = "";
    public string emailRecipientAddress = "";
    public FileEntry currentFile = null;
    public FileMedicalRecord currentRecord = null;
    public ScrollableSectionedPanel displayPanel;
    public string errorMessage = LocaleTerms.Loc("UNKNOWN ERROR");
    public Color theme_deep = new Color(8, 78, 90);
    public Color theme_strong = new Color(64, 157, 174);
    public Color theme_light = new Color(165, 237, 249);
    public Color theme_back = new Color(20, 20, 20);
    public Texture2D logo;
    public GridSpot[,] themeGrid = new GridSpot[20, 100];
    public MedicalDatabaseDaemon(Computer c, OS os)
    public override void initFiles()
    public override void loadInit()
    public override string getSaveString()
    public void ResetThemeGrid()
    public override void navigatedTo()
    public void ResetGridPoint(int x, int y)
    public void LookupEntry()
    public void UpdateStates(float t)
    public void SendReportEmail(FileMedicalRecord record, string emailAddress)
    public void updateGrid()
    public void drawGrid(Rectangle bounds, SpriteBatch sb, int width)
    public void DrawError(Rectangle bounds, SpriteBatch sb)
    public void DrawSendReport(Rectangle bounds, SpriteBatch sb)
    public void DrawAbout(Rectangle bounds, SpriteBatch sb)
    public void DrawEntry(Rectangle bounds, SpriteBatch sb)
    public void DrawMenu(Rectangle bounds, SpriteBatch sb)
    public void DrawNoAdminMenuSection(Rectangle bounds, SpriteBatch sb)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawMessage(string msg, bool big, SpriteBatch sb, Rectangle dest)
    public void DrawMessage(string msg, bool big, SpriteBatch sb, Rectangle dest, Color back, Color front)
    public void DrawMessageBot(string msg, bool big, SpriteBatch sb, Rectangle dest, Color back, Color front)

`Hacknet.MedicalDatabaseDaemon.FileMedicalRecord` (class) — decompiled/game-proj/Hacknet/MedicalDatabaseDaemon.cs:14
    public const string DELIMITER = "\n-----------------\n";
    public static string[] SPLIT_DELIM = new string[1] { "\n-----------------\n" };
    public string Firstname;
    public string Lastname;
    public string record;
    public bool IsMale = true;
    public DateTime DOB;
    public FileMedicalRecord()
    public FileMedicalRecord(Person p)
    public string MedicalRecordToReport(MedicalRecord rec)
    public override string ToString()
    public string ToEmailString()
    public string GetFileName()
    public static bool RecordFromString(string rec, out FileMedicalRecord record)

`Hacknet.MedicalDatabaseDaemon.GridSpot` (struct) — decompiled/game-proj/Hacknet/MedicalDatabaseDaemon.cs:99
    public float from;
    public float to;
    public float time;
    public float totalTime;

`Hacknet.MedicalDatabaseDaemon.MedicalDatabaseState` (enum) — decompiled/game-proj/Hacknet/MedicalDatabaseDaemon.cs:85

`Hacknet.MedicalPortExe` (class) — decompiled/game-proj/Hacknet/MedicalPortExe.cs:5
    public const float RUNTIME = 22f;
    public const float COMPLETE_TIME = 2f;
    public float elapsedTime = 0f;
    public float sucsessTimer = 0f;
    public Color[] displayData;
    public bool[] CompletedIndexes;
    public Color DarkBaseColor = new Color(5, 0, 36);
    public Color LightBaseColor = new Color(39, 32, 83);
    public Color DarkFinColor = new Color(179, 25, 94);
    public Color LightFinColor = new Color(225, 14, 79);
    public MedicalPortExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public void InitializeDisplay()
    public void UpdateDisplay()
    public void Complete()
    public override void Draw(float t)

`Hacknet.MedicalRecord` (class) — decompiled/game-proj/Hacknet/MedicalRecord.cs:8
    public List<string> Visits = new List<string>();
    public List<string> Perscriptions = new List<string>();
    public List<string> Allergies = new List<string>();
    public int Height = 172;
    public string Notes = "N/A";
    public string BloodType = "AB";
    public DateTime DateofBirth;
    public static List<string> Allergants = null;
    public MedicalRecord()
    public MedicalRecord(WorldLocation location, DateTime dob)
    public void LoadStatics()
    public void AddAllergies()
    public void AddRandomVists(int daysOld, WorldLocation loc)
    public override string ToString()
    public static MedicalRecord Load(XmlReader rdr, WorldLocation location, DateTime dob)
    public string GetCSVFromList(List<string> list)

`Hacknet.MemoryContents` (class) — decompiled/game-proj/Hacknet/MemoryContents.cs:9
    public const string EncryptionPass = "19474-217316293";
    public const string FileHeader = "MEMORY_DUMP : FORMAT v1.22 ----------\n\n";
    public List<string> DataBlocks = new List<string>();
    public List<string> CommandsRun = new List<string>();
    public List<KeyValuePair<string, string>> FileFragments = new List<KeyValuePair<string, string>>();
    public List<string> Images = new List<string>();
    public string GetSaveString()
    public static MemoryContents Deserialize(XmlReader rdr)
    public string GetCompactSaveString()
    public static string ReExpandSaveString(string save)
    public string GetEncodedFileString()
    public static MemoryContents GetMemoryFromEncodedFileString(string data)
    public string TestEqualsWithErrorReport(MemoryContents other)

`Hacknet.MemoryDumpDownloader` (class) — decompiled/game-proj/Hacknet/MemoryDumpDownloader.cs:8
    public const float DownloadTime = 6f;
    public const float FailTime = 2f;
    public const float ExitTime = 5f;
    public bool DownloadComplete = false;
    public bool DidFail = false;
    public string savedFileName = "Unknown";
    public float elapsedTime = 0f;
    public Computer target;
    public MemoryDumpDownloader(Rectangle location, OS operatingSystem)
    public static MemoryDumpDownloader GenerateInstanceOrNullFromArguments(string[] args, Rectangle location, object osObj, Computer target)
    public override void Update(float t)
    public void DownloadMemoryDump()
    public string getReportFilename(string s)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.MemoryDumpInjector` (class) — decompiled/game-proj/Hacknet/MemoryDumpInjector.cs:6
    public static void InjectMemory(string memoryFilepath, object computer)

`Hacknet.MemoryForensicsExe` (class) — decompiled/game-proj/Hacknet/MemoryForensicsExe.cs:12
    public const float FileReadTime = 2f;
    public const float BaseProcessingTime = 2f;
    public MemForensicsState State = MemForensicsState.ReadingFile;
    public string ErrorMessage = "Unknown Error";
    public float timeInCurrentState = 0f;
    public float processingTimeThisBatch = 4f;
    public string filenameLoaded = "UNKNOWN";
    public MemoryContents ActiveMem = null;
    public ShiftingGridEffect GridEffect = new ShiftingGridEffect();
    public Color ThemeColorMain = new Color(30, 59, 44, 0);
    public Color ThemeColorLight = new Color(89, 181, 183, 0);
    public Color ThemeColorDark = new Color(19, 51, 35, 0);
    public List<string> OutputData = new List<string>();
    public bool IsDisplayingImages = false;
    public List<Texture2D> OutputTextures = new List<Texture2D>();
    public string AnnouncementData = "Unknown";
    public Vector2 PanelScroll = Vector2.Zero;
    public FlyoutEffect flyoutEffect;
    public bool DisplayOverrideIsActive { get; set; }
    public MemoryForensicsExe(Rectangle location, OS operatingSystem, string[] p)
    public static MemoryForensicsExe GenerateInstanceOrNullFromArguments(Rectangle location, OS os, string[] p)
    public void LoadFile(string filename, Folder f)
    public override void Update(float t)
    public void StartLoadingInTexturesForMemory()
    public override void Completed()
    public override void Draw(float t)
    public void MoveToProcessing()
    public void RenderMainDisplay(Rectangle dest, SpriteBatch sb)
    public Rectangle RenderMainDisplayHeaders(Rectangle dest, SpriteBatch sb)
    public void DrawMainStateBackground(Rectangle dest, SpriteBatch sb)
    public void RenderMenuMainState(Rectangle dest, SpriteBatch sb)
    public void RenderResultsDisplayMainState(Rectangle dest, SpriteBatch sb, bool isProcessing)

`Hacknet.MemoryForensicsExe.MemForensicsState` (enum) — decompiled/game-proj/Hacknet/MemoryForensicsExe.cs:14

`Hacknet.MessageBoardDaemon` (class) — decompiled/game-proj/Hacknet/MessageBoardDaemon.cs:10
    public const int THREAD_PREVIEW_HEIGHT = 415;
    public static Dictionary<MessageBoardPostImage, Texture2D> Images;
    public static Color UsernameColor = new Color(17, 119, 67);
    public static Color ImplicationColor = new Color(56, 184, 131);
    public string boardsListingString = "[a/b/c/d/e/f/g/gif/h/hr/k/m/o/p/r/s/t/u/v/vg/vr/w/wg][i/ic][r9k][s4s][cm/hm/lgbt/y][3/adv/an/asp/cgl/ck/co/diy/fa/fit/gd/hc/int/jp/lit/mlp/mu/n/out/po/pol/sci/soc/sp/tg/toy/trv/tv/vp/wsg/x][rs]";
    public string BoardName = "/el/ - " + LocaleTerms.Loc("Digital Security");
    public List<string> ThreadsToAdd = new List<string>();
    public MessageBoardState state = MessageBoardState.Board;
    public ScrollableSectionedPanel threadsPanel;
    public MessageBoardThread viewingThread;
    public Vector2 ThreadScrollPosition = Vector2.Zero;
    public int CurrentThreadHeight = 100;
    public Folder rootFolder;
    public Folder threadsFolder;
    public Action<string, string> MessageAdded;
    public MessageBoardDaemon(Computer c, OS os)
    public override void initFiles()
    public override void loadInit()
    public void SubscribeToAlertActionFroNewMessage(Action<string, string> act)
    public void UnSubscribeToAlertActionFroNewMessage(Action<string, string> act)
    public bool ShouldDisplayNotifications()
    public string GetName()
    public override string getSaveString()
    public void AddThread(string threadData)
    public MessageBoardThread ParseThread(string threadData)
    public void ViewThread(MessageBoardThread thread, int width, int margin, int ImageSize, int headerOffset)
    public override void navigatedTo()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawHeader(SpriteBatch sb, Rectangle dest)
    public void DrawFullThreadView(SpriteBatch sb, MessageBoardThread thread, Rectangle dest)
    public void DrawThread(MessageBoardThread thread, SpriteBatch sb, Rectangle bounds, bool isPreview = false)
    public int MeasurePost(MessageBoardPost post, int width, int margin, int ImageSize, int postHeaderOffset)
    public void DrawPost(string text, MessageBoardPostImage img, Rectangle dest, int margin, int ImageSize, int postheaderOffset, SpriteBatch sb, SpriteFont font)
    public List<MessageBoardPost> GetLastPostsToFitHeight(MessageBoardThread thread, int height, int width, int margin, int ImageSize, int PostHeaderOffset, int ThreadFooterSize, int maxOPSize = int.MaxValue)

`Hacknet.MessageBoardDaemon.MessageBoardState` (enum) — decompiled/game-proj/Hacknet/MessageBoardDaemon.cs:12

`Hacknet.MessageBoardPost` (struct) — decompiled/game-proj/Hacknet/MessageBoardPost.cs:3
    public string text;
    public MessageBoardPostImage img;

`Hacknet.MessageBoardPostImage` (enum) — decompiled/game-proj/Hacknet/MessageBoardPostImage.cs:3

`Hacknet.MessageBoardThread` (struct) — decompiled/game-proj/Hacknet/MessageBoardThread.cs:5
    public string id;
    public List<MessageBoardPost> posts;

`Hacknet.MessageBoxScreen` (class) — decompiled/game-proj/Hacknet/MessageBoxScreen.cs:9
    public static float HEIGHT_BUFFER = 20f;
    public string message;
    public Texture2D top;
    public Texture2D mid;
    public Texture2D bottom;
    public Texture2D inputGuide;
    public SpriteFont guideFont;
    public Rectangle contentBounds;
    public Vector2 topLeft;
    public bool hasEscGuide = false;
    public string OverrideAcceptedText = null;
    public string OverrideCancelText = null;
    public EventHandler<PlayerIndexEventArgs> Accepted;
    public EventHandler<PlayerIndexEventArgs> Cancelled;
    public Action AcceptedClicked;
    public Action CancelClicked;
    public event EventHandler<PlayerIndexEventArgs> Accepted;
    public event EventHandler<PlayerIndexEventArgs> Cancelled;
    public MessageBoxScreen(string message)
    public MessageBoxScreen(string message, bool includeUsageText)
    public MessageBoxScreen(string message, bool includesUsageText, bool hasEscPrompt)
    public override void LoadContent()
    public override void HandleInput(InputState input)
    public override void Draw(GameTime gameTime)
    public override void inputMethodChanged(bool usingGamePad)

`Hacknet.MissionFunctions` (class) — decompiled/game-proj/Hacknet/MissionFunctions.cs:9
    public static OS os;
    public static Action<string> ReportErrorInCommand;
    public static void assertOS()
    public static void runCommand(int value, string name)
    public static void MediaPlayer_MediaStateChanged(object sender, EventArgs e)
    public static Computer findComp(string target)

`Hacknet.MissionGenerationParser` (class) — decompiled/game-proj/Hacknet/MissionGenerationParser.cs:3
    public static string Path;
    public static string File;
    public static string Comp;
    public static string Client;
    public static string Target;
    public static string Other;
    public static void init()
    public static string parse(string input)

`Hacknet.MissionGenerator` (class) — decompiled/game-proj/Hacknet/MissionGenerator.cs:8
    public const int FILE_DELETION = 0;
    public const int WEBSITE_CHANGE = 1;
    public const int TYPES_OF_GENERATED_MISSIONS = 2;
    public static ContentManager content;
    public static int generationCount = 0;
    public static List<List<string>> MissionLists;
    public static bool customKeysWereSet = false;
    public static string customFileData = null;
    public static void init(ContentManager contentManager)
    public static object generate(int secutiryLevel)
    public static object generateComputer(int secLevel, string name = null)
    public static void setMissionGenerationKeys(Dictionary<string, string> keys)
    public static Computer addFileDeletionRequirements(Computer c, OS os)
    public static Computer addWebsiteChangeRequirements(Computer c, OS os)

`Hacknet.MissionHubServer` (class) — decompiled/game-proj/Hacknet/MissionHubServer.cs:14
    public const string ROOT_FOLDERNAME = "ContractHub";
    public const string CONFIG_FILENAME = "settings.sys";
    public const string CRITICAL_FILE_FILENAME = "net64.sys";
    public const double BUTTON_TRANSITION_OFFSET = 40.0;
    public const float TRANSITION_TIME = 0.3f;
    public const double TRANSITION_ELEMENT_INCREASE = 0.1;
    public Color themeColor = Color.PaleTurquoise;
    public Color themeColorBackground = new Color(10, 15, 25, 200);
    public Color themeColorLine = new Color(20, 25, 45, 200);
    public Folder root;
    public Folder missionsFolder;
    public Folder usersFolder;
    public Folder listingsFolder;
    public Folder listingArchivesFolder;
    public string groupName = "UNKNOWN";
    public int contractRegistryNumber = 256;
    public Dictionary<string, ActiveMission> listingMissions = new Dictionary<string, ActiveMission>();
    public Texture2D decorationPanel;
    public Texture2D decorationPanelSide;
    public Texture2D lockIcon;
    public string MissionSourceFolderPath = "Content/Missions/MainHub/FirstSet/";
    public BarcodeEffect barcode;
    public ThinBarcode thinBarcodeTop;
    public ThinBarcode thinBarcodeBot;
    public bool allowAbandon = true;
    public HubState state = HubState.Welcome;
    public string activeUserName = "UNKNOWN USER";
    public DateTime activeUserLoginTime = DateTime.Now;
    public float screenTransition = 0f;
    public int selectedElementIndex = 0;
    public int userListPageNumber = 0;
    public int missionListPageNumber = 0;
    public int missionListDisplayed = 1;
    public float timeSpentInLoading = 0f;
    public MissionHubServer(Computer c, string serviceName, string group, OS _os)
    public override void initFiles()
    public void populateUserList()
    public void loadInitialContracts()
    public void AddMissionToListings(string missionFilename, int desiredIndex = -1)
    public void RemoveMissionFromListings(string missionFilename)
    public void addMission(ActiveMission mission, bool insertAtTop = false, bool preventRegistryNumberChange = false, int desiredInsertionIndex = -1)
    public FileEntry generateConfigFile()
    public void loadFromConfigFileData(string config)
    public string getDataFromConfigLine(string line, string sentinel = "= ")
    public override void loadInit()
    public void loadListingMissionsFromFiles()
    public override string getSaveString()
    public void initializeUsers()
    public void addUser(UserDetail newUser)
    public string generateUserRank(string username)
    public string generateUserState(string username)
    public int GetNumberOfAvaliableMissions()
    public void CheckForGameStateIssuesAndFix()
    public string getIDStringForContractFile(FileEntry file)
    public override void navigatedTo()
    public override void loginGoBack()
    public override void userLoggedIn()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void updateScreenTransition()
    public int getTransitionOffset(int position)
    public void doMenuScreen(Rectangle bounds, SpriteBatch sb)
    public void doCancelContractScreen(Rectangle bounds, SpriteBatch sb)
    public void doListingScreen(Rectangle bounds, SpriteBatch sb)
    public void drawMissionEntry(Rectangle bounds, SpriteBatch sb, ActiveMission mission, int index)
    public Rectangle doListingScreenBackground(Rectangle bounds, SpriteBatch sb)
    public void doContractPreviewScreen(Rectangle bounds, SpriteBatch sb)
    public void doUserListScreen(Rectangle bounds, SpriteBatch sb)
    public void acceptMission(ActiveMission mission, int index, string id)
    public void drawWelcomeScreen(Rectangle bounds, SpriteBatch sb)
    public void doBarcodeEffect(Rectangle bounds, SpriteBatch sb)
    public void doLoggedInScreenDetailing(Rectangle bounds, SpriteBatch sb)
    public void doBaseBarcodeEffect(Rectangle bounds, SpriteBatch sb)

`Hacknet.MissionHubServer.HubState` (enum) — decompiled/game-proj/Hacknet/MissionHubServer.cs:16

`Hacknet.MissionListingServer` (class) — decompiled/game-proj/Hacknet/MissionListingServer.cs:12
    public const int NEED_LOGIN = 0;
    public const int BOARD = 1;
    public const int MESSAGE = 2;
    public const int LOGIN = 3;
    public string groupName;
    public string listingTitle;
    public Color themeColor;
    public Texture2D topBar;
    public Texture2D corner;
    public Texture2D logo;
    public Rectangle panelRect;
    public Rectangle logoRect;
    public Folder root;
    public Folder missionFolder;
    public Folder closedMissionsFolder;
    public List<int> rootPath;
    public List<int> missionFolderPath;
    public FileEntry sysFile;
    public int targetIndex;
    public ScrollableTextRegion TextRegion;
    public bool missionAssigner = false;
    public bool isPublic;
    public int state;
    public bool NeedsCustomFolderLoad = false;
    public string CustomFolderLoadPath = null;
    public string IconReloadPath = null;
    public string ArticleFolderPath = null;
    public bool HasCustomColor = false;
    public List<ActiveMission> missions;
    public List<List<ActiveMission>> branchMissions = new List<List<ActiveMission>>();
    public MissionListingServer(Computer c, string serviceName, string group, OS _os, bool _isPublic = false, bool _isAssigner = false)
    public MissionListingServer(Computer c, string serviceName, string iconPath, string articleFolderPath, Color themeColor, OS _os, bool _isPublic = false, bool _isAssigner = false)
    public override void initFiles()
    public override void loadInit()
    public override string getSaveString()
    public void addMisison(ActiveMission m, bool injectToTop = false)
    public void removeMission(string missionPath)
    public void removeMission(int index)
    public void initFilesystem()
    public void addListingsForGroup()
    public override void navigatedTo()
    public void ProgressionSaveFixHacks()
    public override void loginGoBack()
    public override void userLoggedIn()
    public bool hasSysfile()
    public bool hasListingFile(string name)
    public void drawTopBar(Rectangle bounds, SpriteBatch sb)
    public override void draw(Rectangle bounds, SpriteBatch sb)

`Hacknet.MissionSerializer` (class) — decompiled/game-proj/Hacknet/MissionSerializer.cs:6
    public const string MISSION_FILE_DELIMITER = "  #%#\n";
    public static string generateMissionFile(object mission_obj, int contractRegistryNumber = 0, string GroupName = "CSEC", string Tag = null)
    public static object restoreMissionFromFile(string data, out int contractRegistryNumber)
    public static object restoreMissionFromFile(string data, out int contractRegistryNumber, out string Tag)
    public static string encodeString(string s)
    public static string decodeString(string s)
    public static string getDataFromConfigLine(string line, string sentinel = "= ")

`Hacknet.Module` (class) — decompiled/game-proj/Hacknet/Module.cs:7
    public static int PANEL_HEIGHT = 15;
    public Rectangle bounds;
    public SpriteBatch spriteBatch;
    public OS os;
    public string name = "Unknown";
    public bool visible = true;
    public static Rectangle tmpRect;
    public Rectangle Bounds
    public Module(Rectangle location, OS operatingSystem)
    public virtual void LoadContent()
    public virtual void Update(float t)
    public virtual void PreDrawStep()
    public virtual void Draw(float t)
    public virtual void PostDrawStep()
    public void drawFrame()

`Hacknet.MSRandom` (class) — decompiled/game-proj/Hacknet/MSRandom.cs:5
    public const int MBIG = int.MaxValue;
    public const int MSEED = 161803398;
    public const int MZ = 0;
    public int inext;
    public int inextp;
    public int[] SeedArray = new int[56];
    public MSRandom()
    public MSRandom(int Seed)
    public virtual double Sample()
    public int InternalSample()
    public virtual int Next()
    public double GetSampleForLargeRange()
    public virtual int Next(int minValue, int maxValue)
    public virtual int Next(int maxValue)
    public virtual double NextDouble()
    public virtual void NextBytes(byte[] buffer)

`Hacknet.Multiplayer` (class) — decompiled/game-proj/Hacknet/Multiplayer.cs:7
    public static int generatedComputerCount = 0;
    public static int PORT = 3020;
    public static char[] delims = new char[2] { ' ', '\n' };
    public static char[] specSplitDelims = new char[1] { '#' };
    public static void parseInputMessage(string message, OS os)
    public static Computer getComp(string ip, OS os)

`Hacknet.MultiplayerGameOverScreen` (class) — decompiled/game-proj/Hacknet/MultiplayerGameOverScreen.cs:8
    public Rectangle contentRect;
    public int screenWidth;
    public int screenHeight;
    public bool isWinner;
    public Color winBacking;
    public Color winPattern;
    public Color lossBacking;
    public Color lossPattern;
    public SpriteFont font;
    public MultiplayerGameOverScreen(bool winner)
    public override void LoadContent()
    public override void HandleInput(InputState input)
    public override void Draw(GameTime gameTime)

`Hacknet.MultiplayerLobby` (class) — decompiled/game-proj/Hacknet/MultiplayerLobby.cs:14
    public Rectangle fullscreen;
    public string destination = "192.168.1.1";
    public string myIP = "?";
    public string externalIP = "Loading...";
    public static List<string> allLocalIPs = new List<string>();
    public TcpListener listener;
    public Thread listenerThread;
    public byte[] buffer;
    public ASCIIEncoding encoder;
    public bool shouldAddServer = false;
    public TcpClient client;
    public NetworkStream clientStream;
    public bool isConnecting = false;
    public List<string> messages;
    public string chatIP = "";
    public string messageString = "Test Message";
    public TcpClient chatclient;
    public NetworkStream chatclientStream;
    public Color darkgrey = new Color(9, 9, 9);
    public Color dark_ish_gray = new Color(20, 20, 20);
    public bool connectingToServer = false;
    public MultiplayerLobby()
    public override void LoadContent()
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public override void HandleInput(InputState input)
    public override void Draw(GameTime gameTime)
    public void drawConnectingGui()
    public void doGui()
    public void ConnectToServer(string ip)
    public void chatToServer(string ip, string msg)
    public void DoAcceptTcpClientCallback(IAsyncResult ar)
    public void listenForConnections()
    public static string getLocalIP()
    public void getExternalIPwithPF()
    public void getExternalIP()

`Hacknet.MusicManager` (class) — decompiled/game-proj/Hacknet/MusicManager.cs:9
    public const int PLAYING = 0;
    public const int FADING_OUT = 1;
    public const int FADING_IN = 2;
    public const int STOPPED = 3;
    public static float DEFAULT_FADE_TIME = (Settings.isConventionDemo ? 0.5f : 2f);
    public static float FADE_TIME = DEFAULT_FADE_TIME;
    public static Song curentSong;
    public static Song nextSong;
    public static string nextSongName;
    public static bool isPlaying;
    public static bool isMuted;
    public static string currentSongName;
    public static float destinationVolume;
    public static float fadeVolume;
    public static float fadeTimer = 0f;
    public static int state;
    public static ContentManager contentManager;
    public static bool initialized = false;
    public static bool dataLoadedFromOutsideFile = false;
    public static Dictionary<string, Song> loadedSongs = new Dictionary<string, Song>();
    public static bool IsMediaPlayerCrashDisabled = false;
    public static void init(ContentManager content)
    public static void playSong()
    public static void toggleMute()
    public static void setIsMuted(bool muted)
    public static void stop()
    public static float getVolume()
    public static void setVolume(float volume)
    public static void playSongImmediatley(string songname)
    public static void loadAsCurrentSong(string songname)
    public static void loadAsCurrentSongUnsafe(string songname)
    public static void transitionToSong(string songName)
    public static void loadSong()
    public static void Update(float t)

`Hacknet.NameGenerator` (class) — decompiled/game-proj/Hacknet/NameGenerator.cs:5
    public static List<string> main;
    public static List<string> postfix;
    public static void init()
    public static string generateName()
    public static string[] generateCompanyName()
    public static string getRandomMain()

`Hacknet.Neopal` (class) — decompiled/game-proj/Hacknet/Neopal.cs:5
    public PetType Type;
    public string Name;
    public int DaysSinceFed;
    public byte CombatRating;
    public float Happiness;
    public string Identifier;
    public static string[] PossibleNames;
    public static string GenerateName()
    public static Neopal GeneratePet(bool isActiveUser = false)

`Hacknet.Neopal.PetType` (enum) — decompiled/game-proj/Hacknet/Neopal.cs:7

`Hacknet.NeopalsAccount` (class) — decompiled/game-proj/Hacknet/NeopalsAccount.cs:6
    public string AccountName;
    public long NeoPoints;
    public long BankedPoints;
    public string InventoryID;
    public List<Neopal> Pets;
    public static NeopalsAccount GenerateAccount(string handle, bool isActiveUser = false)
    public override string ToString()

`Hacknet.NetmapOrganizerExe` (class) — decompiled/game-proj/Hacknet/NetmapOrganizerExe.cs:9
    public bool AllowChaos = false;
    public bool DisplayOverrideIsActive { get; set; }
    public NetmapOrganizerExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)
    public void RenderMainDisplay(Rectangle dest, SpriteBatch sb)

`Hacknet.NetmapSortingAlgorithm` (enum) — decompiled/game-proj/Hacknet/NetmapSortingAlgorithm.cs:3

`Hacknet.NetmapSortingAlgorithms` (class) — decompiled/game-proj/Hacknet/NetmapSortingAlgorithms.cs:6
    public static Vector2 GetNodePosition(NetmapSortingAlgorithm algorithm, float mapWidth, float mapHeight, Computer node, int nodeIndex, int totalNodes, int totalRevealedNodes, OS os)
    public static void ClipToMargins(float x, float y, int revealed, out float xout, out float yout)

`Hacknet.NetworkMap` (class) — decompiled/game-proj/Hacknet/NetworkMap.cs:10
    public static int NODE_SIZE = 26;
    public static float ADMIN_CIRCLE_SCALE = 0.62f;
    public static float PULSE_DECAY = 0.5f;
    public static float PULSE_FREQUENCY = 0.8f;
    public List<Corporation> corporations;
    public List<Computer> nodes;
    public List<int> visibleNodes;
    public Texture2D circle;
    public Texture2D circleOutline;
    public Texture2D adminCircle;
    public Texture2D nodeCircle;
    public Texture2D adminNodeCircle;
    public Texture2D nodeGlow;
    public Texture2D homeNodeCircle;
    public Texture2D targetNodeCircle;
    public Texture2D assetServerNodeOverlay;
    public string label;
    public Vector2 circleOrigin;
    public float rotation = 0f;
    public float pulseFade = 1f;
    public float pulseTimer = PULSE_FREQUENCY;
    public ConnectedNodeEffect nodeEffect;
    public ConnectedNodeEffect adminNodeEffect;
    public bool DimNonConnectedNodes = false;
    public NetmapSortingAlgorithm SortingAlgorithm = NetmapSortingAlgorithm.Scatter;
    public Computer mailServer;
    public Computer academicDatabase;
    public Computer lastAddedNode;
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public void CleanVisibleListofDuplicates()
    public string getSaveString()
    public string getVisibleNodesString()
    public void load(XmlReader reader)
    public void loadAssignGameNodes()
    public List<Corporation> generateCorporations()
    public List<Computer> generateNetwork(OS os)
    public List<Computer> generateSPNetwork(OS os)
    public List<Computer> generateGameNodes()
    public FileEntry GetProgramForNum(int num)
    public List<Computer> generateDemoNodes()
    public void discoverNode(Computer c)
    public void discoverNode(string cName)
    public Vector2 getRandomPosition()
    public Vector2 generatePos()
    public bool collides(Vector2 location, float minSeperation = -1f)
    public void randomizeNetwork()
    public void doGui(float t)
    public Vector2 GetNodeDrawPosDebug(Vector2 nodeLocation)
    public Vector2 GetNodeDrawPos(Computer node)
    public Vector2 GetNodeDrawPos(Computer node, int nodeIndex)
    public static string generateRandomIP()
    public void drawLine(Vector2 origin, Vector2 dest, Vector2 offset)

`Hacknet.NotesDumperExe` (class) — decompiled/game-proj/Hacknet/NotesDumperExe.cs:5
    public static void RunNotesDumperExe(string[] args, object osObj, Computer target)

`Hacknet.NotesExe` (class) — decompiled/game-proj/Hacknet/NotesExe.cs:10
    public const float RAM_CHANGE_PS = 350f;
    public const int BASE_PANEL_HEIGHT = 22;
    public const float NO_MEMORY_WARNING_TIME = 4f;
    public const string NotesSaveFilename = "Notes.txt";
    public const string NotesReopenOnLoadFile = "Notes_Reopener.bat";
    public const string NotesSaveFileDelimiter = "\n\n----------\n\n";
    public static Texture2D crossTexture;
    public static Texture2D circleTexture;
    public static int noteBaseCost = 8;
    public static int noteCostPerLine = 14;
    public List<string> notes = new List<string>();
    public int targetRamUse = 100;
    public new int baseRamCost = 100;
    public bool gettingNewNote = false;
    public float MemoryWarningFlashTime = 0f;
    public NotesExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Killed()
    public void DisplayOutOfMemoryWarning()
    public void RemoveReopnener()
    public static bool NoteExists(string note, OS os)
    public static void AddNoteToOS(string note, OS os, bool isRecursiveSelfAdd = false)
    public override void Update(float t)
    public void AddNote(string note)
    public bool HasNote(string note)
    public void LoadNotesFromDrive()
    public void SaveNotesToDrive()
    public void recalcualteRamCost()
    public override void Draw(float t)
    public void DrawBasePanel(Rectangle dest)
    public void DrawNotes(Rectangle dest)

`Hacknet.ObjectSerializer` (class) — decompiled/game-proj/Hacknet/ObjectSerializer.cs:12
    public static string SerializeObject(object o)
    public static string SerializeObject(object o, bool preventOuterTag = false)
    public static string GetTagNameForType(Type t)
    public static bool TypeInstanceOfInterface(Type objType, Type interfaceType)
    public static string GetSerializedStringForPrimative(string tagName, string tagValue)
    public static string SerializeCollection(ICollection o)
    public static bool IsSimple(Type type)
    public static object DeserializeObject(Stream s, Type t)
    public static object DeserializeObject(XmlReader rdr, Type t)
    public static object DeserializeXMLObject(XmlReader rdr, Type t, string overrideExpectedEndTag = null)
    public static object ReadElementContentWithType(XmlReader reader, Type type, IXmlNamespaceResolver resolver)
    public static object CreateObjectOfType(Type targetType)
    public static object DeserializeCollection(XmlReader rdr, Type t, string tagName = "list")
    public static Type GetTypeForName(string name)
    public static object DeepCopy(object input)
    public static object GetValueFromObject(object o, string FieldName)

`Hacknet.OldSystemSaveFileManifest` (class) — decompiled/game-proj/Hacknet/OldSystemSaveFileManifest.cs:8
    public const string FILENAME = "Accounts.txt";
    public const string File_User_Name = "_accountsMeta";
    public const string AccountsDelimiter = "\r\n%------%";
    public static SaveAccountData LastLoggedInUser = new SaveAccountData
    public static List<SaveAccountData> Accounts = new List<SaveAccountData>();
    public static void Load()
    public static void Save()
    public static string GetFilePathForLogin(string username, string pass)
    public static bool CanCreateAccountForName(string username)
    public static string AddUserAndGetFilename(string username, string password)

`Hacknet.OnlineAccount` (class) — decompiled/game-proj/Hacknet/OnlineAccount.cs:3
    public int ID = 0;
    public string Username;
    public string BanStatus;
    public string Notes;
    public override string ToString()

`Hacknet.OnlineWebServerDaemon` (class) — decompiled/game-proj/Hacknet/OnlineWebServerDaemon.cs:6
    public static string DEFAULT_PAGE_URL = "http://www.google.com";
    public string webURL = DEFAULT_PAGE_URL;
    public string lastRequestedURL = null;
    public void setURL(string url)
    public override void LoadWebPage(string url = null)
    public void web_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
    public void instantiateWebPage(string body)
    public override string getSaveString()

`Hacknet.OptionsMenu` (class) — decompiled/game-proj/Hacknet/OptionsMenu.cs:10
    public string[] resolutions;
    public int currentResIndex;
    public string[] fontConfigs;
    public int currentFontIndex = -1;
    public bool resolutionChanged;
    public bool windowed;
    public char[] xArray = new char[1] { 'x' };
    public bool needsApply = false;
    public bool mouseHasBeenReleasedOnThisScreen = false;
    public string originallyActiveLocale = "en-us";
    public string[] localeNames;
    public int currentLocaleIndex = 0;
    public bool startedFromGameContext = false;
    public OptionsMenu()
    public OptionsMenu(bool startedFromGameContext)
    public override void LoadContent()
    public string getCurrentResolution()
    public bool getIfWindowed()
    public void apply()
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public override void HandleInput(InputState input)
    public override void Draw(GameTime gameTime)

`Hacknet.OS` (class) — decompiled/game-proj/Hacknet/OS.cs:25
    public static bool DEBUG_COMMANDS = Settings.debugCommandsEnabled;
    public static float EXE_MODULE_HEIGHT = 250f;
    public static float TCP_STAYALIVE_TIMER = 10f;
    public static float WARNING_FLASH_TIME = 2f;
    public static int TOP_BAR_HEIGHT = 21;
    public static OS currentInstance;
    public static bool WillLoadSave = false;
    public static bool TestingPassOnly = false;
    public bool FirstTimeStartup = Settings.slowOSStartup;
    public bool initShowsTutorial = Settings.initShowsTutorial;
    public bool inputEnabled = false;
    public bool isLoaded = false;
    public Texture2D scanLines;
    public Texture2D cross;
    public Texture2D cog;
    public Texture2D saveIcon;
    public float PorthackCompleteFlashTime = 0f;
    public float MissionCompleteFlashTime = 0f;
    public string locationString = "";
    public string username = "";
    public int totalRam = 800 - (TOP_BAR_HEIGHT + 2) - RamModule.contentStartOffset;
    public int ramAvaliable = 800 - (TOP_BAR_HEIGHT + 2);
    public int currentPID = 0;
    public List<ShellExe> shells;
    public List<string> shellIPs;
    public bool bootingUp = false;
    public CrashModule crashModule;
    public TraceDangerSequence TraceDangerSequence;
    public IntroTextModule introTextModule;
    public GameTime lastGameTime;
    public UserDetail defaultUser;
    public Terminal terminal;
    public NetworkMap netMap;
    public DisplayModule display;
    public RamModule ram;
    public List<Module> modules;
    public IncomingConnectionOverlay IncConnectionOverlay;
    public AircraftInfoOverlay AircraftInfoOverlay;
    public ActiveMission currentMission;
    public List<ActiveMission> branchMissions = new List<ActiveMission>();
    public TraceTracker traceTracker;
    public List<ExeModule> exes;
    public Rectangle topBar;
    public EndingSequenceModule endingSequence;
    public MailIcon mailicon;
    public AudioVisualizer audioVisualizer = new AudioVisualizer();
    public string connectedIP = "";
    public Computer thisComputer = null;
    public Computer connectedComp = null;
    public Computer opponentComputer = null;
    public float warningFlashTimer = 0f;
    public Faction currentFaction;
    public AllFactions allFactions;
    public List<int> navigationPath = new List<int>();
    public ContentManager content;
    public float gameSavedTextAlpha = -1f;
    public string SaveGameUserName = "";
    public string SaveUserAccountName = null;
    public string SaveUserPassword = "password";
    public bool SaveInProgress = false;
    public bool SaveInQueue = false;
    public bool multiplayer = false;
    public TcpClient client;
    public bool isServer = false;
    public char[] trimChars = new char[3] { ' ', '\n', '\0' };
    public byte[] inBuffer;
    public byte[] outBuffer;
    public ASCIIEncoding encoder;
    public Thread listenerThread;
    public bool DestroyThreads = false;
    public NetworkStream netStream;
    public bool canRunContent = true;
    public float stayAliveTimer = TCP_STAYALIVE_TIMER;
    public bool multiplayerMissionLoaded;
    public string opponentLocation = "";
    public string displayCache = "";
    public string getStringCache = "";
    public static double currentElapsedTime = 0.0;
    public static float operationProgress = 0f;
    public static object displayObjectCache = null;
    public bool commandInvalid = false;
    public Rectangle fullscreen;
    public bool validCommand;
    public ActionDelayer delayer;
    public string connectedIPLastFrame = "";
    public string homeNodeID = "entropy00";
    public string homeAssetServerID = "entropy01";
    public bool DisableTopBarButtons = false;
    public bool DisableEmailIcon = false;
    public string LanguageCreatedIn = "en-us";
    public bool HasExitedAndEnded = false;
    public MessageBoxScreen ExitToMenuMessageBox = null;
    public ProgressionFlags Flags = new ProgressionFlags();
    public List<KeyValuePair<string, string>> ActiveHackers = new List<KeyValuePair<string, string>>();
    public float timer;
    public SoundEffect beepSound;
    public int updateErrorCount = 0;
    public int drawErrorCount = 0;
    public bool terminalOnlyMode = false;
    public bool HasLoadedDLCContent = false;
    public bool IsInDLCMode = false;
    public HubServerAlertsIcon hubServerAlertsIcon;
    public string PreDLCFaction = "entropy";
    public string PreDLCVisibleNodesCache = "";
    public bool IsDLCSave = false;
    public bool IsDLCConventionDemo = false;
    public RunnableConditionalActions ConditionalActions = new RunnableConditionalActions();
    public BootCrashAssistanceModule BootAssitanceModule;
    public string GibsonIP;
    public ActiveEffectsUpdater EffectsUpdater = new ActiveEffectsUpdater();
    public bool ShowDLCAlertsIcon = false;
    public List<TrackerDetail> TrackersInProgress = new List<TrackerDetail>();
    public Stream ForceLoadOverrideStream = null;
    public Action postFXDrawActions;
    public Action<float> UpdateSubscriptions;
    public Action traceCompleteOverrideAction;
    public Color defaultHighlightColor = new Color(0, 139, 199, 255);
    public Color defaultTopBarColor = new Color(130, 65, 27);
    public Color warningColor = Color.Red;
    public Color highlightColor = new Color(0, 139, 199, 255);
    public Color subtleTextColor = new Color(90, 90, 90);
    public Color darkBackgroundColor = new Color(8, 8, 8);
    public Color indentBackgroundColor = new Color(12, 12, 12);
    public Color outlineColor = new Color(68, 68, 68);
    public Color lockedColor = new Color(65, 16, 16, 200);
    public Color brightLockedColor = new Color(160, 0, 0);
    public Color brightUnlockedColor = new Color(0, 160, 0);
    public Color unlockedColor = new Color(39, 65, 36);
    public Color lightGray = new Color(180, 180, 180);
    public Color shellColor = new Color(222, 201, 24);
    public Color shellButtonColor = new Color(105, 167, 188);
    public Color moduleColorSolid = new Color(50, 59, 90, 255);
    public Color moduleColorSolidDefault = new Color(50, 59, 90, 255);
    public Color moduleColorStrong = new Color(14, 28, 40, 80);
    public Color moduleColorBacking = new Color(5, 6, 7, 10);
    public Color topBarColor = new Color(0, 139, 199, 255);
    public Color semiTransText = new Color(120, 120, 120, 0);
    public Color terminalTextColor = new Color(213, 245, 255);
    public Color topBarTextColor = new Color(126, 126, 126, 100);
    public Color superLightWhite = new Color(2, 2, 2, 30);
    public Color connectedNodeHighlight = new Color(222, 0, 0, 195);
    public Color exeModuleTopBar = new Color(130, 65, 27, 80);
    public Color exeModuleTitleText = new Color(155, 85, 37, 0);
    public Color netmapToolTipColor = new Color(213, 245, 255, 0);
    public Color netmapToolTipBackground = new Color(0, 0, 0, 150);
    public Color displayModuleExtraLayerBackingColor = new Color(0, 0, 0, 0);
    public Color topBarIconsColor = Color.White;
    public Color BackgroundImageFillColor = Color.Black;
    public bool UseAspectPreserveBackgroundScaling = false;
    public Color AFX_KeyboardMiddle = new Color(77, 145, 255);
    public Color AFX_KeyboardOuter = new Color(105, 138, 255);
    public Color AFX_WordLogo = new Color(105, 138, 255);
    public Color AFX_Other = new Color(0, 178, 255);
    public Color thisComputerNode = new Color(95, 220, 83);
    public Color scanlinesColor = new Color(255, 255, 255, 15);
    public OS()
    public OS(TcpClient socket, NetworkStream stream, bool actingServer, ScreenManager sman)
    public override void LoadContent()
    public void loadMultiplayerMission()
    public void loadMissionNodes()
    public override void UnloadContent()
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public void handleDisconnection()
    public override void HandleInput(InputState input)
    public void drawBackground()
    public void RequestRemovalOfAllPopups()
    public void drawScanlines()
    public void drawModules(GameTime gameTime)
    public void quitGame(object sender, PlayerIndexEventArgs e)
    public override void Draw(GameTime gameTime)
    public void handleUpdateError()
    public void handleDrawError()
    public void endMultiplayerMatch(bool won)
    public void initializeNetwork()
    public void sendMessage(string message)
    public void listenThread()
    public void clientDisconnected()
    public void timerExpired()
    public void loadSaveFile()
    public void saveGame()
    public void threadedSaveExecute(bool preventSaveText = false)
    public void writeSaveGame(string filename)
    public void LoadExtraTitleSaveData(XmlReader rdr)
    public void ReadDLCSaveData(XmlReader rdr)
    public string GetDLCSaveString()
    public void loadTitleSaveData(XmlReader reader)
    public void setMouseVisiblity(bool mouseIsVisible)
    public void loadBranchMissionsSaveData(XmlReader reader)
    public void loadOtherSaveData(XmlReader reader)
    public override void inputMethodChanged(bool usingGamePad)
    public void write(string text)
    public void writeSingle(string text)
    public void runCommand(string text)
    public void execute(string text)
    public void connectedComputerCrashed(Computer c)
    public void thisComputerCrashed()
    public void thisComputerIPReset()
    public void rebootThisComputer()
    public void RefreshTheme()
    public void threadExecute(object threadText)
    public bool hasConnectionPermission(bool admin)
    public void takeAdmin()
    public void takeAdmin(string ip)
    public void warningFlash()
    public Rectangle getExeBounds()
    public void launchExecutable(string exeName, string exeFileData, int targetPort, string[] allParams = null, string originalName = null)
    public void addExe(ExeModule exe)
    public void failBoot()
    public void graphicsFailBoot()
    public void sucsesfulBoot()

`Hacknet.OS.TrackerDetail` (struct) — decompiled/game-proj/Hacknet/OS.cs:27
    public Computer comp;
    public float timeLeft;

`Hacknet.OSTheme` (enum) — decompiled/game-proj/Hacknet/OSTheme.cs:3

`Hacknet.PacificPortExe` (class) — decompiled/game-proj/Hacknet/PacificPortExe.cs:6
    public const float RUN_TIME = 6f;
    public const float IDLE_TIME = 6.2f;
    public float elapsedTime = 0f;
    public int framesFlashed = 0;
    public FlyoutEffect effect;
    public PacificPortExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.PatternDrawer` (class) — decompiled/game-proj/Hacknet/PatternDrawer.cs:8
    public static Texture2D warningStripe;
    public static Texture2D errorTile;
    public static Texture2D binaryTile;
    public static Texture2D thinStripe;
    public static Texture2D star;
    public static Texture2D wipTile;
    public static float time = 0f;
    public static void init(ContentManager content)
    public static void update(float t)
    public static void draw(Rectangle dest, float offset, Color backingColor, Color patternColor, SpriteBatch sb)
    public static void draw(Rectangle dest, float offset, Color backingColor, Color patternColor, SpriteBatch sb, Texture2D tex)

`Hacknet.People` (class) — decompiled/game-proj/Hacknet/People.cs:9
    public const int NUMBER_OF_PEOPLE = 200;
    public const int NUMBER_OF_HACKERS = 10;
    public const int NUMBER_OF_HUB_AGENTS = 22;
    public static List<Person> all;
    public static List<Person> hackers;
    public static List<Person> hubAgents;
    public static string[] maleNames;
    public static string[] femaleNames;
    public static string[] surnames;
    public static bool PeopleWereGeneratedWithDLCAdditions = false;
    public static void init()
    public static void LoadInDLCPeople()
    public static void ReInitPeopleForExtension()
    public static void generatePeopleForList(List<Person> list, int numberToGenerate, bool areHackers = false)
    public static Person loadPersonFromFile(string path)
    public static void printAllPeople()

`Hacknet.PeopleAssets` (class) — decompiled/game-proj/Hacknet/PeopleAssets.cs:5
    public static string[] degreeTitles = new string[3] { "Bachelor of ", "Masters in ", "PHD in " };
    public static string[] degreeNames = new string[12]
    public static string[] hackerDegreeNames = new string[5] { "Computer Science", "Digital Security", "Computer Networking", "Information Technology", "Computer Graphics" };
    public static Degree getRandomDegree(WorldLocation origin)
    public static Degree getRandomHackerDegree(WorldLocation origin)
    public static string randOf(string[] array)

`Hacknet.Person` (class) — decompiled/game-proj/Hacknet/Person.cs:6
    public DateTime DateOfBirth = DateTime.Now;
    public bool isMale = true;
    public bool isHacker = false;
    public string firstName;
    public string lastName;
    public string handle;
    public List<Degree> degrees;
    public List<VehicleRegistration> vehicles;
    public WorldLocation birthplace;
    public MedicalRecord medicalRecord;
    public NeopalsAccount NeopalsAccount;
    public string FullName => firstName + " " + lastName;
    public Person()
    public Person(string fName, string lName, bool male, bool isHacker = false, string handle = null)
    public void addRandomDegrees()
    public void addRandomVehicles()
    public override string ToString()
    public string getDegreeString()
    public string getVehicleRegString()

`Hacknet.PlatformAPISettings` (class) — decompiled/game-proj/Hacknet/PlatformAPISettings.cs:9
    public static string Report = "";
    public static bool Running = false;
    public static bool RemoteStorageRunning = false;
    public static void InitPlatformAPI()
    public static string GetCodeForActiveLanguage(List<LocaleActivator.LanguageInfo> supportedLanguages)

`Hacknet.PlayerBonuses` (struct) — decompiled/game-proj/Hacknet/PlayerBonuses.cs:3
    public float max_velocity;
    public float acceleration;
    public float dash_regen_rate;
    public float max_dash_range;
    public float gravity_factor;
    public float jump_height_factor;
    public float dash_velocity_retain_factor;
    public int control_invert;
    public int screen_invert;

`Hacknet.PlayerIndexEventArgs` (class) — decompiled/game-proj/Hacknet/PlayerIndexEventArgs.cs:6
    public PlayerIndex playerIndex;
    public PlayerIndex PlayerIndex => playerIndex;
    public PlayerIndexEventArgs(PlayerIndex playerIndex)

`Hacknet.PointClickerDaemon` (class) — decompiled/game-proj/Hacknet/PointClickerDaemon.cs:11
    public float UpgradeCostMultiplier = 13f;
    public List<string> upgradeNames = new List<string>();
    public List<float> upgradeValues = new List<float>();
    public List<float> upgradeCosts = new List<float>();
    public List<string> storyBeats = new List<string>();
    public List<long> storyBeatChangers = new List<long>();
    public PointClickerScreenState state = PointClickerScreenState.Welcome;
    public PointClickerGameState activeState = null;
    public Folder savesFolder;
    public Folder rootFolder;
    public float currentRate = 0f;
    public float pointOverflow = 0f;
    public Texture2D background1;
    public Texture2D background2;
    public Texture2D logoBase;
    public Texture2D logoOverlay1;
    public Texture2D logoOverlay2;
    public Texture2D logoStar;
    public Texture2D scanlinesTextBackground;
    public RenderTarget2D logoRenderBase;
    public SpriteBatch logoBatch;
    public List<PointClickerStar> Stars = new List<PointClickerStar>();
    public Color ThemeColor = new Color(133, 239, 255, 0);
    public Color ThemeColorBacking = new Color(13, 59, 74, 250);
    public Color ThemeColorHighlight = new Color(227, 0, 121, 200);
    public ScrollableSectionedPanel scrollerPanel;
    public int hoverIndex = 0;
    public string ActiveStory = "";
    public string userFilePath = null;
    public float timeSinceLastSave = 0f;
    public List<UpgradeNotifier> UpgradeNotifiers = new List<UpgradeNotifier>();
    public PointClickerDaemon(Computer computer, string serviceName, OS opSystem)
    public void InitRest()
    public void InitLogoSettings()
    public void UpdateRate()
    public void UpdatePoints()
    public void UpdateStory()
    public void PurchaseUpgrade(int index)
    public void SaveProgress()
    public void AddRandomLogoStar(bool randomStartLife = false)
    public void InitGameSettings()
    public override void initFiles()
    public override void navigatedTo()
    public override void loadInit()
    public void AddSaveForName(string name, bool isSuperHighScore = false)
    public override string getSaveString()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void DrawLogo(Rectangle dest, SpriteBatch sb)
    public void DrawStar(Rectangle logoDest, SpriteBatch sb, PointClickerStar star)
    public void DrawMainScreen(Rectangle bounds, SpriteBatch sb)
    public void DrawHoverTooltip(Rectangle bounds, SpriteBatch sb)
    public void DrawStatsTextBlock(string anouncer, string main, string secondary, Rectangle bounds, SpriteBatch sb, float announcerWidth)
    public void DrawUpgrades(Rectangle bounds, SpriteBatch sb)
    public void DrawMonospaceString(Rectangle bounds, SpriteBatch sb, string points)
    public void DrawWelcome(Rectangle bounds, SpriteBatch sb)

`Hacknet.PointClickerDaemon.PointClickerGameState` (class) — decompiled/game-proj/Hacknet/PointClickerDaemon.cs:35
    public int currentStoryElement;
    public long points;
    public List<int> upgradeCounts;
    public PointClickerGameState(int upgradesTotal)
    public string ToSaveString()
    public static PointClickerGameState LoadFromString(string save)

`Hacknet.PointClickerDaemon.PointClickerScreenState` (enum) — decompiled/game-proj/Hacknet/PointClickerDaemon.cs:79

`Hacknet.PointClickerDaemon.PointClickerStar` (struct) — decompiled/game-proj/Hacknet/PointClickerDaemon.cs:13
    public Vector2 Pos;
    public float scale;
    public float life;
    public float rot;
    public float timescale;
    public Color color;

`Hacknet.PointClickerDaemon.UpgradeNotifier` (struct) — decompiled/game-proj/Hacknet/PointClickerDaemon.cs:28
    public string text;
    public float timer;

`Hacknet.PortExploits` (class) — decompiled/game-proj/Hacknet/PortExploits.cs:8
    public const int EXE_FILE_LENGTH = 500;
    public static List<int> portNums;
    public static List<int> exeNums;
    public static Dictionary<int, string> services;
    public static Dictionary<int, string> cracks;
    public static Dictionary<int, string> crackExeData;
    public static Dictionary<int, string> crackExeDataLocalRNG;
    public static Dictionary<int, bool> needsPort;
    public static string ValidPacemakerFirmware;
    public static string DangerousPacemakerFirmware;
    public static string ValidPacemakerFirmwareLRNG;
    public static string DangerousPacemakerFirmwareLRNG;
    public static string ValidAircraftOperatingDLL;
    public static List<string> passwords;
    public static void populate()
    public static string GetExeNameForData(string filename, string exeFileData)
    public static string getRandomPassword()

`Hacknet.PortHackExe` (class) — decompiled/game-proj/Hacknet/PortHackExe.cs:9
    public static float CRACK_TIME = 6f;
    public static float TIME_BETWEEN_TEXT_SWITCH = 0.06f;
    public static float TIME_ALIVE_AFTER_SUCSESS = 5f;
    public static float COMPLETE_LIGHT_FLASH_TIME = 2f;
    public float progress = 0f;
    public int[] textIndex;
    public float textSwitchTimer = TIME_BETWEEN_TEXT_SWITCH;
    public int textOffsetIndex = 0;
    public float sucsessTimer = TIME_ALIVE_AFTER_SUCSESS;
    public bool hasCompleted = false;
    public Computer target;
    public PortHackCubeSequence cubeSeq = new PortHackCubeSequence();
    public RenderTarget2D renderTarget;
    public bool IsTargetingPorthackHeart = false;
    public bool hasCheckedForheart = false;
    public bool StopProgress = false;
    public PortHackExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.PorthackHeartDaemon` (class) — decompiled/game-proj/Hacknet/PorthackHeartDaemon.cs:10
    public RenderTarget2D rendertarget;
    public SpriteBatch rtSpritebatch;
    public float playTimeExpended = 0f;
    public bool PlayingHeartbreak = false;
    public PortHackCubeSequence pcs = new PortHackCubeSequence();
    public float FadeoutDelay = 1f;
    public float FadeoutDuration = 10f;
    public bool IsFlashingOut = false;
    public float flashOutTime = 0f;
    public SoundEffect SpinDownEffect;
    public SoundEffect glowSoundEffect;
    public PorthackHeartDaemon(Computer c, OS os)
    public override void navigatedTo()
    public void UpdateForTime(Rectangle bounds, SpriteBatch sb)
    public void BreakHeart()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.PostProcessor` (class) — decompiled/game-proj/Hacknet/PostProcessor.cs:8
    public static RenderTarget2D target;
    public static RenderTarget2D backTarget;
    public static RenderTarget2D dangerBufferTarget;
    public static GraphicsDevice device;
    public static SpriteBatch sb;
    public static Effect bloom;
    public static Effect blur;
    public static Effect danger;
    public static Color bloomColor;
    public static Color dangerLineColor;
    public static Color dangerLineColorAlt;
    public static Color bloomAbsenceHighlighterColor;
    public static bool bloomEnabled = true;
    public static bool scanlinesEnabled = true;
    public static bool dangerModeEnabled = false;
    public static float dangerModePercentComplete = 0f;
    public static bool EndingSequenceFlashOutActive = false;
    public static float EndingSequenceFlashOutPercentageComplete = 0f;
    public static void init(GraphicsDevice gDevice, SpriteBatch spriteBatch, ContentManager content)
    public static void GenerateMainTarget(GraphicsDevice gDevice)
    public static void begin()
    public static void end()
    public static Texture2D GetLastRenderedCompleteFrame()
    public static string GetStatusReportString()
    public static Rectangle GetFullscreenRect()
    public static void DrawDangerModeFliters()

`Hacknet.Program` (class) — decompiled/game-proj/Hacknet/Program.cs:7
    public static string GraphicsDeviceResetLog = "";
    public static void Main(string[] args)

`Hacknet.ProgramList` (class) — decompiled/game-proj/Hacknet/ProgramList.cs:5
    public static List<string> programs;
    public static void init()
    public static List<string> getExeList(OS os)

`Hacknet.ProgramRunner` (class) — decompiled/game-proj/Hacknet/ProgramRunner.cs:8
    public static bool ExecuteProgram(object os_object, string[] arguments)
    public static bool ExeProgramExists(string name, object binariesFolder)
    public static int GetFileIndexOfExeProgram(string name, object binariesFolder)
    public static int AttemptExeProgramExecution(OS os, string[] p)

`Hacknet.Programs` (class) — decompiled/game-proj/Hacknet/Programs.cs:14
    public static readonly object ConnectionLockObject = new object();
    public static void doDots(int num, int msDelay, OS os)
    public static void typeOut(string s, OS os, int delay = 50)
    public static void firstTimeInit(string[] args, OS os, bool callWasRecursed = false)
    public static void connect(string[] args, OS os)
    public static void disconnect(string[] args, OS os)
    public static void getString(string[] args, OS os)
    public static bool parseStringFromGetStringCommand(OS os, out string data)
    public static void login(string[] args, OS os)
    public static void ls(string[] args, OS os)
    public static void cat(string[] args, OS os)
    public static void ps(string[] args, OS os)
    public static void kill(string[] args, OS os)
    public static void scp(string[] args, OS os)
    public static void upload(string[] args, OS os)
    public static void replace2(string[] args, OS os)
    public static void replace(string[] args, OS os)
    public static void rm(string[] args, OS os)
    public static void rm2(string[] args, OS os)
    public static void mv(string[] args, OS os)
    public static void analyze(string[] args, OS os)
    public static void solve(string[] args, OS os)
    public static void clear(string[] args, OS os)
    public static void execute(string[] args, OS os)
    public static void scan(string[] args, OS os)
    public static void fastHack(string[] args, OS os)
    public static void revealAll(string[] args, OS os)
    public static void cd(string[] args, OS os)
    public static void probe(string[] args, OS os)
    public static void reboot(string[] args, OS os)
    public static void addNote(string[] args, OS os)
    public static void opCDTray(string[] args, OS os, bool isOpen)
    public static extern long mciSendString(string a, StringBuilder b, int c, IntPtr d);
    public static extern void system([MarshalAs(UnmanagedType.LPStr)] string cmd);
    public static void cdDrive(bool open)
    public static void sudo(OS os, Action action)
    public static Folder getCurrentFolder(OS os)
    public static Folder getFolderAtDepth(OS os, int depth)
    public static bool computerExists(OS os, string ip)
    public static Computer getComputer(OS os, string ip_Or_ID_or_Name)
    public static Folder getFolderAtPath(string path, OS os, Folder rootFolder = null, bool returnsNullOnNoFind = false)
    public static Folder getFolderAtPathAsFarAsPossible(string path, OS os, Folder rootFolder)
    public static Folder getFolderAtPathAsFarAsPossible(string path, OS os, Folder rootFolder, out string likelyFilename)
    public static List<int> getNavigationPathAtPath(string path, OS os, Folder currentFolder = null)
    public static Folder getFolderFromNavigationPath(List<int> path, Folder startFolder, OS os)

`Hacknet.ProgressionFlags` (class) — decompiled/game-proj/Hacknet/ProgressionFlags.cs:8
    public List<string> Flags = new List<string>();
    public bool HasFlag(string flag)
    public void AddFlag(string flag)
    public void RemoveFlag(string flag)
    public string GetFlagStartingWith(string start)
    public void Load(XmlReader rdr)
    public string GetSaveString()

`Hacknet.RamModule` (class) — decompiled/game-proj/Hacknet/RamModule.cs:7
    public static int contentStartOffset = 16;
    public static int MODULE_WIDTH = 252;
    public static Color USED_RAM_COLOR = new Color(60, 60, 67);
    public static float FLASH_TIME = 3f;
    public string infoString = "";
    public Vector2 infoStringPos;
    public Rectangle infoBar;
    public Rectangle infoBarUsedRam;
    public float OutOfMemoryFlashTime = 0f;
    public override void LoadContent()
    public override void Update(float t)
    public void FlashMemoryWarning()
    public override void Draw(float t)
    public virtual void drawOutline()

`Hacknet.ReflectiveRenderer` (class) — decompiled/game-proj/Hacknet/ReflectiveRenderer.cs:12
    public static Action<Vector2, Type, string> PreRenderForObject;
    public static int GetEntryLineHeight()
    public static void RenderObject(object o, Rectangle bounds, SpriteBatch spriteBatch, ScrollableSectionedPanel panel, Color TitleColor)
    public static List<RenderableField> GetRenderablesFromType(Type type, object o, int indentLevel = 0)
    public static string FilterTypeName(string name)

`Hacknet.ReflectiveRenderer.RenderableField` (struct) — decompiled/game-proj/Hacknet/ReflectiveRenderer.cs:14
    public string VariableName;
    public string RenderedValue;
    public bool IsTitle;
    public Type t;
    public int IndentLevel;
    public override string ToString()

`Hacknet.RemoteSaveStorage` (class) — decompiled/game-proj/Hacknet/RemoteSaveStorage.cs:8
    public static string BASE_SAVE_FILE_NAME = "save";
    public static string SAVE_FILE_EXT = ".xml";
    public static string Standalone_FolderPath = "Accounts/";
    public static bool FileExists(string playerID, bool forceLocal = false)
    public static Stream GetSaveReadStream(string playerID, bool forceLocal = false)
    public static void WriteSaveData(string saveData, string playerID, bool forcelocal = false)
    public static void Delete(string playerID)
    public static bool CanLoad(string playerID)

`Hacknet.RTSPPortExe` (class) — decompiled/game-proj/Hacknet/RTSPPortExe.cs:6
    public static float RUN_TIME = 6.3f;
    public static float IDLE_TIME = 30.5f;
    public float elapsedTime = 0f;
    public float completionFlashDuration = 3.2f;
    public float completionFlashTimer = 0f;
    public bool isComplete = false;
    public TrailLoadingSpinnerEffect spinner;
    public int completeRamUse = 220;
    public float preciceRamCost = 0f;
    public RTSPPortExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.RunnableConditionalActions` (class) — decompiled/game-proj/Hacknet/RunnableConditionalActions.cs:9
    public const string SerializationKey = "ConditionalActions";
    public List<SerializableConditionalActionSet> Actions = new List<SerializableConditionalActionSet>();
    public bool IsUpdating = false;
    public virtual void Update(float dt, object os)
    public string GetSaveString()
    public static RunnableConditionalActions Deserialize(XmlReader rdr)
    public static void LoadIntoOS(string filepath, object OSobj)

`Hacknet.SAAddAsset` (class) — decompiled/game-proj/Hacknet/SAAddAsset.cs:6
    public string FileName;
    public string FileContents;
    public string TargetComp;
    public string TargetFolderpath;
    public int FunctionValue = 0;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAAddConditionalActions` (class) — decompiled/game-proj/Hacknet/SAAddConditionalActions.cs:6
    public string Filepath;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAAddIRCMessage` (class) — decompiled/game-proj/Hacknet/SAAddIRCMessage.cs:7
    public string Message;
    public string Author;
    public string TargetComp;
    public float Delay = 0f;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAAddMissionToHubServer` (class) — decompiled/game-proj/Hacknet/SAAddMissionToHubServer.cs:7
    public string MissionFilepath;
    public string TargetComp;
    public string AssignmentTag;
    public bool StartsComplete;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAAddThreadToMissionBoard` (class) — decompiled/game-proj/Hacknet/SAAddThreadToMissionBoard.cs:6
    public string ThreadFilepath;
    public string TargetComp;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAAppendToFile` (class) — decompiled/game-proj/Hacknet/SAAppendToFile.cs:6
    public string DataToAdd;
    public string TargetComp;
    public string TargetFolderpath;
    public string TargetFilename;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SACancelScreenBleedEffect` (class) — decompiled/game-proj/Hacknet/SACancelScreenBleedEffect.cs:6
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAChangeAlertIcon` (class) — decompiled/game-proj/Hacknet/SAChangeAlertIcon.cs:6
    public const string TypeFlag = "_changeAlertIconType:";
    public const string TargetFlag = "_changeAlertIconTarget:";
    public string Type;
    public string Target;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)
    public static void UpdateAlertIcon(object osobj)

`Hacknet.SAChangeIP` (class) — decompiled/game-proj/Hacknet/SAChangeIP.cs:6
    public string TargetComp;
    public string NewIP;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAChangeNetmapSortMethod` (class) — decompiled/game-proj/Hacknet/SAChangeNetmapSortMethod.cs:6
    public string Method;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SACopyAsset` (class) — decompiled/game-proj/Hacknet/SACopyAsset.cs:6
    public string DestFileName;
    public string DestFilePath;
    public string DestComp;
    public string SourceComp;
    public string SourceFileName;
    public string SourceFilePath;
    public int FunctionValue = 0;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SACrashComputer` (class) — decompiled/game-proj/Hacknet/SACrashComputer.cs:6
    public string TargetComp;
    public string CrashSource;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SADeleteFile` (class) — decompiled/game-proj/Hacknet/SADeleteFile.cs:6
    public string TargetComp;
    public string FilePath;
    public string FileName;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAGivePlayerUserAccount` (class) — decompiled/game-proj/Hacknet/SAGivePlayerUserAccount.cs:6
    public string TargetComp;
    public string Username;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAHideAllNodes` (class) — decompiled/game-proj/Hacknet/SAHideAllNodes.cs:6
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAHideNode` (class) — decompiled/game-proj/Hacknet/SAHideNode.cs:6
    public string TargetComp;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAKillExe` (class) — decompiled/game-proj/Hacknet/SAKillExe.cs:6
    public string ExeName;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SALaunchHackScript` (class) — decompiled/game-proj/Hacknet/SALaunchHackScript.cs:6
    public string Filepath;
    public string DelayHost;
    public float Delay;
    public string SourceComp;
    public string TargetComp;
    public bool RequireLogsOnSource;
    public bool RequireSourceIntact;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SALoadMission` (class) — decompiled/game-proj/Hacknet/SALoadMission.cs:7
    public string MissionName;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SARemoveMissionFromHubServer` (class) — decompiled/game-proj/Hacknet/SARemoveMissionFromHubServer.cs:6
    public string MissionFilepath;
    public string TargetComp;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SARunFunction` (class) — decompiled/game-proj/Hacknet/SARunFunction.cs:6
    public string FunctionName;
    public int FunctionValue = 0;
    public float Delay = 0f;
    public string DelayHost;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SASaveGame` (class) — decompiled/game-proj/Hacknet/SASaveGame.cs:6
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SASetLock` (class) — decompiled/game-proj/Hacknet/SASetLock.cs:6
    public string DelayHost;
    public float Delay;
    public string Module;
    public bool IsLocked = false;
    public bool IsHidden = false;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAShowNode` (class) — decompiled/game-proj/Hacknet/SAShowNode.cs:6
    public string DelayHost;
    public float Delay;
    public string Target;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SAStartScreenBleedEffect` (class) — decompiled/game-proj/Hacknet/SAStartScreenBleedEffect.cs:7
    public string ContentLines;
    public string AlertTitle;
    public string CompleteAction;
    public float TotalDurationSeconds = 200f;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SASwitchToTheme` (class) — decompiled/game-proj/Hacknet/SASwitchToTheme.cs:7
    public string ThemePathOrName;
    public float FlickerInDuration = 2f;
    public string DelayHost;
    public float Delay;
    public override void Trigger(object os_obj)
    public static SerializableAction DeserializeFromReader(XmlReader rdr)

`Hacknet.SaveData` (class) — decompiled/game-proj/Hacknet/SaveData.cs:7
    public List<Computer> nodes;
    public ActiveMission mission;
    public void addNodes(object nodesToAdd)
    public void setMission(object missionToAdd)
    public object getNodes()
    public object getMission()

`Hacknet.SCDoesNotHaveFlags` (class) — decompiled/game-proj/Hacknet/SCDoesNotHaveFlags.cs:6
    public string Flags;
    public override bool Check(object os_obj)
    public static SerializableCondition DeserializeFromReader(XmlReader rdr)

`Hacknet.SCHasFlags` (class) — decompiled/game-proj/Hacknet/SCHasFlags.cs:6
    public string requiredFlags;
    public override bool Check(object os_obj)
    public static SerializableCondition DeserializeFromReader(XmlReader rdr)

`Hacknet.SCInstantly` (class) — decompiled/game-proj/Hacknet/SCInstantly.cs:5
    public bool needsMissionComplete;
    public override bool Check(object os_obj)
    public static SerializableCondition DeserializeFromReader(XmlReader rdr)

`Hacknet.SCOnAdminGained` (class) — decompiled/game-proj/Hacknet/SCOnAdminGained.cs:6
    public string target;
    public override bool Check(object os_obj)
    public static SerializableCondition DeserializeFromReader(XmlReader rdr)

`Hacknet.SCOnConnect` (class) — decompiled/game-proj/Hacknet/SCOnConnect.cs:6
    public string target;
    public bool needsMissionComplete;
    public string requiredFlags;
    public override bool Check(object os_obj)
    public static SerializableCondition DeserializeFromReader(XmlReader rdr)

`Hacknet.SCOnDisconnect` (class) — decompiled/game-proj/Hacknet/SCOnDisconnect.cs:5
    public bool hasHadFrameWhereThisWasFalse = false;
    public string target;
    public override bool Check(object os_obj)
    public static SerializableCondition DeserializeFromReader(XmlReader rdr)

`Hacknet.ScreenManager` (class) — decompiled/game-proj/Hacknet/ScreenManager.cs:10
    public List<GameScreen> screens = new List<GameScreen>();
    public List<GameScreen> screensToUpdate = new List<GameScreen>();
    public InputState input = new InputState();
    public SpriteBatch spriteBatch;
    public SpriteFont font;
    public SoundEffect alertSound;
    public SpriteFont hugeFont;
    public Texture2D blankTexture;
    public bool isInitialized;
    public bool traceEnabled;
    public PlayerIndex controllingPlayer;
    public Color screenFillColor = Color.Black;
    public bool usingGamePad = false;
    public AudioEngine audioEngine;
    public WaveBank waveBank;
    public WaveBank musicBank;
    public SoundBank soundBank;
    public SpriteBatch SpriteBatch => spriteBatch;
    public SpriteFont Font => font;
    public bool TraceEnabled
    public ScreenManager(Game game)
    public override void Initialize()
    public override void LoadContent()
    public override void UnloadContent()
    public override void Update(GameTime gameTime)
    public void TraceScreens()
    public override void Draw(GameTime gameTime)
    public void handleCriticalErrorBoxAccepted(object sender, PlayerIndexEventArgs e)
    public void handleCriticalError()
    public void AddScreen(GameScreen screen)
    public void AddScreen(GameScreen screen, PlayerIndex? controllingPlayer)
    public void ShowPopup(string message)
    public void playAlertSound()
    public void RemoveScreen(GameScreen screen)
    public GameScreen[] GetScreens()
    public void FadeBackBufferToBlack(int alpha)
    public string clipStringForMessageBox(string s)
    public void ConfirmExitMessageBoxAccepted(object sender, PlayerIndexEventArgs e)

`Hacknet.ScreenState` (enum) — decompiled/game-proj/Hacknet/ScreenState.cs:3

`Hacknet.SecurityTraceExe` (class) — decompiled/game-proj/Hacknet/SecurityTraceExe.cs:5
    public SecurityTraceExe(Rectangle location, OS _os)
    public override void Killed()
    public override void Draw(float t)

`Hacknet.SequencerExe` (class) — decompiled/game-proj/Hacknet/SequencerExe.cs:10
    public static int ACTIVATING_RAM_COST = 170;
    public static int BASE_RAM_COST = 60;
    public static float RAM_CHANGE_PS = 100f;
    public static double Song_Length = 186.0;
    public static float SPIN_UP_TIME = 17f;
    public static float TimeBetweenBeats = 1.832061f;
    public MovingBarsEffect bars = new MovingBarsEffect();
    public string targetID;
    public string flagForProgressionName;
    public string oldSongName = null;
    public int targetRamUse = ACTIVATING_RAM_COST;
    public float stateTimer = 0f;
    public float beatHits = 0.15f;
    public double beatDropTime = 16.64;
    public List<ConnectedNodeEffect> nodeeffects = new List<ConnectedNodeEffect>();
    public SequencerExeState state = SequencerExeState.Unavaliable;
    public Computer targetComp;
    public OSTheme originalTheme;
    public OSTheme targetTheme = OSTheme.HacknetWhite;
    public bool HasBeenKilled = false;
    public SequencerExe(Rectangle location, OS operatingSystem, string[] p)
    public override void LoadContent()
    public override void Update(float t)
    public void ActiveStateUpdate(float t)
    public void UpdateRamCost(float t)
    public override void Killed()
    public void MoveToActiveState()
    public override void Draw(float t)
    public void DrawActiveState()
    public void DrawCountdownOverlay()

`Hacknet.SequencerExe.SequencerExeState` (enum) — decompiled/game-proj/Hacknet/SequencerExe.cs:12

`Hacknet.SerializableAction` (class) — decompiled/game-proj/Hacknet/SerializableAction.cs:10
    public abstract void Trigger(object os_obj);
    public string GetSaveString()
    public static SerializableAction Deserialize(XmlReader rdr)
    public SerializableAction()

`Hacknet.SerializableCondition` (class) — decompiled/game-proj/Hacknet/SerializableCondition.cs:10
    public abstract bool Check(object os_obj);
    public string GetSaveString(string bodyContent)
    public static SerializableCondition Deserialize(XmlReader rdr, Action<XmlReader, string> bodyContentReadAction)
    public SerializableCondition()

`Hacknet.SerializableConditionalActionSet` (class) — decompiled/game-proj/Hacknet/SerializableConditionalActionSet.cs:8
    public SerializableCondition Condition;
    public List<SerializableAction> Actions = new List<SerializableAction>();
    public string GetSaveString()
    public static SerializableConditionalActionSet Deserialize(XmlReader rdr)

`Hacknet.ServerScreen` (class) — decompiled/game-proj/Hacknet/ServerScreen.cs:10
    public string mainIP;
    public List<string> messages;
    public Color backgroundColor = new Color(6, 6, 6);
    public ExternalNetworkedServer server;
    public bool canCloseServer;
    public bool drawingWithEffects = false;
    public ServerScreen()
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public override void HandleInput(InputState input)
    public void parseMessage(string msg)
    public void addDisplayMessage(string msg)
    public override void Draw(GameTime gameTime)
    public void drawMessageLog(int x, int y)

`Hacknet.Services` (class) — decompiled/game-proj/Hacknet/Services.cs:3
    public const int ADMIN = 0;
    public const int ALL = 1;
    public const int MAIL = 2;
    public const int MISSIONLIST = 3;

`Hacknet.Settings` (class) — decompiled/game-proj/Hacknet/Settings.cs:5
    public static bool MenuStartup = true;
    public static bool slowOSStartup = true;
    public static bool osStartsWithTutorial = slowOSStartup;
    public static bool isAlphaDemoMode = false;
    public static bool soundDisabled = false;
    public static bool debugCommandsEnabled = false;
    public static bool testingMenuItemsEnabled = false;
    public static bool debugDrawEnabled = false;
    public static bool forceCompleteEnabled = false;
    public static bool emergencyForceCompleteEnabled = true;
    public static bool emergencyDebugCommandsEnabled = true;
    public static bool AllTraceTimeSlowed = false;
    public static bool FastBootText = false;
    public static bool AllowExtensionMode = true;
    public static bool AllowExtensionPublish = false;
    public static bool EducationSafeBuild = false;
    public static string ActiveLocale = "en-us";
    public static bool EnableDLC = true;
    public static bool isPirateBuild = false;
    public static bool sendsDLC1PromoEmailAtEnd = true;
    public static bool initShowsTutorial = osStartsWithTutorial;
    public static bool windowed = false;
    public static bool IsInExtensionMode = false;
    public static bool DrawHexBackground = true;
    public static bool StartOnAltMonitor = false;
    public static bool isDemoMode = false;
    public static bool isPressBuildDemo = false;
    public static bool isConventionDemo = false;
    public static bool isLockedDemoMode = false;
    public static bool isSpecialTestBuild = false;
    public static bool lighterColorHexBackground = false;
    public static string ConventionLoginName = "Agent";
    public static bool MultiLingualDemo = false;
    public static bool DLCEnabledDemo = true;
    public static bool ShuffleThemeOnDemoStart = true;
    public static bool HasLabyrinthsDemoStartMainMenuButton = false;
    public static bool ForceEnglish = false;
    public static bool IsExpireLocked = false;
    public static DateTime ExpireTime = Utils.SafeParseDateTime("10/06/2017 23:59:01");
    public static bool isServerMode = false;
    public static bool recoverFromErrorsSilently = true;

`Hacknet.SettingsLoader` (class) — decompiled/game-proj/Hacknet/SettingsLoader.cs:10
    public static int resWidth;
    public static int resHeight;
    public static bool isFullscreen = false;
    public static bool didLoad = false;
    public static bool hasEverSaved = false;
    public static bool ShouldMultisample = true;
    public static bool ShouldDrawMusicVis = true;
    public static readonly string settingsPath = GetSettingsPath();
    public static string GetSettingsPath()
    public static void checkStatus()
    public static void writeStatusFile()

`Hacknet.SFX` (class) — decompiled/game-proj/Hacknet/SFX.cs:9
    public static List<Vector2> circlePos = new List<Vector2>();
    public static List<float> circleRadius = new List<float>();
    public static List<float> circleExpand = new List<float>();
    public static List<Color> circleColor = new List<Color>();
    public static List<RadialLineData> RadialLines = new List<RadialLineData>();
    public static Texture2D circleTex;
    public static Vector2 circleOrigin;
    public static void init(ContentManager content)
    public static void AddRadialLine(Vector2 pos, float incomingAngle, float startDistance, float startSpeed, float acceleration, float fadeDistance, float speedSizeMultiplier, Color color, float width = 1f, bool snapMode = false)
    public static void addCircle(Vector2 pos, Color color, float radius)
    public static void Update(float t)
    public static void Draw(SpriteBatch sb)

`Hacknet.SFX.RadialLineData` (struct) — decompiled/game-proj/Hacknet/SFX.cs:11
    public Vector2 destination;
    public float angle;
    public float distance;
    public Color color;
    public float width;
    public float MovementPerSecond;
    public float Acceleration;
    public float FadeDistance;
    public float SizeForSpeedMultiplier;
    public bool SnapMode;

`Hacknet.ShellExe` (class) — decompiled/game-proj/Hacknet/ShellExe.cs:6
    public const int IDLE_STATE = 0;
    public const int CLOSING_STATE = -1;
    public const int PROXY_OVERLOAD_STATE = 1;
    public const int FORKBOMB_TRAP_STATE = 2;
    public static int INFOBAR_HEIGHT = 16;
    public static int BASE_RAM_COST = 40;
    public static float RAM_CHANGE_PS = 200f;
    public static int TRAP_RAM_USE = 100;
    public Rectangle infoBar;
    public string destinationIP = "";
    public Computer destComp = null;
    public Computer compThisShellIsRunningOn = null;
    public int destCompIndex = -1;
    public int state = 0;
    public int targetRamUse = BASE_RAM_COST;
    public ShellExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public void doGui()
    public void StartOverload()
    public void doControlButtons()
    public void completedAction(int action)
    public void cancelTarget()
    public override void Completed()
    public void reportedTo(string data)

`Hacknet.ShellOverloaderExe` (class) — decompiled/game-proj/Hacknet/ShellOverloaderExe.cs:3
    public static void RunShellOverloaderExe(string[] args, object osObj, Computer target)

`Hacknet.ShellReopenerExe` (class) — decompiled/game-proj/Hacknet/ShellReopenerExe.cs:7
    public const string Filename = "ShellSources.txt";
    public static void RunShellReopenerExe(string[] args, object osObj, Computer target)

`Hacknet.SMTPoverflowExe` (class) — decompiled/game-proj/Hacknet/SMTPoverflowExe.cs:8
    public static float DURATION = 12f;
    public static float BAR_MOVEMENT = 30f;
    public static float BAR_HEIGHT = 2f;
    public float progress;
    public bool hasCompleted;
    public float sucsessTimer = 0.5f;
    public float timeAccum = 0f;
    public float barSize = 0f;
    public List<Vector2> leftBars;
    public List<Vector2> rightBars;
    public int completedIndex = 0;
    public Color activeBarColor = new Color(34, 82, 64, 255);
    public Color activeBarHighlightColor = new Color(0, 186, 99, 0);
    public SMTPoverflowExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public void DrawBar(Rectangle dest, bool barActive, bool isLeft)
    public override void Completed()

`Hacknet.SongChangerDaemon` (class) — decompiled/game-proj/Hacknet/SongChangerDaemon.cs:8
    public MovingBarsEffect topEffect;
    public MovingBarsEffect botEffect;
    public SongChangerDaemon(Computer c, OS os)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.SpritePlacementData` (struct) — decompiled/game-proj/Hacknet/SpritePlacementData.cs:5
    public Vector2 pos;
    public float depth;
    public Vector2 scale;
    public int spriteIndex;
    public SpritePlacementData(Vector2 position, Vector2 scales, float layerDepth)
    public SpritePlacementData(Vector2 position, Vector2 scales, float layerDepth, int SpriteIndex)

`Hacknet.SQLExploitExe` (class) — decompiled/game-proj/Hacknet/SQLExploitExe.cs:6
    public const float INTRO_TIME = 3f;
    public const float MAIN_INTRO_TIME = 3f;
    public const float MAIN_BODY_TIME = 5f;
    public const float ENDING_TIME = 1.2f;
    public const string initText = "Initializing###.#.#.#\nConnecting###.#.#.#.#.#.#.#.#..#.\nInjecting Corrupt Sectors###.#.#.##.#.#.#";
    public const string mainIntrotext = "MEMORY CORRUPTION DETECTED\n##Initializing SQL Core Dump##>#>##>#>#>#>>>>>";
    public const string errorIntrotext = "M3^ORK CO3@PI\"} DGT^C.D\n##Ini$$!l^zi/g SQ: -!re 3@Hp##>#>##>#>#>#>>>>>";
    public string[] bodyText;
    public SQLState state = SQLState.Intro;
    public float currentStateTimer = 0f;
    public float timeTaken = 0f;
    public float initStringCharDelay = 0.1f;
    public Color flashColor;
    public Color brightDrawColor;
    public SQLExploitExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public void updateState()
    public override void Draw(float t)
    public void drawBackground(Rectangle dest)
    public void drawIntro(Rectangle dest)
    public void drawMainIntro(Rectangle dest)
    public void drawMainBody(Rectangle dest)
    public void drawEnding(Rectangle dest)
    public string getDelayDrawString(string original, float time = -1f)
    public override void Completed()

`Hacknet.SQLExploitExe.SQLState` (enum) — decompiled/game-proj/Hacknet/SQLExploitExe.cs:8

`Hacknet.SSHCrackExe` (class) — decompiled/game-proj/Hacknet/SSHCrackExe.cs:7
    public static float DURATION = 8f;
    public static float GRID_REVEAL_DELAY = 0.6f;
    public static float ENDING_FLASH = 0.7f;
    public static float SHEEN_FLASH_DELAY = 0.03f;
    public int width;
    public int height;
    public float timeLeft = DURATION;
    public bool complete = false;
    public bool GridShowingSheen = false;
    public SSHCrackGridEntry[,] Grid;
    public int GridEntryWidth;
    public int GridEntryHeight;
    public Color unlockedFlashColor;
    public SSHCrackExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public override void Completed()

`Hacknet.SSHCrackExe.SSHCrackGridEntry` (struct) — decompiled/game-proj/Hacknet/SSHCrackExe.cs:9
    public float TimeTillActive;
    public float TimeTillSolved;
    public float TimeSinceActivated;
    public byte CurrentValue;

`Hacknet.SSLPortExe` (class) — decompiled/game-proj/Hacknet/SSLPortExe.cs:9
    public const float RUN_TIME = 12f;
    public const float IDLE_TIME = 15f;
    public float elapsedTime = 0f;
    public SSLMode Mode = SSLMode.SSH;
    public Vector2 LastCentralRenderOffset = Vector2.Zero;
    public bool IsComplete = false;
    public SSLPortExe(Rectangle location, OS operatingSystem, SSLMode mode)
    public static SSLPortExe GenerateInstanceOrNullFromArguments(string[] args, Rectangle location, object osObj, Computer target)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.SSLPortExe.SSLMode` (enum) — decompiled/game-proj/Hacknet/SSLPortExe.cs:11

`Hacknet.StatsManager` (class) — decompiled/game-proj/Hacknet/StatsManager.cs:5
    public static bool HasReceivedUserStats = false;
    public static Callback<UserStatsReceived_t> UserStatsCallback;
    public static StatData[] StatDefinitions = new StatData[1]
    public static void InitStats()
    public static void OnUserStatsReceived(UserStatsReceived_t pCallback)
    public static void IncrementStat(string statName, int valueChange)
    public static void SaveStatProgress()

`Hacknet.StatsManager.StatData` (struct) — decompiled/game-proj/Hacknet/StatsManager.cs:7
    public string Name;
    public int IntVal;
    public float FloatVal;

`Hacknet.SurveillanceProfile` (class) — decompiled/game-proj/Hacknet/SurveillanceProfile.cs:3
    public string Name;
    public string Age;
    public string HomeCity;
    public string Notes;
    public string CriminalRecord;
    public override string ToString()

`Hacknet.Terminal` (class) — decompiled/game-proj/Hacknet/Terminal.cs:10
    public static float PROMPT_OFFSET = 0f;
    public List<string> history;
    public List<string> runCommands = new List<string>();
    public int commandHistoryOffset;
    public string currentLine;
    public string lastRunCommand;
    public string prompt;
    public bool usingTabExecution = false;
    public bool preventingExecution = false;
    public bool executionPreventionIsInteruptable = false;
    public Color outlineColor = new Color(68, 68, 68);
    public Color backColor = new Color(8, 8, 8);
    public Color historyTextColor = new Color(220, 220, 220);
    public Color currentTextColor = Color.White;
    public override void LoadContent()
    public override void Update(float t)
    public override void Draw(float t)
    public void executeLine()
    public string GetRecentTerminalHistoryString()
    public List<string> GetRecentTerminalHistoryList()
    public void NonThreadedInstantExecuteLine()
    public void doGui()
    public void doTabComplete()
    public void writeLine(string text)
    public void write(string text)
    public void clearCurrentLine()
    public void reset()
    public int commandsRun()
    public string getLastRunCommand()

`Hacknet.TextRecord` (class) — decompiled/game-proj/Hacknet/TextRecord.cs:3
    public string Title;
    public string Data;
    public override string ToString()

`Hacknet.TextureBank` (class) — decompiled/game-proj/Hacknet/TextureBank.cs:8
    public static List<LoadedTexture> textures = new List<LoadedTexture>();
    public static Texture2D load(string filename, ContentManager content)
    public static Texture2D getIfLoaded(string filename)
    public static void unload(Texture2D tex)
    public static void unloadWithoutRemoval(Texture2D tex)

`Hacknet.ThemeChangerExe` (class) — decompiled/game-proj/Hacknet/ThemeChangerExe.cs:10
    public const float START_LOADING_TIME = 25.5f;
    public RenderTarget2D target;
    public float loadingTimeRemaining = 25.5f;
    public ThemeChangerState state = ThemeChangerState.List;
    public SpriteBatch internalSB;
    public Texture2D circle;
    public BarcodeEffect barcodeEffect;
    public Color themeColor;
    public int remotesSelected = -1;
    public int localsSelected = -1;
    public int remoteScroll = 0;
    public int localScroll = 0;
    public ThemeChangerExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Draw(float t)
    public void DrawWithSeperateRT(float t)
    public void DrawListing(Rectangle dest, SpriteBatch sb)
    public void DrawApplyField(string selectedFilename, string selectedFileData, Rectangle bounds, SpriteBatch sb)
    public void ApplyTheme(string themeFilename, string fileData)
    public void DrawHeaders(Rectangle dest, SpriteBatch sb)
    public void DrawLoading(float timeRemaining, float totalTime, Rectangle dest, SpriteBatch sb)
    public void DrawLoadingCircle(float timeRemaining, float totalTime, Rectangle dest, Vector2 loaderCentre, float loaderRadius, float baseRotationAdd, float rotationRateRPS, SpriteBatch sb)

`Hacknet.ThemeChangerExe.ThemeChangerState` (enum) — decompiled/game-proj/Hacknet/ThemeChangerExe.cs:12

`Hacknet.ThemeManager` (class) — decompiled/game-proj/Hacknet/ThemeManager.cs:14
    public static string CustomThemeIDSeperator = "___";
    public static Texture2D backgroundImage;
    public static Texture2D lastLoadedCustomBackground;
    public static string customBackgroundImageLoadPath = null;
    public static bool backgroundNeedsDisposal = false;
    public static OSTheme currentTheme;
    public static CustomTheme LastLoadedCustomTheme = null;
    public static string LastLoadedCustomThemePath = null;
    public static HexGridBackground hexGrid;
    public static Dictionary<OSTheme, string> fileData;
    public static int webWidth;
    public static int webHeight;
    public static int framesTillWebUpdate = -1;
    public static bool HasNeverSwappedThemeBefore = true;
    public static void init(ContentManager content)
    public static void Update(float dt)
    public static void switchTheme(object osObject, string customThemePath)
    public static void switchTheme(object osObject, OSTheme theme)
    public static void switchThemeLayout(OS os, OSTheme theme)
    public static void loadCustomThemeBackground(OS os, string imagePathName)
    public static void loadThemeBackground(OS os, OSTheme theme)
    public static void switchThemeColors(OS os, OSTheme theme)
    public static Color GetRepresentativeColorForTheme(OSTheme theme)
    public static void drawBackgroundImage(SpriteBatch sb, Rectangle area)
    public static string getThemeDataString(OSTheme theme)
    public static string getThemeDataStringForCustomTheme(string customThemePath)
    public static OSTheme getThemeForDataString(string data)
    public static void setThemeOnComputer(object computerObject, OSTheme theme)
    public static void setThemeOnComputer(object computerObject, string customThemePath)

`Hacknet.TorrentPortExe` (class) — decompiled/game-proj/Hacknet/TorrentPortExe.cs:7
    public const float RUN_TIME = 4.8f;
    public const float IDLE_TIME = 16.5f;
    public float elapsedTime = 0f;
    public float completionFlashDuration = 3.2f;
    public float completionFlashTimer = 0f;
    public bool isComplete = false;
    public RaindropsEffect RainEffect = new RaindropsEffect();
    public RaindropsEffect BackgroundRainEffect = new RaindropsEffect();
    public TorrentPortExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public override void Draw(float t)

`Hacknet.TraceKillExe` (class) — decompiled/game-proj/Hacknet/TraceKillExe.cs:11
    public const float TIME_BETWEEN_FOCUS_POINTS = 4f;
    public const float FOCUS_POINT_TRANSITION_TIME = 1.2f;
    public const float MAX_FOCUS_POINT_IDLE_TIME = 0.1f;
    public static Color BoatBarColor = new Color(0, 0, 0, 200);
    public RenderTarget2D effectTarget;
    public SpriteBatch effectSB;
    public SoundEffect traceKillSound;
    public Vector2 EffectFocus = new Vector2(0.5f);
    public float timer = 0f;
    public Vector2 focusPointLocation = Vector2.Zero;
    public float timeTillNextFocusPoint = 1f;
    public bool isOnFocusPoint = false;
    public float focusPointTransitionTime = 0f;
    public float timeOnFocusPoint = 0f;
    public float focusPointIdleTime = 0f;
    public bool hasDoneBurstForThisFocusPoint = false;
    public List<PointImpactEffect> ImpactEffects = new List<PointImpactEffect>();
    public Texture2D circle;
    public BarcodeEffect BotBarcode;
    public float traceActivityTimer = 0f;
    public TraceKillExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public void UpdateEffect(float t)
    public override void Draw(float t)
    public void DrawEffect(float t)
    public void DrawImpactEffects(SpriteBatch sb, Rectangle dest)
    public void DrawEffectFill(SpriteBatch sb, Rectangle dest)
    public void DrawTracerLine(SpriteBatch sb, Vector2 pos, float thickness, Vector2 target, float length, Color baseColor, Color highlightColor)
    public void DrawTracerLineShadow(SpriteBatch sb, Vector2 pos, float thickness, Vector2 target, float length, Color color)

`Hacknet.TraceKillExe.PointImpactEffect` (struct) — decompiled/game-proj/Hacknet/TraceKillExe.cs:13
    public const float TransInTime = 1f;
    public const float TransOutTime = 2f;
    public ConnectedNodeEffect cne = new ConnectedNodeEffect(os, intense: true);
    public float timeEnabled = 0f;
    public Vector2 location = location;
    public float scaleModifier = 1f;
    public bool HasHighlightCircle = true;

`Hacknet.TraceTracker` (class) — decompiled/game-proj/Hacknet/TraceTracker.cs:8
    public OS os;
    public float timer;
    public float lastFrameTime;
    public float startingTimer;
    public bool active;
    public float timeSinceFreezeRequest = 0f;
    public float trackSpeedFactor = 1f;
    public static SpriteFont font;
    public static SoundEffect beep;
    public Computer target;
    public string drawtext;
    public Color timerColor;
    public TraceTracker(OS _os)
    public void Update(float t)
    public void start(float t)
    public void stop()
    public void Draw(SpriteBatch sb)

`Hacknet.TrackerCompleteSequence` (class) — decompiled/game-proj/Hacknet/TrackerCompleteSequence.cs:3
    public static float MinTrackTime = 10f;
    public static float MaxTrackTime = 20f;
    public static bool NextCompleteForkbombShouldTrace;
    public static string ForkbombCompleteTraceIP = null;
    public static void TrackComplete(object osobj, Computer source)
    public static bool CompShouldStartTrackerFromLogs(object osobj, Computer c, string targetIP = null)
    public static void TriggerETAS(object osobj)
    public static void FlagNextForkbombCompletionToTrace(string source)

`Hacknet.TrailLoadingSpinnerEffect` (class) — decompiled/game-proj/Hacknet/TrailLoadingSpinnerEffect.cs:8
    public RenderTarget2D target;
    public SpriteBatch internalSB;
    public Texture2D circle;
    public FlyoutEffect flyout;
    public TrailLoadingSpinnerEffect(OS operatingSystem)
    public void Draw(Rectangle bounds, SpriteBatch spriteBatch, float totalTime, float timeRemaining, float extraTime = 0f, Color? color = null)
    public void Draw2(Rectangle bounds, SpriteBatch spriteBatch, float totalTime, float timeRemaining, float extraTime = 0f, float dt = 0f)
    public void DrawLoading(float timeRemaining, float totalTime, Rectangle dest, SpriteBatch sb, Color c, float timeAdd = 0f)
    public void DrawLoadingCircle(float timeRemaining, float totalTime, Rectangle dest, Vector2 loaderCentre, float loaderRadius, float baseRotationAdd, float rotationRateRPS, SpriteBatch sb, Color c, int NumberOfCircles = 10, float scaleMod = 1f)

`Hacknet.TuneswapExe` (class) — decompiled/game-proj/Hacknet/TuneswapExe.cs:10
    public RaindropsEffect backdrop;
    public Color themeColor = Color.Pink;
    public string oldPlayingSong = null;
    public List<string> SongOptions = new List<string>(new string[8] { "DLC\\Music\\snidelyWhiplash", "DLC\\Music\\Userspacelike", "DLC\\Music\\Slow_Motion", "DLC\\Music\\World_Chase", "DLC\\Music\\HOME_Resonance", "DLC\\Music\\Remi2", "DLC\\Music\\Remi_Finale", "DLC\\Music\\DreamHead" });
    public List<string> SongNames = new List<string>(new string[8] { "Snidely Whiplash", "Payload (AKA Userspacelike)", "Slow Motion", "World Chase", "Resonance", "ClearText", "Sabotage (AKA Altitude_Loss)", "Dream Head" });
    public List<string> ArtistNames = new List<string>(new string[8] { "OGRE", "The Algorithm", "Tonspender", "Cinematrik", "HOME", "The Algorithm", "The Algorithm", "HOME" });
    public string mousedOverArtistName = null;
    public TuneswapExe(Rectangle location, OS operatingSystem, string[] p)
    public override void Update(float t)
    public override void Completed()
    public bool CanActivateSong(int i)
    public void ActivateSong(string song)
    public override void Draw(float t)
    public void RenderMainDisplay(Rectangle dest, SpriteBatch sb)

`Hacknet.TutorialExe` (class) — decompiled/game-proj/Hacknet/TutorialExe.cs:7
    public static bool advanced = false;
    public static List<string> commandSequence;
    public static List<string> feedbackSequence;
    public int state;
    public string lastCommand;
    public string[] renderText;
    public float flashTimer;
    public TutorialExe(Rectangle location, OS operatingSystem)
    public override void LoadContent()
    public override void Update(float t)
    public void parseCommand()
    public void printCurrentCommandToTerminal()
    public void getRenderText()
    public override void Killed()
    public override void Draw(float t)

`Hacknet.UploadServerDaemon` (class) — decompiled/game-proj/Hacknet/UploadServerDaemon.cs:8
    public const string DEFAULT_FOLDERNAME = "Drop";
    public const string STORAGE_FOLDER_FOLDERNAME = "Uploads";
    public const string MESSAGE_FILENAME = "Server_Message.txt";
    public const int NUMBER_OF_ARROWS = 90;
    public const float MAX_FADE_TIME = 2f;
    public static string MESSAGE_FILE_DATA = null;
    public static Vector2 ARROW_VELOCITY = new Vector2(0f, 400f);
    public Texture2D arrow;
    public Color themeColor;
    public Color darkThemeColor;
    public Color lightThemeColor;
    public Folder root;
    public Folder storageFolder;
    public UploadServerState state = UploadServerState.Menu;
    public string Foldername;
    public bool needsAuthentication;
    public bool hasReturnViewButton = false;
    public int uploadFileCountLastFrame;
    public float uploadDetectedEffectTimer = 0f;
    public List<Vector2> arrowPositions;
    public List<float> arrowFades;
    public List<float> arrowDepths;
    public UploadServerDaemon(Computer computer, string serviceName, Color themeColor, OS opSystem, string foldername = null, bool needsAuthentication = false)
    public override string getSaveString()
    public override void initFiles()
    public override void loadInit()
    public override void navigatedTo()
    public void moveToActiveState()
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public void drawBackground(Rectangle bounds, SpriteBatch sb)
    public void drawUploadDetectedEffect(Rectangle bounds, SpriteBatch sb)

`Hacknet.UploadServerDaemon.UploadServerState` (enum) — decompiled/game-proj/Hacknet/UploadServerDaemon.cs:10

`Hacknet.UserDetail` (struct) — decompiled/game-proj/Hacknet/UserDetail.cs:5
    public string name;
    public string pass;
    public byte type;
    public bool known;
    public UserDetail(string user, string password, byte accountType)
    public UserDetail(string user)
    public string getSaveString()
    public static UserDetail loadUserDetail(XmlReader reader)
    public override bool Equals(object obj)
    public override int GetHashCode()

`Hacknet.UsernameGenerator` (class) — decompiled/game-proj/Hacknet/UsernameGenerator.cs:7
    public static string[] names;
    public static string[] delims = new string[1] { "\r\n\r\n" };
    public static int nameIndex;
    public static void init()
    public static string getName()

`Hacknet.Utils` (class) — decompiled/game-proj/Hacknet/Utils.cs:27
    public static float PARRALAX_MULTIPLIER = 1f;
    public static float MIN_DIFF_FOR_PARRALAX = 0.1f;
    public static Random random = new Random();
    public static byte[] byteBuffer = new byte[1];
    public static readonly string LevelStateFilename = "LevelState.lst";
    public static Texture2D white;
    public static Texture2D gradient;
    public static Texture2D gradientLeftRight;
    public static AudioEmitter emitter;
    public static Vector3 vec3;
    public static StorageDevice device;
    public static Color col;
    public static Color VeryDarkGray = new Color(22, 22, 22);
    public static Color SlightlyDarkGray = new Color(100, 100, 100);
    public static Color AddativeWhite = new Color(255, 255, 255, 0);
    public static Color AddativeRed = new Color(255, 15, 15, 0);
    public static HSLColor hslColor = new HSLColor(1f, 1f, 1f);
    public static char[] newlineDelim = new char[1] { '\n' };
    public static string[] robustNewlineDelim = new string[2] { "\r\n", "\n" };
    public static char[] spaceDelim = new char[1] { ' ' };
    public static string[] commaDelim = new string[3] { " ,", ", ", "," };
    public static string[] directorySplitterDelim = new string[2] { "/", "\\" };
    public static string[] WhitespaceDelim = new string[3] { "\r\n", "\n", " " };
    public static LCG LCG = new LCG();
    public static Vector2 getParallax(Vector2 objectPosition, Vector2 CameraPosition, float objectDepth, float focusDepth)
    public static void drawLine(SpriteBatch spriteBatch, Vector2 vector1, Vector2 vector2, Vector2 OffsetPosition, Color Colour, float Depth)
    public static void drawLine(SpriteBatch spriteBatch, Vector2 vector1, Vector2 vector2, Vector2 OffsetPosition, Color Colour, float Depth, Texture2D altTex = null)
    public static void drawLineAlt(SpriteBatch spriteBatch, Vector2 vector1, Vector2 vector2, Vector2 OffsetPosition, Color Colour, float Depth, float width, Texture2D altTex = null)
    public static bool keyPressed(InputState input, Keys key, PlayerIndex? player)
    public static bool buttonPressed(InputState input, Buttons button, PlayerIndex? player)
    public static bool arraysAreTheSame(Keys[] a, Keys[] b)
    public static bool arraysAreTheSame(Buttons[] a, Buttons[] b)
    public static float rand(float range)
    public static float randm(float range)
    public static float rand()
    public static byte getRandomByte()
    public static Rectangle GetFullscreen()
    public static AudioEmitter emitterAtPosition(float x, float y)
    public static AudioEmitter emitterAtPosition(float x, float y, float z)
    public static Texture2D White(ContentManager content)
    public static Color makeColor(byte r, byte g, byte b, byte a)
    public static Color makeColorAddative(Color c)
    public static bool flipCoin()
    public static byte randomCompType()
    public static void writeToFile(string data, string filename)
    public static void SafeWriteToFile(string data, string filename)
    public static void SafeWriteToFile(byte[] data, string filename)
    public static void appendToFile(string data, string filename)
    public static string readEntireFile(string filename)
    public static char getRandomLetter()
    public static char getRandomChar()
    public static char getRandomNumberChar()
    public static Color convertStringToColor(string input)
    public static string convertColorToParseableString(Color c)
    public static void ClipLineSegmentsForRect(Rectangle dest, Vector2 left, Vector2 right, out Vector2 leftOut, out Vector2 rightOut)
    public static Rectangle getClipRectForSpritePos(Rectangle bounds, Texture2D tex, Vector2 pos, float scale)
    public static Rectangle getClipRectForSpritePos(Rectangle bounds, Texture2D tex, Vector2 pos, Vector2 scale)
    public static Rectangle getClipRectForSpritePos(Rectangle bounds, Texture2D tex, Vector2 pos, Vector2 scale, Vector2 origin)
    public static void RenderSpriteIntoClippedRectDest(Rectangle fullBounds, Rectangle targetBounds, Texture2D tex, Color c, SpriteBatch sb)
    public static Vector2 GetCentreOrigin(this Texture2D tex)
    public static string SmartTwimForWidth(string data, int width, SpriteFont font)
    public static Stream GenerateStreamFromString(string s)
    public static string SuperSmartTwimForWidth(string data, int width, SpriteFont font)
    public static float QuadraticOutCurve(float point)
    public static float CubicInCurve(float point)
    public static float CubicOutCurve(float point)
    public static RenderTarget2D GetCurrentRenderTarget()
    public static float SmoothStep(float edge0, float edge1, float x)
    public static Rectangle InsetRectangle(Rectangle rect, int inset)
    public static Vector2 GetNearestPointOnCircle(Vector2 point, Vector2 CircleCentre, float circleRadius)
    public static float Clamp(float val, float min, float max)
    public static Vector2 Clamp(Vector2 val, float min, float max)
    public static string RandomFromArray(string[] array)
    public static string GetNonRepeatingFilename(string filename, string extension, Folder f)
    public static string FlipRandomChars(string original, double chancePerChar)
    public static Vector3 ColorToVec3(Color c)
    public static Color AdditivizeColor(Color c)
    public static Vector2 RotatePoint(Vector2 point, float angle)
    public static bool DebugGoFast()
    public static bool FieldContainsAttributeOfType(FieldInfo field, Type attributeType)
    public static void SendRealWorldEmail(string subject, string to, string body)
    public static string GenerateReportFromException(Exception ex)
    public static string GenerateReportFromExceptionCompact(Exception ex)
    public static bool FloatEquals(float a, float b)
    public static Vector2 PolarToCartesian(float angle, float magnitude)
    public static float GetPolarAngle(Vector2 point)
    public static Vector3 NormalizeRotationVector(Vector3 rot)
    public static void FillEverywhereExcept(Rectangle bounds, Rectangle fullscreen, SpriteBatch sb, Color col)
    public static bool CheckStringIsRenderable(string input)
    public static bool CheckStringIsTitleRenderable(string input)
    public static bool StringContainsInvalidFilenameChars(string input)
    public static string CleanStringToRenderable(string input)
    public static string CleanFilterStringToRenderable(string input)
    public static string CleanStringToLanguageRenderable(string input)
    public static void AppendToErrorFile(string text)
    public static void AppendToWarningsFile(string text)
    public static string SerializeListToCSV(List<string> list)
    public static string[] SplitToTokens(string input)
    public static string[] SplitToTokens(string[] input)
    public static string ExtractBracketedSection(string input, out string bracketedBit)
    public static Color ColorFromHexString(string hexString)
    public static string ReadEntireContentsOfStream(Stream input)
    public static void SendErrorEmail(Exception ex, string postfix = "", string extraData = "")
    public static void SendThreadedErrorReport(Exception ex, string postfix = "", string extraData = "")
    public static DateTime SafeParseDateTime(string input)
    public static string SafeWriteDateTime(DateTime input)
    public static Color GetComplimentaryColor(Color c)
    public static void MeasureTimedSpeechSection()
    public static Color HSL2RGB(double h, double sl, double l)
    public static void RGB2HSL(Color rgb, out double h, out double s, out double l)
    public static void ActOnAllFilesRevursivley(string foldername, Action<string> FileAction)
    public static string GetFileLoadPrefix()
    public static Vector2 ClipVec2ForTextRendering(Vector2 input)
    public static string SerializeObject(object o)
    public static object DeserializeObject(Stream s, Type t)
    public static void ProcessXmlElementInParent(XmlReader rdr, string ParentTag, string ElementTag, Action ProcessElement)
    public static bool PublicInstancePropertiesEqual<T>(T self, T to, params string[] ignore) where T : class
    public static Rectangle DrawSpriteAspectCorrect(Rectangle dest, SpriteBatch sb, Texture2D sprite, Color color, bool ForceToBottom = false)
    public static int GetXForAlignment(AlignmentX align, int width, int margin, int objectWidth)
    public static void LoadImageFromContentOrExtension(string imagePath, ContentManager content, Action<Texture2D> LoadComplete)
    public static float RobustReadAsFloat(XmlReader rdr)
    public static string[] GetQuoteSeperatedArgs(string[] args)
    public static SpriteFont GetTitleFontForLocalizedString(string data)
    public static void DrawStringMonospace(SpriteBatch spriteBatch, string text, SpriteFont font, Vector2 pos, Color c, float charWidth)

`Hacknet.VehicleInfo` (class) — decompiled/game-proj/Hacknet/VehicleInfo.cs:6
    public static List<VehicleType> vehicleTypes;
    public static void init()
    public static VehicleRegistration getRandomRegistration()

`Hacknet.VehicleRegistration` (class) — decompiled/game-proj/Hacknet/VehicleRegistration.cs:3
    public VehicleType vehicle;
    public string licencePlate;
    public string licenceNumber;
    public VehicleRegistration()
    public VehicleRegistration(VehicleType vehicleType, string plate, string regNumber)
    public override string ToString()

`Hacknet.VehicleType` (class) — decompiled/game-proj/Hacknet/VehicleType.cs:3
    public string model;
    public string maker;
    public VehicleType()
    public VehicleType(string vModel, string vMaker)

`Hacknet.WebRenderer` (class) — decompiled/game-proj/Hacknet/WebRenderer.cs:9
    public static WebRenderer instance;
    public static bool Enabled = true;
    public static Texture2D texture;
    public static byte[] texBuffer;
    public static XNAWebRenderer.TextureUpdatedDelegate textureUpdated = TextureUpdated;
    public static GraphicsDevice graphics;
    public static int width = 500;
    public static int height = 500;
    public static string url = "http://www.google.com";
    public static bool loadingPage;
    public static void setSize(int frameWidth, int frameHeight)
    public static void init(GraphicsDevice gd)
    public static void navigateTo(string urlTo)
    public static void TextureUpdated(IntPtr buffer)
    public static void drawTo(Rectangle bounds, SpriteBatch sb)
    public static string getURL()

`Hacknet.WebServerDaemon` (class) — decompiled/game-proj/Hacknet/WebServerDaemon.cs:10
    public const string ROOT_FOLDERNAME = "web";
    public const string DEFAULT_PAGE_FILE = "index.html";
    public const string TEMP_WEBPAGE_CACHE_FILENAME = "/Content/Web/Cache/HN_OS_WebCache.html";
    public const string DEFAULT_PAGE_DATA_LOCATION = "Content/Web/BaseImageWebPage.html";
    public const string COMPANY_NAME_SENTINAL = "#$#COMPANYNAME#$#";
    public const string COMPANY_NAME_COMPACT_SENTINAL = "#$#LC_COMPANYNAME#$#";
    public const int BASE_BAR_HEIGHT = 16;
    public static string BaseWebpageData;
    public static string BaseComnayPageData;
    public string webPageFileLocation;
    public Folder root;
    public FileEntry lastLoadedFile;
    public bool shouldShow404 = false;
    public string saveURL = "";
    public WebServerDaemon(Computer computer, string serviceName, OS opSystem, string pageFileLocation = "Content/Web/BaseImageWebPage.html")
    public override void initFiles()
    public override void loadInit()
    public void generateBaseCorporateSite(string companyName, string targetBaseFile = "Content/Web/BaseCorporatePage.html")
    public virtual void LoadWebPage(string url = "index.html")
    public virtual void ShowPage(string pageData)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public virtual void showSourcePressed()
    public override void navigatedTo()
    public override string getSaveString()

`Hacknet.WhitelistConnectionDaemon` (class) — decompiled/game-proj/Hacknet/WhitelistConnectionDaemon.cs:8
    public const string SystemFilename = "authenticator.dll";
    public const string ListFilename = "list.txt";
    public const string SourceFilename = "source.txt";
    public Folder folder;
    public string RemoteSourceIP = null;
    public bool AuthenticatesItself = true;
    public bool HasAllowedSingleTimeAdminPassthroughThisSession = false;
    public override void initFiles()
    public override void loadInit()
    public override void navigatedTo()
    public void DisconnectTarget()
    public bool RemoteCompCanBeAccessed()
    public bool IPCanPassWhitelist(string ip, bool isFromRemote)
    public override void draw(Rectangle bounds, SpriteBatch sb)
    public override string getSaveString()

`Hacknet.WorldLocation` (class) — decompiled/game-proj/Hacknet/WorldLocation.cs:3
    public string country;
    public string name;
    public float educationLevel;
    public float lifeLevel;
    public float employerLevel;
    public float affordabilityLevel;
    public WorldLocation()
    public WorldLocation(string countryName, string locName, float education, float life, float employer, float affordability)
    public new string ToString()

`Hacknet.WorldLocationLoader` (class) — decompiled/game-proj/Hacknet/WorldLocationLoader.cs:7
    public static List<WorldLocation> locations;
    public static void init()
    public static WorldLocation getRandomLocation()
    public static WorldLocation getClosestOrCreate(string name)

`Hacknet.XMLContentAttribute` (class) — decompiled/game-proj/Hacknet/XMLContentAttribute.cs:5

### Hacknet.DLC.MarkovTextGenerator

`Hacknet.DLC.MarkovTextGenerator.Corpus` (class) — decompiled/game-proj/Hacknet.DLC.MarkovTextGenerator/Corpus.cs:10
    public const string DICT_FILE_TYPE = ".csd";
    public const string BODY_FILE_TYPE = ".csb";
    public const string EMPTY_STRING_KEY_WILDCARD = "\tEMPTY\t";
    public int LearningLength = 3;
    public Dictionary<string, Dictionary<string, int>> data = new Dictionary<string, Dictionary<string, int>>();
    public List<PotentialEntry> GetPotentialEntries(string preceeding)
    public void Serialize(string filename)
    public void LearnText(string input)
    public static bool WordEndsWithSentenceEnder(string word)
    public static string ConvertMemoryToString(List<string> memory)
    public string GetAnalysisStringFromWordList(List<string> words)
    public string GenerateSentence(Action<string> CompleteAction = null)
    public void GenerateSentenceThreaded(Action<string> complete)

`Hacknet.DLC.MarkovTextGenerator.CorpusGenerator` (class) — decompiled/game-proj/Hacknet.DLC.MarkovTextGenerator/CorpusGenerator.cs:8
    public static Corpus GenerateCorpusFromFolder(string name, int maxFilesToRead = -1, Action<float, string> percentCompleteUpdated = null, Action<Corpus> Complete = null)
    public static void GenerateCorpusFromFolderThreaded(string foldername, Action<Corpus> Complete, Action<float, string> percentCompleteUpdated)

`Hacknet.DLC.MarkovTextGenerator.PotentialEntry` (class) — decompiled/game-proj/Hacknet.DLC.MarkovTextGenerator/PotentialEntry.cs:3
    public string word;
    public double weighting;
    public override string ToString()

### Hacknet.Daemons.Helpers

`Hacknet.Daemons.Helpers.AircraftAltitudeIndicator` (class) — decompiled/game-proj/Hacknet.Daemons.Helpers/AircraftAltitudeIndicator.cs:9
    public static Texture2D WarningIcon;
    public static Texture2D PlaneIcon;
    public static void Init(ContentManager content)
    public static void RenderAltitudeIndicator(Rectangle dest, SpriteBatch sb, int currentAltitude, bool IsInCriticalDescenet, bool IconFlashIsVisible, int maxAltitude = 50000, int upperReccomended = 40000, int lowerReccomended = 30000, int warningArea = 14000, int criticalFailureArea = 3000)
    public static int GetHeightForAltitude(int altitude, int maxAltitude, Rectangle glowBar)
    public static void DrawIndicatorForAltitude(Rectangle dest, int altitude, string ElementTitle, int totalAltitude, Rectangle totalBar, SpriteBatch sb, Color c, bool LineAtTop = false, bool useGradientBacking = false)
    public static bool GetFlashRateFromTimer(float timer)

`Hacknet.Daemons.Helpers.AttachmentRenderer` (class) — decompiled/game-proj/Hacknet.Daemons.Helpers/AttachmentRenderer.cs:8
    public static string[] spaceDelim = new string[1] { "#%#" };
    public static bool RenderAttachment(string data, object osObj, Vector2 dpos, int startingButtonIndex, SoundEffect buttonSound)
    public static void DrawButtonGlow(Vector2 dpos, Vector2 labelSize, OS os)

`Hacknet.Daemons.Helpers.BasicMedicalMonitor` (class) — decompiled/game-proj/Hacknet.Daemons.Helpers/BasicMedicalMonitor.cs:9
    public CLinkBuffer<MonitorRecordKeypoint> data = new CLinkBuffer<MonitorRecordKeypoint>(1024);
    public Func<float, float, List<MonitorRecordKeypoint>> UpdateAction;
    public Func<float, float, List<MonitorRecordKeypoint>> HeartBeatAction;
    public BasicMedicalMonitor(Func<float, float, List<MonitorRecordKeypoint>> updateAction, Func<float, float, List<MonitorRecordKeypoint>> heartbeatAction)
    public float GetCurrentValue(float timeRollback)
    public void Update(float dt)
    public void HeartBeat(float beatTime)
    public void Draw(Rectangle bounds, SpriteBatch sb, Color c, float timeRollback)

`Hacknet.Daemons.Helpers.BasicMedicalMonitor.MonitorRecordKeypoint` (struct) — decompiled/game-proj/Hacknet.Daemons.Helpers/BasicMedicalMonitor.cs:11
    public float timeOffset;
    public float value;

`Hacknet.Daemons.Helpers.IMedicalMonitor` (interface) — decompiled/game-proj/Hacknet.Daemons.Helpers/IMedicalMonitor.cs:6

`Hacknet.Daemons.Helpers.IRCSystem` (class) — decompiled/game-proj/Hacknet.Daemons.Helpers/IRCSystem.cs:10
    public const string ACTIVE_LOG_FILENAME = "active.log";
    public const string ATTACHMENT_FLAG_PREFIX = "!ATTACHMENT:";
    public const string ANNOUNCE_FLAG_PREFIX = "!ANNOUNCEMENT!";
    public const string EntryLineDelimiterMarker = "\n#";
    public static string[] EntryLineDelimiter = new string[1] { "\n#" };
    public Folder StorageFolder;
    public FileEntry ActiveLogFile;
    public int DrawnButtonIndex = 0;
    public int messagesAddedSinceLastView = 0;
    public bool isCurrentlyBeingViewed = true;
    public SoundEffect AttachmentPressedSound = null;
    public Action<string, string> LogAdded;
    public IRCSystem(Folder storageFolder)
    public List<IRCLogEntry> GetLogsFromFile()
    public void AddLog(string author, string message, double timestampSecondsOffset)
    public void AddLog(string author, string message, string timestamp = null)
    public void LeftView()
    public void Draw(Rectangle dest, SpriteBatch sb, bool CanWrite, string WriteUsername, Dictionary<string, Color> HighlightKeywords)
    public void DrawLog(Rectangle dest, SpriteBatch sb, Dictionary<string, Color> HighlightKeywords)
    public int DrawLogEntry(IRCLogEntry log, Rectangle startingDest, SpriteBatch sb, Dictionary<string, Color> HighlightKeywords, int lineHeight, int linesRemaining, int yNotToPass, bool needsNewMessagesLineDraw, out Rectangle dest)
    public void DrawLine(string line, Rectangle dest, SpriteBatch sb, Color defaultColor)

`Hacknet.Daemons.Helpers.IRCSystem.IRCLogEntry` (struct) — decompiled/game-proj/Hacknet.Daemons.Helpers/IRCSystem.cs:12
    public const string Delimiter = "//";
    public const string SerializationDelimiterReplacement = "&dsr";
    public static string[] SplitDelmiter = new string[1] { "//" };
    public string Message;
    public string Timestamp;
    public string Author;
    public string Serialize()
    public static IRCLogEntry Deserialize(string entry)
    public static IRCLogEntry DeserializeSafe(string entry)

### Hacknet.Effects

`Hacknet.Effects.ActiveEffectsUpdater` (class) — decompiled/game-proj/Hacknet.Effects/ActiveEffectsUpdater.cs:5
    public bool ScreenBleedActive = false;
    public float ScreenBleedTimeLeft = 0f;
    public float ScreenBleedStartTime = 0f;
    public string screenBleedTitle = "UNKNOWN";
    public string screenBleedL1;
    public string screenBleedL2;
    public string screenBleedL3;
    public string screenBleedCompleteAction = null;
    public OSTheme oldTheme;
    public string oldThemePath;
    public CustomTheme oldCustomTheme;
    public OSTheme newTheme;
    public string newThemePath;
    public CustomTheme newCustomTheme;
    public float themeSwapTimeRemaining;
    public float originalThemeSwapTime;
    public void Update(float dt, object osobj)
    public void StartThemeSwitch(float time, OSTheme newTheme, object osobj, string customThemePath = null)
    public void CompleteThemeSwap(object osobj)
    public void StartScreenBleed(float time, string title, string line1, string line2, string line3, string completeAction)
    public void CancelScreenBleedEffect()

`Hacknet.Effects.AudioVisualizer` (class) — decompiled/game-proj/Hacknet.Effects/AudioVisualizer.cs:11
    public VisualizationData visData = new VisualizationData();
    public List<ReadOnlyCollection<float>> samplesHistory;
    public ReadOnlyCollection<float> previousSamples = null;
    public double SecondsSinceLastDataUpdate = 0.0;
    public void Draw(Rectangle bounds, SpriteBatch sb)

`Hacknet.Effects.BarcodeEffect` (class) — decompiled/game-proj/Hacknet.Effects/BarcodeEffect.cs:8
    public const double MAX_WIDTH = 8.0;
    public const double MAX_OFFSET = 2.0;
    public const double MAX_DELAY = 3.0;
    public const float COMPLETE_TIME = 4f;
    public const float COMPLETE_TIME_DELAY = 0.9f;
    public List<float> widths;
    public List<float> offsets;
    public List<float> delays;
    public List<float> complete;
    public float completeTime = 0f;
    public int maxWidth;
    public bool isInverted = false;
    public bool leftRightBias = false;
    public bool LeftRightMaxSizeFalloff = false;
    public BarcodeEffect(int width, bool inverted = false, bool leftRight = false)
    public void reset()
    public void Update(float t)
    public void Draw(int x, int y, int maxWidth, int maxHeight, SpriteBatch sb, Color? barColor = null)

`Hacknet.Effects.Cube3D` (class) — decompiled/game-proj/Hacknet.Effects/Cube3D.cs:7
    public const int NUM_VERTICES = 36;
    public const int NUM_INDICIES = 14;
    public static VertexPositionNormalTexture[] verts;
    public static VertexBuffer vBuffer;
    public static IndexBuffer ib;
    public static BasicEffect wireframeEfect;
    public static RasterizerState wireframeRaster;
    public static void Initilize(GraphicsDevice gd)
    public static void ResetBuffers()
    public static void RenderWireframe(Vector3 position, float scale, Vector3 rotation, Color color)
    public static void RenderWireframe(Vector3 position, float scale, Vector3 rotation, Color color, Vector3 cameraOffset)
    public static void ConstructCube()

`Hacknet.Effects.DepthDotGridEffect` (class) — decompiled/game-proj/Hacknet.Effects/DepthDotGridEffect.cs:8
    public Texture2D tex;
    public DepthDotGridEffect(ContentManager content)
    public void DrawGrid(Rectangle fullAreaDest, Vector2 xyOffset, SpriteBatch sb, float pixelsInOnRecurse, int recursionSteps, Color dotColor, float dotSeperation, float dotSize, float MaxDepthEffectDistance, float timer, float chaosPercent)

`Hacknet.Effects.ExplodingUIElementEffect` (class) — decompiled/game-proj/Hacknet.Effects/ExplodingUIElementEffect.cs:9
    public List<Texture2D> Textures = new List<Texture2D>();
    public List<ExplosionParticle> Particles = new List<ExplosionParticle>();
    public void Init(ContentManager content)
    public void Explode(int particles, Vector2 AngleRange, Vector2 startPos, float minScale, float maxScale, float minSpeed, float maxSpeed, float minFriction, float maxFriction, float minStartFade, float maxStartFade)
    public void Update(float dt)
    public void Render(SpriteBatch sb)

`Hacknet.Effects.ExplodingUIElementEffect.ExplosionParticle` (struct) — decompiled/game-proj/Hacknet.Effects/ExplodingUIElementEffect.cs:11
    public Vector2 Pos;
    public float angle;
    public float speed;
    public float rotation;
    public float rotationRate;
    public float fade;
    public int textureIndex;
    public float Size;
    public float frictionSlowdownPerSec;
    public float FlickerOffset;

`Hacknet.Effects.FlickeringTextEffect` (class) — decompiled/game-proj/Hacknet.Effects/FlickeringTextEffect.cs:8
    public static float internalTimer = 0f;
    public static RenderTarget2D LinedItemTarget = null;
    public static SpriteBatch LinedItemSB;
    public static void DrawFlickeringText(Rectangle dest, string text, float maxOffset, float rarity, SpriteFont font, object os_Obj, Color BaseCol)
    public static void DrawLinedFlickeringText(Rectangle dest, string text, float maxOffset, float rarity, SpriteFont font, object os_Obj, Color BaseCol, int segmentHeight = 2)
    public static void DrawFlickeringSprite(SpriteBatch sb, Rectangle dest, Texture2D texture, float maxOffset, float rarity, object os_Obj, Color BaseCol)
    public static void DrawFlickeringSpriteAltWeightings(SpriteBatch sb, Rectangle dest, Texture2D texture, float maxOffset, float rarity, object os_Obj, Color BaseCol)
    public static void DrawFlickeringSpriteFull(SpriteBatch sb, Vector2 pos, float rotation, Vector2 scale, Vector2 origin, Texture2D texture, float timerOffset, float maxOffset, float rarity, object os_Obj, Color BaseCol)
    public static float GetOffsetForSinTime(float frequency, float offset, float rarity, object os_Obj)
    public static Vector2 VecAddX(Vector2 vec, float x)
    public static Rectangle RectAddX(Rectangle rect, int x)
    public static string GetReportString()

`Hacknet.Effects.FlyoutEffect` (class) — decompiled/game-proj/Hacknet.Effects/FlyoutEffect.cs:8
    public static RenderTarget2D PooledTarget;
    public static RenderTarget2D PooledBackTarget;
    public RenderTarget2D DrawTarget;
    public RenderTarget2D BackTarget;
    public SpriteBatch innerBatch;
    public Vector2 offset = Vector2.Zero;
    public Texture2D UsedSprite;
    public Texture2D FlashSprite;
    public float timeSinceRender = 0f;
    public float rotation = 0f;
    public float elapsedTime = 0f;
    public float TimeBetweenRenderings = 0.1f;
    public FlyoutEffect(GraphicsDevice gd, ContentManager c, int width, int height)
    public void Draw(float dt, Rectangle dest, SpriteBatch sb, float cornerIn, int spiralElements, float spiralRadius, Color color, bool drawFlashFromMiddle, bool flashBackground)
    public void Draw(float dt, Rectangle dest, SpriteBatch sb, Action<SpriteBatch, Rectangle> render)
    public void Dispose()

`Hacknet.Effects.GridEffect` (class) — decompiled/game-proj/Hacknet.Effects/GridEffect.cs:7
    public static void DrawGridBackground(Rectangle dest, SpriteBatch sb, int desiredNumOfBlocks, Color CrossColor)

`Hacknet.Effects.HexGridBackground` (class) — decompiled/game-proj/Hacknet.Effects/HexGridBackground.cs:9
    public Texture2D Hex;
    public Texture2D Outline;
    public float timer = 0f;
    public LCG lcg = new LCG(microsoft: false);
    public bool HasRedFlashyOnes = false;
    public float HexScale = 0.1f;
    public HexGridBackground(ContentManager content)
    public void Update(float dt)
    public void Draw(Rectangle dest, SpriteBatch sb, Color first, Color second, ColoringAlgorithm algorithm = ColoringAlgorithm.CorrectedSinWash, float angle = 0f)

`Hacknet.Effects.HexGridBackground.ColoringAlgorithm` (enum) — decompiled/game-proj/Hacknet.Effects/HexGridBackground.cs:11

`Hacknet.Effects.MovingBarsEffect` (class) — decompiled/game-proj/Hacknet.Effects/MovingBarsEffect.cs:7
    public List<BarLine> Lines = new List<BarLine>();
    public float MinLineChangeTime = 0.2f;
    public float MaxLineChangeTime = 2f;
    public bool IsInverted = false;
    public void Update(float dt)
    public void Draw(SpriteBatch sb, Rectangle bounds, float minHeight, float lineWidth, float lineSeperation, Color drawColor)

`Hacknet.Effects.MovingBarsEffect.BarLine` (struct) — decompiled/game-proj/Hacknet.Effects/MovingBarsEffect.cs:9
    public float Current;
    public float Next;
    public float TimeRemaining;
    public float TotalTimeThisStep;

`Hacknet.Effects.NodeBounceEffect` (class) — decompiled/game-proj/Hacknet.Effects/NodeBounceEffect.cs:8
    public float TimeBetweenBounces = 0.07f;
    public float NodeHitDelay = 0.2f;
    public List<Vector2> locations = new List<Vector2>();
    public float timeToNextBounce = 0f;
    public float delayTillNextBounce = 0f;
    public int maxNodes = 200;
    public NodeBounceEffect()
    public void Update(float t, Action<Vector2> nodeHitAction = null)
    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, Color lineColor, Color nodeColor)

`Hacknet.Effects.PointGatherEffect` (class) — decompiled/game-proj/Hacknet.Effects/PointGatherEffect.cs:9
    public List<StarPoint> Points = new List<StarPoint>();
    public float timeRemainingWithoutAttract = 0f;
    public float MaxSpeed = 1.6f;
    public float Friction = 0.001f;
    public float NoAttractPhaseFriction = 1.7f;
    public float GravityConstant = 0.001f;
    public float absorbDistance = 0.005f;
    public float attractionToCentreMass = 1f;
    public float GlowScaleMod = 1f;
    public float LineLengthPercentage = 1f;
    public bool AllowDoubleLines = false;
    public float starSize = -1f;
    public Texture2D CircleTex;
    public Texture2D ScanlinesBackground;
    public Texture2D Star;
    public Texture2D LineTexture;
    public Color NodeColor = Utils.AddativeWhite * 0.65f;
    public void Init(ContentManager content)
    public void Explode(int entities)
    public StarPoint ProcessAttractionBetweenPoints(StarPoint pA, StarPoint pB, float dt, bool constantForce = false)
    public void FlashComplete()
    public void Update(float dt)
    public int ResolveMergesThisFrame()
    public void Render(Rectangle dest, SpriteBatch sb)

`Hacknet.Effects.PointGatherEffect.StarPoint` (struct) — decompiled/game-proj/Hacknet.Effects/PointGatherEffect.cs:11
    public Vector2 Pos;
    public Vector2 Velocity;
    public float size;
    public bool AttractedToOthers;
    public float drawnSize;

`Hacknet.Effects.PortHackCubeSequence` (class) — decompiled/game-proj/Hacknet.Effects/PortHackCubeSequence.cs:6
    public float rotTime = 0f;
    public float elapsedTime = 0f;
    public float startup = 0.3f;
    public float spinup = 0f;
    public float runtime = 0f;
    public float spindown = 0f;
    public float idle = 0f;
    public bool HeartFadeSequenceComplete = false;
    public bool ShouldCentralSpinInfinitley = false;
    public void Reset()
    public void DrawSequence(Rectangle dest, float t, float totalTime)
    public void DrawHeartSequence(Rectangle dest, float t, float totalTime)

`Hacknet.Effects.RaindropsEffect` (class) — decompiled/game-proj/Hacknet.Effects/RaindropsEffect.cs:9
    public List<Vector3> Drops = new List<Vector3>();
    public List<Vector3> Circles = new List<Vector3>();
    public List<Vector3> FadeoutLines = new List<Vector3>();
    public float FallRate = 1f;
    public float CircleExpandRate = 0.4f;
    public float LineFadeoutRate = 2f;
    public float MaxVerticalLandingVariane = 0.025f;
    public Texture2D Circle;
    public Texture2D Gradient;
    public Texture2D FlashImage;
    public void Init(ContentManager content)
    public void ForceSpawnDrop(Vector3 dropData)
    public void Update(float dt, float dropsAddedPerSecond)
    public void Render(Rectangle dest, SpriteBatch sb, Color DropColor, float maxCircleRadius, float maxFlashWidth)

`Hacknet.Effects.ShiftingGridEffect` (class) — decompiled/game-proj/Hacknet.Effects/ShiftingGridEffect.cs:6
    public ShiftingGridSpot[,] themeGrid = new ShiftingGridSpot[20, 100];
    public ShiftingGridEffect()
    public ShiftingGridEffect(int patternWidth, int patternHeight)
    public void ResetThemeGrid()
    public void ResetGridPoint(int x, int y)
    public void Update(float t)
    public void RenderGrid(Rectangle bounds, SpriteBatch sb, Color c1, Color c2, Color c3, bool centreEffect = false)

`Hacknet.Effects.ShiftingGridEffect.ShiftingGridSpot` (struct) — decompiled/game-proj/Hacknet.Effects/ShiftingGridEffect.cs:8
    public float from;
    public float to;
    public float time;
    public float totalTime;

`Hacknet.Effects.TextWriterTimed` (class) — decompiled/game-proj/Hacknet.Effects/TextWriterTimed.cs:3
    public static int WriteTextToTerminal(string wholeText, object osObj, float timePerChar, float normalLettersDelayForNewline, float normalLetterDelayForWildcard, float elapsedTimeSoFar, int charsRenderedSoFar)

`Hacknet.Effects.ThinBarcode` (class) — decompiled/game-proj/Hacknet.Effects/ThinBarcode.cs:8
    public List<int> widths = new List<int>();
    public List<int> gaps = new List<int>();
    public List<int> widthsLast = new List<int>();
    public List<int> gapsLast = new List<int>();
    public int height;
    public int oldW;
    public ThinBarcode(int w, int height)
    public void regenerate()
    public void Draw(SpriteBatch sb, int posX, int posY, Color c)

`Hacknet.Effects.TraceDangerSequence` (class) — decompiled/game-proj/Hacknet.Effects/TraceDangerSequence.cs:11
    public const float WARNING_INTRO_TIME = 1f;
    public const float WARNING_EXIT_TIME = 13.9f;
    public const float DISCONNECT_REBOOT_TIME = 10f;
    public const float COUNTDOWN_TIME = 130f;
    public const float FLASH_FREQUENCY = 1.9376667f;
    public bool IsActive = false;
    public bool PreventOSRendering = false;
    public SpriteFont titleFont;
    public SpriteFont bodyFont;
    public Rectangle fullscreen;
    public SpriteBatch spriteBatch;
    public SpriteBatch scaleupSpriteBatch;
    public OS os;
    public SoundEffect spinDownSound;
    public SoundEffect spinUpSound;
    public SoundEffect impactSound;
    public float onBeatFlashTimer = 0f;
    public float timeThisState = 0f;
    public float percentComplete = 0f;
    public TraceDangerState state = TraceDangerState.WarningScrenIntro;
    public string oldSong = null;
    public bool warningScreenIsActivating = false;
    public static Color DarkRed = new Color(105, 0, 0, 200);
    public static Color BackgroundRed = new Color(120, 0, 0);
    public TraceDangerSequence(ContentManager content, SpriteBatch sb, Rectangle fullscreenRect, OS os)
    public void BeginTraceDangerSequence()
    public void CompleteIPResetSucsesfully()
    public void CancelTraceDangerSequence()
    public void Update(float t)
    public void Draw()
    public void DrawDisconnectedScreen()
    public void DrawWarningScreen()
    public Vector2 DrawFlashInString(string text, Vector2 pos, float offset, float transitionInTime = 0.2f, bool hasDots = false, float dotsDelayer = 0.2f)
    public static void DrawCountdownOverlay(SpriteFont titleFont, SpriteFont bodyFont, object osobj, string title = null, string l1 = null, string l2 = null, string l3 = null)
    public void DrawFlashingRedBackground()

`Hacknet.Effects.TraceDangerSequence.TraceDangerState` (enum) — decompiled/game-proj/Hacknet.Effects/TraceDangerSequence.cs:13

`Hacknet.Effects.TunnelingCircleEffect` (class) — decompiled/game-proj/Hacknet.Effects/TunnelingCircleEffect.cs:7
    public static Texture2D CircleTex;
    public static void Draw(SpriteBatch sb, Vector2 circleCentre, float diamater, float nodeDiamater, int subdivisions, float timer, Color nodeColor, Color lineColor, Color ZeroNodeColor, Rectangle clipBounds)

`Hacknet.Effects.WebpageLoadingEffect` (class) — decompiled/game-proj/Hacknet.Effects/WebpageLoadingEffect.cs:8
    public static void DrawLoadingEffect(Rectangle bounds, SpriteBatch sb, object OS_obj, bool drawLoadingText = true)

`Hacknet.Effects.ZoomingDotGridEffect` (class) — decompiled/game-proj/Hacknet.Effects/ZoomingDotGridEffect.cs:7
    public static Texture2D CircleTex;
    public static void Render(Rectangle dest, SpriteBatch sb, float timer, Color themeColor)
    public static Vector2 rotatePointAroundOrigin(Vector2 point, Vector2 origin, float rotation)

### Hacknet.Extensions

`Hacknet.Extensions.ExtensionInfo` (class) — decompiled/game-proj/Hacknet.Extensions/ExtensionInfo.cs:9
    public const string INFO_FILENAME = "ExtensionInfo.xml";
    public const string LOGO_FILENAME = "Logo";
    public const string NODES_FOLDER = "/Nodes";
    public const string MISSIONS_FOLDER = "/Missions";
    public string Name;
    public string Language;
    public string FolderPath;
    public string StartingMissionPath;
    public string StartingActionsPath;
    public string Description = "";
    public bool AllowSave = true;
    public bool StartsWithTutorial = false;
    public bool HasIntroStartup = true;
    public string Theme = "HacknetBlue";
    public string IntroStartupSong = null;
    public float IntroStartupSongDelay = 0f;
    public string[] StartingVisibleNodes = new string[0];
    public List<string> FactionDescriptorPaths = new List<string>();
    public Texture2D LogoImage;
    public string SequencerTargetID;
    public string SequencerFlagRequiredForStart;
    public string ActionsToRunOnSequencerStart;
    public float SequencerSpinUpTime = 17f;
    public string WorkshopDescription;
    public string WorkshopLanguage;
    public byte WorkshopVisibility = 2;
    public string WorkshopTags;
    public string WorkshopPreviewImagePath;
    public string WorkshopPublishID;
    public string GetFullFolderPath()
    public static ExtensionInfo ReadExtensionInfo(string folderpath)
    public static void VerifyExtensionInfo(ExtensionInfo info)
    public static bool ExtensionExists(string folderpath)
    public string GetFoldersafeName()

`Hacknet.Extensions.ExtensionLoader` (class) — decompiled/game-proj/Hacknet.Extensions/ExtensionLoader.cs:9
    public static ExtensionInfo ActiveExtensionInfo = null;
    public static void LoadNewExtensionSession(ExtensionInfo info, object os_obj)
    public static void LoadExtensionStartTrackAsCurrentSong(ExtensionInfo info)
    public static void SendStartingEmailForActiveExtensionNextFrame(object os_obj)
    public static void CheckAndAssignCoreServer(Computer c, OS os)
    public static void ReloadExtensionNodes(object osobj)

### Hacknet.ExternalCounterparts

`Hacknet.ExternalCounterparts.ExternalNetworkedServer` (class) — decompiled/game-proj/Hacknet.ExternalCounterparts/ExternalNetworkedServer.cs:9
    public const int BUFFER_SIZE = 4096;
    public Action<string> messageReceived;
    public static ASCIIEncoding encoder;
    public List<TcpClient> connections;
    public Dictionary<NetworkStream, byte[]> buffers;
    public TcpListener listener;
    public ExternalNetworkedServer()
    public void initializeListener()
    public void closeServer()
    public void AcceptTcpConnectionCallback(IAsyncResult ar)
    public void TcpReadCallback(IAsyncResult ar)

### Hacknet.Factions

`Hacknet.Factions.AllFactions` (class) — decompiled/game-proj/Hacknet.Factions/AllFactions.cs:6
    public Dictionary<string, Faction> factions = new Dictionary<string, Faction>();
    public string currentFaction;
    public AllFactions()
    public void init()
    public string getSaveString()
    public void setCurrentFaction(string newFaction, OS os)
    public static AllFactions loadFromSave(XmlReader xmlRdr)

`Hacknet.Factions.CustomFaction` (class) — decompiled/game-proj/Hacknet.Factions/CustomFaction.cs:8
    public List<CustomFactionAction> CustomActions = new List<CustomFactionAction>();
    public CustomFaction(string _name, int _neededValue)
    public static CustomFaction ParseFromFile(string filepath)
    public void CheckForAllCustomActionsToRun(object os_obj)
    public override void addValue(int value, object os_obj)
    public void SendNotification(object osIn, string body, string subject)
    public override string getSaveString()
    public static CustomFaction DeserializeFromXmlReader(XmlReader rdr, string name, string id, int playerVal, bool playerHasPassed)

`Hacknet.Factions.CustomFactionAction` (class) — decompiled/game-proj/Hacknet.Factions/CustomFactionAction.cs:9
    public const string XML_ELEMENT_NAME = "Action";
    public int ValueRequiredForTrigger = 10;
    public string FlagsRequiredForTrigger = null;
    public List<SerializableAction> TriggerActions = new List<SerializableAction>();
    public static CustomFactionAction Deserialize(XmlReader rdr)
    public string GetSaveString()
    public void Trigger(object os_obj)

`Hacknet.Factions.EntropyFaction` (class) — decompiled/game-proj/Hacknet.Factions/EntropyFaction.cs:5
    public EntropyFaction(string _name, int _neededValue)
    public override void addValue(int value, object os)
    public override void playerPassedValue(object os)

`Hacknet.Factions.HubFaction` (class) — decompiled/game-proj/Hacknet.Factions/HubFaction.cs:6
    public HubFaction(string _name, int _neededValue)
    public override void addValue(int value, object os)
    public void ForceStartBitMissions(object os)
    public void SendNotification(object osIn, string body, string subject)
    public void SendNotification(object osIn, string contractName)
    public void SendAssetAddedNotification(object osIn)

### Hacknet.Gui

`Hacknet.Gui.Button` (class) — decompiled/game-proj/Hacknet.Gui/Button.cs:9
    public const int BORDER_WIDTH = 1;
    public static bool wasPressedDown = false;
    public static bool wasReleased = false;
    public static bool drawingOutline = true;
    public static bool outlineOnly = false;
    public static bool smallButtonDraw = false;
    public static bool DisableIfAnotherIsActive = false;
    public static bool ForceNoColorTag = false;
    public static bool doButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor)
    public static bool doButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor, Texture2D tex)
    public static void drawButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor, Texture2D tex)
    public static void drawModernButton(int myID, int x, int y, int width, int height, string text, Color? selectedColor, Texture2D tex)
    public static bool doHoldDownButton(int myID, int x, int y, int width, int height, string text, bool hasOutline, Color? outlineColor, Color? selectedColor)

`Hacknet.Gui.CheckBox` (class) — decompiled/game-proj/Hacknet.Gui/CheckBox.cs:5
    public const int WIDTH = 20;
    public const int HEIGHT = 20;
    public const int INTERIOR_BORDER = 4;
    public static bool doCheckBox(int myID, int x, int y, bool isChecked, Color? selectedColor)
    public static bool doCheckBox(int myID, int x, int y, bool isChecked, Color? selectedColor, string text)

`Hacknet.Gui.DraggableRectangle` (class) — decompiled/game-proj/Hacknet.Gui/DraggableRectangle.cs:6
    public static bool isDragging = false;
    public static Vector2 originalClickPos;
    public static Vector2 originalClickOffset;
    public static Vector2 doDraggableRectangle(int myID, float x, float y, int width, int height)
    public static Vector2 doDraggableRectangle(int myID, float x, float y, int width, int height, float selectableBorder, Color? selectedColor, Color? deselectedColor)
    public static Vector2 doDraggableRectangle(int myID, float x, float y, int width, int height, float selectableBorder, Color? selectedColor, Color? deselectedColor, bool canMoveY, bool canMoveX, float xMax, float yMax, float xMin, float yMin)

`Hacknet.Gui.RenderedRectangle` (class) — decompiled/game-proj/Hacknet.Gui/RenderedRectangle.cs:6
    public static void doRectangle(int x, int y, int width, int height, Color? color)
    public static void doRectangle(int x, int y, int width, int height, Color? color, bool blocking)
    public static void doRectangleOutline(int x, int y, int width, int height, int thickness, Color? color)

`Hacknet.Gui.ScrollablePanel` (class) — decompiled/game-proj/Hacknet.Gui/ScrollablePanel.cs:8
    public static Stack<RenderTarget2D> targets;
    public static Stack<SpriteBatch> batches;
    public static List<RenderTarget2D> targetPool;
    public static List<SpriteBatch> batchPool;
    public static Color scrollBarColor = new Color(120, 120, 120, 80);
    public static Stack<Vector2> offsetStack;
    public static void beginPanel(int id, Rectangle drawbounds, Vector2 scroll)
    public static Vector2 endPanel(int id, Vector2 scroll, Rectangle bounds, float maxScroll, bool onlyScrollWithMouseOver = false)
    public static void ClearCache()

`Hacknet.Gui.ScrollBar` (class) — decompiled/game-proj/Hacknet.Gui/ScrollBar.cs:6
    public static bool AlwaysDrawUnderBar = false;
    public static float doVerticalScrollBar(int id, int xPos, int yPos, int drawWidth, int drawHeight, int contentHeight, float scroll)

`Hacknet.Gui.SelectableTextList` (class) — decompiled/game-proj/Hacknet.Gui/SelectableTextList.cs:8
    public const int BORDER_WIDTH = 2;
    public const float ITEM_HEIGHT = 18f;
    public static int scrollOffset = 0;
    public static bool wasActivated = false;
    public static bool selectionWasChanged = false;
    public static Color scrollBarColor = new Color(140, 140, 140, 80);
    public static int doList(int myID, int x, int y, int width, int height, string[] text, int lastSelectedIndex, Color? selectedColor)
    public static int doFancyList(int myID, int x, int y, int width, int height, string[] text, int lastSelectedIndex, Color? selectedColor, bool HasDraggableScrollbar = false)

`Hacknet.Gui.SliderBar` (class) — decompiled/game-proj/Hacknet.Gui/SliderBar.cs:8
    public const int SELECTOR_BAR_WIDTH = 8;
    public const int BOARDER = 2;
    public const int SLIDE_BAR_HEIGHT = 10;
    public static float doSliderBar(int myID, int x, int y, int width, int height, float maxValue, float minValue, float currentValue, float barStep)

`Hacknet.Gui.TextBox` (class) — decompiled/game-proj/Hacknet.Gui/TextBox.cs:9
    public const float DELAY_BEFORE_KEY_REPEAT_START = 0.44f;
    public const float KEY_REPEAT_DELAY = 0.04f;
    public const int OUTLINE_WIDTH = 2;
    public static Keys lastHeldKey;
    public static float keyRepeatDelay = 0.44f;
    public static int LINE_HEIGHT = 25;
    public static int cursorPosition = 0;
    public static int textDrawOffsetPosition = 0;
    public static int FramesSelected = 0;
    public static bool MaskingText = false;
    public static bool BoxWasActivated = false;
    public static bool UpWasPresed = false;
    public static bool DownWasPresed = false;
    public static bool TabWasPresed = false;
    public static string doTextBox(int myID, int x, int y, int width, int lines, string str, SpriteFont font)
    public static string doTerminalTextField(int myID, int x, int y, int width, int selectionHeight, int lines, string str, SpriteFont font)
    public static string getStringInput(string s, KeyboardState input, KeyboardState lastInput)
    public static string getFilteredStringInput(string s, KeyboardState input, KeyboardState lastInput)
    public static string forceHandleKeyPress(string s, Keys key, KeyboardState input, KeyboardState lastInput)
    public static bool IsSpecialKey(Keys key)
    public static string ConvertKeyToChar(Keys key, bool shift)
    public static void moveCursorToEnd(string targetString)

`Hacknet.Gui.TextItem` (class) — decompiled/game-proj/Hacknet.Gui/TextItem.cs:7
    public static bool DrawShadow = false;
    public static Vector2 doMeasuredLabel(Vector2 pos, string text, Color? color)
    public static void doLabel(Vector2 pos, string text, Color? color)
    public static void doLabel(Vector2 pos, string text, Color? color, float MaxWidth)
    public static void doFontLabelToSize(Rectangle dest, string text, SpriteFont font, Color color, bool doNotOversize = false, bool offsetToTopLeft = false)
    public static void doCenteredFontLabel(Rectangle dest, string text, SpriteFont font, Color color, bool LockToLeft = false)
    public static void doFontLabel(Vector2 pos, string text, SpriteFont font, Color? color, float widthTo = float.MaxValue, float heightTo = float.MaxValue, bool centreVertically = false)
    public static void doRightAlignedBackingLabel(Rectangle dest, string msg, SpriteFont font, Color back, Color front)
    public static void doRightAlignedBackingLabelScaled(Rectangle dest, string msg, SpriteFont font, Color back, Color front)
    public static void doRightAlignedBackingLabelFill(Rectangle dest, string msg, SpriteFont font, Color back, Color front)
    public static Vector2[] GetStringScaleForSize(SpriteFont font, string text, Rectangle dest)
    public static Vector2 doMeasuredFontLabel(Vector2 pos, string text, SpriteFont font, Color? color, float widthTo = float.MaxValue, float heightTo = float.MaxValue)
    public static void doSmallLabel(Vector2 pos, string text, Color? color)
    public static void doTinyLabel(Vector2 pos, string text, Color? color)
    public static void doSmallLabel(Vector2 pos, string text, Color? color, float widthTo, float heightTo)
    public static Vector2 doMeasuredSmallLabel(Vector2 pos, string text, Color? color)
    public static Vector2 doMeasuredTinyLabel(Vector2 pos, string text, Color? color)

### Hacknet.Input

`Hacknet.Input.Stack<T>` (class) — decompiled/game-proj/Hacknet.Input/Stack.cs:5
    public T[] stack;
    public int Capacity => stack.Length;
    public int Count { get; set; }
    public Stack()
    public Stack(int capacity)
    public void Push(ref T item)
    public void Pop(out T item)
    public void PopSegment(out ArraySegment<T> segment)

`Hacknet.Input.TextInputHook` (class) — decompiled/game-proj/Hacknet.Input/TextInputHook.cs:7
    public string buffer = "";
    public bool backSpace = false;
    public string Buffer => buffer;
    public bool BackSpace
    public void clearBuffer()
    public TextInputHook(IntPtr whnd)
    public void Dispose()
    public void OnTextInput(char c)

### Hacknet.Localization

`Hacknet.Localization.LocaleActivator` (class) — decompiled/game-proj/Hacknet.Localization/LocaleActivator.cs:8
    public static List<LanguageInfo> supportedLanguages = null;
    public static List<LanguageInfo> SupportedLanguages
    public static void LoadSupportedLocales()
    public static void ActivateLocale(string localeCode, ContentManager content)
    public static bool ActiveLocaleIsCJK()

`Hacknet.Localization.LocaleActivator.LanguageInfo` (struct) — decompiled/game-proj/Hacknet.Localization/LocaleActivator.cs:10
    public string Name;
    public string Code;
    public string SteamCode;

`Hacknet.Localization.LocaleFontLoader` (class) — decompiled/game-proj/Hacknet.Localization/LocaleFontLoader.cs:7
    public static void LoadFontConfigForLocale(string locale, ContentManager content)
    public static List<GuiData.FontCongifOption> LoadFontConfigSetForLocale(string locale, string fontPrefix, ContentManager content)

### Hacknet.Magic

`Hacknet.Magic.NAT` (class) — decompiled/game-proj/Hacknet.Magic/NAT.cs:10
    public static TimeSpan _timeout = new TimeSpan(0, 0, 0, 3);
    public static string _descUrl;
    public static string _serviceUrl;
    public static string _eventUrl;
    public static TimeSpan TimeOut
    public static bool Discover()
    public static string GetServiceUrl(string resp)
    public static string CombineUrls(string resp, string p)
    public static void ForwardPort(int port, ProtocolType protocol, string description)
    public static void DeleteForwardingRule(int port, ProtocolType protocol)
    public static IPAddress GetExternalIP()
    public static XmlDocument SOAPRequest(string url, string soap, string function)

### Hacknet.Misc

`Hacknet.Misc.DLCExtendedTests` (class) — decompiled/game-proj/Hacknet.Misc/DLCExtendedTests.cs:9
    public static string TesExtendedFunctionality(ScreenManager screenMan, out int errorsAdded)
    public static string TestConditionalActionSets(ScreenManager screenMan, out int errorsAdded)
    public static string TestAdvancedConditionalActionSets(ScreenManager screenMan, out int errorsAdded)
    public static string TestConditionalActionSetCollections(ScreenManager screenMan, out int errorsAdded)
    public static string TestConditionalActionSetCollections2(ScreenManager screenMan, out int errorsAdded)
    public static string TestConditionalActionSetCollectionsOnOS(ScreenManager screenMan, out int errorsAdded)
    public static string TestObjectSerializer(ScreenManager screenMan, out int errorsAdded)
    public static string TestDLCSessionUpgrader(ScreenManager screenMan, out int errorsAdded)

`Hacknet.Misc.DLCLocalizationTests` (class) — decompiled/game-proj/Hacknet.Misc/DLCLocalizationTests.cs:7
    public static string TestDLCLocalizations(ScreenManager screenMan, out int errorsAdded)
    public static string LoadAndTestOS(ScreenManager screenMan, out int errorsAdded)

`Hacknet.Misc.DLCTests` (class) — decompiled/game-proj/Hacknet.Misc/DLCTests.cs:9
    public static string TestDLCFunctionality(ScreenManager screenMan, out int errorsAdded)
    public static string TestCustomPortMapping(ScreenManager screenMan, out int errorsAdded)
    public static string TestMemorySerialization(ScreenManager screenMan, out int errorsAdded)
    public static string TestCustomPortMappingOnLoadedComputer(ScreenManager screenMan, out int errorsAdded)
    public static string TestMemoryOnLoadedComputer(ScreenManager screenMan, out int errorsAdded)
    public static string TestMemoryInjectionOnLoadedComputer(ScreenManager screenMan, out int errorsAdded)
    public static string TestDLCProgression(ScreenManager screenMan, out int errorsAdded)
    public static string TestDLCPasswordsCorrect(ScreenManager screenMan, out int errorsAdded)
    public static string TestDLCMiscFilesCorrect(ScreenManager screenMan, out int errorsAdded)
    public static string TestPassword(OS os, string compID, string passToFind)
    public static void CheckAllFiles(Folder f, Action<FileEntry> act)
    public static void SetupTestingEnvironment(ScreenManager screenMan, Action<OS, List<Computer>> CompareSessions)
    public static void TestComputersForLoad(ScreenManager screenMan, Action<Computer, Computer> CompareComputerAfterLoad)

`Hacknet.Misc.EduEditionTests` (class) — decompiled/game-proj/Hacknet.Misc/EduEditionTests.cs:3
    public static string TestEDUFunctionality(ScreenManager screenMan, out int errorsAdded)
    public static string TestEduSafeFileFlags(ScreenManager screenMan, out int errorsAdded)

`Hacknet.Misc.ExtensionTests` (class) — decompiled/game-proj/Hacknet.Misc/ExtensionTests.cs:13
    public static bool OS_wasTestingPass = false;
    public static string RuntimeLoadAdditionalErrors = "";
    public static string TestExtensions(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionCustomStartSong(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionForRuntime(ScreenManager screenMan, string path, out int errorsAdded)
    public static string TestAllExtensionMissions(OS os, out int errorsAdded)
    public static string TestAllExtensionNodesRuntime(OS os, out int errorsAdded)
    public static string TestTagisClosed(string tagname, string allData)
    public static string TestExtensionMission(object mission, string filepath, object os)
    public static string TestExtensionCustomThemeSerialization(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionCustomThemeFile(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionCustomFactions(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionCustomFactionsActions(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionsFactions(AllFactions allFactions, out int errorsAdded)
    public static string TestExtensionMissionLoading(ScreenManager screenMan, out int errorsAdded)
    public static string TestExtensionStartNodeVisibility(ScreenManager screenMan, out int errorsAdded)
    public static string TestPopulatedExtension(ScreenManager screenMan, out int errorsAdded)
    public static string TestPopulatedExtensionRead(object os_obj, out int errorsAdded)
    public static string TestBlankExtension(ScreenManager screenMan, out int errorsAdded)
    public static string TestAcademicDatabase(ScreenManager screenMan, out int errorsAdded)
    public static void TestOSForBlankSession(object osobj, out string ret, out int errors)
    public static string TestBlankExtensionSaveLoad(ScreenManager screenMan, out int errorsAdded)
    public static object SetupOSForTests(string ActiveExtensionFoldername, ScreenManager screenMan)
    public static void CompleteExtensiontesting()

`Hacknet.Misc.HSLColor` (class) — decompiled/game-proj/Hacknet.Misc/HSLColor.cs:6
    public float Hue;
    public float Saturation;
    public float Luminosity;
    public HSLColor(float H, float S, float L)
    public static HSLColor FromRGB(Color Clr)
    public static HSLColor FromRGB(byte R, byte G, byte B)
    public float Hue_2_RGB(float v1, float v2, float vH)
    public Color ToRGB()
    public static double ColorCalc(double c, double t1, double t2)

`Hacknet.Misc.LocalizationTests` (class) — decompiled/game-proj/Hacknet.Misc/LocalizationTests.cs:7
    public static string TestLocalizations(ScreenManager screenMan, out int errorsAdded)
    public static string LoadAndTestOS(ScreenManager screenMan, out int errorsAdded)

`Hacknet.Misc.MultiLanguageScreenshotTool` (class) — decompiled/game-proj/Hacknet.Misc/MultiLanguageScreenshotTool.cs:9
    public static void CaptureMultiLanguageScreen(object os_obj)

`Hacknet.Misc.SaveFixHacks` (class) — decompiled/game-proj/Hacknet.Misc/SaveFixHacks.cs:7
    public static void FixSavesWithTerribleHacks(object osObj)
    public static string GetReportOnHashCodeOfEmptyStringForOtherCLRVersion()

`Hacknet.Misc.SessionAccelerator` (class) — decompiled/game-proj/Hacknet.Misc/SessionAccelerator.cs:3
    public static void AccelerateSessionToDLCHA(object osObj)
    public static void AccelerateSessionToDLCEND(object osObj)
    public static void AccelerateSessionToDLCStart(object osObj)
    public static void AddProgramToComputer(Computer c, int portnum)

`Hacknet.Misc.TestSuite` (class) — decompiled/game-proj/Hacknet.Misc/TestSuite.cs:10
    public static string ActiveObjectID = "";
    public static List<string> TestedMissionNames = new List<string>();
    public static string RunTestSuite(ScreenManager screenMan, bool IsQuickTestMode = false)
    public static string TestSaveLoadOnFile(ScreenManager screenMan, bool IsQuicktestMode = false)
    public static string TestMiscAndCLRFeatures(ScreenManager screenMan, out int errorsAdded)
    public static void DeleteAllFilesRecursivley(Folder f)
    public static string getTestingReportForLoadComparison(object osobj, List<Computer> oldComps, int currentErrorCount, out int errorCount)
    public static string TestMissions(object os_obj)
    public static string TestMission(string missionName, object os_obj)
    public static string TestMissionEndFucntion(string missionName, string expectedEndFunction, object os_obj)
    public static string TestGameProgression(object os_obj, out int errorsOut)
    public static void Assert(string first, string second)
    public static void DoWordCount()

`Hacknet.Misc.WordCounter` (class) — decompiled/game-proj/Hacknet.Misc/WordCounter.cs:6
    public static string accum;
    public static int charAccum;
    public static void PerformWordCount(string[] folders, string[] fileOnlyFolders)
    public static int GetWordCountFromFolder(string folderpath)
    public static int CountString(string input)
    public static int GetWordCountFromComputer(Computer c)
    public static int GetWordCountFromFolder(Folder f)
    public static int GetWordCountFromMission(ActiveMission m)
    public static int GetTextCountFromXMLFile(string path)

### Hacknet.Mission

`Hacknet.Mission.AddDegreeMission` (class) — decompiled/game-proj/Hacknet.Mission/AddDegreeMission.cs:6
    public AcademicDatabaseDaemon database;
    public string ownerName;
    public string degreeName;
    public string uniName;
    public float desiredGPA;
    public AddDegreeMission(string targetName, string degreeName, string uniName, float desiredGPA, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.BootLoadList` (class) — decompiled/game-proj/Hacknet.Mission/BootLoadList.cs:6
    public static List<string> getList()
    public static List<string> getDLCList()
    public static List<string> getDemoList()
    public static List<string> getAdventureList()
    public static List<string> getListFromData(string data)

`Hacknet.Mission.CheckFlagSetMission` (class) — decompiled/game-proj/Hacknet.Mission/CheckFlagSetMission.cs:5
    public string target;
    public OS os;
    public CheckFlagSetMission(string targetFlagName, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.DatabaseEntryChangeMission` (class) — decompiled/game-proj/Hacknet.Mission/DatabaseEntryChangeMission.cs:6
    public Computer c;
    public string Operation;
    public string RecordName;
    public string FieldName;
    public string TargetValue;
    public DatabaseEntryChangeMission(string computerIP, OS os, string operation, string FieldName, string targetValue, string recordName)
    public override bool isComplete(List<string> additionalDetails = null)
    public override string TestCompletable()

`Hacknet.Mission.DeathRowRecordModifyMission` (class) — decompiled/game-proj/Hacknet.Mission/DeathRowRecordModifyMission.cs:5
    public Folder container;
    public string fname;
    public string lname;
    public Computer deathRowDatabase;
    public OS os;
    public string lastWords;
    public DeathRowRecordModifyMission(string firstName, string lastName, string lastWords, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.DeathRowRecordRemovalMission` (class) — decompiled/game-proj/Hacknet.Mission/DeathRowRecordRemovalMission.cs:5
    public Folder container;
    public string fname;
    public string lname;
    public Computer deathRowDatabase;
    public OS os;
    public DeathRowRecordRemovalMission(string firstName, string lastName, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.DelayMission` (class) — decompiled/game-proj/Hacknet.Mission/DelayMission.cs:6
    public float time;
    public DateTime? firstRequest = null;
    public DelayMission(float time)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.FileChangeMission` (class) — decompiled/game-proj/Hacknet.Mission/FileChangeMission.cs:6
    public Folder container;
    public string target;
    public string targetData;
    public string targetKeyword;
    public Computer targetComp;
    public OS os;
    public bool isRemoval = false;
    public bool caseSensitive = false;
    public FileChangeMission(string path, string filename, string computerIP, string targetKeyword, OS _os, bool isRemoval = false)
    public override bool isComplete(List<string> additionalDetails = null)
    public override string TestCompletable()

`Hacknet.Mission.FileDeleteAllMission` (class) — decompiled/game-proj/Hacknet.Mission/FileDeleteAllMission.cs:6
    public Folder container;
    public Computer targetComp;
    public OS os;
    public FileDeleteAllMission(string path, string computerIP, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)
    public override string TestCompletable()

`Hacknet.Mission.FileDeletionMission` (class) — decompiled/game-proj/Hacknet.Mission/FileDeletionMission.cs:6
    public Folder container;
    public string target;
    public string targetData;
    public string targetComp;
    public string targetPath;
    public OS os;
    public FileDeletionMission(string path, string filename, string computerIP, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)
    public override string TestCompletable()

`Hacknet.Mission.FileDownloadMission` (class) — decompiled/game-proj/Hacknet.Mission/FileDownloadMission.cs:5
    public FileDownloadMission(string path, string filename, string computerIP, OS os)
    public override bool isComplete(List<string> additionalDetails = null)
    public override string TestCompletable()

`Hacknet.Mission.FileUploadMission` (class) — decompiled/game-proj/Hacknet.Mission/FileUploadMission.cs:6
    public Folder container;
    public string target;
    public string targetData;
    public Computer targetComp;
    public Computer uploadTargetComp;
    public OS os;
    public Folder destinationFolder;
    public FileUploadMission(string path, string filename, string computerWithFileIP, string computerToUploadToIP, string destToUploadToPath, OS _os, bool needsDecrypt = false, string decryptPass = "")
    public override bool isComplete(List<string> additionalDetails = null)
    public override string TestCompletable()

`Hacknet.Mission.GetAdminMission` (class) — decompiled/game-proj/Hacknet.Mission/GetAdminMission.cs:6
    public Computer target;
    public OS os;
    public GetAdminMission(string compIP, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)
    public override void reset()

`Hacknet.Mission.GetAdminPasswordStringMission` (class) — decompiled/game-proj/Hacknet.Mission/GetAdminPasswordStringMission.cs:5
    public Computer target;
    public OS os;
    public GetAdminPasswordStringMission(string compIP, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.GetStringMission` (class) — decompiled/game-proj/Hacknet.Mission/GetStringMission.cs:5
    public string target;
    public GetStringMission(string targetData)
    public override bool isComplete(List<string> additionalDetails = null)

`Hacknet.Mission.MisisonGoal` (class) — decompiled/game-proj/Hacknet.Mission/MisisonGoal.cs:5
    public virtual bool isComplete(List<string> additionalDetails = null)
    public virtual void reset()
    public virtual string TestCompletable()

`Hacknet.Mission.SendEmailMission` (class) — decompiled/game-proj/Hacknet.Mission/SendEmailMission.cs:5
    public string mailSubject;
    public string mailRecipient;
    public MailServer server;
    public SendEmailMission(string mailServerID, string mailRecipient, string proposedEmailSubject, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)
    public override void reset()

`Hacknet.Mission.WipeDegreesMission` (class) — decompiled/game-proj/Hacknet.Mission/WipeDegreesMission.cs:6
    public AcademicDatabaseDaemon database;
    public string ownerName;
    public WipeDegreesMission(string targetName, OS _os)
    public override bool isComplete(List<string> additionalDetails = null)

### Hacknet.Modules.Helpers

`Hacknet.Modules.Helpers.DisplayModuleLSHelper` (class) — decompiled/game-proj/Hacknet.Modules.Helpers/DisplayModuleLSHelper.cs:10
    public const int BUTTON_HEIGHT = 20;
    public const int BUTTON_MARGIN = 2;
    public const int INDENT = 30;
    public ScrollableSectionedPanel panel;
    public DisplayModuleLSHelper()
    public void DrawUI(Rectangle dest, OS os)
    public List<LSItem> BuildDirectoryDrawList(Folder f, int recItteration, int indentOffset, OS os)

`Hacknet.Modules.Helpers.DisplayModuleLSHelper.LSItem` (struct) — decompiled/game-proj/Hacknet.Modules.Helpers/DisplayModuleLSHelper.cs:12
    public Action Clicked;
    public int indent;
    public string DisplayName;
    public bool IsEmtyDisplay;

### Hacknet.Modules.Overlays

`Hacknet.Modules.Overlays.AircraftInfoOverlay` (class) — decompiled/game-proj/Hacknet.Modules.Overlays/AircraftInfoOverlay.cs:10
    public AircraftDaemon CrashingAircraft;
    public AircraftDaemon SecondaryAircraft;
    public bool IsActive = false;
    public float timeElapsed = 0f;
    public float flashInTimeLeft = 1f;
    public OS os;
    public bool IsMonitoringDLCEndingCases = false;
    public bool TargetHasStartedCrashing = false;
    public bool IsInPostSaveState = false;
    public SoundEffect AircraftSaveSound;
    public AircraftInfoOverlay(object OSobj)
    public void Activate()
    public void Update(float dt)
    public void Draw(Rectangle dest, SpriteBatch sb)

`Hacknet.Modules.Overlays.IncomingConnectionOverlay` (class) — decompiled/game-proj/Hacknet.Modules.Overlays/IncomingConnectionOverlay.cs:8
    public const float DURATION = 6f;
    public bool IsActive = false;
    public float timeElapsed = 0f;
    public Texture2D CautionSign;
    public Texture2D CautionSignBG;
    public static Color DrawColor = new Color(290, 0, 0, 0);
    public SoundEffect sound1;
    public SoundEffect sound2;
    public IncomingConnectionOverlay(object OSobj)
    public void Activate()
    public void Update(float dt)
    public void Draw(Rectangle dest, SpriteBatch sb)

### Hacknet.PlatformAPI

`Hacknet.PlatformAPI.AlienwareFXManager` (class) — decompiled/game-proj/Hacknet.PlatformAPI/AlienwareFXManager.cs:9
    public const double MIN_SECONDS_BETWEEN_UPDATES = 0.1;
    public static bool IsRunning = false;
    public static ILightFXController LightFX;
    public static bool HasUpdatedPostFlash = false;
    public static uint numDevices;
    public static List<uint> deviceLightCounts = new List<uint>();
    public static List<List<string>> deviceLightDescriptions = new List<List<string>>();
    public static DateTime TimeStarted;
    public static DateTime LastUpdateTime;
    public static Color LastLogoColor;
    public static Color LastMidKeyColor;
    public static Color LastOutKeyColor;
    public static Color LastOtherColor;
    public static void Init()
    public static void UpdateForOS(object OS_Obj)
    public static void UpdateLightsForThemeAndFlash(OS os)
    public static void CycleAllLights(Color from, Color to, float time)
    public static void UpdateLightColors(Color LogoColor, Color KeyboardMiddleColor, Color KeyboardOuterColor, Color StatusColor)
    public static LFX_ColorStruct Col2LFXC(Color c)
    public static void ReleaseHandle()

### Hacknet.PlatformAPI.Storage

`Hacknet.PlatformAPI.Storage.BasicStorageMethod` (class) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/BasicStorageMethod.cs:6
    public SaveFileManifest manifest;
    public virtual bool ShouldWrite
    public virtual bool DidDeserialize
    public virtual void Load()
    public virtual SaveFileManifest GetSaveManifest()
    public virtual Stream GetFileReadStream(string filename)
    public virtual bool FileExists(string filename)
    public virtual void WriteFileData(string filename, string data)
    public virtual void WriteFileData(string filename, byte[] data)
    public virtual void WriteSaveFileData(string filename, string username, string data, DateTime utcSaveFileTime)
    public virtual void UpdateDataFromOtherManager(IStorageMethod otherMethod)

`Hacknet.PlatformAPI.Storage.IStorageMethod` (interface) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/IStorageMethod.cs:6

`Hacknet.PlatformAPI.Storage.LocalDocumentsStorageMethod` (class) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/LocalDocumentsStorageMethod.cs:8
    public string FolderPath;
    public bool deserialized = false;
    public string FullFolderPath
    public override bool ShouldWrite => true;
    public override bool DidDeserialize => deserialized;
    public override void Load()
    public override bool FileExists(string filename)
    public override Stream GetFileReadStream(string filename)
    public override void WriteFileData(string filename, byte[] data)
    public override void WriteFileData(string filename, string data)

`Hacknet.PlatformAPI.Storage.OldSystemStorageMethod` (class) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/OldSystemStorageMethod.cs:6
    public SaveFileManifest manifest;
    public bool ShouldWrite => false;
    public bool DidDeserialize => false;
    public OldSystemStorageMethod(SaveFileManifest manifest)
    public void Load()
    public SaveFileManifest GetSaveManifest()
    public Stream GetFileReadStream(string filename)
    public bool FileExists(string filename)
    public void WriteFileData(string filename, byte[] data)
    public void WriteFileData(string filename, string data)
    public void UpdateDataFromOtherManager(IStorageMethod otherMethod)
    public void WriteSaveFileData(string filename, string username, string data, DateTime utcSaveFileTime)

`Hacknet.PlatformAPI.Storage.SaveAccountData` (struct) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/SaveAccountData.cs:5
    public static string[] Delimiter = new string[2] { "\r\n__", "\n__" };
    public string Username;
    public string Password;
    public string FileUsername;
    public DateTime LastWriteTime;
    public static SaveAccountData ParseFromString(string input)
    public string Serialize()

`Hacknet.PlatformAPI.Storage.SaveFileManager` (class) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/SaveFileManager.cs:8
    public static object CurrentlySaving = false;
    public static List<IStorageMethod> StorageMethods = new List<IStorageMethod>();
    public static bool HasSentErrorReport = false;
    public static List<SaveAccountData> Accounts => StorageMethods[0].GetSaveManifest().Accounts;
    public static SaveAccountData LastLoggedInUser => StorageMethods[0].GetSaveManifest().LastLoggedInUser;
    public static bool HasSaves => StorageMethods[0].GetSaveManifest().Accounts.Count > 0;
    public static void Init(bool needsOtherSourcesUpdate = true)
    public static void UpdateStorageMethodsFromSourcesToLatest()
    public static SaveFileManifest ReadOldSysemSteamCloudSaveManifest()
    public static SaveFileManifest ReadOldSysemLocalSaveManifest()
    public static string GetSaveFileNameForUsername(string username)
    public static Stream GetSaveReadStream(string playerID)
    public static void WriteSaveData(string saveData, string playerID)
    public static bool AddUser(string username, string pass)
    public static string GetFilePathForLogin(string username, string pass)
    public static bool CanCreateAccountForName(string username)
    public static void DeleteUser(string username)

`Hacknet.PlatformAPI.Storage.SaveFileManifest` (class) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/SaveFileManifest.cs:7
    public const string FILENAME = "Accounts.txt";
    public const string AccountsDelimiter = "\r\n%------%";
    public SaveAccountData LastLoggedInUser = new SaveAccountData
    public List<SaveAccountData> Accounts = new List<SaveAccountData>();
    public static SaveFileManifest Deserialize(IStorageMethod storage)
    public static SaveFileManifest Deserialize(string data)
    public static SaveFileManifest DeserializeSafe(string data)
    public string GetFilePathForLogin(string username, string pass)
    public bool CanCreateAccountForName(string username)
    public void UpdateLastWriteTimeForUserFile(string username, DateTime writeTime)
    public void Save(IStorageMethod storage, bool ExpectNoAccounts = false)
    public bool AddUser(string username, string password, DateTime creationTime, string filepath = null)
    public SaveAccountData GetAccount(string username)
    public string AddUserAndGetFilename(string username, string password)

`Hacknet.PlatformAPI.Storage.SteamCloudStorageMethod` (class) — decompiled/game-proj/Hacknet.PlatformAPI.Storage/SteamCloudStorageMethod.cs:9
    public bool deserialized = false;
    public string PathPrefix
    public override bool ShouldWrite => true;
    public override bool DidDeserialize => deserialized;
    public override void Load()
    public override bool FileExists(string filename)
    public override Stream GetFileReadStream(string filename)
    public override void WriteFileData(string filename, byte[] data)
    public override void WriteFileData(string filename, string data)

### Hacknet.ScreenManagement

`Hacknet.ScreenManagement.MessagePopup` (class) — decompiled/game-proj/Hacknet.ScreenManagement/MessagePopup.cs:8
    public static int BOX_WIDTH = 500;
    public static int BOX_HEIGHT = 350;
    public Texture2D blankTexture = null;
    public string messageContent = "";
    public MessagePopup(string message)
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    public override void HandleInput(InputState input)
    public override void Draw(GameTime gameTime)
    public override void UnloadContent()

### Hacknet.Screens

`Hacknet.Screens.ExtensionsMenuScreen` (class) — decompiled/game-proj/Hacknet.Screens/ExtensionsMenuScreen.cs:18
    public const string ExtensionBaseFolder = "Extensions/";
    public List<ExtensionInfo> Extensions = new List<ExtensionInfo>();
    public bool HasLoaded = false;
    public bool HasStartedLoading = false;
    public ExtensionInfo ExtensionInfoToShow = null;
    public Texture2D DefaultModImage = null;
    public SavefileLoginScreen SaveScreen = new SavefileLoginScreen();
    public SteamWorkshopPublishScreen workshopPublishScreen = null;
    public bool IsInPublishScreen = false;
    public string ReportOverride = null;
    public Action ExitClicked;
    public Action<string, string> CreateNewAccountForExtension_UserAndPass;
    public Action<string, string> LoadAccountForExtension_FileAndUsername;
    public EMSState State = EMSState.Normal;
    public string LoadErrors = "";
    public int ScrollStartIndex = 0;
    public ExtensionsMenuScreen()
    public void LoadExtensions()
    public void Reset()
    public void AddExtensionSafe(string folderpath)
    public void EnsureHasLoaded()
    public void ShowError(string error)
    public void ActivateExtensionPage(ExtensionInfo info)
    public void ExitExtensionsScreen()
    public void Draw(Rectangle dest, SpriteBatch sb, ScreenManager screenman)
    public Vector2 DrawExtensionCreateNewUserOrLoadScreen(Vector2 drawpos, Rectangle dest, SpriteBatch sb, ScreenManager screenMan, ExtensionInfo info)
    public Vector2 DrawExtensionInfoDetail(Vector2 drawpos, Rectangle dest, SpriteBatch sb, ScreenManager screenMan, ExtensionInfo info)
    public Vector2 DrawExtensionList(Vector2 drawpos, Rectangle dest, SpriteBatch sb)

`Hacknet.Screens.ExtensionsMenuScreen.EMSState` (enum) — decompiled/game-proj/Hacknet.Screens/ExtensionsMenuScreen.cs:20

`Hacknet.Screens.SteamWorkshopPublishScreen` (class) — decompiled/game-proj/Hacknet.Screens/SteamWorkshopPublishScreen.cs:12
    public Action GoBack;
    public bool isInUpload = false;
    public bool showLoadingSpinner = false;
    public bool HasInitializedSteamCallbacks = false;
    public string currentStatusMessage = "";
    public string currentTitleMessage = "";
    public string currentBodyMessage = "";
    public Texture2D spinnerTex;
    public float spinnerRot = 0f;
    public ExtensionInfo ActiveInfo;
    public CallResult<CreateItemResult_t> m_CreateItemResult;
    public CallResult<SubmitItemUpdateResult_t> SubmitItemUpdateResult_t;
    public CallResult<NumberOfCurrentPlayers_t> m_NumberOfCurrentPlayers;
    public AppId_t hacknetAppID = new AppId_t(365450u);
    public UGCUpdateHandle_t updateHandle;
    public DateTime transferStarted;
    public SteamWorkshopPublishScreen(ContentManager content)
    public void SubmitUpdateResult(SubmitItemUpdateResult_t callback, bool bIOFailure)
    public void CreateItemResult(CreateItemResult_t callback, bool bIOFailure)
    public void PerformUpdate(ExtensionInfo info)
    public void OnNumberOfCurrentPlayers(NumberOfCurrentPlayers_t pCallback, bool bIOFailure)
    public void InitSteamCallbacks()
    public void Update()
    public Vector2 Draw(SpriteBatch sb, Rectangle dest, ExtensionInfo info)
    public string getTimespanDisplayString(TimeSpan time)
    public void CreateExtensionInSteam(ExtensionInfo info)

### Hacknet.Security

`Hacknet.Security.PortRemappingSerializer` (class) — decompiled/game-proj/Hacknet.Security/PortRemappingSerializer.cs:6
    public static char[] EqualsDelimiter = new char[1] { '=' };
    public static string GetSaveString(Dictionary<int, int> input)
    public static Dictionary<int, int> Deserialize(string input)

### Hacknet.UIUtils

`Hacknet.UIUtils.AnimatedSpriteExporter` (class) — decompiled/game-proj/Hacknet.UIUtils/AnimatedSpriteExporter.cs:8
    public static void ExportAnimation(string folderPath, string nameStarter, int width, int height, float framesPerSecond, float totalTime, GraphicsDevice gd, Action<float> update, Action<SpriteBatch, Rectangle> draw, int antialiasingMultiplier = 1)

`Hacknet.UIUtils.AttractModeMenuScreen` (class) — decompiled/game-proj/Hacknet.UIUtils/AttractModeMenuScreen.cs:10
    public Action Start;
    public Action Exit;
    public Action StartDLC;
    public void Draw(Rectangle dest, SpriteBatch sb)

`Hacknet.UIUtils.AudioUtils` (class) — decompiled/game-proj/Hacknet.UIUtils/AudioUtils.cs:5
    public static void openWav(string filename, out double[] left, out double[] right)
    public static double bytesToDouble(byte firstByte, byte secondByte)

`Hacknet.UIUtils.CLinkBuffer<T>` (class) — decompiled/game-proj/Hacknet.UIUtils/CLinkBuffer.cs:3
    public T[] data;
    public int currentIndex = 0;
    public CLinkBuffer(int BufferSize = 128)
    public T Get(int offset)
    public void Add(T added)
    public void AddOneAhead(T added)
    public int NextIndex()

`Hacknet.UIUtils.FancyOutlines` (class) — decompiled/game-proj/Hacknet.UIUtils/FancyOutlines.cs:6
    public static void DrawCornerCutOutline(Rectangle bounds, SpriteBatch sb, float cornerCut, Color col)

`Hacknet.UIUtils.GetStringUIControl` (class) — decompiled/game-proj/Hacknet.UIUtils/GetStringUIControl.cs:8
    public static void StartGetString(string prompt, object os_obj)
    public static string DrawGetStringControl(string prompt, Rectangle bounds, Action errorOccurs, Action cancelled, SpriteBatch sb, object os_obj, Color SearchButtonColor, Color CancelButtonColor, string upperPrompt = null, Color? BackingPanelColor = null)
    public static void DrawGetStringControlInactive(string prompt, string valueText, Rectangle bounds, SpriteBatch sb, object os_obj, string upperPrompt = null)

`Hacknet.UIUtils.LCG` (class) — decompiled/game-proj/Hacknet.UIUtils/LCG.cs:6
    public int _state;
    public bool Microsoft { get; set; }
    public bool BSD
    public LCG(bool microsoft = true)
    public LCG(int n, bool microsoft = true)
    public void reSeed(int seed)
    public int Next()
    public float NextFloat()
    public float NextFloatScaled()
    public bool Flip()
    public IEnumerable<int> Seq()

`Hacknet.UIUtils.SavefileLoginScreen` (class) — decompiled/game-proj/Hacknet.UIUtils/SavefileLoginScreen.cs:12
    public Action<string, string> StartNewGameForUsernameAndPass;
    public Action<string, string> LoadGameForUserFileAndUsername;
    public Action RequestGoBack;
    public string terminalString = "";
    public List<string> History = new List<string>();
    public string currentPrompt = "USERNAME :";
    public int promptIndex = 0;
    public string ProjectName = "Hacknet";
    public List<string> PromptSequence = new List<string>();
    public List<string> Answers = new List<string>();
    public bool IsReady = false;
    public bool IsNewAccountMode = true;
    public bool InPasswordMode = false;
    public bool CanReturnEnter = false;
    public string userPathCache = null;
    public bool DrawFromTop = false;
    public bool HasOverlayScreen = false;
    public static Color CancelColor = new Color(125, 82, 82);
    public bool PreventAdvancing = false;
    public void WriteToHistory(string message)
    public void ClearTextBox()
    public void ResetForNewAccount()
    public void ResetForLogin()
    public void Advance(string answer)
    public void Draw(SpriteBatch sb, Rectangle dest)

`Hacknet.UIUtils.ScrollableSectionedPanel` (class) — decompiled/game-proj/Hacknet.UIUtils/ScrollableSectionedPanel.cs:8
    public int PanelHeight = 100;
    public float ScrollDown = 0f;
    public int NumberOfPanels = 0;
    public RenderTarget2D AboveFragment;
    public RenderTarget2D BelowFragment;
    public SpriteBatch fragmentBatch;
    public GraphicsDevice graphics;
    public int ScrollbarUIIndexOffset = 0;
    public bool HasScrollBar = true;
    public ScrollableSectionedPanel(int panelHeight, GraphicsDevice graphics)
    public void UpdateInput(Rectangle dest)
    public float GetMaxScroll(Rectangle dest)
    public void Draw(Action<int, Rectangle, SpriteBatch> DrawSection, SpriteBatch sb, Rectangle destination)
    public void DrawScrollBar(Rectangle dest, int width)
    public void RenderToTarget(Action<int, Rectangle, SpriteBatch> DrawSection, RenderTarget2D target, int index, Rectangle destination)
    public void SetRenderTargetsToFrame(Rectangle frame)

`Hacknet.UIUtils.ScrollableTextRegion` (class) — decompiled/game-proj/Hacknet.UIUtils/ScrollableTextRegion.cs:9
    public ScrollableSectionedPanel Panel;
    public string activeFontConfigName = null;
    public ScrollableTextRegion(GraphicsDevice gd)
    public void Draw(Rectangle dest, string text, SpriteBatch sb)
    public void Draw(Rectangle dest, string text, SpriteBatch sb, Color TextDrawColor)
    public void Draw(Rectangle dest, List<string> textLines, SpriteBatch sb, Color TextDrawColor)
    public void UpdateScroll(float newScroll)
    public float GetScrollDown()
    public void SetScrollbarUIIndexOffset(int index)

`Hacknet.UIUtils.WaveformRenderer` (class) — decompiled/game-proj/Hacknet.UIUtils/WaveformRenderer.cs:7
    public double[] left;
    public double[] right;
    public int SecondBlockSize = 100;
    public WaveformRenderer(string filename)
    public void RenderWaveform(double time, double totalTime, SpriteBatch sb, Rectangle bounds)

## PathfinderAPI（框架）

### Pathfinder

`Pathfinder.PathfinderAPIPlugin` (class) — decompiled/pathfinder/Pathfinder/PathfinderAPIPlugin.cs:20
    public const string ModGUID = "com.Pathfinder.API";
    public const string ModName = "PathfinderAPI";
    public static readonly bool GameIsSteamVersion = typeof(SteamCloudStorageMethod).GetField("deserialized") != null;
    public override bool Load()

`Pathfinder.SteamPatches` (class) — decompiled/pathfinder/Pathfinder/SteamPatches.cs:12

### Pathfinder.Action

`Pathfinder.Action.ActionDelayDecorator` (class) — decompiled/pathfinder/Pathfinder.Action/ActionDelayDecorator.cs:8
    public static SerializableAction Create(ElementInfo info, SerializableAction action)
    public ActionDelayDecorator(ElementInfo info, SerializableAction action)
    public override void Trigger(OS os)
    public override XElement GetSaveElement()

`Pathfinder.Action.ActionManager` (class) — decompiled/pathfinder/Pathfinder.Action/ActionManager.cs:16
    public static void RegisterAction<T>(string xmlName) where T : PathfinderAction
    public static void RegisterAction(Type actionType, string xmlName)
    public static void UnregisterAction<T>() where T : PathfinderAction
    public static void UnregisterAction(Type actionType)
    public static void UnregisterAction(string xmlName)
    public static string GetXmlNameFor(Type type)

`Pathfinder.Action.ConditionManager` (class) — decompiled/pathfinder/Pathfinder.Action/ConditionManager.cs:16
    public static void RegisterCondition<T>(string xmlName) where T : PathfinderCondition
    public static void RegisterCondition(Type conditionType, string xmlName)
    public static void UnregisterCondition<T>() where T : PathfinderCondition
    public static void UnregisterCondition(Type conditionType)
    public static void UnregisterCondition(string xmlName)
    public static string GetXmlNameFor(Type type)

`Pathfinder.Action.DelayablePathfinderAction` (class) — decompiled/pathfinder/Pathfinder.Action/DelayablePathfinderAction.cs:8
    public string DelayHost;
    public string Delay;
    public sealed override void Trigger(object os_obj)
    public abstract void Trigger(OS os);
    public override void LoadFromXml(ElementInfo info)

`Pathfinder.Action.PathfinderAction` (class) — decompiled/pathfinder/Pathfinder.Action/PathfinderAction.cs:8
    public string XmlName => ActionManager.GetXmlNameFor(((object)this).GetType()) ?? ((object)this).GetType().Name;
    public virtual XElement GetSaveElement()
    public virtual void LoadFromXml(ElementInfo info)

`Pathfinder.Action.PathfinderCondition` (class) — decompiled/pathfinder/Pathfinder.Action/PathfinderCondition.cs:8
    public string XmlName => ConditionManager.GetXmlNameFor(((object)this).GetType()) ?? ((object)this).GetType().Name;
    public virtual XElement GetSaveElement()
    public virtual void LoadFromXml(ElementInfo info)

### Pathfinder.Administrator

`Pathfinder.Administrator.AdministratorManager` (class) — decompiled/pathfinder/Pathfinder.Administrator/AdministratorManager.cs:12
    public static void RegisterAdministrator<T>() where T : BaseAdministrator
    public static void RegisterAdministrator(Type adminType)
    public static void RegisterAdministrator<T>(string xmlName) where T : BaseAdministrator
    public static void RegisterAdministrator(Type adminType, string xmlName)
    public static void UnregisterAdministrator<T>() where T : BaseAdministrator
    public static void UnregisterAdministrator(Type adminType)
    public static void UnregisterAdministrator(string xmlName)

`Pathfinder.Administrator.BaseAdministrator` (class) — decompiled/pathfinder/Pathfinder.Administrator/BaseAdministrator.cs:8
    public string XmlName => "admin";
    public BaseAdministrator(Computer computer, OS opSystem)
    public virtual void LoadFromXml(ElementInfo info)
    public virtual XElement GetSaveElement()

### Pathfinder.BaseGameFixes

`Pathfinder.BaseGameFixes.FixExtensionTests` (class) — decompiled/pathfinder/Pathfinder.BaseGameFixes/FixExtensionTests.cs:13

`Pathfinder.BaseGameFixes.FixTutorialStartup` (class) — decompiled/pathfinder/Pathfinder.BaseGameFixes/FixTutorialStartup.cs:12

`Pathfinder.BaseGameFixes.FlickeringTextReportNull` (class) — decompiled/pathfinder/Pathfinder.BaseGameFixes/FlickeringTextReportNull.cs:10

`Pathfinder.BaseGameFixes.KeepBranchesOnMailMissionCompletion` (class) — decompiled/pathfinder/Pathfinder.BaseGameFixes/KeepBranchesOnMailMissionCompletion.cs:14
    public static void DoRespondDisplayManipulator(ILContext context)

`Pathfinder.BaseGameFixes.KillExeCheckIdentifierName` (class) — decompiled/pathfinder/Pathfinder.BaseGameFixes/KillExeCheckIdentifierName.cs:11

### Pathfinder.BaseGameFixes.Performance

`Pathfinder.BaseGameFixes.Performance.ThemeCaching` (class) — decompiled/pathfinder/Pathfinder.BaseGameFixes.Performance/ThemeCaching.cs:21

### Pathfinder.Command

`Pathfinder.Command.CommandManager` (class) — decompiled/pathfinder/Pathfinder.Command/CommandManager.cs:15
    public string Name;
    public Action<OS, string[]> CommandAction;
    public bool Autocomplete;
    public bool CaseSensitive;
    public static void RegisterCommand(string commandName, Action<OS, string[]> handler, bool addAutocomplete = true, bool caseSensitive = false)
    public static void UnregisterCommand(string commandName, Assembly pluginAsm = null)

### Pathfinder.Daemon

`Pathfinder.Daemon.BaseDaemon` (class) — decompiled/pathfinder/Pathfinder.Daemon/BaseDaemon.cs:8
    public virtual string Identifier => ((object)this).GetType().Name;
    public BaseDaemon(Computer computer, string serviceName, OS opSystem)
    public sealed override string getSaveString()
    public virtual XElement GetSaveElement()
    public virtual void LoadFromXml(ElementInfo info)

`Pathfinder.Daemon.DaemonManager` (class) — decompiled/pathfinder/Pathfinder.Daemon/DaemonManager.cs:12
    public static void RegisterDaemon<T>() where T : BaseDaemon
    public static void RegisterDaemon(Type daemonType)
    public static void UnregisterDaemon<T>() where T : BaseDaemon
    public static void UnregisterDaemon(Type daemonType)

### Pathfinder.Event

`Pathfinder.Event.EventHandlerOptions` (struct) — decompiled/pathfinder/Pathfinder.Event/EventHandlerOptions.cs:3
    public int? Priority;
    public bool ContinueOnCancel;
    public bool ContinueOnThrow;
    public int PrioritySafe => Priority ?? 0;

`Pathfinder.Event.EventManager` (class) — decompiled/pathfinder/Pathfinder.Event/EventManager.cs:12
    public static void AddHandler(Type pathfinderEvent, MethodInfo handler, EventHandlerOptions options = default(EventHandlerOptions))

`Pathfinder.Event.EventManager<T>` (class) — decompiled/pathfinder/Pathfinder.Event/EventManager.cs:50
    public static EventManager<T> Instance => _instance ?? (_instance = new EventManager<T>());
    public static int HandlerCount => Instance.handlers.AllItems.Count;
    public static void AddHandler(Action<T> handler, EventHandlerOptions options = default(EventHandlerOptions))
    public static void RemoveHandler(Action<T> handler, Assembly eventAssembly = null)
    public static T InvokeAll(T eventArgs)
    public static T InvokeAssembly(Assembly asm, T eventArgs)

`Pathfinder.Event.PathfinderEvent` (class) — decompiled/pathfinder/Pathfinder.Event/PathfinderEvent.cs:3
    public bool Cancelled
    public bool Thrown { get; internal set; }

### Pathfinder.Event.BepInEx

`Pathfinder.Event.BepInEx.LoadEvent` (class) — decompiled/pathfinder/Pathfinder.Event.BepInEx/LoadEvent.cs:8

`Pathfinder.Event.BepInEx.PostLoadEvent` (class) — decompiled/pathfinder/Pathfinder.Event.BepInEx/PostLoadEvent.cs:7

`Pathfinder.Event.BepInEx.UnloadEvent` (class) — decompiled/pathfinder/Pathfinder.Event.BepInEx/UnloadEvent.cs:7

### Pathfinder.Event.Gameplay

`Pathfinder.Event.Gameplay.CommandExecuteEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Gameplay/CommandExecuteEvent.cs:7
    public OS Os { get; }
    public string[] Args { get; set; }
    public bool Found
    public CommandExecuteEvent(OS os, string[] args)

`Pathfinder.Event.Gameplay.ExecutableExecuteEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Gameplay/ExecutableExecuteEvent.cs:11
    public Computer Computer { get; private set; }
    public OS OS { get; private set; }
    public string ExecutableName
    public string ExecutableData
    public List<string> Arguments { get; private set; }
    public Folder ExeFolder { get; private set; }
    public int FileIndex { get; private set; }
    public FileEntry ExeFile { get; private set; }
    public ExecutionResult Result
    public string this[int index]
    public ExecutableExecuteEvent(Computer com, OS os, Folder fol, int finde, FileEntry file, string[] args)

`Pathfinder.Event.Gameplay.ExecutableListEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Gameplay/ExecutableListEvent.cs:9
    public OS OS { get; }
    public List<string> EmbeddedExes { get; } = new List<string> { "PortHack", "ForkBomb", "Shell", "Tutorial" };
    public Dictionary<FileEntry, bool> BinExes { get; }
    public ExecutableListEvent(OS os, Dictionary<FileEntry, bool> binExes)

`Pathfinder.Event.Gameplay.ExecutionResult` (enum) — decompiled/pathfinder/Pathfinder.Event.Gameplay/ExecutionResult.cs:6

`Pathfinder.Event.Gameplay.OSUpdateEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Gameplay/OSUpdateEvent.cs:8
    public OS OS { get; }
    public GameTime GameTime { get; }
    public OSUpdateEvent(OS os, GameTime gameTime)

### Pathfinder.Event.Loading

`Pathfinder.Event.Loading.ExtensionLoadEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Loading/ExtensionLoadEvent.cs:12
    public ExtensionInfo Info { get; }
    public bool Unload { get; }
    public ExtensionLoadEvent(ExtensionInfo info, bool unload)

`Pathfinder.Event.Loading.OSLoadedEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Loading/OSLoadedEvent.cs:7
    public OS Os { get; }
    public OSLoadedEvent(OS os)

`Pathfinder.Event.Loading.SaveComputerLoadedEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Loading/SaveComputerLoadedEvent.cs:8
    public OS Os { get; }
    public Computer Comp { get; }
    public ElementInfo Info { get; }
    public SaveComputerLoadedEvent(OS os, Computer comp, ElementInfo info)

`Pathfinder.Event.Loading.TextReplaceEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Loading/TextReplaceEvent.cs:7
    public string Original { get; }
    public string Replacement { get; set; }
    public TextReplaceEvent(string original, string replacement)

### Pathfinder.Event.Menu

`Pathfinder.Event.Menu.DrawMainMenuEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Menu/DrawMainMenuEvent.cs:11
    public DrawMainMenuEvent(MainMenu mainMenu)

`Pathfinder.Event.Menu.DrawMainMenuTitlesEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Menu/DrawMainMenuTitlesEvent.cs:15
    public TitleData Main { get; private set; }
    public TitleData Sub { get; private set; }
    public DrawMainMenuTitlesEvent(MainMenu menu, TitleData main, TitleData sub)

`Pathfinder.Event.Menu.DrawMainMenuTitlesEvent.TitleData` (class) — decompiled/pathfinder/Pathfinder.Event.Menu/DrawMainMenuTitlesEvent.cs:19
    public string Title { get; set; }
    public Color Color { get; set; }
    public SpriteFont Font { get; set; }
    public Rectangle Destination { get; set; }
    public TitleData(string title, Color color, SpriteFont font, Rectangle dest)

`Pathfinder.Event.Menu.MainMenuEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Menu/MainMenuEvent.cs:5
    public MainMenu MainMenu { get; private set; }
    public MainMenuEvent(MainMenu mainMenu)

### Pathfinder.Event.Options

`Pathfinder.Event.Options.CustomOptionsSaveEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Options/CustomOptionsSaveEvent.cs:3

### Pathfinder.Event.Pathfinder

`Pathfinder.Event.Pathfinder.BuildAutocompletesEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Pathfinder/BuildAutocompletesEvent.cs:5
    public List<string> Autocompletes { get; set; }
    public BuildAutocompletesEvent(List<string> autocompletes)

### Pathfinder.Event.Saving

`Pathfinder.Event.Saving.SaveComputerEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Saving/SaveComputerEvent.cs:6
    public OS Os { get; }
    public Computer Comp { get; }
    public XElement Element { get; set; }
    public SaveComputerEvent(OS os, Computer comp, XElement element)

`Pathfinder.Event.Saving.SaveEvent` (class) — decompiled/pathfinder/Pathfinder.Event.Saving/SaveEvent.cs:6
    public OS Os { get; }
    public XElement Save { get; }
    public string Filename { get; }
    public SaveEvent(OS os, XElement save, string filename)

### Pathfinder.Executable

`Pathfinder.Executable.BaseExecutable` (class) — decompiled/pathfinder/Pathfinder.Executable/BaseExecutable.cs:7
    public string[] Args;
    public virtual string GetIdentifier()
    public BaseExecutable(Rectangle location, OS operatingSystem, string[] args)

`Pathfinder.Executable.CompletionResult` (enum) — decompiled/pathfinder/Pathfinder.Executable/CompletionResult.cs:3

`Pathfinder.Executable.ExecutableManager` (class) — decompiled/pathfinder/Pathfinder.Executable/ExecutableManager.cs:19
    public static IReadOnlyList<CustomExeInfo> AllCustomExes => _customExes;
    public static void RegisterExecutable<T>(string xmlName) where T : BaseExecutable
    public static void RegisterExecutable(Type executableType, string xmlName)
    public static bool IsXmlId(string xmlName)
    public static bool IsExeData(string exeData)
    public static bool IsRegistered<T>() where T : BaseExecutable
    public static bool IsRegistered(Type exeType)
    public static string GetCustomExeData(string xmlName)
    public static void UnregisterExecutable(string xmlName)
    public static void UnregisterExecutable<T>()
    public static void UnregisterExecutable(Type exeType)
    public static void AddGameExecutable(this OS os, GameExecutable exe, Rectangle location, string[] args)
    public static void AddGameExecutable(this OS os, GameExecutable exe)

`Pathfinder.Executable.ExecutableManager.CustomExeInfo` (struct) — decompiled/pathfinder/Pathfinder.Executable/ExecutableManager.cs:21
    public string ExeData;
    public string XmlId;
    public Type ExeType;

`Pathfinder.Executable.ExeModuleExtensions` (class) — decompiled/pathfinder/Pathfinder.Executable/ExeModuleExtensions.cs:5
    public static bool CanKill(this ExeModule module)
    public static bool Kill(this ExeModule module)

`Pathfinder.Executable.GameExecutable` (class) — decompiled/pathfinder/Pathfinder.Executable/GameExecutable.cs:7
    public float Lifetime { get; set; }
    public CompletionResult Result
    public virtual bool CanAddToSystem { get; set; } = true;
    public virtual bool CanBeKilled { get; set; } = true;
    public virtual string ErrorReturn { get; set; }
    public virtual bool IgnoreProxyFailPrint { get; set; }
    public virtual bool IgnoreMemoryBehaviorPrint { get; set; }
    public GameExecutable()
    public void Assign(Rectangle location, OS os, string[] args)
    public virtual void OnInitialize()
    public virtual void OnCompleteError()
    public virtual void OnCompleteFailure()
    public virtual void OnCompleteKilled()
    public virtual void OnCompleteSuccess()
    public virtual void OnComplete()
    public virtual void OnNoAvailableRam()
    public virtual void OnProxyBypassFailure()
    public virtual void OnUpdate(float delta)
    public virtual bool CatchException(Exception exception)
    public sealed override void LoadContent()
    public sealed override void Completed()
    public sealed override void Killed()
    public override void Update(float t)
    public sealed override string GetIdentifier()

### Pathfinder.GUI

`Pathfinder.GUI.PFButton` (class) — decompiled/pathfinder/Pathfinder.GUI/PFButton.cs:9
    public readonly int ID = GetNextID();
    public int X;
    public int Y;
    public int Height;
    public int Width;
    public string Text;
    public Color? Color;
    public Texture2D Texture;
    public static int GetNextID()
    public static void ReturnID(int id)
    public PFButton(int x, int y, int width, int height, string text, Color? color = null, Texture2D texture = null)
    public bool Do()
    public bool Do(Point offset)
    public bool Do(Rectangle offset)
    public bool Do(Vector2 offset)
    public bool Do(int offsetX, int offsetY)
    public void Dispose()

### Pathfinder.Meta

`Pathfinder.Meta.UpdaterAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta/UpdaterAttribute.cs:5
    public string GithubApiUrl { get; set; }
    public string AssetFileName { get; set; }
    public string ZipEntryPath { get; set; }
    public bool IncludePrerelease { get; set; }
    public UpdaterAttribute(string apiUrl, string assetName, string zipPath = null, bool includePrerlease = false)

### Pathfinder.Meta.Load

`Pathfinder.Meta.Load.ActionAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/ActionAttribute.cs:9
    public string XmlName { get; }
    public ActionAttribute(string xmlName)

`Pathfinder.Meta.Load.AdministratorAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/AdministratorAttribute.cs:9
    public string XmlName { get; }
    public AdministratorAttribute()
    public AdministratorAttribute(string xmlName)

`Pathfinder.Meta.Load.BaseAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/BaseAttribute.cs:7

`Pathfinder.Meta.Load.CommandAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/CommandAttribute.cs:10
    public string CommandName { get; }
    public bool AddAutocomplete { get; set; }
    public bool CaseSensitive { get; set; }
    public CommandAttribute(string commandName, bool addAutocomplete = true, bool caseSensitive = false)

`Pathfinder.Meta.Load.ComputerExecutorAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/ComputerExecutorAttribute.cs:10
    public string Element { get; }
    public ParseOption ParseOptions { get; set; }
    public ComputerExecutorAttribute(string element, ParseOption parseOptions = ParseOption.None)

`Pathfinder.Meta.Load.ConditionAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/ConditionAttribute.cs:9
    public string XmlName { get; }
    public ConditionAttribute(string xmlName)

`Pathfinder.Meta.Load.DaemonAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/DaemonAttribute.cs:9

`Pathfinder.Meta.Load.EventAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/EventAttribute.cs:10
    public int Priority
    public bool ContinueOnCancel
    public bool ContinueOnThrow
    public EventAttribute()
    public EventAttribute(int priority, bool continueOnCancel = false, bool continueOnThrow = false)
    public EventAttribute(bool continueOnCancel = false, bool continueOnThrow = false)

`Pathfinder.Meta.Load.ExecutableAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/ExecutableAttribute.cs:9
    public string XmlName { get; }
    public ExecutableAttribute(string xmlName)

`Pathfinder.Meta.Load.ExtensionInfoExecutorAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/ExtensionInfoExecutorAttribute.cs:10
    public string Element { get; }
    public ParseOption ParseOptions { get; set; }
    public ExtensionInfoExecutorAttribute(string element, ParseOption parseOptions = ParseOption.None)

`Pathfinder.Meta.Load.GoalAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/GoalAttribute.cs:9
    public string XmlName { get; }
    public GoalAttribute(string xmlName)

`Pathfinder.Meta.Load.HacknetPluginExtensions` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/HacknetPluginExtensions.cs:5
    public static string GetOptionsTag(this HacknetPlugin plugin)
    public static bool HasOptionsTag(this HacknetPlugin plugin)

`Pathfinder.Meta.Load.IgnoreEventAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/IgnoreEventAttribute.cs:6

`Pathfinder.Meta.Load.IgnorePluginAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/IgnorePluginAttribute.cs:6

`Pathfinder.Meta.Load.MissionExecutorAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/MissionExecutorAttribute.cs:10
    public string Element { get; }
    public ParseOption ParseOptions { get; set; }
    public MissionExecutorAttribute(string element, ParseOption parseOptions = ParseOption.None)

`Pathfinder.Meta.Load.OptionAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/OptionAttribute.cs:9
    public string Tag { get; set; }
    public OptionAttribute(string tag = null)
    public OptionAttribute(Type pluginType)

`Pathfinder.Meta.Load.OptionsTabAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/OptionsTabAttribute.cs:9
    public string Tag { get; }
    public OptionsTabAttribute(string tag)

`Pathfinder.Meta.Load.PortAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/PortAttribute.cs:9

`Pathfinder.Meta.Load.SaveExecutorAttribute` (class) — decompiled/pathfinder/Pathfinder.Meta.Load/SaveExecutorAttribute.cs:10
    public string Element { get; }
    public ParseOption ParseOptions { get; set; }
    public SaveExecutorAttribute(string element, ParseOption parseOptions = ParseOption.None)

### Pathfinder.Mission

`Pathfinder.Mission.GoalManager` (class) — decompiled/pathfinder/Pathfinder.Mission/GoalManager.cs:11
    public static void RegisterGoal<T>(string xmlName) where T : MisisonGoal
    public static void RegisterGoal(Type goalType, string xmlName)
    public static void UnregisterGoal<T>()
    public static void UnregisterGoal(Type goalType)
    public static void UnregisterGoal(string xmlName)

`Pathfinder.Mission.PathfinderGoal` (class) — decompiled/pathfinder/Pathfinder.Mission/PathfinderGoal.cs:7
    public virtual void LoadFromXML(ElementInfo info)

### Pathfinder.Options

`Pathfinder.Options.Option` (class) — decompiled/pathfinder/Pathfinder.Options/Option.cs:3
    public string Name;
    public string Description = "";
    public bool Enabled = true;
    public virtual int SizeX => 0;
    public virtual int SizeY => 0;
    public Option(string name, string description = "")
    public abstract void Draw(int x, int y);

`Pathfinder.Options.OptionCheckbox` (class) — decompiled/pathfinder/Pathfinder.Options/OptionCheckbox.cs:7
    public bool Value;
    public override int SizeX => 100;
    public override int SizeY => 75;
    public OptionCheckbox(string name, string description = "", bool defVal = false)
    public override void Draw(int x, int y)

`Pathfinder.Options.OptionsManager` (class) — decompiled/pathfinder/Pathfinder.Options/OptionsManager.cs:5
    public static readonly Dictionary<string, OptionsTab> Tabs;
    public static void AddOption(string tag, Option opt)

`Pathfinder.Options.OptionsTab` (class) — decompiled/pathfinder/Pathfinder.Options/OptionsTab.cs:6
    public string Name;
    public List<Option> Options = new List<Option>();
    public OptionsTab(string name)

### Pathfinder.Port

`Pathfinder.Port.ComputerExtensions` (class) — decompiled/pathfinder/Pathfinder.Port/ComputerExtensions.cs:19
    public static void AddPort(this Computer comp, string protocol, int portNum, string displayName)
    public static void AddPort(this Computer comp, PortData port)
    public static void AddPort(this Computer comp, PortRecord record)
    public static void AddPort(this Computer comp, PortState port)
    public static bool RemovePort(this Computer comp, string protocol)
    public static bool RemovePort(this Computer comp, PortRecord record)
    public static PortState GetPortState(this Computer comp, string protocol)
    public static PortData GetPort(this Computer comp, string protocol)
    public static List<PortState> GetAllPortStates(this Computer comp)
    public static List<PortData> GetAllPorts(this Computer comp)
    public static Dictionary<string, PortState> GetPortStateDict(this Computer comp)
    public static Dictionary<string, PortData> GetPortDict(this Computer comp)
    public static bool HasInitializedPorts(Computer comp)
    public static int CountOpenPorts(this Computer comp)
    public static void openPort(this Computer comp, string protocol, string ipFrom)
    public static void closePort(this Computer comp, string protocol, string ipFrom)
    public static bool isPortOpen(this Computer comp, string protocol)
    public static void ClearPorts(this Computer comp)

`Pathfinder.Port.PortData` (class) — decompiled/pathfinder/Pathfinder.Port/PortData.cs:6
    public string Protocol { get; }
    public string DisplayName { get; set; }
    public int Port { get; set; }
    public int OriginalPort { get; private set; }
    public bool Cracked { get; set; }
    public PortData(string proto, int portNum, string displayName)
    public PortData Clone()
    public bool Equals(PortData other)
    public static bool operator ==(PortData first, PortData second)
    public static bool operator !=(PortData first, PortData second)
    public override bool Equals(object obj)
    public override int GetHashCode()

`Pathfinder.Port.PortManager` (class) — decompiled/pathfinder/Pathfinder.Port/PortManager.cs:15
    public static void RegisterPort(string protocol, string displayName, int defaultPort = -1)
    public static void RegisterPort(PortData info)
    public static void RegisterPort(PortRecord record)
    public static void UnregisterPort(string protocol, Assembly pluginAsm = null)
    public static bool IsPortRecordRegistered(PortRecord record)
    public static bool IsPortRegistered(string protocol)
    public static PortRecord GetPortRecordFromProtocol(string proto)
    public static PortRecord GetPortRecordFromNumber(int num)
    public static PortData GetPortDataFromProtocol(string proto)
    public static PortData GetPortDataFromNumber(int num)
    public static void LoadPortsFromChildren(Computer comp, IEnumerable<ElementInfo> children, bool clearAll)
    public static void LoadPortsFromString(Computer comp, string portString, bool clearExisting)
    public static void LoadPortsFromStringVanilla(Computer comp, string portsList)
    public static void LoadPortRemapsFromStringVanilla(Computer comp, string remap)

`Pathfinder.Port.PortRecord` (class) — decompiled/pathfinder/Pathfinder.Port/PortRecord.cs:7
    public string Protocol { get; }
    public int OriginalPortNumber { get; }
    public string DefaultDisplayName { get; }
    public int DefaultPortNumber { get; private set; }
    public PortRecord(string protocol, string defDisplayName, int defPortNumber)
    public PortState CreateState(Computer computer, string displayName = null, int portNumber = -1, bool cracked = false)
    public PortState CreateState(Computer computer, bool cracked)
    public bool Equals(PortRecord other)
    public static bool operator ==(PortRecord first, PortRecord second)
    public static bool operator !=(PortRecord first, PortRecord second)
    public override bool Equals(object obj)
    public override int GetHashCode()
    public static explicit operator PortRecord(PortData data)
    public static explicit operator PortData(PortRecord record)

`Pathfinder.Port.PortState` (class) — decompiled/pathfinder/Pathfinder.Port/PortState.cs:6
    public Computer Computer { get; internal set; }
    public PortRecord Record { get; }
    public string DisplayName
    public int PortNumber
    public bool Cracked { get; set; }
    public void SetCracked(bool cracked, string ipFrom)
    public PortState(Computer comp, PortRecord record, bool cracked)
    public PortState(Computer comp, PortRecord record, string displayName = null, int portNumber = -1, bool cracked = false)
    public PortState(Computer comp, string protocol, bool cracked)
    public PortState(Computer comp, string protocol, string displayName = null, int portNumber = -1, bool cracked = false)
    public PortState Clone(Computer comp = null)
    public bool Remove()
    public static explicit operator PortData(PortState state)
    public static explicit operator PortState(PortData data)

### Pathfinder.Replacements

`Pathfinder.Replacements.ActionsLoader` (class) — decompiled/pathfinder/Pathfinder.Replacements/ActionsLoader.cs:18
    public static RunnableConditionalActions LoadActionSets(ElementInfo root)
    public static SerializableCondition ReadCondition(ElementInfo conditionInfo)
    public static SerializableAction ReadAction(ElementInfo actionInfo)

`Pathfinder.Replacements.ContentLoader` (class) — decompiled/pathfinder/Pathfinder.Replacements/ContentLoader.cs:23
    public static void RegisterExecutor<T>(string element, ParseOption options = ParseOption.None) where T : ComputerExecutor, new()
    public static void RegisterExecutor(Type executorType, string element, ParseOption options = ParseOption.None)
    public static void UnregisterExecutor<T>() where T : ComputerExecutor, new()
    public static Computer LoadComputer(string filename, bool preventAddingToNetmap = false, bool preventInitDaemons = false)

`Pathfinder.Replacements.ContentLoader.ComputerExecutor` (class) — decompiled/pathfinder/Pathfinder.Replacements/ContentLoader.cs:25
    public OS Os { get; private set; }
    public Computer Comp => _comp;
    public virtual void Init(OS os, ref ComputerHolder comp)
    public abstract void Execute(EventExecutor exec, ElementInfo info);

`Pathfinder.Replacements.ContentLoader.ComputerHolder` (class) — decompiled/pathfinder/Pathfinder.Replacements/ContentLoader.cs:42
    public static implicit operator Computer(ComputerHolder holder)
    public string Element;
    public Type ExecutorType;
    public ParseOption Options;

`Pathfinder.Replacements.ExtensionInfoLoader` (class) — decompiled/pathfinder/Pathfinder.Replacements/ExtensionInfoLoader.cs:17
    public static void AddLanguage(string language)
    public static void RegisterExecutor<T>(string element, ParseOption options = ParseOption.None) where T : ExtensionInfoExecutor, new()
    public static void RegisterExecutor(Type executorType, string element, ParseOption options = ParseOption.None)
    public static void UnregisterExecutor<T>() where T : ExtensionInfoExecutor, new()
    public static ExtensionInfo LoadExtensionInfo(string folderpath)

`Pathfinder.Replacements.ExtensionInfoLoader.ExtensionInfoExecutor` (class) — decompiled/pathfinder/Pathfinder.Replacements/ExtensionInfoLoader.cs:19
    public ExtensionInfo ExtensionInfo;
    public virtual void Init(ref ExtensionInfo extensionInfo)
    public abstract void Execute(EventExecutor exec, ElementInfo info);
    public string Element;
    public Type ExecutorType;
    public ParseOption Options;

`Pathfinder.Replacements.MissionLoader` (class) — decompiled/pathfinder/Pathfinder.Replacements/MissionLoader.cs:17
    public static void RegisterExecutor<T>(string element, ParseOption options = ParseOption.None) where T : MissionExecutor, new()
    public static void RegisterExecutor(Type executorType, string element, ParseOption options = ParseOption.None)
    public static void UnregisterExecutor<T>() where T : MissionExecutor, new()
    public static ActiveMission LoadContentMission(string filename)
    public static MisisonGoal LoadGoal(ElementInfo info)

`Pathfinder.Replacements.MissionLoader.MissionExecutor` (class) — decompiled/pathfinder/Pathfinder.Replacements/MissionLoader.cs:19
    public OS Os { get; private set; }
    public ActiveMission Mission { get; private set; }
    public virtual void Init(OS os, ref ActiveMission mission)
    public abstract void Execute(EventExecutor exec, ElementInfo info);
    public string Element;
    public Type ExecutorType;
    public ParseOption Options;

`Pathfinder.Replacements.ObjectSerializerReplacement` (class) — decompiled/pathfinder/Pathfinder.Replacements/ObjectSerializerReplacement.cs:15
    public static bool SerializeObject(object o, bool preventOuterTag, ref string __result)
    public static bool DeserializeObject(XmlReader rdr, Type t, out object __result)
    public static bool GetRenderablesFromTypePrefix(Type type, object o, int indentLevel, ref List<RenderableField> __result)

`Pathfinder.Replacements.ReplacementsCommon` (class) — decompiled/pathfinder/Pathfinder.Replacements/ReplacementsCommon.cs:14
    public static MemoryContents LoadMemoryContents(ElementInfo info)
    public static Faction LoadFaction(ElementInfo info)

`Pathfinder.Replacements.SaveLoader` (class) — decompiled/pathfinder/Pathfinder.Replacements/SaveLoader.cs:23
    public static void RegisterExecutor<T>(string element, ParseOption options = ParseOption.None) where T : SaveExecutor, new()
    public static void RegisterExecutor(Type executorType, string element, ParseOption options = ParseOption.None)
    public static void UnregisterExecutor<T>() where T : SaveExecutor, new()
    public static Computer LoadComputer(ElementInfo info, OS os)
    public static Folder LoadFolder(ElementInfo info)
    public static ActiveMission LoadMission(ElementInfo root)

`Pathfinder.Replacements.SaveLoader.SaveExecutor` (class) — decompiled/pathfinder/Pathfinder.Replacements/SaveLoader.cs:25
    public OS Os { get; private set; }
    public virtual void Init(OS os)
    public abstract void Execute(EventExecutor exec, ElementInfo info);
    public string Element;
    public Type ExecutorType;
    public ParseOption Options;

`Pathfinder.Replacements.SaveWriter` (class) — decompiled/pathfinder/Pathfinder.Replacements/SaveWriter.cs:25
    public static XElement GetHacknetSaveElement(OS os)
    public static XElement GetActionSaveElement(SerializableAction action)
    public static XElement GetConditionSaveElement(SerializableCondition cond)
    public static XElement GetConditionalActionSetSaveElement(SerializableConditionalActionSet set)
    public static XElement GetConditionalActionsSaveElement(RunnableConditionalActions actions)
    public static XElement GetDLCSaveElement(OS os)
    public static XElement GetFlagsSaveElement(ProgressionFlags flags)
    public static XElement GetNetmapVisibleNodesSaveElement(NetworkMap nmap)
    public static XElement GetFirewallSaveElement(Firewall firewall)
    public static XElement GetPortSaveElement(Computer node)
    public static XElement GetUserDetailSaveElement(UserDetail user)
    public static XElement GetMemoryContentsSaveElement(MemoryContents contents)
    public static XElement GetFolderSaveElement(Folder folder)
    public static XElement GetFilesystemSaveElement(FileSystem fs)
    public static XElement GetDaemonSaveElement(object daemon)
    public static XElement GetNodeSaveElement(Computer node)
    public static XElement GetNetmapNodesSaveElement(NetworkMap nmap)
    public static XElement GetNetmapSaveElement(NetworkMap nmap)
    public static XElement GetMissionSaveElement(ActiveMission mission)
    public static XElement GetFactionSaveElement(Faction faction)
    public static XElement GetAllFactionsSaveElement(AllFactions factions)

### Pathfinder.Util

`Pathfinder.Util.AssemblyAssociatedList<T>` (class) — decompiled/pathfinder/Pathfinder.Util/AssemblyAssociatedList.cs:8
    public ReadOnlyCollection<T> AllItems
    public ReadOnlyCollection<T> this[Assembly assembly]
    public void Add(T val, Assembly owner)
    public void Remove(T val, Assembly owner)
    public void RemoveAll(Predicate<T> predicate, Assembly owner)
    public bool RemoveAssembly(Assembly asm, out List<T> removed)

`Pathfinder.Util.CachedCustomTheme` (class) — decompiled/pathfinder/Pathfinder.Util/CachedCustomTheme.cs:19
    public ElementInfo ThemeInfo { get; }
    public Texture2D BackgroundImage { get; internal set; }
    public string Path { get; }
    public bool Loaded { get; private set; }
    public CachedCustomTheme(string themeFileName)
    public void Load(bool isMainThread)
    public void ApplyTo(OS os)
    public void Dispose()

`Pathfinder.Util.ComputerLookup` (class) — decompiled/pathfinder/Pathfinder.Util/ComputerLookup.cs:6
    public static void RebuildLookups(List<Computer> nodes = null)
    public static void Add(Computer node)
    public static Computer Find(string target, SearchType type = SearchType.Any)
    public static Computer FindById(string id)
    public static Computer FindByIp(string ip, bool filter = true)
    public static Computer FindByName(string name, bool filter = true)

`Pathfinder.Util.DictionaryExtensions` (class) — decompiled/pathfinder/Pathfinder.Util/DictionaryExtensions.cs:9
    public static string GetString<Key>(this Dictionary<Key, string> dict, Key key, string defaultVal = "")
    public static string GetOrThrow<Key>(this Dictionary<Key, string> dict, Key key, string exMessage, Predicate<string> validator = null)
    public static string GetAnyOrThrow<Key>(this Dictionary<Key, string> dict, Key[] keys, string exMessage, Predicate<string> validator = null)
    public static string GetOrWarn<Key>(this Dictionary<Key, string> dict, Key key, string warnMessage, Predicate<string> validator = null)
    public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
    public static int GetInt<Key>(this Dictionary<Key, string> dict, Key key, int defaultVal = 0)
    public static bool GetBool<Key>(this Dictionary<Key, string> dict, Key key, bool defaultVal = false)
    public static float GetFloat<Key>(this Dictionary<Key, string> dict, Key key, float defaultVal = 0f)
    public static byte GetByte<Key>(this Dictionary<Key, string> dict, Key key, byte defaultVal = 0)
    public static Vector2? GetVector<Key>(this Dictionary<Key, string> dict, Key x, Key y, Vector2? defaultVal = null)
    public static Color? GetColor<Key>(this Dictionary<Key, string> dict, Key key, Color? defaultVal = null)
    public static Computer GetComp<Key>(this Dictionary<Key, string> dict, Key key, SearchType searchType = SearchType.Any, string exMessage = null)

`Pathfinder.Util.EnumerableExtensions` (class) — decompiled/pathfinder/Pathfinder.Util/EnumerableExtensions.cs:6
    public static T? FirstOrNull<T>(this IEnumerable<T> source) where T : struct
    public static T? FirstOrNull<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : struct

`Pathfinder.Util.ErrorHelper` (class) — decompiled/pathfinder/Pathfinder.Util/ErrorHelper.cs:8
    public static void ThrowNotInherit(this Type type, string typeRefName, Type parentType, string extra = null)
    public static void ThrowNotInherit<InheritT>(this Type type, string typeRefName, string extra = null)
    public static void ThrowNoDefaultCtor(this Type type, string nameOf)
    public static void ThrowNull<T>(this T check, string nameOf) where T : class
    public static void ThrowNull<T>(this T check, string nameOf, string msg) where T : class
    public static void ThrowOutOfRange(this int check, string nameOf, int lowerLimit = int.MinValue, int upperLimit = int.MaxValue)
    public static void ThrowNotSameSizeAs(this ICollection left, string leftNameOf, ICollection right, string rightNameOf)

`Pathfinder.Util.FixedSizeCacheDict<K,V>` (class) — decompiled/pathfinder/Pathfinder.Util/FixedSizeCacheDict.cs:6
    public readonly int Max;
    public LRUCacheLinkedListNode<K, V> BackingListHead { get; private set; }
    public LRUCacheLinkedListNode<K, V> BackingListTail { get; private set; }
    public FixedSizeCacheDict(int maxSize)
    public void Register(K key, V item)
    public bool TryGetCached(K key, out V val)

`Pathfinder.Util.FromTypeConverter` (delegate) — decompiled/pathfinder/Pathfinder.Util/FromTypeConverter.cs:3

`Pathfinder.Util.IXmlName` (interface) — decompiled/pathfinder/Pathfinder.Util/IXmlName.cs:3

`Pathfinder.Util.LRUCacheLinkedListNode<K,V>` (class) — decompiled/pathfinder/Pathfinder.Util/LRUCacheLinkedListNode.cs:3
    public K Key { get; set; }
    public V Value { get; set; }
    public LRUCacheLinkedListNode<K, V> Next { get; set; }
    public LRUCacheLinkedListNode<K, V> Previous { get; set; }

`Pathfinder.Util.RefColorFieldDelegate` (delegate) — decompiled/pathfinder/Pathfinder.Util/CachedCustomTheme.cs:21

`Pathfinder.Util.SearchType` (enum) — decompiled/pathfinder/Pathfinder.Util/SearchType.cs:6

`Pathfinder.Util.StringExtensions` (class) — decompiled/pathfinder/Pathfinder.Util/StringExtensions.cs:7
    public static bool HasContent(this string s)
    public static bool ContentFileExists(this string filename)
    public static string ContentFilePath(this string filename)
    public static string Filter(this string s)

`Pathfinder.Util.ToTypeConverter` (delegate) — decompiled/pathfinder/Pathfinder.Util/ToTypeConverter.cs:3

`Pathfinder.Util.XMLStorageAttribute` (class) — decompiled/pathfinder/Pathfinder.Util/XMLStorageAttribute.cs:10
    public bool IsContent { get; set; }
    public Type Converter { get; set; }
    public static XElement WriteToElement(IXmlName obj)
    public static XElement WriteToElement(object obj)
    public static void ReadFromElement(ElementInfo info, object obj)

`Pathfinder.Util.XMLTypeConverter` (class) — decompiled/pathfinder/Pathfinder.Util/XMLTypeConverter.cs:9
    public ConversionFailureException(string val, Type t, Exception inner)
    public ConversionFailureException(object o, Exception inner)
    public ToTypeConverter ToType;
    public FromTypeConverter FromType;
    public TypeConverter(ToTypeConverter toType, FromTypeConverter fromType)
    public static object ConvertToType(Type t, string s)
    public static string ConvertToString(object o, Type t = null)
    public static void AddTypeConverter(Type t, ToTypeConverter to, FromTypeConverter from)

### Pathfinder.Util.XML

`Pathfinder.Util.XML.ElementInfo` (class) — decompiled/pathfinder/Pathfinder.Util.XML/ElementInfo.cs:9
    public string Name { get; set; }
    public string Content { get; set; }
    public ElementInfo Parent { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    public List<ElementInfo> Children { get; set; } = new List<ElementInfo>();
    public ulong NodeID { get; } = freeId++;
    public override string ToString()
    public bool ContentAsBoolean()
    public int ContentAsInt()
    public float ContentAsFloat()
    public void WriteToXML(XmlWriter writer)
    public XElement ConvertToXElement()

`Pathfinder.Util.XML.EventExecutor` (class) — decompiled/pathfinder/Pathfinder.Util.XML/EventExecutor.cs:7
    public ReadExecution Executor;
    public ParseOption Options;
    public XmlReader Reader;
    public string Text;
    public Dictionary<string, List<ExecutorHolder>> Temps;
    public Dictionary<string, List<ExecutorHolder>> AllExecs;
    public List<ReadExecution> CurrentExecs;
    public Stack<ElementInfo> ElementStack;
    public List<string> ParentNames;
    public EventExecutor()
    public EventExecutor(string text, bool isPath)
    public EventExecutor(XmlReader rdr)
    public void SaveState()
    public void PopState()
    public void RegisterExecutor(string element, ReadExecution executor, ParseOption options = ParseOption.None)
    public void RegisterTempExecutor(string element, ReadExecution executor, ParseOption options = ParseOption.None)

`Pathfinder.Util.XML.EventReader` (class) — decompiled/pathfinder/Pathfinder.Util.XML/EventReader.cs:8
    public List<string> ParentNames = new List<string>();
    public XmlReader Reader { get; protected set; }
    public string CurrentNamespace => string.Join(".", ParentNames);
    public EventReader()
    public EventReader(string text, bool isPath)
    public EventReader(XmlReader rdr)
    public void SetText(string text, bool isPath)
    public void Parse()
    public bool TryParse(out Exception exception)

`Pathfinder.Util.XML.ListExtensions` (class) — decompiled/pathfinder/Pathfinder.Util.XML/ListExtensions.cs:5
    public static ElementInfo GetElement(this List<ElementInfo> list, string elementName)
    public static bool TryGetElement(this List<ElementInfo> list, string elementName, out ElementInfo info)

`Pathfinder.Util.XML.ParseOption` (enum) — decompiled/pathfinder/Pathfinder.Util.XML/ParseOption.cs:6

`Pathfinder.Util.XML.ReadExecution` (delegate) — decompiled/pathfinder/Pathfinder.Util.XML/ReadExecution.cs:3
