using Acr.UserDialogs;
using FormsVideoLibrary;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using log4net;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Controls;
using MissionPlanner.Maps;
using MissionPlanner.Utilities;
using MissionPlanner.Warnings;
using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xamarin.Controls;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Button = Xamarin.Forms.Button;
using Color = System.Drawing.Color;
using Device = Xamarin.Forms.Device;
using Exception = System.Exception;
using Label = Xamarin.Forms.Label;


namespace Xamarin
{
    public partial class FlightData : ContentPage, IActivate, IDeactivate
    {
        // 🎮 Android ネイティブ接続のジョイスティック列挙用デリゲート
        public static Func<List<string>> GetConnectedJoysticksFunc;

        // 🎮 リアルタイム・スティック入力バッファ (MainActivityから更新)
        public static float LastStickRoll = 0f;    // -1.0 〜 +1.0 (X)
        public static float LastStickPitch = 0f;   // -1.0 〜 +1.0 (Y)
        public static float LastStickThrottle = -1f; // -1.0 〜 +1.0 (Z / Throttle)
        public static float LastStickYaw = 0f;     // -1.0 〜 +1.0 (Rz / Rudder)
        public static float LastStickAux1 = 0f;
        public static float LastStickAux2 = 0f;
        public static float LastRawAxisX = 0f;
        public static float LastRawAxisY = 0f;
        public static float LastRawAxisZ = 0f;
        public static float LastRawAxisRz = 0f;
        public static float LastRawAxisRx = 0f;
        public static float LastRawAxisRy = 0f;
        public static float LastRawThrottle = 0f;
        public static float LastRawRudder = 0f;
        public static float LastRawGas = 0f;
        public static float LastRawBrake = 0f;
        public static bool IsJoystickActive = false;
        public static int JoystickSendIntervalMs = 50; // Default 50ms (20Hz)
        private static System.Threading.Timer _rcOverrideTimer = null;
        private static readonly object _rcOverrideLock = new object();

        // 📡 RC_CHANNELS パケット受信統計 (周期・カウント計測)
        private static int _rcChannelsPacketCount = 0;
        private static int _rcChannelsWindowCount = 0;
        private static DateTime _lastRcStatsTime = DateTime.UtcNow;
        private static double _lastRcChannelsHz = 0.0;
        private static ulong _lastRcChannelsCount = 0;
        private static long _hzCalcStartTicks = 0;
        private static ulong _hzCalcStartRxCount = 0;
        private static ulong _lastRcTxCount = 0;
        private static double _lastRcTxHz = 0.0;
        private static int _rcChannelsSub1 = -1;
        private static int _rcChannelsSub2 = -1;

        // 🎮 18チャンネルの軸・キー割り当てデータ配列 (重複割り当て完全対応・各チャンネル独立保持)
        public static string[] ChannelAxisMapping = new string[19]
        {
            "", "X", "Y", "Z", "Rz", "Slider1", "None",
            "None", "None", "None", "None", "None", "None",
            "None", "None", "None", "None", "None", "None"
        };
        // 🎮 18チャンネルのリバース設定データ配列
        public static bool[] ChannelReverseMapping = new bool[19];
        // 🎮 18チャンネルのエクスポ設定データ配列 (0〜100%)
        public static float[] ChannelExpoMapping = new float[19];

        public static int LastPressedButtonCode = 0;
        public static Dictionary<int, bool> PressedButtonMap = new Dictionary<int, bool>();

        public static void SetButtonState(int keyCode, bool isDown)
        {
            PressedButtonMap[keyCode] = isDown;
        }

        // 🎮 割り当てられた軸・ボタンからリアルタイムPWM値を算出 (1000〜2000µs)
                public static string NormalizeAxisName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "None";
            string s = raw.Replace("▾", "").Trim();

            // 1. スティック軸
            if (s.Equals("X", StringComparison.OrdinalIgnoreCase)) return "X";
            if (s.Equals("Y", StringComparison.OrdinalIgnoreCase)) return "Y";
            if (s.Equals("Z", StringComparison.OrdinalIgnoreCase)) return "Z";
            if (s.Equals("Rz", StringComparison.OrdinalIgnoreCase)) return "Rz";
            if (s.Equals("Rx", StringComparison.OrdinalIgnoreCase)) return "Rx";
            if (s.Equals("Ry", StringComparison.OrdinalIgnoreCase)) return "Ry";

