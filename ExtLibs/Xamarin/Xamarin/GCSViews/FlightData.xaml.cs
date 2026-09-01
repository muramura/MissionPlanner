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

        // 🎮 18チャンネルの軸・キー割り当てデータ配列 (重複割り当て完全対応・各チャンネル独立保持)
        public static string[] ChannelAxisMapping = new string[19]
        {
            "", "X", "Y", "Z", "Rz", "Slider1", "None",
            "None", "None", "None", "None", "None", "None",
            "None", "None", "None", "None", "None", "None"
        };

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

            // 4. 十字キー (Dpad)
            if (s.IndexOf("Dpad Up", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Up", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Up";
            if (s.IndexOf("Dpad Down", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Down", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Down";
            if (s.IndexOf("Dpad Left", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Left";
            if (s.IndexOf("Dpad Right", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) return "Dpad Right";

            if (s.Equals("None", StringComparison.OrdinalIgnoreCase)) return "None";
            return s;
        }

        // 🎮 割り当てられた軸・ボタンからリアルタイムPWM値を算出 (1000〜2000µs)
        public int CalculateChannelPWM(string axisSetting, int defaultPwm = 1500)
        {
            try
            {
                string norm = NormalizeAxisName(axisSetting);
                if (norm.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return defaultPwm;
                }

                // 1. スティック軸 (X, Y, Z, Rz, Rx, Ry: -1.0〜+1.0 -> 1000〜2000µs)
                if (norm == "X")
                {
                    float val = (LastStickRoll != 0f) ? LastStickRoll : LastRawAxisX;
                    return (int)Math.Max(1000, Math.Min(2000, 1500 + val * 500));
                }
                if (norm == "Y")
                {
                    float val = (LastStickPitch != 0f) ? LastStickPitch : LastRawAxisY;
                    return (int)Math.Max(1000, Math.Min(2000, 1500 + val * 500));
                }
                if (norm == "Z")
                {
                    float val = (LastRawAxisZ != 0f) ? LastRawAxisZ : ((LastStickThrottle != 0f) ? LastStickThrottle : LastRawThrottle);
                    return (int)Math.Max(1000, Math.Min(2000, 1500 + val * 500));
                }
                if (norm == "Rz")
                {
                    float val = (LastRawAxisRz != 0f) ? LastRawAxisRz : ((LastStickYaw != 0f) ? LastStickYaw : LastRawRudder);
                    return (int)Math.Max(1000, Math.Min(2000, 1500 + val * 500));
                }
                if (norm == "Rx") return (int)Math.Max(1000, Math.Min(2000, 1500 + LastRawAxisRx * 500));
                if (norm == "Ry") return (int)Math.Max(1000, Math.Min(2000, 1500 + LastRawAxisRy * 500));

                // 2. スライダー・トリガー (Slider1, Slider2: 0.0〜1.0 または -1.0〜+1.0 -> 1000〜2000µs)
                if (norm == "Slider1")
                {
                    float val = (LastRawBrake != 0) ? LastRawBrake : ((LastRawAxisZ != 0) ? LastRawAxisZ : LastRawThrottle);
                    return (int)Math.Max(1000, Math.Min(2000, 1000 + Math.Max(0f, (val + 1f) / 2f) * 1000));
                }
                if (norm == "Slider2")
                {
                    float val = (LastRawGas != 0) ? LastRawGas : ((LastRawAxisRz != 0) ? LastRawAxisRz : LastRawRudder);
                    return (int)Math.Max(1000, Math.Min(2000, 1000 + Math.Max(0f, (val + 1f) / 2f) * 1000));
                }

                // 3. ゲームパッドボタン (押下中 2000µs, 離すと 1000µs)
                foreach (var kvp in PressedButtonMap)
                {
                    if (kvp.Value) // 押下中
                    {
                        string pressedNorm = NormalizeAxisName(ConvertKeyCodeToName(kvp.Key));
                        if (norm == pressedNorm)
                        {
                            return 2000;
                        }
                    }
                }

                // ボタンまたはDpad割り当て済みだが離されている状態
                if (norm.StartsWith("Btn") || norm.StartsWith("Dpad"))
                {
                    return 1000;
                }
            }
            catch { }

            return defaultPwm;
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
            Forms.Device.StartTimer(TimeSpan.FromMilliseconds(50), () =>
            {
                try
                {
                    // 🎮 1. ジョイスティック・モーダル用リアルタイム更新 (画面上のボタンの最新テキストを直接読み取って全ポジションバーをダイレクト更新！)
                    try
                    {
                        if (Pnl_JoystickModal != null && Pnl_JoystickModal.IsVisible)
                        {
                            for (int ch = 1; ch <= 18; ch++)
                            {
                                // 画面上のボタンテキスト (例: "X ▾", "Btn A (×) ▾", "L2 ▾") または データ配列から最新設定を取得
                                string mapping = (ch < ChannelAxisMapping.Length) ? ChannelAxisMapping[ch] : "None";
                                var btn = this.FindByName<Button>($"Btn_RCAxis_{ch}");
                                if (btn != null && !string.IsNullOrEmpty(btn.Text))
                                {
                                    mapping = btn.Text.Replace(" ▾", "").Trim();
                                }

                                int defPwm = (ch == 3) ? 1000 : 1500;
                                int pwm = CalculateChannelPWM(mapping, defPwm);

                                // 🎯 ポジションバー更新 (0.0〜1.0)
                                var pb = this.FindByName<ProgressBar>($"PB_joy_rc{ch}");
                                if (pb != null)
                                {
                                    pb.Progress = Math.Max(0.0, Math.Min(1.0, (pwm - 1000) / 1000.0));
                                }

                                // 🎯 数値ラベル更新 (例: "1500 µs")
                                var lbl = this.FindByName<Label>($"LBL_joy_rc{ch}");
                                if (lbl != null)
                                {
                                    lbl.Text = pwm + " µs";
                                }
                            }

                            // 🕹️ ジョイスティック有効時: MAVLink RC Override パケットを機体へ送信 (Ch1〜Ch8)
                            if (IsJoystickActive && MainV2.comPort != null && MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen)
                            {
                                try
                                {
                                    var rcOverride = new MAVLink.mavlink_rc_channels_override_t
                                    {
                                        target_system = 1,
                                        target_component = 1,
                                        chan1_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[1], 1500),
                                        chan2_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[2], 1500),
                                        chan3_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[3], 1000),
                                        chan4_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[4], 1500),
                                        chan5_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[5], 1500),
                                        chan6_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[6], 1500),
                                        chan7_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[7], 1500),
                                        chan8_raw = (ushort)CalculateChannelPWM(ChannelAxisMapping[8], 1500)
                                    };
                                    MainV2.comPort.sendPacket(rcOverride, 1, 1);
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Joystick modal timer error: " + ex);
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
                            if (++streamRequestCounter % 20 == 1)
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
                            Picker_ActiveJoystick.Items.Add("⚠️ 未接続 (No Device Detected)");
                            Picker_ActiveJoystick.SelectedIndex = 0;
                        }
                    }
                }
                catch { }

                // 画面上のボタンテキストをデータ配列と同期
                for (int ch = 1; ch <= 18; ch++)
                {
                    try
                    {
                        var btn = this.FindByName<Button>($"Btn_RCAxis_{ch}");
                        if (btn != null && ch < ChannelAxisMapping.Length)
                        {
                            btn.Text = ChannelAxisMapping[ch] + " ▾";
                        }
                    }
                    catch { }
                }
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
                        Picker_ActiveJoystick.Items.Add("⚠️ 未接続 (No Device Detected)");
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

        public async void OnJoystickSaveClicked(object sender, EventArgs e)
        {
            try
            {
                await DisplayAlert("ジョイスティック設定", "設定を正常に保存しました！", "OK");
                if (Pnl_JoystickModal != null) Pnl_JoystickModal.IsVisible = false;
            }
            catch { }
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

                string result = await DisplayActionSheet($"RC{ch} 割り当て軸・キーの選択", "キャンセル", null,
                    "X (Roll / スティック横)",
                    "Y (Pitch / スティック縦)",
                    "Z (Throttle / スロットル)",
                    "Rz (Yaw / ラダー)",
                    "Rx (右スティック横)",
                    "Ry (右スティック縦)",
                    "Slider1 (L2 / 左スライダー・トリガー)",
                    "Slider2 (R2 / 右スライダー・トリガー)",
                    "Btn A (×ボタン)",
                    "Btn B (○ボタン)",
                    "Btn X (□ボタン)",
                    "Btn Y (△ボタン)",
                    "Btn L1 (左バンパー)",
                    "Btn R1 (右バンパー)",
                    "Btn L3 (左押し込み)",
                    "Btn R3 (右押し込み)",
                    "Dpad Up (十字上)",
                    "Dpad Down (十字下)",
                    "Dpad Left (十字左)",
                    "Dpad Right (十字右)",
                    "None (割り当てなし)");

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
                btn.Text = "動かして...";
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
                        btn.Text = "完了!";
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

}

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
            // wait in this proc, until user did his input 
            var tcs = new TaskCompletionSource<string>();

            var lblTitle = new Label
                {Text = title, HorizontalOptions = LayoutOptions.Center, FontAttributes = FontAttributes.Bold};
            var lblMessage = new Label {Text = description};
            var txtInput = new Entry {Text = ""};

            var btnOk = new Button
            {
                Text = "Ok",
                WidthRequest = 100,
                //BackgroundColor = Color.FromRgb(0.8, 0.8, 0.8),
            };
            btnOk.Clicked += async (s, e) =>
            {
                // close page
                var result = txtInput.Text;
                await navigation.PopModalAsync();
                // pass result
                tcs.SetResult(result);
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                WidthRequest = 100,
                //BackgroundColor = Color.FromRgb(0.8, 0.8, 0.8)
            };
            btnCancel.Clicked += async (s, e) =>
            {
                // close page
                await navigation.PopModalAsync();
                // pass empty result
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

            // create and show page
            var page = new ContentPage();
            page.Content = layout;
            navigation.PushModalAsync(page);
            // open keyboard
            txtInput.Focus();

            // code is waiting her, until result is passed with tcs.SetResult() in btn-Clicked
            // then proc returns the result
            return tcs.Task;
        }
    

}