            // 2. スライダー・トリガー
            if (s.IndexOf("Slider1", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("L2", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Brake", StringComparison.OrdinalIgnoreCase) >= 0) return "Slider1";
            if (s.IndexOf("Slider2", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("R2", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Gas", StringComparison.OrdinalIgnoreCase) >= 0) return "Slider2";
            if (s.Equals("Slider", StringComparison.OrdinalIgnoreCase)) return "Slider1";

            // 3. ボタン
            if (s.IndexOf("Btn A", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnA", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("(×)", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn A";
            if (s.IndexOf("Btn B", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnB", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("(○)", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn B";
            if (s.IndexOf("Btn X", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnX", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("(□)", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn X";
            if (s.IndexOf("Btn Y", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnY", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("(△)", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn Y";
            if (s.IndexOf("Btn L1", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnL1", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn L1";
            if (s.IndexOf("Btn R1", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnR1", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn R1";
            if (s.IndexOf("Btn L3", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnL3", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn L3";
            if (s.IndexOf("Btn R3", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BtnR3", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn R3";
            if (s.IndexOf("Btn Start", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn Start";
            if (s.IndexOf("Btn Select", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn Select";
            if (s.IndexOf("Btn Mode", StringComparison.OrdinalIgnoreCase) >= 0) return "Btn Mode";

            // 4. 十字キー (Dpad) - 部分一致の順序を厳密化
            if (s.IndexOf("Dpad Up", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("十字上", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Up";
            if (s.IndexOf("Dpad Down", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("十字下", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Down";
            if (s.IndexOf("Dpad Left", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("十字左", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Left";
            if (s.IndexOf("Dpad Right", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("十字右", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Right";

            if (s.Equals("None", StringComparison.OrdinalIgnoreCase)) return "None";
            return s;
        }

        /// <summary>
        /// 各チャンネルの FC パラメータ (RCx_MIN, RCx_MAX, RCx_TRIM) を取得
        /// </summary>
        public static void GetChannelLimits(int ch, out float min, out float max, out float trim)
        {
            min = 1000f;
            max = 2000f;
            trim = (ch == 3) ? 1000f : 1500f;

            try
            {
                if (MainV2.comPort != null && MainV2.comPort.MAV != null && MainV2.comPort.MAV.param != null)
                {
                    var p = MainV2.comPort.MAV.param;
                    string pMin = $"RC{ch}_MIN";
                    string pMax = $"RC{ch}_MAX";
                    string pTrim = $"RC{ch}_TRIM";

                    if (p.ContainsKey(pMin)) min = Convert.ToSingle(p[pMin].Value);
                    if (p.ContainsKey(pMax)) max = Convert.ToSingle(p[pMax].Value);
                    if (p.ContainsKey(pTrim)) trim = Convert.ToSingle(p[pTrim].Value);

                    if (min >= max)
                    {
                        min = 1000f;
                        max = 2000f;
                    }
                    if (trim < min || trim > max)
                    {
                        trim = (min + max) / 2f;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// FCから受信したテレメトリ (RC_CHANNELS) の各チャンネル入力値 (PWM) を取得
        /// </summary>
        public static float GetFCChannelPwm(CurrentState cs, int ch)
        {
            if (cs == null) return 0f;
            switch (ch)
            {
                case 1: return cs.ch1in;
                case 2: return cs.ch2in;
                case 3: return cs.ch3in;
                case 4: return cs.ch4in;
                case 5: return cs.ch5in;
                case 6: return cs.ch6in;
                case 7: return cs.ch7in;
                case 8: return cs.ch8in;
                case 9: return cs.ch9in;
                case 10: return cs.ch10in;
                case 11: return cs.ch11in;
                case 12: return cs.ch12in;
                case 13: return cs.ch13in;
                case 14: return cs.ch14in;
                case 15: return cs.ch15in;
                case 16: return cs.ch16in;
                case 17: return (cs.rcoverridech17 > 0) ? (float)cs.rcoverridech17 : 0f;
                case 18: return (cs.rcoverridech18 > 0) ? (float)cs.rcoverridech18 : 0f;
                default: return 0f;
            }
        }

        // 後方互換用
        public int CalculateChannelPWM(string axisSetting, int defaultPwm = 1500, bool isReverse = false)
        {
            return CalculateChannelPWM(1, axisSetting, isReverse, 0f);
        }

        // 🎮 ジョイスティック入力（軸・ボタン）を相対値に変換後、FCパラメータ (MIN, MAX, TRIM) の範囲にマッピングしてPWM値を算出
        public int CalculateChannelPWM(int ch, string axisSetting, bool isReverse = false, float expoPercent = 0f)
        {
            GetChannelLimits(ch, out float min, out float max, out float trim);

            try
            {
                string norm = NormalizeAxisName(axisSetting);
                if (norm.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return (int)Math.Round((ch == 3) ? min : trim);
                }

                // 1. スライダー・トリガー（片方向 0.0〜1.0）
                if (norm == "Slider1" || norm == "Slider2")
                {
                    float val = (norm == "Slider1")
                        ? ((LastRawBrake != 0f) ? LastRawBrake : LastRawThrottle)
                        : ((LastRawGas != 0f) ? LastRawGas : LastRawRudder);
                    float ratio = (val >= 0f) ? val : Math.Max(0f, (val + 1f) / 2f);
                    ratio = Math.Max(0f, Math.Min(1f, ratio));

                    if (isReverse) ratio = 1.0f - ratio;

                    float pwm = min + ratio * (max - min);
                    return (int)Math.Round(Math.Max(min, Math.Min(max, pwm)));
                }

                // 2. ボタン（デジタル ON/OFF）
                if (norm.StartsWith("Btn") || norm.StartsWith("Dpad"))
                {
                    bool isPressed = false;
                    foreach (var kvp in PressedButtonMap)
                    {
                        if (kvp.Value)
                        {
                            string pressedRaw = ConvertKeyCodeToName(kvp.Key);
                            string pressedNorm = NormalizeAxisName(pressedRaw);
                            if (norm == pressedNorm ||
                                norm.IndexOf(pressedNorm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                pressedNorm.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                norm.Contains($"Btn {kvp.Key}"))
                            {
                                isPressed = true;
                                break;
                            }
                        }
                    }

                    if (isReverse) isPressed = !isPressed;
                    float pwm = isPressed ? max : min;
                    return (int)Math.Round(pwm);
                }

                // 3. スティック軸（双方向 -1.0〜+1.0）
                float rawAxis = 0f;
                if (norm == "X") rawAxis = (LastRawAxisX != 0f) ? LastRawAxisX : LastStickRoll;
                else if (norm == "Y") rawAxis = (LastRawAxisY != 0f) ? LastRawAxisY : LastStickPitch;
                else if (norm == "Z") rawAxis = LastRawAxisZ;
                else if (norm == "Rz") rawAxis = LastRawAxisRz;
                else if (norm == "Rx") rawAxis = LastRawAxisRx;
                else if (norm == "Ry") rawAxis = LastRawAxisRy;
                else if (norm == "Throttle") rawAxis = LastRawThrottle;
                else if (norm == "Rudder") rawAxis = LastRawRudder;

                rawAxis = Math.Max(-1.0f, Math.Min(1.0f, rawAxis));

                // Expoカーブ適用 (0〜100%)
                if (expoPercent > 0f)
                {
                    float e = Math.Max(0f, Math.Min(1f, expoPercent / 100f));
                    rawAxis = (1f - e) * rawAxis + e * (rawAxis * rawAxis * rawAxis);
                }

                // リバース反転
                if (isReverse)
                {
                    rawAxis = -rawAxis;
                }

                // スロットル (Ch3) かつスティック下端〜上端をフルに使う場合:
                if (ch == 3)
                {
                    float ratio = (rawAxis + 1.0f) / 2.0f;
                    float pwm = min + ratio * (max - min);
                    return (int)Math.Round(Math.Max(min, Math.Min(max, pwm)));
                }
                else
                {
                    // 中立がある軸（Roll, Pitch, Yawなど）:
                    // rawAxis < 0: trim + rawAxis * (trim - min)
                    // rawAxis >= 0: trim + rawAxis * (max - trim)
                    float pwm;
                    if (rawAxis < 0f)
                    {
                        pwm = trim + rawAxis * (trim - min);
                    }
                    else
                    {
                        pwm = trim + rawAxis * (max - trim);
                    }
                    return (int)Math.Round(Math.Max(min, Math.Min(max, pwm)));
                }
            }
            catch { }

            return (int)Math.Round((ch == 3) ? min : trim);
        }

        public static string ConvertKeyCodeToName(int keyCode)
        {
            switch (keyCode)
            {
                case 96: return "Btn A (×)";
                case 97: return "Btn B (○)";
                case 99: return "Btn X (□)";
                case 100: return "Btn Y (△)";
                case 102: return "Btn L1";
                case 103: return "Btn R1";
                case 104: return "Btn L2";
                case 105: return "Btn R2";
                case 106: return "Btn L3";
                case 107: return "Btn R3";
                case 108: return "Btn Start";
                case 109: return "Btn Select";
                case 110: return "Btn Mode";
                case 19: return "Dpad Up";
                case 20: return "Dpad Down";
                case 21: return "Dpad Left";
                case 22: return "Dpad Right";
                case 23: return "Dpad Center";
                default: return $"Btn {keyCode}";
            }
        }
        public static FlightData instance;
        public static GMapOverlay kmlpolygons;

        Slider tracklog = new Slider();
        Label LBL_logfn = new Label();
        Label lbl_logpercent = new Label();
        Label lbl_playbackspeed = new Label();
        Button BUT_playlog = new Button();

        public static HUD myhud;
        public static GMapControl mymap;
        public static bool threadrun;
        internal static GMapOverlay geofence;
        internal static GMapOverlay photosoverlay;
        internal static GMapOverlay poioverlay = new GMapOverlay("POI");
        internal static GMapOverlay rallypointoverlay;
        internal static GMapOverlay tfrpolygons;
        internal GMapMarker CurrentGMapMarker;
        internal PointLatLng MouseDownStart;
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool CameraOverlap;
        GMapMarker center = new GMarkerGoogle(new PointLatLng(0.0, 0.0), GMarkerGoogleType.none);

        /// <summary>
        /// Try to reduce the number of map position changes generated by the code
        /// </summary>
        DateTime lastmapposchange = DateTime.MinValue;

        GMapMarker marker;

        GMapOverlay polygons;
        private Propagation prop;
        Random random = new Random();
        GMapRoute route;
        GMapOverlay routes;
        Thread thisthread;
        SKPoint touchpoint = new SKPoint();

        public FlightData()
        {
            log.Info("Ctor Start");

            InitializeComponent();

            log.Info("Components Done");

            instance = this;
            mymap = gMapControl1;
            myhud = hud1;

            switch (Forms.Device.RuntimePlatform)
            {
                case Device.Android:
                    //myhud.IgnorePixelScaling = true;
                    break;
            }

            if (!string.IsNullOrEmpty(Settings.Instance["hudcolor"]))
            {
                hud1.hudcolor = Color.FromName(Settings.Instance["hudcolor"]);
            }

            // 🎮 保存されたジョイスティック設定を起動時に自動読み込み
            LoadJoystickSettings();

            List<string> list = new List<string>();

            {
                list.Add("LOITER_UNLIM");
                list.Add("RETURN_TO_LAUNCH");
                list.Add("PREFLIGHT_CALIBRATION");
                list.Add("MISSION_START");
                list.Add("PREFLIGHT_REBOOT_SHUTDOWN");
                list.Add("Trigger Camera NOW");
                list.Add("SYSTEM_TIME");
                //DO_SET_SERVO
                //DO_REPEAT_SERVO
            }

            GMap.NET.GMaps.Instance.PrimaryCache = new MissionPlanner.Maps.MyImageCache();

            gMapControl1.LevelsKeepInMemmory = 10;
            //gMapControl1.Manager.MemoryCache.Size

            gMapControl1.MapProvider = GMapProviders.GoogleSatelliteMap;

            gMapControl1.ShowTileGridLines = false;
            gMapControl1.MapScaleInfoEnabled = false;
            gMapControl1.MinZoom = 1;
            gMapControl1.MaxZoom = 24;

            // Default position centered on Japan with high zoom so satellite imagery fills 100% full screen
            gMapControl1.EmptyTileBorders = new Pen(Color.Transparent, 0);
            gMapControl1.EmptyTileColor = Color.FromArgb(17, 24, 39);
            gMapControl1.ShowTileGridLines = false;

            double initLat = Settings.Instance.GetDouble("maplast_lat", 35.6812);
            double initLng = Settings.Instance.GetDouble("maplast_lng", 139.7671);
            if (initLat == 0 && initLng == 0)
            {
                initLat = 35.6812;
                initLng = 139.7671;
            }
            gMapControl1.Position = new PointLatLng(initLat, initLng);
            gMapControl1.Zoom = 16;

            this.gMapControl1.OnPositionChanged += new GMap.NET.PositionChanged(this.gMapControl1_OnPositionChanged);
            // this.gMapControl1.Click += new System.EventHandler(this.gMapControl1_Click);
            this.gMapControl1.MouseDown += this.gMapControl1_MouseDown;
            this.gMapControl1.MouseLeave += this.gMapControl1_MouseLeave;
            this.gMapControl1.MouseMove += this.gMapControl1_MouseMove;

            //gMapControl1.ShowTileGridLines = true;

            // config map      
            log.Info("Map Setup");
            gMapControl1.CacheLocation = Settings.GetDataDirectory() +
                                         "gmapcache" + Path.DirectorySeparatorChar;
            gMapControl1.MaxZoom = 24;
            gMapControl1.MinZoom = 1;
            gMapControl1.Zoom = 16;

            gMapControl1.ScaleMode = ScaleModes.Fractional;
            gMapControl1.LevelsKeepInMemmory = 5;

            gMapControl1.OnMapZoomChanged += gMapControl1_OnMapZoomChanged;

            gMapControl1.DisableFocusOnMouseEnter = true;

            gMapControl1.OnMarkerEnter += gMapControl1_OnMarkerEnter;
            gMapControl1.OnMarkerLeave += gMapControl1_OnMarkerLeave;

            gMapControl1.RoutesEnabled = true;
            gMapControl1.PolygonsEnabled = true;

            tfrpolygons = new GMapOverlay("tfrpolygons");
            gMapControl1.Overlays.Add(tfrpolygons);

            kmlpolygons = new GMapOverlay("kmlpolygons");
            gMapControl1.Overlays.Add(kmlpolygons);

            geofence = new GMapOverlay("geofence");
            gMapControl1.Overlays.Add(geofence);

            polygons = new GMapOverlay("polygons");
            gMapControl1.Overlays.Add(polygons);

            photosoverlay = new GMapOverlay("photos overlay");
            gMapControl1.Overlays.Add(photosoverlay);

            routes = new GMapOverlay("routes");
            gMapControl1.Overlays.Add(routes);

            rallypointoverlay = new GMapOverlay("rally points");
            gMapControl1.Overlays.Add(rallypointoverlay);

            gMapControl1.Overlays.Add(poioverlay);

            FlightData_Load(null, null);

            int streamRequestCounter = 0;
            Forms.Device.StartTimer(TimeSpan.FromMilliseconds(16), () =>
            {
                try
                {
                    // 🕹️ 60Hz 完全同期: RC_CHANNELS_OVERRIDE 送信 (画面開閉問わず常時最高速送信)
                    SendRCOverridePacket();

                    // 🎮 1. ジョイスティック・モーダル用リアルタイム更新 (FCからのRC_CHANNELS値をリアルタイム表示)
                    try
                    {
                        SubscribeRcChannelsPackets();

                        // 📊 QGC方式: RC_CHANNELS (RX) と RC_OVERRIDE (TX) の周波数 (Hz) とカウントを 1Hz で計算
                        var now = DateTime.UtcNow;
                        double elapsedStats = (now - _lastRcStatsTime).TotalSeconds;
                        if (elapsedStats >= 0.95)
                        {
                            _lastRcStatsTime = now;

                            // FCに対して RC_CHANNELS のストリーム配信 (10Hz) を定期リクエスト
                            if (Pnl_JoystickModal != null && Pnl_JoystickModal.IsVisible)
                            {
                                RequestRCChannelsStream();
                            }

                            // 1. 受信 (RX: FC -> GCS)
                            var csLocal = (MainV2.comPort != null && MainV2.comPort.MAV != null) ? MainV2.comPort.MAV.cs : null;
                            ulong currentRxCount = MAVLinkInterface.GlobalRcChannelsCount;
                            if (currentRxCount == 0 && csLocal != null && csLocal.rcChannelsPacketCount > 0)
                            {
                                currentRxCount = csLocal.rcChannelsPacketCount;
                            }
                            if (currentRxCount == 0)
                            {
                                currentRxCount = (ulong)_rcChannelsPacketCount;
                            }

                            // 🎯 安定した 1秒単位の正確な Hz 計測
                            long nowTicks = DateTime.UtcNow.Ticks;
                            if (_hzCalcStartTicks == 0) _hzCalcStartTicks = nowTicks;
                            double elapsedSec = (nowTicks - _hzCalcStartTicks) / 10000000.0;

                            if (elapsedSec >= 1.0)
                            {
                                ulong rxDelta = (currentRxCount >= _hzCalcStartRxCount) ? (currentRxCount - _hzCalcStartRxCount) : 0;
                                double measuredHz = (double)rxDelta / elapsedSec;
                                _lastRcChannelsHz = measuredHz;

                                _hzCalcStartTicks = nowTicks;
                                _hzCalcStartRxCount = currentRxCount;
                            }

                            // フォールバック: MissionPlanner コアの packetspersecond
                            if (_lastRcChannelsHz <= 0.0 && MainV2.comPort != null && MainV2.comPort.MAV != null && MainV2.comPort.MAV.packetspersecond != null)
                            {
                                if (MainV2.comPort.MAV.packetspersecond.TryGetValue((uint)MAVLink.MAVLINK_MSG_ID.RC_CHANNELS, out double pps) && pps > 0)
                                {
                                    _lastRcChannelsHz = pps;
                                }
                            }

                            // 3. むらさんのご要望: 受信 (RX: RC_CHANNELS) の Hz とカウントを専用表示
                            if (Pnl_JoystickModal != null && Pnl_JoystickModal.IsVisible && LBL_RCChannelsStats != null)
                            {
                                LBL_RCChannelsStats.Text = $"📡 RC_CHANNELS: {_lastRcChannelsHz:F1} Hz | Count: {currentRxCount}";
                            }
                        }

                        // 🎯 チャンネルPWM表示更新
                        if (Pnl_JoystickModal != null && Pnl_JoystickModal.IsVisible)
                        {
                            UpdateJoystickPWMValues();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Joystick modal timer error: " + ex);
                    }

                    // 🛠️ 2. SETUP (キャリブレーション) モーダル用リアルタイム更新
                    try
                    {
                        if (Pnl_SetupModal != null && Pnl_SetupModal.IsVisible)
                        {
                            var cs = MainV2.comPort.MAV.cs;

                            // A. ジャイロ姿勢更新
                            if (View_Setup_Gyro != null && View_Setup_Gyro.IsVisible && LBL_gyro_attitude_live != null)
                            {
                                LBL_gyro_attitude_live.Text = string.Format("Attitude: Roll {0:F1}° | Pitch {1:F1}° | Yaw {2:F1}°", cs.roll, cs.pitch, cs.yaw);
                            }

                            // B. RCプロポバー更新 (全18チャンネル)
                            if (View_Setup_Radio != null && View_Setup_Radio.IsVisible)
                            {
                                for (int ch = 1; ch <= 18; ch++)
                                {
                                    int i = ch - 1;
                                    int val = (int)GetRCChannelInputValue(cs, ch);
                                    if (val > 800 && val < 2200)
                                    {
                                        if (_isRadioCalibrating)
                                        {
                                            _radioMin[i] = Math.Min(_radioMin[i], val);
                                            _radioMax[i] = Math.Max(_radioMax[i], val);
                                        }

                                        var lblVal = this.FindByName<Label>($"LBL_cal_rc{ch}_val");
                                        if (lblVal != null) lblVal.Text = val.ToString();

                                        var pb = this.FindByName<ProgressBar>($"PB_cal_rc{ch}");
                                        if (pb != null) pb.Progress = Math.Max(0.0, Math.Min(1.0, (val - 1000) / 1000.0));

                                        var lblMin = this.FindByName<Label>($"LBL_cal_rc{ch}_min");
                                        if (lblMin != null) lblMin.Text = (_radioMin[i] <= 2200) ? _radioMin[i].ToString() : "1000";

                                        var lblMax = this.FindByName<Label>($"LBL_cal_rc{ch}_max");
                                        if (lblMax != null) lblMax.Text = (_radioMax[i] >= 800) ? _radioMax[i].ToString() : "2000";
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Setup modal timer error: " + ex);
                    }

                    if (MainV2.comPort != null)
                    {
                        var mav = MainV2.comPort.MAV;
                        if (mav == null || mav.sysid == 0)
                        {
                            foreach (var m in MainV2.comPort.MAVlist)
                            {
                                if (m.sysid > 0)
                                {
                                    MainV2.comPort.sysidcurrent = m.sysid;
                                    MainV2.comPort.compidcurrent = m.compid;
                                    mav = m;
                                    break;
                                }
                            }
                        }
                        var cs = mav?.cs ?? MainV2.comPort.MAV?.cs;
                        bool isOpen = MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;

                        if (cs != null && isOpen)
                        {
                            if (++streamRequestCounter % 60 == 1)
                            {
                                log.Info($"=== StampFly Live Attitude === Roll={cs.roll:0.1} Pitch={cs.pitch:0.1} Yaw={cs.yaw:0.1} Batt={cs.battery_voltage:0.2}V");
                                try
                                {
                                    MainV2.comPort.MAVlist[1, 1].mavlinkv2 = true;
                                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.ALL, 10, 1, 1);
                                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA1, 10, 1, 1);
                                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA2, 10, 1, 1);
                                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.POSITION, 5, 1, 1);
                                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTENDED_STATUS, 2, 1, 1);
                                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, 20, 1, 1);
                                    _ = MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.SET_MESSAGE_INTERVAL, 30, 50000, 0, 0, 0, 0, 0, false);
                                }
                                catch { }
                            }
                            // Update HUD attitude & values in real-time
                            hud1.roll = (float)cs.roll;
                            hud1.pitch = (float)cs.pitch;
                            hud1.heading = (float)cs.yaw;
                            hud1.status = cs.armed;
                            hud1.alt = (float)cs.alt;
                            hud1.groundspeed = (float)cs.groundspeed;
                            hud1.airspeed = (float)cs.airspeed;
                            hud1.batterylevel = (float)cs.battery_voltage;
                            hud1.batteryremaining = (float)cs.battery_remaining;
                            hud1.linkqualitygcs = (float)cs.linkqualitygcs;
                            hud1.mode = cs.mode;
                            hud1.connected = true;
                            hud1.Invalidate();

                            // Update top telemetry bar
                            LBL_battery_volt.Text = $"{cs.battery_voltage:0.00} V";
                            LBL_mode_val.Text = (string.IsNullOrEmpty(cs.mode) ? "STABILIZE" : cs.mode.ToUpper()) + " ▾";
                            LBL_link_val.Text = $"{cs.linkqualitygcs}%";
                            LBL_link_val.TextColor = cs.linkqualitygcs > 50 ? global::Xamarin.Forms.Color.FromHex("#10B981") : global::Xamarin.Forms.Color.FromHex("#EF4444");

                            // GNSS Status
                            if (cs.gpsstatus >= 3)
                            {
                                LBL_gps_status.Text = $"3D ({cs.satcount})";
                                LBL_gps_status.TextColor = global::Xamarin.Forms.Color.FromHex("#10B981");
                            }
                            else if (cs.gpsstatus > 0)
                            {
                                LBL_gps_status.Text = $"Fix ({cs.satcount})";
                                LBL_gps_status.TextColor = global::Xamarin.Forms.Color.FromHex("#F59E0B");
                            }
                            else
                            {
                                LBL_gps_status.Text = cs.satcount > 0 ? $"No GPS ({cs.satcount})" : "No GPS";
                                LBL_gps_status.TextColor = global::Xamarin.Forms.Color.FromHex("#F87171");
                            }

                            // Quick Tab Telemetry Updates
                            try
                            {
                                LBL_quick_alt.Text = $"{cs.alt:0.0} m";
                                LBL_quick_speed.Text = $"{cs.groundspeed:0.0} m/s";
                                LBL_quick_yaw.Text = $"{cs.yaw:0}°";
                                LBL_quick_dist.Text = $"{cs.wp_dist:0.0} m";
                                LBL_quick_climb.Text = $"{cs.verticalspeed:0.0} m/s";
                                LBL_quick_volt.Text = $"{cs.battery_voltage:0.00} V";
                                LBL_quick_curr.Text = $"{cs.current:0.0} A";
                                LBL_quick_sats.Text = $"{cs.satcount} sats";

                                if (View_StatusTab.IsVisible)
                                {
                                    LBL_status_list.Text = $"Roll: {cs.roll:0.0}°\nPitch: {cs.pitch:0.0}°\nYaw: {cs.yaw:0.0}°\nAlt: {cs.alt:0.0}m\nClimb: {cs.verticalspeed:0.0}m/s\nVolt: {cs.battery_voltage:0.00}V\nCur: {cs.current:0.0}A\nSatCount: {cs.satcount}\nGPSFix: {cs.gpsstatus}\nArmed: {cs.armed}\nMode: {cs.mode}";
                                }
                            }
                            catch { }

                            // Phone Battery Level
                            try
                            {
                                var phoneBat = (int)(global::Xamarin.Essentials.Battery.ChargeLevel * 100);
                                if (phoneBat >= 0)
                                    LBL_phone_battery.Text = $"{phoneBat}%";
                            }
                            catch { }

                            // Arm / Disarm Status
                            if (cs.armed)
                            {
                                LBL_arm_val.Text = "🟢 ARMED ▾";
                                LBL_arm_val.TextColor = global::Xamarin.Forms.Color.FromHex("#10B981");
                            }
                            else
                            {
                                LBL_arm_val.Text = "🔴 DISARMED ▾";
                                LBL_arm_val.TextColor = global::Xamarin.Forms.Color.FromHex("#EF4444");
                            }

                            if (cs.lat != 0 && cs.lng != 0 && Math.Abs(cs.lat) > 0.001)
                            {
                                gMapControl1.Position = new PointLatLng(cs.lat, cs.lng);
                            }
                        }
                    }
                }
                catch { }
                return true;
            });


            Activate();
        }

        public void Activate()
        {
            log.Info("Activate Called");

            hud1.altunit = CurrentState.AltUnit;
            hud1.speedunit = CurrentState.SpeedUnit;
            hud1.distunit = CurrentState.DistanceUnit;

            // Mode items populated via OnFlightModeTapped

            CheckBatteryShow();

            // make sure the hud user items/warnings/checklist are using the current state
            HUD.Custom.src = MainV2.comPort.MAV.cs;
            CustomWarning.defaultsrc = MainV2.comPort.MAV.cs;


            if (Settings.Instance["maplast_lat"] != "")
            {
                try
                {
                    gMapControl1.Position = new PointLatLng(Settings.Instance.GetDouble("maplast_lat"),
                        Settings.Instance.GetDouble("maplast_lng"));
                    if (Math.Round(Settings.Instance.GetDouble("maplast_lat"), 1) == 0)
                    {
                        // no zoom in

                    }
                    else
                    {
                        var zoom = Settings.Instance.GetFloat("maplast_zoom");

                    }
                }
                catch
                {
                }
            }

            //videoPlayer.Source = VideoSource.FromUri("rtsp://192.168.0.10:8554/H264Video");

            //videoPlayer.Play();
        }

        public void BUT_playlog_Click(object sender, EventArgs e)
        {
            if (MainV2.comPort.logreadmode)
            {
                MainV2.comPort.logreadmode = false;

                playingLog = false;
            }
            else
            {
                // BUT_clear_track_Click(sender, e);
                MainV2.comPort.logreadmode = true;

                playingLog = true;
            }
        }

        public void CheckBatteryShow()
        {
            // ensure battery display is on - also set in hud if current is updated
            if (MainV2.comPort.MAV.param.ContainsKey("BATT_MONITOR") &&
                (float) MainV2.comPort.MAV.param["BATT_MONITOR"] != 0)
            {
                hud1.batteryon = true;
            }
            else
            {
                hud1.batteryon = false;
            }
        }

        public void Deactivate()
        {

            Settings.Instance["maplast_lat"] = gMapControl1.Position.Lat.ToString();
            Settings.Instance["maplast_lng"] = gMapControl1.Position.Lng.ToString();
            Settings.Instance["maplast_zoom"] = gMapControl1.Zoom.ToString();

        }

        public void LoadLogFile(FileData file)
        {
            if (file != null)
            {
                try
                {
                    BUT_clear_track_Click(null, null);

                    MainV2.comPort.logreadmode = true;
                    MainV2.comPort.logplaybackfile = new BinaryReader(file.GetStream());
                    MainV2.comPort.lastlogread = DateTime.MinValue;

                    LBL_logfn.Text = Path.GetFileName(file.FileName);

                    log.Info("Open logfile " + file);

                    MainV2.comPort.getHeartBeat();

                    tracklog.Value = 0;
                    tracklog.Minimum = 0;
                    tracklog.Maximum = 100;
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(Strings.PleaseLoadValidFile + ex.ToString(), Strings.ERROR);
                }
            }
        }

        public void Invoke(Action action)
        {
            Forms.Device.BeginInvokeOnMainThread(action);
        }

        protected void Dispose(bool disposing)
        {
            MainV2.comPort.logreadmode = false;
            try
            {
                //if (hud1 != null)
                //Settings.Instance["FlightSplitter"] = MainH.SplitterDistance.ToString();
            }
            catch
            {
            }

            if (polygons != null)
                polygons.Dispose();
            if (routes != null)
                routes.Dispose();
            if (route != null)
                route.Dispose();
            if (marker != null)
                marker.Dispose();

            if (prop != null)
                prop.Stop();
        }

        void addHudUserItem(ref HUD.Custom cust, string name)
        {
            setupPropertyInfo(ref cust.Item, name, MainV2.comPort.MAV.cs);

            hud1.CustomItems[name] = cust;

            hud1.Invalidate();
        }

        private void addMissionPhotoMarker(GMapMarker marker)
        {
            // not async
            Invoke((Action) delegate { photosoverlay.Markers.Add(marker); });
        }

        private void addMissionRouteMarker(GMapMarker marker)
        {
            // not async
            Invoke((Action) delegate { routes.Markers.Add(marker); });
        }


        private void addpolygonmarker(string tag, double lng, double lat, int alt, Color? color, GMapOverlay overlay)
        {
            try
            {
                PointLatLng point = new PointLatLng(lat, lng);
                GMarkerGoogle m = new GMarkerGoogle(point, GMarkerGoogleType.green);
                m.ToolTipMode = MarkerTooltipMode.Always;
                m.ToolTipText = tag;
                m.Tag = tag;

                GMapMarkerRect mBorders = new GMapMarkerRect(point);
                {
                    mBorders.InnerMarker = m;
                    try
                    {
                        mBorders.wprad =
                            (int) (Settings.Instance.GetFloat("TXT_WPRad") / CurrentState.multiplierdist);
                    }
                    catch
                    {
                    }

                    if (color.HasValue)
                    {
                        mBorders.Color = color.Value;
                    }
                }

                Invoke((Action) delegate
                {
                    overlay.Markers.Add(m);
                    overlay.Markers.Add(mBorders);
                });
            }
            catch (Exception)
            {
            }
        }

        private void addpolygonmarkerred(string tag, double lng, double lat, int alt, Color? color, GMapOverlay overlay)
        {
            try
            {
                PointLatLng point = new PointLatLng(lat, lng);
                GMarkerGoogle m = new GMarkerGoogle(point, GMarkerGoogleType.red);
                m.ToolTipMode = MarkerTooltipMode.Always;
                m.ToolTipText = tag;
                m.Tag = tag;

                GMapMarkerRect mBorders = new GMapMarkerRect(point);
                {
                    mBorders.InnerMarker = m;
                }

                Invoke((Action) delegate
                {
                    overlay.Markers.Add(m);
                    overlay.Markers.Add(mBorders);
                });
            }
            catch (Exception)
            {
            }
        }

        private void BUT_clear_track_Click(object sender, EventArgs e)
        {
            if (route != null)
                route.Points.Clear();

            if (MainV2.comPort.MAV.camerapoints != null)
                MainV2.comPort.MAV.camerapoints.Clear();
        }

        private async void BUT_loadtelem_Click(object sender, EventArgs e)
        {
            LBL_logfn.Text = "";

            if (MainV2.comPort.logplaybackfile != null)
            {
                try
                {
                    MainV2.comPort.logplaybackfile.Close();
                    MainV2.comPort.logplaybackfile = null;
                }
                catch
                {
                }
            }



            //using (OpenFileDialog fd = new OpenFileDialog())
            {
                //fd.AddExtension = true;
                //fd.Filter = "Telemetry log (*.tlog)|*.tlog;*.tlog.*|Mavlink Log (*.mavlog)|*.mavlog";
                //fd.InitialDirectory = Settings.Instance.LogDir;
                //fd.DefaultExt = ".tlog";
                //DialogResult result = fd.ShowDialog();

                FileData file = await CrossFilePicker.Current.PickFile(new string[] {".tlog"});
                if (file == null)
                    return; // user canceled file picking

                LoadLogFile(file);
            }
        }

        private void BUT_log2kml_Click(object sender, EventArgs e)
        {
            //Form frm = new MavlinkLog();
            //ThemeManager.ApplyThemeTo(frm);
            //frm.Show();
        }

        private void BUT_quickauto_Click(object sender, EventArgs e)
        {
            try
            {
                ((Button) sender).IsEnabled = false;
                MainV2.comPort.setMode("Auto");
            }
            catch
            {
                CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
            }

            ((Button) sender).IsEnabled = true;
        }

        private void BUT_quickmanual_Click(object sender, EventArgs e)
        {
            try
            {
                ((Button) sender).IsEnabled = false;
                if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduPlane ||
                    MainV2.comPort.MAV.cs.firmware == Firmwares.Ateryx ||
                    MainV2.comPort.MAV.cs.firmware == Firmwares.ArduRover)
                    MainV2.comPort.setMode("Loiter");
                if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2)
                    MainV2.comPort.setMode("Loiter");
            }
            catch
            {
                CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
            }

            ((Button) sender).IsEnabled = true;
        }

        private void BUT_quickrtl_Click(object sender, EventArgs e)
        {
            try
            {
                ((Button) sender).IsEnabled = false;
                MainV2.comPort.setMode("RTL");
            }
            catch
            {
                CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
            }

            ((Button) sender).IsEnabled = true;
        }

        void cam_camimage(System.Drawing.Image camimage)
        {
            hud1.bgimage = camimage;
        }

        private void CheckAndBindPreFlightData()
        {
            //this.Invoke((Action) delegate { preFlightChecklist1.BindData(); });
        }

 

        private void FlightData_Load(object sender, EventArgs e)
        {
            //POI.POIModified += POI_POIModified;

            // tfr.GotTFRs += tfr_GotTFRs;

            //if (!Settings.Instance.ContainsKey("ShowNoFly") || Settings.Instance.GetBoolean("ShowNoFly"))
            //NoFly.NoFly.NoFlyEvent += NoFly_NoFlyEvent;

  

            gMapControl1.EmptyTileColor = Color.Gray;

            //Zoomlevel.Minimum = gMapControl1.MapProvider.MinZoom;
            //Zoomlevel.Maximum = 24;
            //Zoomlevel.Value = Convert.ToDecimal(gMapControl1.Zoom);

            var item1 = ParameterMetaDataRepository.GetParameterOptionsInt("MNT_MODE",
                MainV2.comPort.MAV.cs.firmware.ToString());
            var item2 = ParameterMetaDataRepository.GetParameterOptionsInt("MNT_DEFLT_MODE",
                MainV2.comPort.MAV.cs.firmware.ToString());
            //if (item1.Count > 0)
            //CMB_mountmode.DataSource = item1;

            //if (item2.Count > 0)
            //CMB_mountmode.DataSource = item2;

            //CMB_mountmode.DisplayMember = "Value";
            //CMB_mountmode.ValueMember = "Key";



            //if (Settings.Instance.ContainsKey("HudSwap") && Settings.Instance["HudSwap"] == "true")
            //SwapHud1AndMap();

            if (Settings.Instance.ContainsKey("FlightSplitter"))
            {
                //MainH.SplitterDistance = Settings.Instance.GetInt32("FlightSplitter");
            }

            if (Settings.Instance.ContainsKey("russian_hud"))
            {
                hud1.Russian = Settings.Instance.GetBoolean("russian_hud");
            }

            //groundColorToolStripMenuItem.Checked = Settings.Instance.GetBoolean("groundColorToolStripMenuItem");
            //groundColorToolStripMenuItem_Click(null, null);

            hud1.doResize();

            prop = new Propagation(gMapControl1);

            thisthread = new Thread(mainloop);
            thisthread.Name = "FD Mainloop";
            thisthread.IsBackground = true;
            thisthread.Start();
        }

        private void gMapControl1_MouseDown(object sender, MouseEventArgs e)
        {
            MouseDownStart = gMapControl1.FromLocalToLatLng(e.X, e.Y);


            if (gMapControl1.IsMouseOverMarker)
            {
                if (CurrentGMapMarker is GMapMarkerADSBPlane)
                {
                    var marker = CurrentGMapMarker as GMapMarkerADSBPlane;
                    if (marker.Tag is adsb.PointLatLngAltHdg)
                    {
                        var plla = marker.Tag as adsb.PointLatLngAltHdg;
                        plla.DisplayICAO = !plla.DisplayICAO;
                    }
                }
            }
        }

        private void gMapControl1_MouseLeave(object sender, EventArgs e)
        {
            if (marker != null)
            {
                try
                {
                    if (routes.Markers.Contains(marker))
                        routes.Markers.Remove(marker);
                }
                catch
                {
                }
            }
        }

        private void gMapControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointLatLng point = gMapControl1.FromLocalToLatLng(e.X, e.Y);

                double latdif = MouseDownStart.Lat - point.Lat;
                double lngdif = MouseDownStart.Lng - point.Lng;

                gMapControl1.Position = new PointLatLng(center.Position.Lat + latdif,
                    center.Position.Lng + lngdif);
            }
            else
            {
                // setup a ballon with home distance
                if (marker != null)
                {
                    if (routes.Markers.Contains(marker))
                        routes.Markers.Remove(marker);
                }

                if (Settings.Instance.GetBoolean("CHK_disttohomeflightdata") != false)
                {
                    PointLatLng point = gMapControl1.FromLocalToLatLng(e.X, e.Y);

                    marker = new GMapMarkerRect(point);
                    marker.ToolTip = new GMapToolTip(marker);
                    marker.ToolTipMode = MarkerTooltipMode.Always;
                    marker.ToolTipText = "Dist to Home: " +
                                         ((gMapControl1.MapProvider.Projection.GetDistance(point,
                                               MainV2.comPort.MAV.cs.HomeLocation.Point()) * 1000) *
                                          CurrentState.multiplierdist).ToString("0");

                    routes.Markers.Add(marker);
                }
            }
        }

        void gMapControl1_OnMapZoomChanged()
        {
            try
            {
                // Exception System.Runtime.InteropServices.SEHException: External component has thrown an exception.
       
                //  Zoomlevel.Value = Convert.ToDecimal(gMapControl1.Zoom);
            }
            catch
            {
            }

            center.Position = gMapControl1.Position;
        }

        void gMapControl1_OnMarkerEnter(GMapMarker item)
        {
            CurrentGMapMarker = item;
        }

        void gMapControl1_OnMarkerLeave(GMapMarker item)
        {
            CurrentGMapMarker = null;
        }

        private void gMapControl1_OnPositionChanged(PointLatLng point)
        {
            center.Position = point;

            UpdateOverlayVisibility();
        }

        double LogPlayBackSpeed = 1.0;
        bool playingLog;
        List<PointLatLng> trackPoints = new List<PointLatLng>();

        private async void mainloop()
        {
            if (threadrun == true)
                return;
            threadrun = true;
            EndPoint Remote = new IPEndPoint(IPAddress.Any, 0);

            DateTime tracklast = DateTime.Now.AddSeconds(0);

            DateTime tunning = DateTime.Now.AddSeconds(0);

            DateTime mapupdate = DateTime.Now.AddSeconds(0);

            DateTime vidrec = DateTime.Now.AddSeconds(0);

            DateTime waypoints = DateTime.Now.AddSeconds(0);

            DateTime updatescreen = DateTime.Now;

            DateTime tsreal = DateTime.Now;
            double taketime = 0;
            double timeerror = 0;


            while (threadrun)
            {
                if (MainV2.comPort.giveComport)
                {
                    Thread.Sleep(50);
                    updateBindingSource();
                    continue;
                }

                if (!MainV2.comPort.logreadmode)
                    Thread.Sleep(50); // max is only ever 10 hz but we go a little faster to empty the serial queue


                // log playback
                if (MainV2.comPort.logreadmode && MainV2.comPort.logplaybackfile != null)
                {
                    if (MainV2.comPort.BaseStream.IsOpen)
                    {
                        MainV2.comPort.logreadmode = false;
                        try
                        {
                            MainV2.comPort.logplaybackfile.Close();
                        }
                        catch
                        {
                            log.Error("Failed to close logfile");
                        }

                        MainV2.comPort.logplaybackfile = null;
                    }


                    //Console.WriteLine(DateTime.Now.Millisecond);

                    if (updatescreen.AddMilliseconds(300) < DateTime.Now)
                    {
                        try
                        {
                            updatePlayPauseButton(true);
                            updateLogPlayPosition();
                        }
                        catch
                        {
                            log.Error("Failed to update log playback pos");
                        }

                        updatescreen = DateTime.Now;
                    }

                    //Console.WriteLine(DateTime.Now.Millisecond + " done ");

                    DateTime logplayback = MainV2.comPort.lastlogread;
                    try
                    {
                        if (!MainV2.comPort.giveComport)
                            await MainV2.comPort.readPacketAsync().ConfigureAwait(false);

                        // update currentstate of sysids on the port
                        foreach (var MAV in MainV2.comPort.MAVlist)
                        {
                            try
                            {
                                MAV.cs.UpdateCurrentSettings(null, false, MainV2.comPort, MAV);
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }
                        }
                    }
                    catch
                    {
                        log.Error("Failed to read log packet");
                    }

                    double act = (MainV2.comPort.lastlogread - logplayback).TotalMilliseconds;

                    if (act > 9999 || act < 0)
                        act = 0;

                    double ts = 0;
                    if (LogPlayBackSpeed == 0)
                        LogPlayBackSpeed = 0.01;
                    try
                    {
                        ts = Math.Min((act / LogPlayBackSpeed), 1000);
                    }
                    catch
                    {
                    }

                    if (LogPlayBackSpeed >= 4 && MainV2.speechEnabled())
                        MainV2.speechEngine.SpeakAsyncCancelAll();

                    double timetook = (DateTime.Now - tsreal).TotalMilliseconds;
                    if (timetook != 0)
                    {
                        //Console.WriteLine("took: " + timetook + "=" + taketime + " " + (taketime - timetook) + " " + ts);
                        //Console.WriteLine(MainV2.comPort.lastlogread.Second + " " + DateTime.Now.Second + " " + (MainV2.comPort.lastlogread.Second - DateTime.Now.Second));
                        //if ((taketime - timetook) < 0)
                        {
                            timeerror += (taketime - timetook);
                            if (ts != 0)
                            {
                                ts += timeerror;
                                timeerror = 0;
                            }
                        }
                        if (Math.Abs(ts) > 1000)
                            ts = 1000;
                    }

                    taketime = ts;
                    tsreal = DateTime.Now;

                    if (ts > 0 && ts < 1000)
                        Thread.Sleep((int) ts);

                    tracklast = tracklast.AddMilliseconds(ts - act);
                    tunning = tunning.AddMilliseconds(ts - act);

                    if (tracklast.Month != DateTime.Now.Month)
                    {
                        tracklast = DateTime.Now;
                        tunning = DateTime.Now;
                    }

                    try
                    {
                        if (MainV2.comPort.logplaybackfile != null &&
                            MainV2.comPort.logplaybackfile.BaseStream.Position ==
                            MainV2.comPort.logplaybackfile.BaseStream.Length)
                        {
                            MainV2.comPort.logreadmode = false;
                        }
                    }
                    catch
                    {
                        MainV2.comPort.logreadmode = false;
                    }
                }
                else
                {
                    // ensure we know to stop
                    if (MainV2.comPort.logreadmode)
                        MainV2.comPort.logreadmode = false;
                    updatePlayPauseButton(false);

                    if (!playingLog && MainV2.comPort.logplaybackfile != null)
                    {
                        continue;
                    }
                }

                try
                {
                    CheckAndBindPreFlightData();
                    //Console.WriteLine(DateTime.Now.Millisecond);
                    //int fixme;
                    updateBindingSource();
                    // Console.WriteLine(DateTime.Now.Millisecond + " done ");

                    // battery warning.
                    float warnvolt = Settings.Instance.GetFloat("speechbatteryvolt");
                    float warnpercent = Settings.Instance.GetFloat("speechbatterypercent");

                    if (MainV2.comPort.MAV.cs.battery_voltage <= warnvolt)
                    {
                        hud1.lowvoltagealert = true;
                    }
                    else if ((MainV2.comPort.MAV.cs.battery_remaining) < warnpercent)
                    {
                        hud1.lowvoltagealert = true;
                    }
                    else
                    {
                        hud1.lowvoltagealert = false;
                    }



                    Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        var start = DateTime.Now;

                        hud1.HoldInvalidation = true;
                        hud1.airspeed = MainV2.comPort.MAV.cs.airspeed;
                        hud1.alt = MainV2.comPort.MAV.cs.alt;
                        hud1.batterylevel = (float) MainV2.comPort.MAV.cs.battery_voltage;
                        hud1.batteryremaining = MainV2.comPort.MAV.cs.battery_remaining;
                        hud1.connected = MainV2.comPort.MAV.cs.connected;
                        hud1.current = (float) MainV2.comPort.MAV.cs.current;
                        hud1.datetime = MainV2.comPort.MAV.cs.datetime;
                        hud1.disttowp = MainV2.comPort.MAV.cs.wp_dist;
                        hud1.ekfstatus = MainV2.comPort.MAV.cs.ekfstatus;
                        hud1.failsafe = MainV2.comPort.MAV.cs.failsafe;
                        hud1.gpsfix = MainV2.comPort.MAV.cs.gpsstatus;
                        hud1.gpsfix2 = MainV2.comPort.MAV.cs.gpsstatus2;
                        hud1.gpshdop = MainV2.comPort.MAV.cs.gpshdop;
                        hud1.gpshdop2 = MainV2.comPort.MAV.cs.gpshdop2;
                        hud1.groundalt = (float) MainV2.comPort.MAV.cs.HomeAlt;
                        hud1.groundcourse = MainV2.comPort.MAV.cs.groundcourse;
                        hud1.groundspeed = MainV2.comPort.MAV.cs.groundspeed;
                        hud1.heading = MainV2.comPort.MAV.cs.yaw;
                        hud1.linkqualitygcs = MainV2.comPort.MAV.cs.linkqualitygcs;
                        hud1.message = MainV2.comPort.MAV.cs.messageHigh;
                        hud1.mode = MainV2.comPort.MAV.cs.mode;
                        hud1.navpitch = MainV2.comPort.MAV.cs.nav_pitch;
                        hud1.navroll = MainV2.comPort.MAV.cs.nav_roll;
                        hud1.pitch = MainV2.comPort.MAV.cs.pitch;
                        hud1.roll = MainV2.comPort.MAV.cs.roll;
                        hud1.status = MainV2.comPort.MAV.cs.armed;
                        hud1.targetalt = MainV2.comPort.MAV.cs.targetalt;
                        hud1.targetheading = MainV2.comPort.MAV.cs.nav_bearing;
                        hud1.targetspeed = MainV2.comPort.MAV.cs.targetairspeed;
                        hud1.turnrate = MainV2.comPort.MAV.cs.turnrate;
                        hud1.verticalspeed = MainV2.comPort.MAV.cs.verticalspeed;
                        hud1.vibex = MainV2.comPort.MAV.cs.vibex;
                        hud1.vibey = MainV2.comPort.MAV.cs.vibey;
                        hud1.vibez = MainV2.comPort.MAV.cs.vibez;
                        hud1.wpno = (int) MainV2.comPort.MAV.cs.wpno;
                        hud1.xtrack_error = MainV2.comPort.MAV.cs.xtrack_error;
                        hud1.AOA = MainV2.comPort.MAV.cs.AOA;
                        hud1.SSA = MainV2.comPort.MAV.cs.SSA;
                        hud1.critAOA = MainV2.comPort.MAV.cs.crit_AOA;
                        hud1.HoldInvalidation = false;
                        hud1.Invalidate();

                        hud1.Refresh();
                    });
                    // update map
                    if (tracklast.AddSeconds(Settings.Instance.GetDouble("FD_MapUpdateDelay", 1.2)) < DateTime.Now)
                    {
                        adsb.CurrentPosition = MainV2.comPort.MAV.cs.HomeLocation;

                        // show proximity screen
                        if (MainV2.comPort.MAV?.Proximity != null && MainV2.comPort.MAV.Proximity.DataAvailable)
                        {
                            //this.BeginInvoke((MethodInvoker)delegate { new ProximityControl(MainV2.comPort.MAV).Show(); });
                        }

                        if (Settings.Instance.GetBoolean("CHK_maprotation"))
                        {
                            // dont holdinvalidation here
                            setMapBearing();
                        }

                        if (route == null)
                        {
                            route = new GMapRoute(trackPoints, "track");
                            routes.Routes.Add(route);
                        }

                        PointLatLng currentloc = new PointLatLng(MainV2.comPort.MAV.cs.lat, MainV2.comPort.MAV.cs.lng);

                        gMapControl1.HoldInvalidation = true;

                        int numTrackLength = Settings.Instance.GetInt32("NUM_tracklength", 200);
                        // maintain route history length
                        if (route.Points.Count > numTrackLength)
                        {
                            route.Points.RemoveRange(0,
                                route.Points.Count - numTrackLength);
                        }

                        // add new route point
                        if (MainV2.comPort.MAV.cs.lat != 0 && MainV2.comPort.MAV.cs.lng != 0)
                        {
                            route.Points.Add(currentloc);
                        }

                        updateRoutePosition();

                        // update programed wp course
                        if (waypoints.AddSeconds(5) < DateTime.Now)
                        {
                            //Console.WriteLine("Doing FD WP's");
                            updateClearMissionRouteMarkers();

                            var wps = MainV2.comPort.MAV.wps.Values.ToList();
                            if (wps.Count >= 1)
                            {
                                var homeplla = new PointLatLngAlt(MainV2.comPort.MAV.cs.HomeLocation.Lat,
                                    MainV2.comPort.MAV.cs.HomeLocation.Lng,
                                    MainV2.comPort.MAV.cs.HomeLocation.Alt / CurrentState.multiplieralt, "H");

                                var overlay = new WPOverlay();

                                {
                                    List<Locationwp> mission_items;
                                    mission_items = MainV2.comPort.MAV.wps.Values.Select(a => (Locationwp) a).ToList();
                                    mission_items.RemoveAt(0);

                                    if (wps.Count == 1)
                                    {
                                        overlay.CreateOverlay(homeplla,
                                            mission_items,
                                            0 / CurrentState.multiplieralt, 0 / CurrentState.multiplieralt);
                                    }
                                    else
                                    {
                                        overlay.CreateOverlay(homeplla,
                                            mission_items,
                                            0 / CurrentState.multiplieralt, 0 / CurrentState.multiplieralt);

                                    }
                                }

                                var existing = gMapControl1.Overlays.Where(a => a.Id == overlay.overlay.Id).ToList();
                                foreach (var b in existing)
                                {
                                    gMapControl1.Overlays.Remove(b);
                                }

                                gMapControl1.Overlays.Insert(1, overlay.overlay);

                                overlay.overlay.ForceUpdate();

                                //distanceBar1.ClearWPDist();

                                var i = -1;
                                var travdist = 0.0;
                                var lastplla = overlay.pointlist.First();
                                foreach (var plla in overlay.pointlist)
                                {
                                    i++;
                                    if (plla == null)
                                        continue;

                                    var dist = lastplla.GetDistance(plla);

                                    //distanceBar1.AddWPDist((float)dist);

                                    if (i <= MainV2.comPort.MAV.cs.wpno)
                                    {
                                        travdist += dist;
                                    }
                                }

                                travdist -= MainV2.comPort.MAV.cs.wp_dist;

                                //if (MainV2.comPort.MAV.cs.mode.ToUpper() == "AUTO")
                                //distanceBar1.traveleddist = (float)travdist;
                            }

                            RegeneratePolygon();

                            // update rally points

                            rallypointoverlay.Markers.Clear();

                            foreach (var mark in MainV2.comPort.MAV.rallypoints.Values)
                            {
                                rallypointoverlay.Markers.Add(new GMapMarkerRallyPt(new PointLatLngAlt(mark)));
                            }

                            geofence.Clear();

                            var fenceoverlay = new WPOverlay();
                            fenceoverlay.overlay.Id = "fence";

                            fenceoverlay.CreateOverlay(PointLatLngAlt.Zero,
                                MainV2.comPort.MAV.fencepoints.Values.Select(a => (Locationwp) a).ToList(), 0, 0);

                            var fence = mymap.Overlays.Where(a => a.Id == "fence");
                            if (fence.Count() > 0)
                                mymap.Overlays.Remove(fence.First());
                            mymap.Overlays.Add(fenceoverlay.overlay);

                            fenceoverlay.overlay.ForceUpdate();

                            // optional on Flight data
                            if (MainV2.ShowAirports)
                            {
                                // airports
                                foreach (var item in Airports.getAirports(gMapControl1.Position).ToArray())
                                {
                                    try
                                    {
                                        rallypointoverlay.Markers.Add(new GMapMarkerAirport(item)
                                        {
                                            ToolTipText = item.Tag,
                                            ToolTipMode = MarkerTooltipMode.OnMouseOver
                                        });
                                    }
                                    catch (Exception e)
                                    {
                                        log.Error(e);
                                    }
                                }
                            }

                            waypoints = DateTime.Now;
                        }

                        updateClearRoutesMarkers();

                        // add this after the mav icons are drawn
                        if (false)
                        {
                            addMissionRouteMarker(new GMarkerGoogle(currentloc, GMarkerGoogleType.blue_dot)
                            {
                                Position = PointLatLngAlt.Zero,
                                ToolTipText = "Moving Base",
                                ToolTipMode = MarkerTooltipMode.OnMouseOver
                            });
                        }

                        // add gimbal point center
                        try
                        {
                            if (MainV2.comPort.MAV.param.ContainsKey("MNT_STAB_TILT")
                                && MainV2.comPort.MAV.param.ContainsKey("MNT_STAB_ROLL")
                                && MainV2.comPort.MAV.param.ContainsKey("MNT_TYPE"))
                            {
                                float temp1 = (float) MainV2.comPort.MAV.param["MNT_STAB_TILT"];
                                float temp2 = (float) MainV2.comPort.MAV.param["MNT_STAB_ROLL"];

                                float temp3 = (float) MainV2.comPort.MAV.param["MNT_TYPE"];

                                if (MainV2.comPort.MAV.param.ContainsKey("MNT_STAB_PAN") &&
                                    // (float)MainV2.comPort.MAV.param["MNT_STAB_PAN"] == 1 &&
                                    ((float) MainV2.comPort.MAV.param["MNT_STAB_TILT"] == 1 &&
                                     (float) MainV2.comPort.MAV.param["MNT_STAB_ROLL"] == 0) ||
                                    (float) MainV2.comPort.MAV.param["MNT_TYPE"] == 4) // storm driver
                                {
                                    var marker = GimbalPoint.ProjectPoint(MainV2.comPort);

                                    if (marker != PointLatLngAlt.Zero)
                                    {
                                        MainV2.comPort.MAV.cs.GimbalPoint = marker;

                                        addMissionRouteMarker(new GMarkerGoogle(marker, GMarkerGoogleType.blue_dot)
                                        {
                                            ToolTipText = "Camera Target\n" + marker,
                                            ToolTipMode = MarkerTooltipMode.OnMouseOver
                                        });
                                    }
                                }
                            }


                            // cleanup old - no markers where added, so remove all old 
                            if (MainV2.comPort.MAV.camerapoints.Count < photosoverlay.Markers.Count)
                                photosoverlay.Markers.Clear();

                            var min_interval = 0.0;
                            if (MainV2.comPort.MAV.param.ContainsKey("CAM_MIN_INTERVAL"))
                                min_interval = MainV2.comPort.MAV.param["CAM_MIN_INTERVAL"].Value / 1000.0;

                            // set fov's based on last grid calc
                            if (Settings.Instance["camera_fovh"] != null)
                            {
                                GMapMarkerPhoto.hfov = Settings.Instance.GetDouble("camera_fovh");
                                GMapMarkerPhoto.vfov = Settings.Instance.GetDouble("camera_fovv");
                            }

                            // add new - populate camera_feedback to map
                            double oldtime = double.MinValue;
                            foreach (var mark in MainV2.comPort.MAV.camerapoints.ToArray())
                            {
                                var timesincelastshot = (mark.time_usec / 1000.0) / 1000.0 - oldtime;
                                MainV2.comPort.MAV.cs.timesincelastshot = timesincelastshot;
                                bool contains = photosoverlay.Markers.Any(p => p.Tag.Equals(mark.time_usec));
                                if (!contains)
                                {
                                    if (timesincelastshot < min_interval)
                                        addMissionPhotoMarker(new GMapMarkerPhoto(mark, true));
                                    else
                                        addMissionPhotoMarker(new GMapMarkerPhoto(mark, false));
                                }

                                oldtime = (mark.time_usec / 1000.0) / 1000.0;
                            }

                            var GMapMarkerOverlapCount = new GMapMarkerOverlapCount(PointLatLng.Empty);

                            // age current
                            int camcount = MainV2.comPort.MAV.camerapoints.Count;
                            int a = 0;
                            foreach (var mark in photosoverlay.Markers)
                            {
                                if (mark is GMapMarkerPhoto)
                                {
                                    if (CameraOverlap)
                                    {
                                        var marker = ((GMapMarkerPhoto) mark);
                                        // abandon roll higher than 25 degrees
                                        if (Math.Abs(marker.Roll) < 25)
                                        {
                                            GMapMarkerOverlapCount.Add(
                                                ((GMapMarkerPhoto) mark).footprintpoly);
                                        }
                                    }

                                    if (a < (camcount - 4))
                                        ((GMapMarkerPhoto) mark).drawfootprint = false;
                                }

                                a++;
                            }

                            if (CameraOverlap)
                            {
                                if (!kmlpolygons.Markers.Contains(GMapMarkerOverlapCount) &&
                                    camcount > 0)
                                {
                                    kmlpolygons.Markers.Clear();
                                    kmlpolygons.Markers.Add(GMapMarkerOverlapCount);
                                }
                            }
                            else if (kmlpolygons.Markers.Contains(GMapMarkerOverlapCount))
                            {
                                kmlpolygons.Markers.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }

                        lock (MainV2.instance.adsblock)
                        {
                            foreach (adsb.PointLatLngAltHdg plla in MainV2.instance.adsbPlanes.Values)
                            {
                                // 30 seconds history
                                if (((DateTime) plla.Time) > DateTime.Now.AddSeconds(-30))
                                {
                                    var adsbplane = new GMapMarkerADSBPlane(plla, plla.Heading)
                                    {
                                        ToolTipText = "ICAO: " + plla.Tag + "\n" +
                                                      "Alt: " + plla.Alt.ToString("0") + "\n" +
                                                      "Speed: " + plla.Speed.ToString("0") + "\n" +
                                                      "Heading: " + plla.Heading.ToString("0"),
                                        ToolTipMode = MarkerTooltipMode.OnMouseOver,
                                        Tag = plla
                                    };

                                    if (plla.DisplayICAO)
                                        adsbplane.ToolTipMode = MarkerTooltipMode.Always;

                                    switch (plla.ThreatLevel)
                                    {
                                        case MAVLink.MAV_COLLISION_THREAT_LEVEL.NONE:
                                            adsbplane.AlertLevel = GMapMarkerADSBPlane.AlertLevelOptions.Green;
                                            break;
                                        case MAVLink.MAV_COLLISION_THREAT_LEVEL.LOW:
                                            adsbplane.AlertLevel = GMapMarkerADSBPlane.AlertLevelOptions.Orange;
                                            break;
                                        case MAVLink.MAV_COLLISION_THREAT_LEVEL.HIGH:
                                            adsbplane.AlertLevel = GMapMarkerADSBPlane.AlertLevelOptions.Red;
                                            break;
                                    }

                                    addMissionRouteMarker(adsbplane);
                                }
                            }
                        }


                        if (route.Points.Count > 0)
                        {
                            // add primary route icon

                            // draw guide mode point for only main mav
                            if (MainV2.comPort.MAV.cs.mode.ToLower() == "guided" &&
                                MainV2.comPort.MAV.GuidedMode.x != 0)
                            {
                                addpolygonmarker("Guided Mode", MainV2.comPort.MAV.GuidedMode.y / 1e7,
                                    MainV2.comPort.MAV.GuidedMode.x / 1e7, (int) MainV2.comPort.MAV.GuidedMode.z,
                                    Color.Blue,
                                    routes);
                            }

                            // draw all icons for all connected mavs
                            foreach (var port in MainV2.Comports.ToArray())
                            {
                                // draw the mavs seen on this port
                                foreach (var MAV in port.MAVlist)
                                {
                                    var marker = Common.getMAVMarker(MAV);

                                    if (marker.Position.Lat == 0 && marker.Position.Lng == 0)
                                        continue;

                                    addMissionRouteMarker(marker);
                                }
                            }

                            if (route.Points.Count == 0 || route.Points[route.Points.Count - 1].Lat != 0 &&
                                (mapupdate.AddSeconds(3) < DateTime.Now))
                            {
                                updateMapPosition(currentloc);
                                mapupdate = DateTime.Now;
                            }

                            if (route.Points.Count == 1 && gMapControl1.Zoom <= 5) // 3 is the default load zoom
                            {
                                updateMapPosition(currentloc);
                                updateMapZoom(17);
                            }
                        }

                        prop.Update(MainV2.comPort.MAV.cs.HomeLocation, MainV2.comPort.MAV.cs.Location,
                            MainV2.comPort.MAV.cs.battery_kmleft);

                        prop.alt = MainV2.comPort.MAV.cs.alt;
                        prop.altasl = MainV2.comPort.MAV.cs.altasl;
                        prop.center = gMapControl1.Position;

                        gMapControl1.HoldInvalidation = false;

                        if (gMapControl1.Visible)
                        {
                            gMapControl1.Invalidate();
                        }

                        tracklast = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    Console.WriteLine("FD Main loop exception " + ex);
                }
            }

            Console.WriteLine("FD Main loop exit");
        }

        /*        void NoFly_NoFlyEvent(object sender, NoFly.NoFly.NoFlyEventArgs e)
        {
            Invoke((Action)delegate
           {
               foreach (var poly in e.NoFlyZones.Polygons)
               {
                   kmlpolygons.Polygons.Add(poly);
               }
           });
        }
        */


        private void PinchGestureRecognizer_OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {

        }

        /// <summary>
        /// used to redraw the polygon
        /// </summary>
        void RegeneratePolygon()
        {
            List<PointLatLng> polygonPoints = new List<PointLatLng>();

            if (routes == null)
                return;

            foreach (GMapMarker m in polygons.Markers)
            {
                if (m is GMapMarkerRect)
                {
                    m.Tag = polygonPoints.Count;
                    polygonPoints.Add(m.Position);
                }
            }

            if (polygonPoints.Count < 2)
                return;

            GMapRoute homeroute = new GMapRoute("homepath");
            homeroute.Stroke = new Pen(Color.Yellow, 2);
            homeroute.Stroke.DashStyle = DashStyle.Dash;
            // add first point past home
            homeroute.Points.Add(polygonPoints[1]);
            // add home location
            homeroute.Points.Add(polygonPoints[0]);
            // add last point
            homeroute.Points.Add(polygonPoints[polygonPoints.Count - 1]);

            GMapRoute wppath = new GMapRoute("wp path");
            wppath.Stroke = new Pen(Color.Yellow, 4);
            wppath.Stroke.DashStyle = DashStyle.Custom;

            for (int a = 1; a < polygonPoints.Count; a++)
            {
                wppath.Points.Add(polygonPoints[a]);
            }

            Invoke((Action) delegate
            {
                polygons.Routes.Add(homeroute);
                polygons.Routes.Add(wppath);
            });
        }

        private void setMapBearing()
        {
            Invoke((Action) delegate { gMapControl1.Bearing = (int) ((MainV2.comPort.MAV.cs.yaw + 360) % 360); });
        }

        private async void setMJPEGSourceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string url = Settings.Instance["mjpeg_url"] != null
                ? Settings.Instance["mjpeg_url"]
                : @"http://127.0.0.1:56781/map.jpg";

            if ((url = await InputBox.Show("Mjpeg url", "Enter the url to the mjpeg source url")) != null)
            {
                Settings.Instance["mjpeg_url"] = url;

                CaptureMJPEG.Stop();

                CaptureMJPEG.URL = url;

                CaptureMJPEG.runAsync();
            }
            else
            {
                CaptureMJPEG.Stop();
            }
        }

        bool setupPropertyInfo(ref PropertyInfo input, string name, object source)
        {
            Type test = source.GetType();

            foreach (var field in test.GetProperties())
            {
                if (field.Name == name)
                {
                    input = field;
                    return true;
                }
            }

            return false;
        }


        private void SkglView_OnTouch(object sender, SKTouchEventArgs e)
        {
            touchpoint = e.Location;
            //SkglView.InvalidateSurface();

            e.Handled = true;
        }

        DateTime lastscreenupdate = DateTime.Now;
        volatile int updateBindingSourcecount;

        object updateBindingSourcelock = new object();
        string updateBindingSourceThreadName = "";
        private string modeselected;

        void tfr_GotTFRs(object sender, EventArgs e)
        {
            TFR tfr = new TFR();
            Invoke((Action) delegate
            {
                foreach (var item in tfr.tfrs)
                {
                    List<List<PointLatLng>> points = item.GetPaths();

                    foreach (var list in points)
                    {
                        GMapPolygon poly = new GMapPolygon(list, item.NAME);

                        poly.Fill = new SolidBrush(Color.FromArgb(30, Color.Blue));

                        tfrpolygons.Polygons.Add(poly);
                    }
                }

                tfrpolygons.IsVisibile = MainV2.ShowTFR;
            });
        }

        private void tracklog_Scroll(object sender, EventArgs e)
        {
            try
            {
                BUT_clear_track_Click(sender, e);

                MainV2.comPort.lastlogread = DateTime.MinValue;
                MainV2.comPort.MAV.cs.ResetInternals();

                if (MainV2.comPort.logplaybackfile != null)
                    MainV2.comPort.logplaybackfile.BaseStream.Position =
                        (long) (MainV2.comPort.logplaybackfile.BaseStream.Length * (tracklog.Value / 100.0));

                updateLogPlayPosition(false);
            }
            catch
            {
            } // ignore any invalid 
        }


        private void updateBindingSource()
        {
            //  run at 25 hz.
            if (lastscreenupdate.AddMilliseconds(40) < DateTime.Now)
            {
                lock (updateBindingSourcelock)
                {
                    // this is an attempt to prevent an invoke queue on the binding update on slow machines
                    if (updateBindingSourcecount > 0)
                    {
                        if (lastscreenupdate < DateTime.Now.AddSeconds(-5))
                        {
                            updateBindingSourcecount = 0;
                        }

                        return;
                    }

                    updateBindingSourcecount++;
                    updateBindingSourceThreadName = Thread.CurrentThread.Name;
                }

                Forms.Device.BeginInvokeOnMainThread((Action) delegate
                {
                    //updateBindingSourceWork();

                    lock (updateBindingSourcelock)
                    {
                        updateBindingSourcecount--;
                    }
                });
            }
        }


        // to prevent cross thread calls while in a draw and exception
        private void updateClearMissionRouteMarkers()
        {
            // not async
            Invoke((Action) delegate
            {
                polygons.Routes.Clear();
                polygons.Markers.Clear();
                routes.Markers.Clear();
            });
        }

        // to prevent cross thread calls while in a draw and exception
        private void updateClearRoutes()
        {
            // not async
            Invoke((Action) delegate
            {
                routes.Routes.Clear();
                routes.Routes.Add(route);
            });
        }

        private void updateClearRoutesMarkers()
        {
            Invoke((Action) delegate { routes.Markers.Clear(); });
        }

        private void updateLogPlayPosition(bool updatetracklog = true)
        {
            BeginInvoke((Action) delegate
            {
                try
                {
                    if (updatetracklog && tracklog.IsVisible)
                    {
                        // prevent event fire
                        tracklog.ValueChanged -= tracklog_Scroll;
                        tracklog.Value = (int) (MainV2.comPort.logplaybackfile.BaseStream.Position /
                                                (double) MainV2.comPort.logplaybackfile.BaseStream.Length * 100);
                        tracklog.ValueChanged += tracklog_Scroll;
                    }

                    if (lbl_logpercent.IsVisible)
                        lbl_logpercent.Text =
                            (MainV2.comPort.logplaybackfile.BaseStream.Position /
                             (double) MainV2.comPort.logplaybackfile.BaseStream.Length).ToString("0.00%");

                    if (lbl_playbackspeed.IsVisible)
                        lbl_playbackspeed.Text = "x " + LogPlayBackSpeed;
                }
                catch
                {
                }
            });
        }

        private void updateMapPosition(PointLatLng currentloc)
        {
            Invoke((Action) delegate
            {
                try
                {
                    if (lastmapposchange.Second != DateTime.Now.Second)
                    {
                        if (Math.Abs(currentloc.Lat - gMapControl1.Position.Lat) > 0.0001 ||
                            Math.Abs(currentloc.Lng - gMapControl1.Position.Lng) > 0.0001)
                        {
                            gMapControl1.Position = currentloc;
                        }

                        lastmapposchange = DateTime.Now;
                    }

                    //hud1.Refresh();
                }
                catch
                {
                }
            });
        }

        private void updateMapZoom(int zoom)
        {
            Invoke((Action) delegate
            {
                try
                {
                    gMapControl1.Zoom = zoom;
                }
                catch
                {
                }
            });
        }

        void UpdateOverlayVisibility()
        {
            // change overlay visability
            if (gMapControl1.ViewArea != null)
            {
                var bounds = gMapControl1.ViewArea;
                bounds.Inflate(1, 1);

                if (kmlpolygons == null)
                    return;

                foreach (var poly in kmlpolygons.Polygons)
                {
                    if (bounds.Contains(poly.Points[0]))
                        poly.IsVisible = true;
                    else
                        poly.IsVisible = false;
                }
            }
        }

        private void updatePlayPauseButton(bool playing)
        {
            if (playing)
            {
                if (BUT_playlog.Text == "Pause")
                    return;

                BeginInvoke((Action) delegate
                {
                    try
                    {
                        BUT_playlog.Text = "Pause";
                    }
                    catch
                    {
                    }
                });
            }
            else
            {
                if (BUT_playlog.Text == "Play")
                    return;

                BeginInvoke((Action) delegate
                {
                    try
                    {
                        BUT_playlog.Text = "Play";
                    }
                    catch
                    {
                    }
                });
            }
        }

        private void BeginInvoke(Action action)
        {
            Forms.Device.BeginInvokeOnMainThread(action);
        }

        private void updateRoutePosition()
        {
            // not async
            Invoke((Action) delegate { gMapControl1.UpdateRouteLocalPosition(route); });
        }

        private void Button_Onclicked(object sender, EventArgs e)
        {
            
        }

        private MAVLinkInterface mav => MainV2.comPort;

        
                private async void OnFlightModeTapped(object sender, EventArgs e)
        {
            try
            {
                string currentMode = MainV2.comPort.MAV?.cs?.mode ?? "STABILIZE";
                string action = await DisplayActionSheet($"フライトモード選択 (現在: {currentMode})", "キャンセル", null,
                    "STABILIZE (手動)",
                    "ALTHOLD (高度維持)",
                    "LOITER (定点維持)",
                    "LAND (着陸)",
                    "RTL (自動帰還)",
                    "POSHOLD (位置維持)",
                    "FLOWHOLD (フロー維持)",
                    "ACRO (アクロ)");

                if (string.IsNullOrEmpty(action) || action == "キャンセル")
                    return;

                string targetMode = "STABILIZE";
                uint customMode = 0;

                if (action.StartsWith("STABILIZE")) { targetMode = "Stabilize"; customMode = 0; }
                else if (action.StartsWith("ALTHOLD")) { targetMode = "AltHold"; customMode = 2; }
                else if (action.StartsWith("LOITER")) { targetMode = "Loiter"; customMode = 5; }
                else if (action.StartsWith("LAND")) { targetMode = "Land"; customMode = 9; }
                else if (action.StartsWith("RTL")) { targetMode = "RTL"; customMode = 6; }
                else if (action.StartsWith("POSHOLD")) { targetMode = "PosHold"; customMode = 16; }
                else if (action.StartsWith("FLOWHOLD")) { targetMode = "FlowHold"; customMode = 22; }
                else if (action.StartsWith("ACRO")) { targetMode = "Acro"; customMode = 1; }

                log.InfoFormat("User selected mode from TopBar: {0} ({1})", action, targetMode);

                MainV2.comPort.setMode(1, 1, targetMode);
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, customMode, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast($"{targetMode.ToUpper()} モードに変更要求送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

                        private async void OnMotorControlTapped(object sender, EventArgs e)
        {
            try
            {
                bool isArmed = MainV2.comPort.MAV?.cs?.armed ?? false;
                string statusStr = isArmed ? "アーム中（ARMED）" : "停止中（DISARMED）";
                string action = await DisplayActionSheet($"モーター制御 (現在: {statusStr})", "キャンセル", null,
                    "🟢 ARM（モーター始動）",
                    "🔴 DISARM（モーター停止）",
                    "🚨 緊急着陸（LAND）");

                if (string.IsNullOrEmpty(action) || action == "キャンセル")
                    return;

                if (action.Contains("ARM（モーター始動）"))
                {
                    await MainV2.comPort.doARMAsync(1, 1, true);
                    UserDialogs.Instance.Toast("🟢 ARM（始動）コマンド送信", TimeSpan.FromSeconds(1));
                }
                else if (action.Contains("DISARM（モーター停止）"))
                {
                    await MainV2.comPort.doARMAsync(1, 1, false);
                    UserDialogs.Instance.Toast("🔴 DISARM（停止）コマンド送信", TimeSpan.FromSeconds(1));
                }
                else if (action.Contains("緊急着陸"))
                {
                    MainV2.comPort.setMode(1, 1, "Land");
                    await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 9, 0, 0, 0, 0, 0, false);
                    UserDialogs.Instance.Toast("🚨 緊急着陸コマンド送信", TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }


        private async void OnMessagesTapped(object sender, EventArgs e)
        {
            try
            {
                var cs = MainV2.comPort?.MAV?.cs;
                if (cs != null && cs.messages != null && cs.messages.Count > 0)
                {
                    var msgList = cs.messages.Skip(Math.Max(0, cs.messages.Count - 20)).Select(m => $"[{m.time:HH:mm:ss}] {m.message}").ToList();
                    msgList.Reverse(); // 最新を上に
                    string allMsgs = string.Join(Environment.NewLine + Environment.NewLine, msgList);
                    await DisplayAlert("FC / MP メッセージ履歴", allMsgs, "閉じる");
                }
                else
                {
                    await DisplayAlert("FC / MP メッセージ", "現在、受信したメッセージはありません。", "OK");
                }
            }
            catch (Exception ex)
            {
                log.Error("OnMessagesTapped ex: " + ex);
            }
        }


        private void OnTabQuickClicked(object sender, EventArgs e)
        {
            View_QuickTab.IsVisible = true;
            View_ActionsTab.IsVisible = false;
            View_StatusTab.IsVisible = false;

            Btn_Tab_Quick.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#2563EB");
            Btn_Tab_Quick.TextColor = global::Xamarin.Forms.Color.White;
            Btn_Tab_Actions.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
            Btn_Tab_Actions.TextColor = global::Xamarin.Forms.Color.FromHex("#94A3B8");
            Btn_Tab_Status.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
            Btn_Tab_Status.TextColor = global::Xamarin.Forms.Color.FromHex("#94A3B8");
        }

        private void OnTabActionsClicked(object sender, EventArgs e)
        {
            View_QuickTab.IsVisible = false;
            View_ActionsTab.IsVisible = true;
            View_StatusTab.IsVisible = false;

            Btn_Tab_Quick.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
            Btn_Tab_Quick.TextColor = global::Xamarin.Forms.Color.FromHex("#94A3B8");
            Btn_Tab_Actions.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#2563EB");
            Btn_Tab_Actions.TextColor = global::Xamarin.Forms.Color.White;
            Btn_Tab_Status.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
            Btn_Tab_Status.TextColor = global::Xamarin.Forms.Color.FromHex("#94A3B8");
        }

        private void OnTabStatusClicked(object sender, EventArgs e)
        {
            View_QuickTab.IsVisible = false;
            View_ActionsTab.IsVisible = false;
            View_StatusTab.IsVisible = true;

            Btn_Tab_Quick.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
            Btn_Tab_Quick.TextColor = global::Xamarin.Forms.Color.FromHex("#94A3B8");
            Btn_Tab_Actions.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
            Btn_Tab_Actions.TextColor = global::Xamarin.Forms.Color.FromHex("#94A3B8");
            Btn_Tab_Status.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#2563EB");
            Btn_Tab_Status.TextColor = global::Xamarin.Forms.Color.White;
        }

        private void OnCollapseDockClicked(object sender, EventArgs e)
        {
            Pnl_FlightDock.IsVisible = false;
            Pnl_FlightDockCollapsed.IsVisible = true;
        }

        private void OnExpandDockClicked(object sender, EventArgs e)
        {
            Pnl_FlightDock.IsVisible = true;
            Pnl_FlightDockCollapsed.IsVisible = false;
        }

        private async void OnModeStabilizeClicked(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.setMode(1, 1, "Stabilize");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 0, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("🕹️ STABILIZE モード要求", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex) { log.Error(ex); }
        }

        private async void OnModeAltHoldClicked(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.setMode(1, 1, "AltHold");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 2, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("🔒 ALTHOLD (高度維持) 要求", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex) { log.Error(ex); }
        }

        private async void OnModeLoiterClicked(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.setMode(1, 1, "Loiter");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 5, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("📍 LOITER (位置維持) 要求", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex) { log.Error(ex); }
        }

        private async void OnQuickLandTapped(object sender, EventArgs e)
        {
            try
            {
                log.Info("OnQuickLandTapped");
                MainV2.comPort.setMode(1, 1, "Land");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 9, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("🛬 着陸（LAND）モード送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void OnQuickRTLTapped(object sender, EventArgs e)
        {
            try
            {
                log.Info("OnQuickRTLTapped");
                MainV2.comPort.setMode(1, 1, "RTL");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 6, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("🏠 自動帰還（RTL）モード送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Takeoff_1m_OnClicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Takeoff_1m_OnClicked");
                if (!MainV2.comPort.MAV.cs.armed)
                {
                    await MainV2.comPort.doARMAsync(1, 1, true);
                    await Task.Delay(500);
                }
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, 1.0f, false);
                UserDialogs.Instance.Toast("🛫 離陸（1.0m）コマンド送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Btn_Arm_Toggle_Clicked(object sender, EventArgs e)
        {
            try
            {
                bool isArmed = MainV2.comPort.MAV?.cs?.armed ?? false;
                string statusStr = isArmed ? "アーム中（ARMED）" : "停止中（DISARMED）";
                string action = await DisplayActionSheet($"モーター制御 (現在: {statusStr})", "キャンセル", null,
                    "🟢 ARM（始動）",
                    "🔴 DISARM（停止）",
                    "🚨 緊急着陸（LAND）");

                if (string.IsNullOrEmpty(action) || action == "キャンセル")
                    return;

                if (action.Contains("ARM（始動）"))
                {
                    await MainV2.comPort.doARMAsync(1, 1, true);
                    UserDialogs.Instance.Toast("🟢 ARM（始動）コマンド送信", TimeSpan.FromSeconds(1));
                }
                else if (action.Contains("DISARM（停止）"))
                {
                    await MainV2.comPort.doARMAsync(1, 1, false);
                    UserDialogs.Instance.Toast("🔴 DISARM（停止）コマンド送信", TimeSpan.FromSeconds(1));
                }
                else if (action.Contains("緊急着陸"))
                {
                    MainV2.comPort.setMode(1, 1, "Land");
                    await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 9, 0, 0, 0, 0, 0, false);
                    UserDialogs.Instance.Toast("🚨 緊急着陸コマンド送信", TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Land_OnClicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Land_OnClicked");
                MainV2.comPort.setMode(1, 1, "Land");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 9, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("緊急着陸（LAND）送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Btn_AltHold_Clicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Btn_AltHold_Clicked");
                MainV2.comPort.setMode(1, 1, "AltHold");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 2, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("ALTHOLD モード要求送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Btn_Loiter_Clicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Btn_Loiter_Clicked");
                MainV2.comPort.setMode(1, 1, "Loiter");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 5, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("LOITER モード要求送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Btn_Stabilize_Clicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Btn_Stabilize_Clicked");
                MainV2.comPort.setMode(1, 1, "Stabilize");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 0, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("STABILIZE モード要求送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Btn_RTL_Clicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Btn_RTL_Clicked");
                MainV2.comPort.setMode(1, 1, "RTL");
                await MainV2.comPort.doCommandAsync(1, 1, MAVLink.MAV_CMD.DO_SET_MODE, (float)MAVLink.MAV_MODE_FLAG.CUSTOM_MODE_ENABLED, 6, 0, 0, 0, 0, 0, false);
                UserDialogs.Instance.Toast("RTL モード要求送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async void Arm_OnClicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Arm_OnClicked");
                await MainV2.comPort.doARMAsync(1, 1, true);
                UserDialogs.Instance.Toast("ARM（始動）送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception exception)
            {
                UserDialogs.Instance.Toast("ARM Error: " + exception.Message, TimeSpan.FromSeconds(2));
                log.Error(exception);
            }
        }

        private async void Disarm_OnClicked(object sender, EventArgs e)
        {
            try
            {
                log.Info("Disarm_OnClicked");
                await MainV2.comPort.doARMAsync(1, 1, false);
                UserDialogs.Instance.Toast("DISARM（停止）送信", TimeSpan.FromSeconds(1));
            }
            catch (Exception exception)
            {
                UserDialogs.Instance.Toast("DISARM Error: " + exception.Message, TimeSpan.FromSeconds(2));
                log.Error(exception);
            }
        }

        private void Set_Mode_OnClicked(object sender, EventArgs e)
        {
            mav.setMode(mav.MAV.sysid, mav.MAV.compid, modeselected);
        }

        private async void Get_Mission_OnClicked(object sender, EventArgs e)
        {
            try
            {
                await mav_mission.download(mav, mav.MAV.sysid, mav.MAV.compid, MAVLink.MAV_MISSION_TYPE.MISSION);
            }
            catch (Exception exception)
            {
                UserDialogs.Instance.Toast(exception.Message, TimeSpan.FromSeconds(3));
                Console.WriteLine(exception);
                //throw;
            }
        }

        private async void Get_Fence_OnClicked(object sender, EventArgs e)
        {
            try
            {
                await mav_mission.download(mav, mav.MAV.sysid, mav.MAV.compid, MAVLink.MAV_MISSION_TYPE.FENCE);
            }
            catch (Exception exception)
            {
                UserDialogs.Instance.Toast(exception.Message, TimeSpan.FromSeconds(3));
                Console.WriteLine(exception);
                //throw;
            }
        }

        private async void Get_Rally_OnClicked(object sender, EventArgs e)
        {
            try
            {
                await mav_mission.download(mav, mav.MAV.sysid, mav.MAV.compid, MAVLink.MAV_MISSION_TYPE.RALLY);
            }
            catch (Exception exception)
            {
                UserDialogs.Instance.Toast(exception.Message, TimeSpan.FromSeconds(3));
                Console.WriteLine(exception);
                //throw;
            }
        }

        private async void Takeoff___2m_OnClicked(object sender, EventArgs e)
        {
            try
            {
                mav.setMode("GUIDED"); 
                await mav.doCommandAsync(mav.MAV.sysid, mav.MAV.compid, MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, 2);
            }
            catch (Exception exception)
            {
                UserDialogs.Instance.Toast(exception.Message, TimeSpan.FromSeconds(3));
                Console.WriteLine(exception);
                //throw;
            }
        }

        private void Mode_OnSelectedIndexChanged(object sender, EventArgs e)
        {
        }

        // 🎮 ジョイスティック設定モーダルの開閉
        public void OnOpenJoystickModalClicked(object sender, EventArgs e)
        {
            if (Pnl_JoystickModal != null)
            {
                Pnl_JoystickModal.IsVisible = true;
                SubscribeRcChannelsPackets();
                RequestRCChannelsStream();

                // 接続デバイス名の更新
                try
                {
                    if (GetConnectedJoysticksFunc != null && Picker_ActiveJoystick != null)
                    {
                        var list = GetConnectedJoysticksFunc();
                        Picker_ActiveJoystick.Items.Clear();
                        if (list != null && list.Count > 0)
                        {
                            foreach (var j in list) Picker_ActiveJoystick.Items.Add(j);
                            Picker_ActiveJoystick.SelectedIndex = 0;
                        }
                        else
                        {
                            Picker_ActiveJoystick.Items.Add("⚠️ No Device Detected");
                            Picker_ActiveJoystick.SelectedIndex = 0;
                        }
                    }
                }
                catch { }

                // 画面上の全UI（軸割り当て・リバース・エクスポ・モード）を保存設定と同期
                LoadJoystickSettings();
            }
        }

        public void OnCloseJoystickModalClicked(object sender, EventArgs e)
        {
            if (Pnl_JoystickModal != null)
            {
                Pnl_JoystickModal.IsVisible = false;
            }
        }

        public void OnJoystickRescanClicked(object sender, EventArgs e)
        {
            try
            {
                if (GetConnectedJoysticksFunc != null && Picker_ActiveJoystick != null)
                {
                    var list = GetConnectedJoysticksFunc();
                    Picker_ActiveJoystick.Items.Clear();
                    if (list != null && list.Count > 0)
                    {
                        foreach (var j in list) Picker_ActiveJoystick.Items.Add(j);
                        Picker_ActiveJoystick.SelectedIndex = 0;
                    }
                    else
                    {
                        Picker_ActiveJoystick.Items.Add("⚠️ No Device Detected");
                        Picker_ActiveJoystick.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnJoystickRescanClicked error: " + ex);
            }
        }

        public async void OnJoystickTestVibeClicked(object sender, EventArgs e)
        {
            try
            {
                global::Xamarin.Essentials.Vibration.Vibrate(TimeSpan.FromMilliseconds(200));
            }
            catch { }
        }

        // 💾 ジョイスティック設定の完全永続化保存
        public void SaveJoystickSettings()
        {
            try
            {
                for (int ch = 1; ch <= 18; ch++)
                {
                    // 1. 軸・キー割り当て
                    var btn = this.FindByName<Button>($"Btn_RCAxis_{ch}");
                    string mapping = (btn != null && !string.IsNullOrEmpty(btn.Text))
                        ? NormalizeAxisName(btn.Text)
                        : ((ch < ChannelAxisMapping.Length) ? ChannelAxisMapping[ch] : "None");

                    if (ch < ChannelAxisMapping.Length)
                    {
                        ChannelAxisMapping[ch] = mapping;
                    }
                    global::Xamarin.Essentials.Preferences.Set($"MP_Joy_RC{ch}_Axis", mapping);

                    // 2. リバース (Rev)
                    var chkRev = this.FindByName<CheckBox>($"CHK_RCRev_{ch}");
                    if (chkRev != null)
                    {
                        if (ch < ChannelReverseMapping.Length) ChannelReverseMapping[ch] = chkRev.IsChecked;
                        global::Xamarin.Essentials.Preferences.Set($"MP_Joy_RC{ch}_Rev", chkRev.IsChecked);
                    }

                    // 3. エクスポ (Expo)
                    var entExpo = this.FindByName<Entry>($"ENT_RCExpo_{ch}");
                    if (entExpo != null)
                    {
                        float.TryParse(entExpo.Text, out float expVal);
                        if (ch < ChannelExpoMapping.Length) ChannelExpoMapping[ch] = expVal;
                        global::Xamarin.Essentials.Preferences.Set($"MP_Joy_RC{ch}_Expo", entExpo.Text ?? "0");
                    }
                }

                // 4. スティックモード
                if (Picker_StickMode != null && Picker_StickMode.SelectedIndex >= 0)
                {
                    global::Xamarin.Essentials.Preferences.Set("MP_Joy_StickMode", Picker_StickMode.SelectedIndex);
                }

                // 5. Elevons / ManualControl
                if (CHK_elevons != null)
                {
                    global::Xamarin.Essentials.Preferences.Set("MP_Joy_Elevons", CHK_elevons.IsChecked);
                }
                if (CHK_manual_control != null)
                {
                    global::Xamarin.Essentials.Preferences.Set("MP_Joy_ManualControl", CHK_manual_control.IsChecked);
                }

                // 6. Send Rate & Enable
                if (Picker_SendRate != null && Picker_SendRate.SelectedIndex >= 0)
                {
                    global::Xamarin.Essentials.Preferences.Set("MP_Joy_SendRateIdx", Picker_SendRate.SelectedIndex);
                    JoystickSendIntervalMs = GetSendIntervalMsFromIndex(Picker_SendRate.SelectedIndex);
                }
                if (CHK_enable_joystick != null)
                {
                    global::Xamarin.Essentials.Preferences.Set("MP_Joy_Enabled", CHK_enable_joystick.IsChecked);
                    IsJoystickActive = CHK_enable_joystick.IsChecked;
                }
                UpdateRCOverrideTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("SaveJoystickSettings error: " + ex);
            }
        }

        // 📂 ジョイスティック設定の完全復元読み込み
        public void LoadJoystickSettings()
        {
            try
            {
                string[] defaults = new string[19]
                {
                    "", "X", "Y", "Z", "Rz", "Slider1", "None",
                    "None", "None", "None", "None", "None", "None",
                    "None", "None", "None", "None", "None", "None"
                };

                for (int ch = 1; ch <= 18; ch++)
                {
                    string def = (ch < defaults.Length) ? defaults[ch] : "None";
                    string savedAxis = global::Xamarin.Essentials.Preferences.Get($"MP_Joy_RC{ch}_Axis", def);
                    savedAxis = NormalizeAxisName(savedAxis);

                    if (ch < ChannelAxisMapping.Length)
                    {
                        ChannelAxisMapping[ch] = savedAxis;
                    }

                    var btn = this.FindByName<Button>($"Btn_RCAxis_{ch}");
                    if (btn != null)
                    {
                        btn.Text = savedAxis + " ▾";
                        btn.TextColor = (savedAxis != "None")
                            ? global::Xamarin.Forms.Color.FromHex("#10B981")
                            : global::Xamarin.Forms.Color.FromHex("#64748B");
                    }

                    bool isRev = global::Xamarin.Essentials.Preferences.Get($"MP_Joy_RC{ch}_Rev", false);
                    if (ch < ChannelReverseMapping.Length) ChannelReverseMapping[ch] = isRev;
                    var chkRev = this.FindByName<CheckBox>($"CHK_RCRev_{ch}");
                    if (chkRev != null)
                    {
                        chkRev.IsChecked = isRev;
                    }

                    var entExpo = this.FindByName<Entry>($"ENT_RCExpo_{ch}");
                    if (entExpo != null)
                    {
                        string savedExpo = global::Xamarin.Essentials.Preferences.Get($"MP_Joy_RC{ch}_Expo", "0");
                        entExpo.Text = savedExpo;
                        float.TryParse(savedExpo, out float expVal);
                        if (ch < ChannelExpoMapping.Length) ChannelExpoMapping[ch] = expVal;
                    }
                }

                if (Picker_StickMode != null)
                {
                    int mode = global::Xamarin.Essentials.Preferences.Get("MP_Joy_StickMode", 0);
                    if (mode >= 0 && mode < Picker_StickMode.Items.Count)
                    {
                        Picker_StickMode.SelectedIndex = mode;
                    }
                }

                if (CHK_elevons != null)
                {
                    CHK_elevons.IsChecked = global::Xamarin.Essentials.Preferences.Get("MP_Joy_Elevons", false);
                }
                if (CHK_manual_control != null)
                {
                    CHK_manual_control.IsChecked = global::Xamarin.Essentials.Preferences.Get("MP_Joy_ManualControl", true);
                }

                // 送信レート & 有効化の復元
                int savedRateIdx = global::Xamarin.Essentials.Preferences.Get("MP_Joy_SendRateIdx", 0); // 0 = 60 Hz (16.6ms)
                if (Picker_SendRate != null)
                {
                    Picker_SendRate.SelectedIndex = Math.Max(0, Math.Min(4, savedRateIdx));
                }
                JoystickSendIntervalMs = GetSendIntervalMsFromIndex(savedRateIdx);

                bool savedEnabled = global::Xamarin.Essentials.Preferences.Get("MP_Joy_Enabled", false);
                if (CHK_enable_joystick != null)
                {
                    CHK_enable_joystick.IsChecked = savedEnabled;
                }
                IsJoystickActive = savedEnabled;
                UpdateRCOverrideTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadJoystickSettings error: " + ex);
            }
        }

        private static bool _isRcPacketSubscribed = false;

        private void SubscribeRcChannelsPackets()
        {
            try
            {
                if (MainV2.comPort != null && !_isRcPacketSubscribed)
                {
                    MainV2.comPort.OnPacketReceived += OnGlobalPacketReceived;
                    _isRcPacketSubscribed = true;
                }
            }
            catch { }
        }

        private static int _streamParamSetCounter = 0;
        private static DateTime _lastStreamRequestTime = DateTime.MinValue;

        private void RequestRCChannelsStream()
        {
            try
            {
                // StampFly (ESP32 Wi-Fi) のバッファ溢れ防止: 10秒に1回だけキープアライブ送信
                if ((DateTime.UtcNow - _lastStreamRequestTime).TotalSeconds < 10.0)
                    return;
                _lastStreamRequestTime = DateTime.UtcNow;

                if (MainV2.comPort != null && MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen)
                {
                    byte sysid = (byte)((MainV2.comPort.MAV != null && MainV2.comPort.MAV.sysid > 0) ? MainV2.comPort.MAV.sysid : (MainV2.comPort.sysidcurrent > 0 ? MainV2.comPort.sysidcurrent : 1));
                    byte compid = (byte)((MainV2.comPort.MAV != null && MainV2.comPort.MAV.compid > 0) ? MainV2.comPort.MAV.compid : (MainV2.comPort.compidcurrent > 0 ? MainV2.comPort.compidcurrent : 1));

                    if (MainV2.comPort.MAV != null && MainV2.comPort.MAV.cs != null)
                    {
                        MainV2.comPort.MAV.cs.raterc = 10;
                    }

                    // 1. QGC準拠: MAV_CMD_SET_MESSAGE_INTERVAL (RC_CHANNELS msgid 65, 100,000 µs = 10Hz)
                    var cmdSetInterval = new MAVLink.mavlink_command_long_t
                    {
                        target_system = sysid,
                        target_component = compid,
                        command = (ushort)MAVLink.MAV_CMD.SET_MESSAGE_INTERVAL,
                        confirmation = 0,
                        param1 = 65,        // RC_CHANNELS
                        param2 = 100000,    // 100,000 µs = 10.0 Hz (StampFly Wi-Fi に最適な黄金比)
                        param3 = 0,
                        param4 = 0,
                        param5 = 0,
                        param6 = 0,
                        param7 = 0
                    };
                    MainV2.comPort.sendPacket(cmdSetInterval, sysid, compid);

                    // 2. ダイレクト REQUEST_DATA_STREAM パケット (10Hz)
                    var req = new MAVLink.mavlink_request_data_stream_t
                    {
                        target_system = sysid,
                        target_component = compid,
                        req_message_rate = 10,
                        start_stop = 1,
                        req_stream_id = (byte)MAVLink.MAV_DATA_STREAM.RC_CHANNELS
                    };
                    MainV2.comPort.sendPacket(req, sysid, compid);

                    // 3. FCパラメータ設定 (10Hz)
                    try
                    {
                        MainV2.comPort.setParam("SR1_RC_CHAN", 10);
                        MainV2.comPort.setParam("SR0_RC_CHAN", 10);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private long _lastInstantPwmUpdateTicks = 0;
        public void TriggerInstantPWMUpdate()
        {
            long now = DateTime.UtcNow.Ticks;
            // 50Hz (20ms) 以上の頻度での過剰なUIディスパッチを抑制
            if (now - _lastInstantPwmUpdateTicks > 200000)
            {
                _lastInstantPwmUpdateTicks = now;
                global::Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                {
                    UpdateJoystickPWMValues();
                });
            }
        }

        public void UpdateJoystickPWMValues()
        {
            if (Pnl_JoystickModal == null || !Pnl_JoystickModal.IsVisible) return;
            var cs = (MainV2.comPort != null && MainV2.comPort.MAV != null) ? MainV2.comPort.MAV.cs : null;

            for (int ch = 1; ch <= 18; ch++)
            {
                string mapping = (ch < ChannelAxisMapping.Length) ? ChannelAxisMapping[ch] : "None";
                var btn = this.FindByName<Button>($"Btn_RCAxis_{ch}");
                if (btn != null && !string.IsNullOrEmpty(btn.Text))
                {
                    mapping = btn.Text.Replace(" ▾", "").Trim();
                }

                bool isRev = (ch < ChannelReverseMapping.Length) ? ChannelReverseMapping[ch] : false;
                var chkRev = this.FindByName<CheckBox>($"CHK_RCRev_{ch}");
                if (chkRev != null)
                {
                    isRev = chkRev.IsChecked;
                    if (ch < ChannelReverseMapping.Length) ChannelReverseMapping[ch] = isRev;
                }

                float expo = (ch < ChannelExpoMapping.Length) ? ChannelExpoMapping[ch] : 0f;
                var entExpo = this.FindByName<Entry>($"ENT_RCExpo_{ch}");
                if (entExpo != null && float.TryParse(entExpo.Text, out float parsedExpo))
                {
                    expo = parsedExpo;
                    if (ch < ChannelExpoMapping.Length) ChannelExpoMapping[ch] = expo;
                }

                GetChannelLimits(ch, out float min, out float max, out float trim);

                // 🎯 1. リアルポジション表示: FC からの RC_CHANNELS テレメトリ値を表示
                float fcPwm = (cs != null) ? GetFCChannelPwm(cs, ch) : 0f;

                // 🎯 2. FC未接続/未受信時のフォールバック用ローカル計算PWM値（相対 -> MIN/MAX変換）
                int localPwm = CalculateChannelPWM(ch, mapping, isRev, expo);

                float displayPwm = (fcPwm > 0f) ? fcPwm : (float)localPwm;

                var lbl = this.FindByName<Label>($"LBL_joy_rc{ch}");
                if (lbl != null)
                {
                    lbl.Text = $"{(int)Math.Round(displayPwm)} µs";
                    lbl.TextColor = (fcPwm > 0f)
                        ? global::Xamarin.Forms.Color.FromHex("#38BDF8")
                        : global::Xamarin.Forms.Color.FromHex("#94A3B8");
                }
            }
        }

        private static void OnGlobalPacketReceived(object sender, MAVLink.MAVLinkMessage msg)
        {
            if (msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.RC_CHANNELS || msg.msgid == 65 ||
                msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.RC_CHANNELS_RAW || msg.msgid == 35 ||
                msg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.RC_CHANNELS_SCALED)
            {
                System.Threading.Interlocked.Increment(ref _rcChannelsPacketCount);
                System.Threading.Interlocked.Increment(ref _rcChannelsWindowCount);
                // UIスレッド競合防止: タイマー側の滑らかな定期更新(30Hz)に一本化
            }
        }

        // 📡 RC_CHANNELS_OVERRIDE パケット送信処理 (60Hz 同期・将来の V2 拡張対応)
        public static bool UseRCOverrideV2 = false; // 🚀 将来の RC_CHANNELS_OVERRIDE-V2 移行用フラグ

        private void SendRCOverridePacket()
        {
            if (!IsJoystickActive || MainV2.comPort == null)
                return;

            try
            {
                if (UseRCOverrideV2)
                {
                    // 🚀 将来の RC_CHANNELS_OVERRIDE-V2 送信処理
                    SendRCOverrideV2Packet();
                    return;
                }

                var rcOverride = new MAVLink.mavlink_rc_channels_override_t
                {
                    target_system = 1,
                    target_component = 1,
                    chan1_raw = (ushort)CalculateChannelPWM(1, ChannelAxisMapping[1], ChannelReverseMapping[1], ChannelExpoMapping[1]),
                    chan2_raw = (ushort)CalculateChannelPWM(2, ChannelAxisMapping[2], ChannelReverseMapping[2], ChannelExpoMapping[2]),
                    chan3_raw = (ushort)CalculateChannelPWM(3, ChannelAxisMapping[3], ChannelReverseMapping[3], ChannelExpoMapping[3]),
                    chan4_raw = (ushort)CalculateChannelPWM(4, ChannelAxisMapping[4], ChannelReverseMapping[4], ChannelExpoMapping[4]),
                    chan5_raw = (ushort)CalculateChannelPWM(5, ChannelAxisMapping[5], ChannelReverseMapping[5], ChannelExpoMapping[5]),
                    chan6_raw = (ushort)CalculateChannelPWM(6, ChannelAxisMapping[6], ChannelReverseMapping[6], ChannelExpoMapping[6]),
                    chan7_raw = (ushort)CalculateChannelPWM(7, ChannelAxisMapping[7], ChannelReverseMapping[7], ChannelExpoMapping[7]),
                    chan8_raw = (ushort)CalculateChannelPWM(8, ChannelAxisMapping[8], ChannelReverseMapping[8], ChannelExpoMapping[8]),
                    chan9_raw = (ushort)CalculateChannelPWM(9, ChannelAxisMapping[9], ChannelReverseMapping[9], ChannelExpoMapping[9]),
                    chan10_raw = (ushort)CalculateChannelPWM(10, ChannelAxisMapping[10], ChannelReverseMapping[10], ChannelExpoMapping[10]),
                    chan11_raw = (ushort)CalculateChannelPWM(11, ChannelAxisMapping[11], ChannelReverseMapping[11], ChannelExpoMapping[11]),
                    chan12_raw = (ushort)CalculateChannelPWM(12, ChannelAxisMapping[12], ChannelReverseMapping[12], ChannelExpoMapping[12]),
                    chan13_raw = (ushort)CalculateChannelPWM(13, ChannelAxisMapping[13], ChannelReverseMapping[13], ChannelExpoMapping[13]),
                    chan14_raw = (ushort)CalculateChannelPWM(14, ChannelAxisMapping[14], ChannelReverseMapping[14], ChannelExpoMapping[14]),
                    chan15_raw = (ushort)CalculateChannelPWM(15, ChannelAxisMapping[15], ChannelReverseMapping[15], ChannelExpoMapping[15]),
                    chan16_raw = (ushort)CalculateChannelPWM(16, ChannelAxisMapping[16], ChannelReverseMapping[16], ChannelExpoMapping[16]),
                    chan17_raw = (ushort)CalculateChannelPWM(17, ChannelAxisMapping[17], ChannelReverseMapping[17], ChannelExpoMapping[17]),
                    chan18_raw = (ushort)CalculateChannelPWM(18, ChannelAxisMapping[18], ChannelReverseMapping[18], ChannelExpoMapping[18])
                };
                MainV2.comPort.sendPacket(rcOverride, 1, 1);
                MAVLinkInterface.GlobalRcOverrideSentCount++;
            }
            catch { }
        }

        private void SendRCOverrideV2Packet()
        {
            // 🚀 将来の RC_CHANNELS_OVERRIDE-V2 実装用フック
        }

        public void UpdateRCOverrideTimer()
        {
            // 🕹️ 60Hz メインループ完全同期駆動のため、別スレッドタイマーは停止・破棄
            lock (_rcOverrideLock)
            {
                if (_rcOverrideTimer != null)
                {
                    _rcOverrideTimer.Dispose();
                    _rcOverrideTimer = null;
                }
            }
        }

        public static int GetSendIntervalMsFromIndex(int index)
        {
            switch (index)
            {
                case 0: return 16;  // 60 Hz (Direct 16.6ms)
                case 1: return 20;  // 50 Hz
                case 2: return 30;  // 33 Hz
                case 3: return 40;  // 25 Hz
                case 4: return 50;  // 20 Hz
                case 5: return 100; // 10 Hz
                default: return 16;
            }
        }

        private void OnJoystickSendRateChanged(object sender, EventArgs e)
        {
            try
            {
                if (Picker_SendRate == null || Picker_SendRate.SelectedIndex < 0) return;
                int interval = GetSendIntervalMsFromIndex(Picker_SendRate.SelectedIndex);
                JoystickSendIntervalMs = interval;
                global::Xamarin.Essentials.Preferences.Set("MP_Joy_SendRateIdx", Picker_SendRate.SelectedIndex);
                UpdateRCOverrideTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnJoystickSendRateChanged error: " + ex);
            }
        }

        private void OnJoystickEnableCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            try
            {
                IsJoystickActive = e.Value;
                global::Xamarin.Essentials.Preferences.Set("MP_Joy_Enabled", IsJoystickActive);
                UpdateRCOverrideTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnJoystickEnableCheckedChanged error: " + ex);
            }
        }

        public async void OnJoystickSaveClicked(object sender, EventArgs e)
        {
            try
            {
                SaveJoystickSettings();
                await DisplayAlert("Joystick Settings", "Settings saved successfully!\nPreferences will be preserved on next launch.", "OK");
                if (Pnl_JoystickModal != null) Pnl_JoystickModal.IsVisible = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnJoystickSaveClicked error: " + ex);
            }
        }

        // 🔘 軸・キーの手動選択 (同じキーの重複割り当ても完全許可！)
        public async void OnSelectAxisClicked(object sender, EventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn == null) return;

                int ch = 0;
                if (btn.CommandParameter != null) int.TryParse(btn.CommandParameter.ToString(), out ch);

                string result = await DisplayActionSheet($"Select Axis/Key for RC{ch}", "Cancel", null,
                    "X (Roll / Stick Horiz)",
                    "Y (Pitch / Stick Vert)",
                    "Z (Throttle / Stick Vert)",
                    "Rz (Yaw / Stick Horiz)",
                    "Rx (Right Stick Horiz)",
                    "Ry (Right Stick Vert)",
                    "Slider1 (L2 / Left Trigger)",
                    "Slider2 (R2 / Right Trigger)",
                    "Btn A (Cross Button)",
                    "Btn B (Circle Button)",
                    "Btn X (Square Button)",
                    "Btn Y (Triangle Button)",
                    "Btn L1 (Left Bumper)",
                    "Btn R1 (Right Bumper)",
                    "Btn L3 (Left Stick Click)",
                    "Btn R3 (Right Stick Click)",
                    "Dpad Up",
                    "Dpad Down",
                    "Dpad Left",
                    "Dpad Right",
                    "None");

                if (!string.IsNullOrEmpty(result) && result != "キャンセル")
                {
                    string cleanName = result.Split('(')[0].Trim();
                    btn.Text = cleanName + " ▾";
                    btn.TextColor = global::Xamarin.Forms.Color.FromHex("#10B981");

                    // 🎯 該当チャンネルのデータ配列のみを更新 (他チャンネルはそのまま保持)
                    if (ch >= 1 && ch <= 18)
                    {
                        ChannelAxisMapping[ch] = cleanName;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnSelectAxisClicked error: " + ex);
            }
        }

        public static Button ActiveDetectingButton = null;

        // 🎯 軸・キーの自動検出 (同じキー・軸の複数チャンネル割り当ても完全許可！)
        public async void OnJoystickDetectClicked(object sender, EventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn == null) return;

                int ch = 0;
                if (btn.CommandParameter != null) int.TryParse(btn.CommandParameter.ToString(), out ch);

                ActiveDetectingButton = btn;
                btn.Text = "MOVE...";
                btn.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#EAB308");

                float baseRawX = LastRawAxisX;
                float baseRawY = LastRawAxisY;
                float baseRawZ = LastRawAxisZ;
                float baseRawRz = LastRawAxisRz;
                float baseRawRx = LastRawAxisRx;
                float baseRawRy = LastRawAxisRy;
                float baseRawThr = LastRawThrottle;
                float baseRawRud = LastRawRudder;
                LastPressedButtonCode = 0;

                int detectTicks = 0;
                float maxDelta = 0.25f;

                Forms.Device.StartTimer(global::System.TimeSpan.FromMilliseconds(25), () =>
                {
                    if (ActiveDetectingButton != btn) return false;

                    float dX = Math.Abs(LastRawAxisX - baseRawX);
                    float dY = Math.Abs(LastRawAxisY - baseRawY);
                    float dZ = Math.Abs(LastRawAxisZ - baseRawZ);
                    float dRz = Math.Abs(LastRawAxisRz - baseRawRz);
                    float dRx = Math.Abs(LastRawAxisRx - baseRawRx);
                    float dRy = Math.Abs(LastRawAxisRy - baseRawRy);
                    float dThr = Math.Abs(LastRawThrottle - baseRawThr);
                    float dRud = Math.Abs(LastRawRudder - baseRawRud);

                    string detected = null;

                    // 1. トリガー・スライダー判定 (Slider1 / Slider2)
                    if (dThr > maxDelta || Math.Abs(LastRawBrake) > 0.2f) { detected = "Slider1"; }
                    else if (dRud > maxDelta || Math.Abs(LastRawGas) > 0.2f) { detected = "Slider2"; }
                    // 2. スティック軸判定 (X, Y, Z, Rz)
                    else if (dX > maxDelta && dX >= dY && dX >= dZ && dX >= dRz) { detected = "X"; }
                    else if (dY > maxDelta && dY >= dX && dY >= dZ && dY >= dRz) { detected = "Y"; }
                    else if (dZ > maxDelta && dZ >= dX && dZ >= dY && dZ >= dRz) { detected = "Z"; }
                    else if (dRz > maxDelta && dRz >= dX && dRz >= dY && dRz >= dZ) { detected = "Rz"; }
                    else if (dRx > maxDelta) { detected = "Rx"; }
                    else if (dRy > maxDelta) { detected = "Ry"; }

                    // 3. ボタン押下判定 (○, ×, △, □, L1, R1, L3, R3, Dpad)
                    if (string.IsNullOrEmpty(detected) && LastPressedButtonCode != 0)
                    {
                        detected = ConvertKeyCodeToName(LastPressedButtonCode);
                    }

                    if (!string.IsNullOrEmpty(detected))
                    {
                        ActiveDetectingButton = null;
                        btn.Text = "DONE!";
                        btn.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#10B981");

                        // 🎯 1. 処理用データ配列を即座に更新 (他チャンネルはそのまま保持)
                        if (ch >= 1 && ch <= 18)
                        {
                            ChannelAxisMapping[ch] = detected;
                        }

                        // 🎯 2. 画面上のボタン表示を更新
                        try
                        {
                            var axisBtn = this.FindByName<Button>($"Btn_RCAxis_{ch}");
                            if (axisBtn != null)
                            {
                                axisBtn.Text = detected + " ▾";
                                axisBtn.TextColor = global::Xamarin.Forms.Color.FromHex("#10B981");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("FindByName error: " + ex);
                        }

                        Forms.Device.StartTimer(global::System.TimeSpan.FromSeconds(1.5), () =>
                        {
                            if (btn != null)
                            {
                                btn.Text = "DETECT";
                                btn.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
                            }
                            return false;
                        });

                        return false;
                    }

                    detectTicks++;
                    if (detectTicks > 160) // 4秒タイムアウト
                    {
                        ActiveDetectingButton = null;
                        btn.Text = "DETECT";
                        btn.BackgroundColor = global::Xamarin.Forms.Color.FromHex("#1E293B");
                        return false;
                    }

                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnJoystickDetectClicked error: " + ex);
            }
        }

        #region ================= ARDUPILOT SETUP & CALIBRATION SUITE =================

        private int _accelSub1 = -1;
        private int _accelSub2 = -1;
        private MAVLink.ACCELCAL_VEHICLE_POS _currentAccelPos = MAVLink.ACCELCAL_VEHICLE_POS.LEVEL;
        private bool _isAccelCalibrating = false;

        private int _compassSub1 = -1;
        private int _compassSub2 = -1;
        private bool _isCompassCalibrating = false;
        private int _compassPointsSampled = 0;

        #region --- 3D COMPASS SPHERE POINT CLOUD ENGINE ---
        private struct SpherePoint { public float X, Y, Z; }
        private static readonly SpherePoint[] _magCalSpherePoints = GenerateFibonacciSpherePoints(80);
        private readonly bool[] _compassMaskBits = new bool[80];
        private float _compassDirX = 0f, _compassDirY = 0f, _compassDirZ = 1f;
        private float _sphereRotYaw = 25f, _sphereRotPitch = -15f;
        private double _panLastX = 0, _panLastY = 0;

        private static SpherePoint[] GenerateFibonacciSpherePoints(int count)
        {
            var pts = new SpherePoint[count];
            float phi = (1f + (float)Math.Sqrt(5)) / 2f;
            float goldenAngle = (2f - phi) * 2f * (float)Math.PI;

            for (int i = 0; i < count; i++)
            {
                float y = 1f - (i / (float)(count - 1)) * 2f;
                float radius = (float)Math.Sqrt(Math.Max(0, 1f - y * y));
                float theta = goldenAngle * i;

                float x = (float)Math.Cos(theta) * radius;
                float z = (float)Math.Sin(theta) * radius;
                pts[i] = new SpherePoint { X = x, Y = y, Z = z };
            }
            return pts;
        }

        private void OnCompass3DPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            try
            {
                if (e.StatusType == GestureStatus.Running)
                {
                    float dx = (float)(e.TotalX - _panLastX);
                    float dy = (float)(e.TotalY - _panLastY);
                    _sphereRotYaw += dx * 0.6f;
                    _sphereRotPitch -= dy * 0.6f;
                    _panLastX = e.TotalX;
                    _panLastY = e.TotalY;

                    _sphereRotPitch = Math.Max(-85f, Math.Min(85f, _sphereRotPitch));
                    Canvas_Compass3D?.InvalidateSurface();
                }
                else if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Canceled)
                {
                    _panLastX = 0;
                    _panLastY = 0;
                }
            }
            catch { }
        }

        private void OnCompass3DPaintSurface(object sender, SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            float cx = info.Width / 2f;
            float cy = info.Height / 2f;
            float R = Math.Min(info.Width, info.Height) * 0.40f;

            if (R <= 10) return;

            // 1. 球体背景（ダークネイビー）と外周リング
            using (var bgPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#0B132B"), Style = SkiaSharp.SKPaintStyle.Fill, IsAntialias = true })
            {
                canvas.DrawCircle(cx, cy, R, bgPaint);
            }

            using (var ringPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#0284C7"), Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
            {
                canvas.DrawCircle(cx, cy, R, ringPaint);
            }

            // 2. 緯度・経度ワイヤーフレーム（点線/半透明）
            using (var wirePaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#1E3A8A").WithAlpha(120), Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            {
                canvas.DrawOval(cx, cy, R, R * 0.4f, wirePaint); // 赤道
                canvas.DrawOval(cx, cy, R * 0.4f, R, wirePaint); // 本初子午線
            }

            // 3. 3D 回転変換マトリクス準備
            float radYaw = _sphereRotYaw * (float)Math.PI / 180f;
            float radPitch = _sphereRotPitch * (float)Math.PI / 180f;
            float cosY = (float)Math.Cos(radYaw), sinY = (float)Math.Sin(radYaw);
            float cosP = (float)Math.Cos(radPitch), sinP = (float)Math.Sin(radPitch);

            // 80ポイントの投影計算
            var projected = new List<(float sx, float sy, float depth, bool sampled)>(80);
            for (int i = 0; i < _magCalSpherePoints.Length; i++)
            {
                var pt = _magCalSpherePoints[i];
                // Yaw 回転 (around Y)
                float x1 = pt.X * cosY + pt.Z * sinY;
                float y1 = pt.Y;
                float z1 = -pt.X * sinY + pt.Z * cosY;

                // Pitch 回転 (around X)
                float x2 = x1;
                float y2 = y1 * cosP - z1 * sinP;
                float z2 = y1 * sinP + z1 * cosP;

                float sx = cx + x2 * R;
                float sy = cy - y2 * R;
                bool isSampled = (i < _compassMaskBits.Length) && _compassMaskBits[i];
                projected.Add((sx, sy, z2, isSampled));
            }

            // 深度ソート（奥 z2 < 0 から手前 z2 >= 0 へ）
            projected.Sort((a, b) => a.depth.CompareTo(b.depth));

            // 描画用ペイント
            using (var pSampledFront = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#10B981"), Style = SkiaSharp.SKPaintStyle.Fill, IsAntialias = true })
            using (var pSampledBack = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#064E3B").WithAlpha(140), Style = SkiaSharp.SKPaintStyle.Fill, IsAntialias = true })
            using (var pMissingFront = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#EF4444"), Style = SkiaSharp.SKPaintStyle.Fill, IsAntialias = true })
            using (var pMissingBack = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#475569").WithAlpha(90), Style = SkiaSharp.SKPaintStyle.Fill, IsAntialias = true })
            using (var pGlow = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#10B981").WithAlpha(60), Style = SkiaSharp.SKPaintStyle.Fill, IsAntialias = true })
            {
                foreach (var p in projected)
                {
                    bool isFront = p.depth >= 0;
                    if (p.sampled)
                    {
                        if (isFront)
                        {
                            canvas.DrawCircle(p.sx, p.sy, 7f, pGlow);
                            canvas.DrawCircle(p.sx, p.sy, 4.5f, pSampledFront);
                        }
                        else
                        {
                            canvas.DrawCircle(p.sx, p.sy, 3f, pSampledBack);
                        }
                    }
                    else
                    {
                        if (isFront)
                        {
                            canvas.DrawCircle(p.sx, p.sy, 4.2f, pMissingFront);
                        }
                        else
                        {
                            canvas.DrawCircle(p.sx, p.sy, 2.5f, pMissingBack);
                        }
                    }
                }
            }

            // 4. 現在の機体磁気ベクトル（方向ターゲットカーソル）
            float dirLen = (float)Math.Sqrt(_compassDirX * _compassDirX + _compassDirY * _compassDirY + _compassDirZ * _compassDirZ);
            if (dirLen > 0.001f)
            {
                float dx = _compassDirX / dirLen;
                float dy = _compassDirY / dirLen;
                float dz = _compassDirZ / dirLen;

                // 回転変換
                float dx1 = dx * cosY + dz * sinY;
                float dy1 = dy;
                float dz1 = -dx * sinY + dz * cosY;

                float dx2 = dx1;
                float dy2 = dy1 * cosP - dz1 * sinP;
                float dz2 = dy1 * sinP + dz1 * cosP;

                float targetX = cx + dx2 * R;
                float targetY = cy - dy2 * R;

                if (dz2 >= -0.2f)
                {
                    using (var targetPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#F59E0B"), Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true })
                    using (var crossPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#FBBF24"), Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
                    {
                        canvas.DrawCircle(targetX, targetY, 8f, targetPaint);
                        canvas.DrawLine(targetX - 12f, targetY, targetX + 12f, targetY, crossPaint);
                        canvas.DrawLine(targetX, targetY - 12f, targetX, targetY + 12f, crossPaint);
                    }
                }
            }
        }
        #endregion

        private bool _isRadioCalibrating = false;
        private readonly int[] _radioMin = new int[18] { 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000 };
        private readonly int[] _radioMax = new int[18] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private readonly int[] _radioTrim = new int[18] { 1500, 1500, 1000, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500 };

        private float GetRCChannelInputValue(CurrentState cs, int ch)
        {
            switch (ch)
            {
                case 1: return cs.ch1in;
                case 2: return cs.ch2in;
                case 3: return cs.ch3in;
                case 4: return cs.ch4in;
                case 5: return cs.ch5in;
                case 6: return cs.ch6in;
                case 7: return cs.ch7in;
                case 8: return cs.ch8in;
                case 9: return cs.ch9in;
                case 10: return cs.ch10in;
                case 11: return cs.ch11in;
                case 12: return cs.ch12in;
                case 13: return cs.ch13in;
                case 14: return cs.ch14in;
                case 15: return cs.ch15in;
                case 16: return cs.ch16in;
                case 17: return (cs.rcoverridech17 > 0) ? (float)cs.rcoverridech17 : (IsJoystickActive ? (float)CalculateChannelPWM(ChannelAxisMapping.Length > 17 ? ChannelAxisMapping[17] : "None", 1500, ChannelReverseMapping.Length > 17 ? ChannelReverseMapping[17] : false) : 1500f);
                case 18: return (cs.rcoverridech18 > 0) ? (float)cs.rcoverridech18 : (IsJoystickActive ? (float)CalculateChannelPWM(ChannelAxisMapping.Length > 18 ? ChannelAxisMapping[18] : "None", 1500, ChannelReverseMapping.Length > 18 ? ChannelReverseMapping[18] : false) : 1500f);
                default: return 1500f;
            }
        }

        private void OnOpenSetupModalClicked(object sender, EventArgs e)
        {
            try
            {
                Pnl_SetupModal.IsVisible = true;
                SwitchSetupTab("accel");
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnOpenSetupModalClicked error: " + ex);
            }
        }

        private void OnCloseSetupModalClicked(object sender, EventArgs e)
        {
            try
            {
                Pnl_SetupModal.IsVisible = false;
                CleanupCalibrationSubscriptions();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnCloseSetupModalClicked error: " + ex);
            }
        }

        private void CleanupCalibrationSubscriptions()
        {
            try
            {
                if (_accelSub1 != -1) { MainV2.comPort.UnSubscribeToPacketType(_accelSub1); _accelSub1 = -1; }
                if (_accelSub2 != -1) { MainV2.comPort.UnSubscribeToPacketType(_accelSub2); _accelSub2 = -1; }
                if (_compassSub1 != -1) { MainV2.comPort.UnSubscribeToPacketType(_compassSub1); _compassSub1 = -1; }
                if (_compassSub2 != -1) { MainV2.comPort.UnSubscribeToPacketType(_compassSub2); _compassSub2 = -1; }
                _isAccelCalibrating = false;
                _isCompassCalibrating = false;
                _isRadioCalibrating = false;
            }
            catch { }
        }

        private void SwitchSetupTab(string tab)
        {
            try
            {
                View_Setup_Accel.IsVisible = (tab == "accel");
                View_Setup_Compass.IsVisible = (tab == "compass");
                View_Setup_Gyro.IsVisible = (tab == "gyro");
                View_Setup_Radio.IsVisible = (tab == "radio");

                Btn_SetupTab_Accel.BackgroundColor = (tab == "accel") ? global::Xamarin.Forms.Color.FromHex("#0284C7") : global::Xamarin.Forms.Color.FromHex("#1E293B");
                Btn_SetupTab_Accel.TextColor = (tab == "accel") ? global::Xamarin.Forms.Color.White : global::Xamarin.Forms.Color.FromHex("#94A3B8");

                Btn_SetupTab_Compass.BackgroundColor = (tab == "compass") ? global::Xamarin.Forms.Color.FromHex("#0284C7") : global::Xamarin.Forms.Color.FromHex("#1E293B");
                Btn_SetupTab_Compass.TextColor = (tab == "compass") ? global::Xamarin.Forms.Color.White : global::Xamarin.Forms.Color.FromHex("#94A3B8");

                Btn_SetupTab_Gyro.BackgroundColor = (tab == "gyro") ? global::Xamarin.Forms.Color.FromHex("#0284C7") : global::Xamarin.Forms.Color.FromHex("#1E293B");
                Btn_SetupTab_Gyro.TextColor = (tab == "gyro") ? global::Xamarin.Forms.Color.White : global::Xamarin.Forms.Color.FromHex("#94A3B8");

                Btn_SetupTab_Radio.BackgroundColor = (tab == "radio") ? global::Xamarin.Forms.Color.FromHex("#0284C7") : global::Xamarin.Forms.Color.FromHex("#1E293B");
                Btn_SetupTab_Radio.TextColor = (tab == "radio") ? global::Xamarin.Forms.Color.White : global::Xamarin.Forms.Color.FromHex("#94A3B8");

                if (tab == "accel")
                {
                    UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.LEVEL);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SwitchSetupTab error: " + ex);
            }
        }

        private void OnSetupTabAccelClicked(object sender, EventArgs e) => SwitchSetupTab("accel");
        private void OnSetupTabCompassClicked(object sender, EventArgs e) => SwitchSetupTab("compass");
        private void OnSetupTabGyroClicked(object sender, EventArgs e) => SwitchSetupTab("gyro");
        private void OnSetupTabRadioClicked(object sender, EventArgs e) => SwitchSetupTab("radio");

        #region --- 1. ACCELEROMETER CALIBRATION ---

        private void UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS pos)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _currentAccelPos = pos;
                    byte[] imgBytes = MissionPlanner.Properties.ResourcesX.calibration01;

                    // 6方向インジケーターカードのハイライト更新
                    Frame_Pos_Level.BorderColor = (pos == MAVLink.ACCELCAL_VEHICLE_POS.LEVEL) ? global::Xamarin.Forms.Color.FromHex("#F59E0B") : global::Xamarin.Forms.Color.FromHex("#334155");
                    Frame_Pos_Left.BorderColor = (pos == MAVLink.ACCELCAL_VEHICLE_POS.LEFT) ? global::Xamarin.Forms.Color.FromHex("#F59E0B") : global::Xamarin.Forms.Color.FromHex("#334155");
                    Frame_Pos_Right.BorderColor = (pos == MAVLink.ACCELCAL_VEHICLE_POS.RIGHT) ? global::Xamarin.Forms.Color.FromHex("#F59E0B") : global::Xamarin.Forms.Color.FromHex("#334155");
                    Frame_Pos_NoseDown.BorderColor = (pos == MAVLink.ACCELCAL_VEHICLE_POS.NOSEDOWN) ? global::Xamarin.Forms.Color.FromHex("#F59E0B") : global::Xamarin.Forms.Color.FromHex("#334155");
                    Frame_Pos_NoseUp.BorderColor = (pos == MAVLink.ACCELCAL_VEHICLE_POS.NOSEUP) ? global::Xamarin.Forms.Color.FromHex("#F59E0B") : global::Xamarin.Forms.Color.FromHex("#334155");
                    Frame_Pos_Back.BorderColor = (pos == MAVLink.ACCELCAL_VEHICLE_POS.BACK) ? global::Xamarin.Forms.Color.FromHex("#F59E0B") : global::Xamarin.Forms.Color.FromHex("#334155");

                    switch (pos)
                    {
                        case MAVLink.ACCELCAL_VEHICLE_POS.LEVEL:
                            imgBytes = MissionPlanner.Properties.ResourcesX.calibration01;
                            LBL_accel_title.Text = "[1/6] Hold vehicle LEVEL and still";
                            LBL_accel_desc.Text = "Place vehicle level and stationary, then press [Position Done] below.";
                            break;
                        case MAVLink.ACCELCAL_VEHICLE_POS.LEFT:
                            imgBytes = MissionPlanner.Properties.ResourcesX.calibration07;
                            LBL_accel_title.Text = "[2/6] Place vehicle on its LEFT side and hold still";
                            LBL_accel_desc.Text = "Roll vehicle 90° to the left, then press [Position Done] below.";
                            break;
                        case MAVLink.ACCELCAL_VEHICLE_POS.RIGHT:
                            imgBytes = MissionPlanner.Properties.ResourcesX.calibration05;
                            LBL_accel_title.Text = "[3/6] Place vehicle on its RIGHT side and hold still";
                            LBL_accel_desc.Text = "Roll vehicle 90° to the right, then press [Position Done] below.";
                            break;
                        case MAVLink.ACCELCAL_VEHICLE_POS.NOSEDOWN:
                            imgBytes = MissionPlanner.Properties.ResourcesX.calibration04;
                            LBL_accel_title.Text = "[4/6] Place vehicle NOSE DOWN and hold still";
                            LBL_accel_desc.Text = "Pitch vehicle 90° nose down, then press [Position Done] below.";
                            break;
                        case MAVLink.ACCELCAL_VEHICLE_POS.NOSEUP:
                            imgBytes = MissionPlanner.Properties.ResourcesX.calibration06;
                            LBL_accel_title.Text = "[5/6] Place vehicle NOSE UP and hold still";
                            LBL_accel_desc.Text = "Pitch vehicle 90° nose up, then press [Position Done] below.";
                            break;
                        case MAVLink.ACCELCAL_VEHICLE_POS.BACK:
                            imgBytes = MissionPlanner.Properties.ResourcesX.calibration03;
                            LBL_accel_title.Text = "[6/6] Place vehicle on its BACK (Inverted) and hold still";
                            LBL_accel_desc.Text = "Turn vehicle completely upside down, then press [Position Done] below.";
                            break;
                    }

                    if (imgBytes != null && imgBytes.Length > 0)
                    {
                        IMG_accel_guide.Source = ImageSource.FromStream(() => new System.IO.MemoryStream(imgBytes));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("UpdateAccelOrientationUI error: " + ex);
                }
            });
        }

        private void OnAccelCalStartClicked(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.BaseStream.IsOpen)
                {
                    DisplayAlert("Not Connected", "Flight controller is not connected.", "OK");
                    return;
                }

                _isAccelCalibrating = true;
                Btn_AccelCal_Start.IsVisible = false;
                Btn_AccelCal_Next.IsVisible = true;
                Btn_AccelCal_Cancel.IsVisible = true;
                LBL_accel_status_msg.Text = "Calibration started... Follow orientation instructions";

                // PREFLIGHT_CALIBRATION (param5 = 1 : Accel Calib)
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 1, 0, 0);

                _accelSub1 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.STATUSTEXT, HandleAccelPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
                _accelSub2 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, HandleAccelPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);

                UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.LEVEL);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Failed to start calibration: " + ex.Message, "OK");
            }
        }

        private bool HandleAccelPacket(MAVLink.MAVLinkMessage arg)
        {
            try
            {
                if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.COMMAND_LONG)
                {
                    var msg = arg.ToStructure<MAVLink.mavlink_command_long_t>();
                    if (msg.command == (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS)
                    {
                        var pos = (MAVLink.ACCELCAL_VEHICLE_POS)(int)msg.param1;
                        UpdateAccelOrientationUI(pos);
                    }
                }
                else if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.STATUSTEXT)
                {
                    var stat = arg.ToStructure<MAVLink.mavlink_statustext_t>();
                    string text = System.Text.Encoding.ASCII.GetString(stat.text).Trim('\0', ' ', '\r', '\n');
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        LBL_accel_status_msg.Text = text;
                        string lower = text.ToLowerInvariant();
                        if (lower.Contains("place vehicle"))
                        {
                            if (lower.Contains("level")) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.LEVEL);
                            else if (lower.Contains("left")) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.LEFT);
                            else if (lower.Contains("right")) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.RIGHT);
                            else if (lower.Contains("nose down") || lower.Contains("down")) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.NOSEDOWN);
                            else if (lower.Contains("nose up") || lower.Contains("up")) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.NOSEUP);
                            else if (lower.Contains("back")) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.BACK);
                        }
                        else if (lower.Contains("calibration successful"))
                        {
                            DisplayAlert("Calibration Complete", "6-Axis accelerometer calibration completed successfully!", "OK");
                            OnAccelCalCancelClicked(null, null);
                        }
                        else if (lower.Contains("calibration failed"))
                        {
                            DisplayAlert("Calibration Failed", "Calibration failed. Please retry on an undisturbed surface.", "OK");
                            OnAccelCalCancelClicked(null, null);
                        }
                    });
                }
            }
            catch { }
            return true;
        }

        private void OnAccelCalNextClicked(object sender, EventArgs e)
        {
            try
            {
                // FCに現在の姿勢完了を送信
                MainV2.comPort.sendPacket(new MAVLink.mavlink_command_long_t
                {
                    command = (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS,
                    param1 = (float)_currentAccelPos,
                    target_system = (byte)MainV2.comPort.sysidcurrent,
                    target_component = (byte)MainV2.comPort.compidcurrent
                }, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);

                LBL_accel_status_msg.Text = "Position sent. Waiting for next step...";
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Failed to send position: " + ex.Message, "OK");
            }
        }

        private void OnAccelCalCancelClicked(object sender, EventArgs e)
        {
            _isAccelCalibrating = false;
            Btn_AccelCal_Start.IsVisible = true;
            Btn_AccelCal_Next.IsVisible = false;
            Btn_AccelCal_Cancel.IsVisible = false;
            LBL_accel_status_msg.Text = "Waiting";
            CleanupCalibrationSubscriptions();
        }

        private void OnPosCardClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender == Frame_Pos_Level) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.LEVEL);
                else if (sender == Frame_Pos_Left) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.LEFT);
                else if (sender == Frame_Pos_Right) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.RIGHT);
                else if (sender == Frame_Pos_NoseDown) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.NOSEDOWN);
                else if (sender == Frame_Pos_NoseUp) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.NOSEUP);
                else if (sender == Frame_Pos_Back) UpdateAccelOrientationUI(MAVLink.ACCELCAL_VEHICLE_POS.BACK);
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnPosCardClicked error: " + ex);
            }
        }

        private void OnAccelCalLevelOnlyClicked(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.BaseStream.IsOpen)
                {
                    DisplayAlert("Not Connected", "Flight controller is not connected.", "OK");
                    return;
                }

                // param5 = 2 : Level Only
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 2, 0, 0);

                DisplayAlert("Level Calibration", "Level trim calibration command sent. Keep vehicle level and still.", "OK");
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Level calibration failed: " + ex.Message, "OK");
            }
        }

        #endregion

        #region --- 2. COMPASS CALIBRATION ---

        private void OnCompassStartClicked(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.BaseStream.IsOpen)
                {
                    DisplayAlert("Not Connected", "Flight controller is not connected.", "OK");
                    return;
                }

                _isCompassCalibrating = true;
                _compassPointsSampled = 0;
                for (int i = 0; i < _compassMaskBits.Length; i++) _compassMaskBits[i] = false;
                _compassDirX = 0; _compassDirY = 0; _compassDirZ = 1;
                Canvas_Compass3D?.InvalidateSurface();

                Btn_Compass_Start.IsVisible = false;
                Btn_Compass_Accept.IsVisible = true;
                Btn_Compass_Cancel.IsVisible = true;
                Frm_compass_quality.IsVisible = false;
                LBL_compass_guide_msg.Text = "Rotate vehicle slowly in all directions (Roll, Pitch, Yaw). Aim at RED dots!";
                LBL_compass_guide_msg.TextColor = global::Xamarin.Forms.Color.FromHex("#F59E0B");
                LBL_compass_result_text.Text = "Sampling 3D points...";
                LBL_compass_points.Text = "0 / 80 pts (0%)";

                PB_compass1.Progress = 0;
                PB_compass2.Progress = 0;
                PB_compass3.Progress = 0;
                LBL_compass1_pct.Text = "0%";
                LBL_compass2_pct.Text = "0%";
                LBL_compass3_pct.Text = "0%";

                // DO_START_MAG_CAL
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.DO_START_MAG_CAL, 0, 1, 1, 0, 0, 0, 0);

                _compassSub1 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS, HandleCompassPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
                _compassSub2 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT, HandleCompassPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Failed to start mag calibration: " + ex.Message, "OK");
            }
        }

        private bool HandleCompassPacket(MAVLink.MAVLinkMessage arg)
        {
            try
            {
                if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS)
                {
                    var prog = arg.ToStructure<MAVLink.mavlink_mag_cal_progress_t>();
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        float p = prog.completion_pct / 100f;
                        if (prog.compass_id == 0)
                        {
                            PB_compass1.Progress = p;
                            LBL_compass1_pct.Text = prog.completion_pct + "%";
                        }
                        else if (prog.compass_id == 1)
                        {
                            PB_compass2.Progress = p;
                            LBL_compass2_pct.Text = prog.completion_pct + "%";
                        }
                        else if (prog.compass_id == 2)
                        {
                            PB_compass3.Progress = p;
                            LBL_compass3_pct.Text = prog.completion_pct + "%";
                        }

                        if (prog.completion_mask != null)
                        {
                            for (int b = 0; b < prog.completion_mask.Length && b < 10; b++)
                            {
                                byte byteVal = prog.completion_mask[b];
                                for (int bit = 0; bit < 8; bit++)
                                {
                                    int idx = b * 8 + bit;
                                    if (idx < _compassMaskBits.Length)
                                    {
                                        _compassMaskBits[idx] = ((byteVal & (1 << bit)) != 0);
                                    }
                                }
                            }
                        }
                        _compassDirX = prog.direction_x;
                        _compassDirY = prog.direction_y;
                        _compassDirZ = prog.direction_z;

                        int sampledCount = _compassMaskBits.Count(x => x);
                        _compassPointsSampled = sampledCount;
                        LBL_compass_points.Text = string.Format("{0} / 80 pts ({1}%)", sampledCount, sampledCount * 100 / 80);

                        Canvas_Compass3D?.InvalidateSurface();

                        if (prog.completion_pct >= 100 || sampledCount >= 78)
                        {
                            LBL_compass_result_text.Text = "Sampling complete! Press [Accept & Save] to apply.";
                        }
                    });
                }
                else if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT)
                {
                    var rep = arg.ToStructure<MAVLink.mavlink_mag_cal_report_t>();
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        var status = (MAVLink.MAG_CAL_STATUS)rep.cal_status;
                        
                        // 1. Sphere Quality & Fitness Rating Badge
                        Frm_compass_quality.IsVisible = true;
                        if (rep.fitness < 16.0f)
                        {
                            LBL_compass_quality.Text = string.Format("🌟 Sphere Quality: Excellent (Fit: {0:F2})", rep.fitness);
                            LBL_compass_quality.TextColor = global::Xamarin.Forms.Color.FromHex("#10B981");
                            Frm_compass_quality.BorderColor = global::Xamarin.Forms.Color.FromHex("#10B981");
                        }
                        else if (rep.fitness < 25.0f)
                        {
                            LBL_compass_quality.Text = string.Format("🟢 Sphere Quality: Good (Fit: {0:F2})", rep.fitness);
                            LBL_compass_quality.TextColor = global::Xamarin.Forms.Color.FromHex("#38BDF8");
                            Frm_compass_quality.BorderColor = global::Xamarin.Forms.Color.FromHex("#38BDF8");
                        }
                        else if (rep.fitness < 35.0f)
                        {
                            LBL_compass_quality.Text = string.Format("🟡 Sphere Quality: Acceptable (Fit: {0:F2})", rep.fitness);
                            LBL_compass_quality.TextColor = global::Xamarin.Forms.Color.FromHex("#F59E0B");
                            Frm_compass_quality.BorderColor = global::Xamarin.Forms.Color.FromHex("#F59E0B");
                        }
                        else
                        {
                            LBL_compass_quality.Text = string.Format("🔴 Sphere Quality: High Noise (Fit: {0:F2})", rep.fitness);
                            LBL_compass_quality.TextColor = global::Xamarin.Forms.Color.FromHex("#EF4444");
                            Frm_compass_quality.BorderColor = global::Xamarin.Forms.Color.FromHex("#EF4444");
                        }

                        // 2. Offsets Details
                        LBL_compass_result_text.Text = string.Format("Compass #{0} Offsets: X={1:F1}, Y={2:F1}, Z={3:F1} ({4})",
                            rep.compass_id + 1, rep.ofs_x, rep.ofs_y, rep.ofs_z, status);

                        if (status == MAVLink.MAG_CAL_STATUS.MAG_CAL_SUCCESS)
                        {
                            // 完了時は 100% / 80pts を表示
                            if (rep.compass_id == 0) { PB_compass1.Progress = 1.0; LBL_compass1_pct.Text = "100%"; }
                            else if (rep.compass_id == 1) { PB_compass2.Progress = 1.0; LBL_compass2_pct.Text = "100%"; }
                            else if (rep.compass_id == 2) { PB_compass3.Progress = 1.0; LBL_compass3_pct.Text = "100%"; }

                            for (int i = 0; i < _compassMaskBits.Length; i++) _compassMaskBits[i] = true;
                            _compassPointsSampled = 80;
                            LBL_compass_points.Text = "80 / 80 pts (100%)";
                            Canvas_Compass3D?.InvalidateSurface();
                            LBL_compass_guide_msg.Text = "🎉 Calibration Successful! Please press [Accept & Save] below.";
                            LBL_compass_guide_msg.TextColor = global::Xamarin.Forms.Color.FromHex("#10B981");

                            Btn_Compass_Start.IsVisible = false;
                            Btn_Compass_Accept.IsVisible = true;
                            Btn_Compass_Cancel.IsVisible = true;
                        }
                    });
                }
            }
            catch { }
            return true;
        }

        private void OnCompassAcceptClicked(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL, 0, 0, 1, 0, 0, 0, 0);

                DisplayAlert("Compass Complete", "Compass calibration results saved to flight controller!", "OK");
                OnCompassCancelClicked(null, null);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Save failed: " + ex.Message, "OK");
            }
        }

        private void OnCompassCancelClicked(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL, 0, 0, 0, 0, 0, 0, 0);
            }
            catch { }

            _isCompassCalibrating = false;
            Btn_Compass_Start.IsVisible = true;
            Btn_Compass_Accept.IsVisible = false;
            Btn_Compass_Cancel.IsVisible = false;
            CleanupCalibrationSubscriptions();
        }

        #endregion

        #region --- 3. GYRO & HORIZON LEVEL CALIBRATION ---

        private void OnGyroCalibrateClicked(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.BaseStream.IsOpen)
                {
                    DisplayAlert("Not Connected", "Flight controller is not connected.", "OK");
                    return;
                }

                // param1 = 1 : Gyro
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 1, 0, 0, 0, 0, 0, 0);

                LBL_gyro_status_msg.Text = "Gyro calibration sent. Keep vehicle stationary.";
                DisplayAlert("Gyro Calibration", "Gyro bias calibration initiated. Keep vehicle still for a few seconds.", "OK");
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Gyro calibration failed: " + ex.Message, "OK");
            }
        }

        private void OnLevelCalibrateClicked(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.BaseStream.IsOpen)
                {
                    DisplayAlert("Not Connected", "Flight controller is not connected.", "OK");
                    return;
                }

                // param5 = 2 : Level
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 2, 0, 0);

                LBL_gyro_status_msg.Text = "Level trim calibration sent.";
                DisplayAlert("Level Trim Calibration", "Level trim calibration command sent. Please keep the vehicle level and stationary.", "OK");
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Level calibration failed: " + ex.Message, "OK");
            }
        }

        #endregion

        #region --- 4. RC RADIO CALIBRATION ---

        private void OnRadioCalStartClicked(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.BaseStream.IsOpen)
                {
                    DisplayAlert("Not Connected", "Flight controller is not connected.", "OK");
                    return;
                }

                _isRadioCalibrating = true;
                try { MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, 10, 1, 1); } catch { }
                for (int i = 0; i < 18; i++)
                {
                    _radioMin[i] = 3000;
                    _radioMax[i] = 0;
                }

                Btn_RadioCal_Start.IsVisible = false;
                Btn_RadioCal_Save.IsVisible = true;
                Btn_RadioCal_Cancel.IsVisible = true;

                DisplayAlert("RC Calibration (CH1-18)", "Move all transmitter sticks and switches across their full ranges.\nWhen finished, return sticks to center and press [Save to FC].", "OK");
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Failed to start RC calibration: " + ex.Message, "OK");
            }
        }

        private void OnRadioCalSaveClicked(object sender, EventArgs e)
        {
            try
            {
                var cs = MainV2.comPort.MAV.cs;

                for (int ch = 1; ch <= 18; ch++)
                {
                    int i = ch - 1;
                    int cur = (int)GetRCChannelInputValue(cs, ch);
                    _radioTrim[i] = (cur > 800 && cur < 2200) ? cur : 1500;
                    if (_radioMin[i] < 800 || _radioMin[i] > 2200) _radioMin[i] = 1000;
                    if (_radioMax[i] < 800 || _radioMax[i] > 2200) _radioMax[i] = 2000;

                    // ArduPilot パラメータへ書き込み
                    MainV2.comPort.setParam("RC" + ch + "_MIN", _radioMin[i]);
                    MainV2.comPort.setParam("RC" + ch + "_MAX", _radioMax[i]);
                    MainV2.comPort.setParam("RC" + ch + "_TRIM", _radioTrim[i]);
                }

                DisplayAlert("Save Complete", "RC1-18 calibration parameters (Min/Max/Trim) saved to flight controller successfully!", "OK");
                OnRadioCalCancelClicked(null, null);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", "Failed to save parameters: " + ex.Message, "OK");
            }
        }

        private void OnRadioCalCancelClicked(object sender, EventArgs e)
        {
            _isRadioCalibrating = false;
            Btn_RadioCal_Start.IsVisible = true;
            Btn_RadioCal_Save.IsVisible = false;
            Btn_RadioCal_Cancel.IsVisible = false;
        }

        #endregion

        #endregion
    }

    internal class InputBox
    {
        public static async Task<string> Show(string mjpegUrl, string enterTheUrlToTheMjpegSourceUrl)
        {
            var result = await InputBox1(mjpegUrl, enterTheUrlToTheMjpegSourceUrl, global::Xamarin.MainPage.Instance.Navigation);
            return result;
        }

        public static Task<string> InputBox1(string title, string description, INavigation navigation)
        {
            var tcs = new TaskCompletionSource<string>();

            var lblTitle = new Label
                {Text = title, HorizontalOptions = LayoutOptions.Center, FontAttributes = FontAttributes.Bold};
            var lblMessage = new Label {Text = description};
            var txtInput = new Entry {Text = ""};

            var btnOk = new Button
            {
                Text = "Ok",
                WidthRequest = 100,
            };
            btnOk.Clicked += async (s, e) =>
            {
                var result = txtInput.Text;
                await navigation.PopModalAsync();
                tcs.SetResult(result);
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                WidthRequest = 100,
            };
            btnCancel.Clicked += async (s, e) =>
            {
                await navigation.PopModalAsync();
                tcs.SetResult(null);
            };

            var slButtons = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Children = {btnOk, btnCancel},
            };

            var layout = new StackLayout
            {
                Padding = new Thickness(0, 40, 0, 0),
                VerticalOptions = LayoutOptions.StartAndExpand,
                HorizontalOptions = LayoutOptions.CenterAndExpand,
                Orientation = StackOrientation.Vertical,
                Children = {lblTitle, lblMessage, txtInput, slButtons},
            };

            var page = new ContentPage();
            page.Content = layout;
            navigation.PushModalAsync(page);
            txtInput.Focus();

            return tcs.Task;
        }
    }
}
