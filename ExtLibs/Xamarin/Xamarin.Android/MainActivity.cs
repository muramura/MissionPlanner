using Resource = _Microsoft.Android.Resource.Designer.Resource;
﻿using Acr.UserDialogs;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using Android.Views;
using Mono.Unix;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.Util;
using Android.Bluetooth;
using Android.Runtime;
using AndroidX.Core.App;
using Android.Bluetooth;
using AndroidX.Core.Content;
using Xamarin.Essentials;
using MissionPlanner.GCSViews;
// using MissionPlanner.GCSViews.ConfigurationView;
using Environment = Android.OS.Environment;
using Settings = MissionPlanner.Utilities.Settings;
using Thread = System.Threading.Thread;
using Android.Content;
using Android.Media;
using Android.Provider;
using Android.Views.InputMethods;
using Android.Widget;
using Hoho.Android.UsbSerial.Util;
using Java.Lang;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using Xamarin.Forms;
using Xamarin.GCSViews;
using Application = Android.App.Application;
using Exception = System.Exception;
using File = Java.IO.File;
using Process = Android.OS.Process;
using String = System.String;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Uri = Android.Net.Uri;
using View = Android.Views.View;
using Interfaces;
using Encoding = Android.Media.Encoding;
using Stream = Android.Media.Stream;

[assembly: UsesFeature("android.hardware.usb.host", Required = false)]
[assembly: UsesFeature("android.hardware.bluetooth", Required = false)]
[assembly: UsesFeature(GLESVersion = 0x00030000, Required = true)]
[assembly: UsesLibrary("org.apache.http.legacy", false)]
[assembly: UsesPermission("android.permission.RECEIVE_D2D_COMMANDS")]
[assembly: UsesPermission("android.permission.BLUETOOTH")]
[assembly: UsesPermission("android.permission.BLUETOOTH_CONNECT")]
[assembly: UsesPermission("android.permission.BLUETOOTH_ADMIN")]
[assembly: UsesFeature("android.hardware.bluetooth", Required = false)]
[assembly: UsesFeature("android.hardware.bluetooth_le", Required = false)]
[assembly: UsesPermission("android.permission.ACCESS_FINE_LOCATION")]
[assembly: UsesPermission("android.permission.ACCESS_COARSE_LOCATION")]
[assembly: UsesPermission("android.permission.INTERNET")]
[assembly: UsesPermission("android.permission.LOCATION_HARDWARE")]
[assembly: UsesPermission("android.permission.WAKE_LOCK")]
[assembly: UsesPermission("android.permission.CHANGE_WIFI_MULTICAST_STATE")]
[assembly: UsesPermission("android.permission.ACCESS_NETWORK_STATE")]
[assembly: UsesPermission("android.permission.ACCESS_WIFI_STATE")]
[assembly: UsesPermission("android.permission.USB_PERMISSION")]
[assembly: UsesPermission("android.permission.BATTERY_STATS")]
[assembly: UsesFeature("android.hardware.usb.accessory", Required = false)]
[assembly: UsesFeature("android.hardware.touchscreen" , Required = false)]
[assembly: UsesFeature("android.hardware.location" , Required = false)]
[assembly: UsesFeature("android.hardware.telephony", Required = false)]
[assembly: UsesFeature("android.hardware.faketouch" , Required = true)]


namespace Xamarin.Droid
{ //global::Android.Content.Intent.CategoryLauncher
  //global::Android.Content.Intent.CategoryHome,
    [IntentFilter(new[] { global::Android.Content.Intent.ActionMain, global::Android.Content.Intent.ActionAirplaneModeChanged ,
        global::Android.Content.Intent.ActionBootCompleted , UsbManager.ActionUsbDeviceAttached, UsbManager.ActionUsbDeviceDetached,
        global::Android.Bluetooth.BluetoothDevice.ActionFound, global::Android.Bluetooth.BluetoothDevice.ActionAclConnected, UsbManager.ActionUsbAccessoryAttached},
        Categories = new[] { global::Android.Content.Intent.CategoryLauncher })]
    [IntentFilter(actions: new[] { global::Android.Content.Intent.ActionView }, Categories = new[] { global::Android.Content.Intent.CategoryBrowsable, global::Android.Content.Intent.ActionDefault, global::Android.Content.Intent.CategoryOpenable }, DataHost = "*", DataPathPattern = ".*\\.tlog", DataMimeType = "*/*", DataSchemes = new[] { "file", "http", "https", "content" })]
    [IntentFilter(actions: new[] { global::Android.Content.Intent.ActionView }, Categories = new[] { global::Android.Content.Intent.CategoryBrowsable, global::Android.Content.Intent.ActionDefault, global::Android.Content.Intent.CategoryOpenable }, DataHost = "*", DataPathPattern = ".*\\.bin", DataMimeType = "*/*", DataSchemes = new[] { "file", "http", "https", "content" })]
    [MetaData("android.hardware.usb.action.USB_DEVICE_ATTACHED", Resource = "@xml/device_filter")]
    [Activity(Label = "Mission Planner", Exported = true, ScreenOrientation = ScreenOrientation.SensorLandscape, Icon = "@mipmap/icon", Theme = "@style/MainTheme",
        MainLauncher = true, HardwareAccelerated = true, DirectBootAware = true, Immersive = true, LaunchMode = LaunchMode.SingleInstance)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private const int SAF = 12321;
        readonly string TAG = "MP";
        private Socket server;
        public UsbDeviceReceiver UsbBroadcastReceiver;

        public static MainActivity Current { private set; get; }
        public static readonly int PickImageId = 1000;
        private DeviceDiscoveredReceiver BTBroadcastReceiver;
        private AndroidVideo androidvideo;

        public TaskCompletionSource<string> PickImageTaskCompletionSource { set; get; }

        private global::Android.Net.Wifi.WifiManager.WifiLock _wifiLock;

        private void AcquireLowLatencyWifiLock()
        {
            try
            {
                if (_wifiLock != null && _wifiLock.IsHeld)
                {
                    return;
                }

                var wifiManager = (global::Android.Net.Wifi.WifiManager)GetSystemService(Context.WifiService);
                if (wifiManager != null)
                {
                    global::Android.Net.WifiMode mode;
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q) // API 29+ (Android 10+)
                    {
                        mode = global::Android.Net.WifiMode.FullLowLatency;
                    }
                    else
                    {
                        mode = global::Android.Net.WifiMode.FullHighPerf;
                    }

                    _wifiLock = wifiManager.CreateWifiLock(mode, "MP_LowLatency_WifiLock");
                    _wifiLock.SetReferenceCounted(false);
                    _wifiLock.Acquire();
                    Log.Info("MP", $"[WIFI-PERF] Acquired Low-Latency WifiLock (Mode: {mode})");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MP", $"[WIFI-PERF] Failed to acquire WifiLock: {ex.Message}");
            }
        }

        private void ReleaseWifiLock()
        {
            try
            {
                if (_wifiLock != null && _wifiLock.IsHeld)
                {
                    _wifiLock.Release();
                    Log.Info("MP", "[WIFI-PERF] Released WifiLock");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MP", $"[WIFI-PERF] Failed to release WifiLock: {ex.Message}");
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == PickImageId)
            {
                if ((resultCode == Result.Ok) && (data != null))
                {
                    // Set the filename as the completion of the Task
                    PickImageTaskCompletionSource.SetResult(data.DataString);
                }
                else
                {
                    PickImageTaskCompletionSource.SetResult(null);
                }
            }

            if (requestCode == SAF)
            {
                // content:/com.android.externalstorage.documents/tree/primary%3AMp

                var pref = this.GetSharedPreferences("pref", FileCreationMode.Private);

                Uri docUriTree =
                    DocumentsContract.BuildDocumentUriUsingTree(data.Data,
                        DocumentsContract.GetTreeDocumentId(data.Data));

                var query = this.ContentResolver.Query(docUriTree, null, null,
                    null, null);
                query.MoveToFirst();
                var filePath = query.GetString(0);
                query.Close();

                pref.Edit().PutString("Directory", filePath).Commit();

                ContinueInit();
            }
        }

        public static void ShowKeyboard(View pView) {
            pView.RequestFocus();

            InputMethodManager inputMethodManager = Current.GetSystemService(Context.InputMethodService) as InputMethodManager;
            inputMethodManager.ShowSoftInput(pView, ShowFlags.Forced);
            inputMethodManager.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.ImplicitOnly);
        }

        public static void HideKeyboard(View pView) {
            InputMethodManager inputMethodManager = Current.GetSystemService(Context.InputMethodService) as InputMethodManager;
            inputMethodManager.HideSoftInputFromWindow(pView.WindowToken, HideSoftInputFlags.None);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Current = this;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            SetSupportActionBar((Toolbar)FindViewById(ToolbarResource));

            this.Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.TurnScreenOn |
                                 WindowManagerFlags.HardwareAccelerated);

            base.OnCreate(savedInstanceState);
            try
            {
                var connectivityManager = (global::Android.Net.ConnectivityManager)GetSystemService(global::Android.Content.Context.ConnectivityService);
                if (connectivityManager != null)
                {
                    foreach (var net in connectivityManager.GetAllNetworks())
                    {
                        var caps = connectivityManager.GetNetworkCapabilities(net);
                        if (caps != null && caps.HasTransport(global::Android.Net.TransportType.Wifi))
                        {
                            connectivityManager.BindProcessToNetwork(net);
                            global::Android.Util.Log.Info("MainActivity", "Successfully bound process to WiFi network: " + net);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("MainActivity", "BindProcessToNetwork ex: " + ex);
            }


            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            var pref = this.GetSharedPreferences("pref", FileCreationMode.Private);

            var pass = false;

            if (pref.Contains("Directory"))
            {
                try
                {
                    var files = Directory.GetFiles(pref.GetString("Directory", ""), "*.*");
                    pass = true;
                }
                catch
                {
                    pass = false;
                }
            }
            else
            {
                pass = false;
            }
            /*
            if (!pass)
            {
                Intent intent = new Intent(Intent.ActionOpenDocumentTree);
                intent.AddCategory(Intent.CategoryDefault);
                intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);

                //intent.PutExtra(DocumentsContract.ExtraInitialUri, Application.Context.getExternalStorageDirectory "MissionPlanner");
                StartActivityForResult(Intent.CreateChooser(intent, "Select a folder to save config settings"), SAF);
            }
            else*/
            {
                ContinueInit();
            }
        }

        void ContinueInit()
        {

            var list = Application.Context.GetExternalFilesDirs(null);
            list.ForEach(a => Log.Info("MP", "External dir option: " + a.AbsolutePath));

            var list2 = Application.Context.GetExternalFilesDirs(Environment.DirectoryDownloads);
            list2.ForEach(a => Log.Info("MP", "External DirectoryDownloads option: " + a.AbsolutePath));

            var pref = this.GetSharedPreferences("pref", FileCreationMode.Private);


            Settings.CustomUserDataDirectory = Application.Context.GetExternalFilesDir(null).ToString();
                //pref.GetString("Directory", Application.Context.GetExternalFilesDir(null).ToString());
            Log.Info("MP", "Settings.CustomUserDataDirectory " + Settings.CustomUserDataDirectory);

            try { 
            // WinForms.Android = true;
            // WinForms.BundledPath = Application.Context.ApplicationInfo.NativeLibraryDir;
                GStreamer.BundledPath = Application.Context.ApplicationInfo.NativeLibraryDir;
                GStreamer.Android = true;
            } catch { }
            // Log.Info("MP", "WinForms.BundledPath " + WinForms.BundledPath);

            try
            {
                var am = (ActivityManager)Application.Context.GetSystemService(Context.ActivityService);

                var devinfo = am?.DeviceConfigurationInfo;

                if (devinfo != null)
                {
                    Log.Info("MP", "opengl es version " + devinfo.GlEsVersion);
                    Log.Info("MP", "opengl es app req " + devinfo.ReqGlEsVersion);
                }
            }
            catch { }

            try
            {
                JavaSystem.LoadLibrary("gstreamer_android");

            // Org.Freedesktop.Gstreamer.GStreamer.Init(this.ApplicationContext);
            }
            catch (Exception ex) { Log.Error("MP", ex.ToString()); }

            ServiceLocator.Register<IBlueToothDevice>(() => new BTDevice());
            ServiceLocator.Register<IUSBDevices>(() => new USBDevices());
            ServiceLocator.Register<IRadio>(() => new Radio());
            ServiceLocator.Register<IGPS>(() => new GPS());
            ServiceLocator.Register<ISystemInfo>(() => new SystemInfo());

            Test.BlueToothDevice = ServiceLocator.Get<IBlueToothDevice>();
            Test.UsbDevices = ServiceLocator.Get<IUSBDevices>();
            Test.Radio = ServiceLocator.Get<IRadio>();
            Test.GPS = ServiceLocator.Get<IGPS>();
            Test.SystemInfo = ServiceLocator.Get<ISystemInfo>();

            Vario.Beep = (i, i1) => { playSound(i, i1); };

            androidvideo = new AndroidVideo();
            //disable
            //androidvideo.Start();
            AndroidVideo.onNewImage += (e, o) => 
            {
            // WinForms.SetHUDbg(o);
            };


            //ConfigFirmwareManifest.ExtraDeviceInfo
            /*
            var intent = new global::Android.Content.Intent(Intent.ActionOpenDocumentTree);

            intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
            intent.PutExtra(DocumentsContract.ExtraInitialUri, "Mission Planner");

            StartActivityForResult(intent, 1);
            */

            UserDialogs.Init(this);

            // 🎮 ジョイスティック・ゲームパッド検出ハンドラ登録
            FlightData.GetConnectedJoysticksFunc = () =>
            {
                var list = new global::System.Collections.Generic.List<string>();
                try
                {
                    var ids = global::Android.Views.InputDevice.GetDeviceIds();
                    if (ids != null)
                    {
                        foreach (var id in ids)
                        {
                            var dev = global::Android.Views.InputDevice.GetDevice(id);
                            if (dev != null && !dev.IsVirtual)
                            {
                                var src = dev.Sources;
                                if ((src & global::Android.Views.InputSourceType.Joystick) == global::Android.Views.InputSourceType.Joystick ||
                                    (src & global::Android.Views.InputSourceType.Gamepad) == global::Android.Views.InputSourceType.Gamepad ||
                                    (src & global::Android.Views.InputSourceType.ClassJoystick) == global::Android.Views.InputSourceType.ClassJoystick)
                                {
                                    list.Add(dev.Name);
                                }
                            }
                        }
                    }
                }
                catch (global::System.Exception ex)
                {
                    global::Android.Util.Log.Error("MainActivity", "GetConnectedJoysticks error: " + ex);
                }
                return list;
            };

            AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironment_UnhandledExceptionRaiser;

            {
                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) !=
                    (int)Permission.Granted ||
                    ContextCompat.CheckSelfPermission(this, Manifest.Permission.Bluetooth) !=
                    (int)Permission.Granted ||
                    ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothConnect) !=
                    (int)Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(this,
                        new String[]
                        {
                            Manifest.Permission.AccessFineLocation, Manifest.Permission.LocationHardware,
                            Manifest.Permission.Bluetooth,
                            Manifest.Permission.BluetoothConnect,
                        }, 1);
                }

            }

            try {
                // print some info
                var pm = this.PackageManager;
                var name = this.PackageName;

                var pi = pm.GetPackageInfo(name, PackageInfoFlags.Activities);

                Console.WriteLine("pi.ApplicationInfo.DataDir " + pi?.ApplicationInfo?.DataDir);
                Console.WriteLine("pi.ApplicationInfo.NativeLibraryDir " + pi?.ApplicationInfo?.NativeLibraryDir);

                // api level 24 - android 7
                Console.WriteLine("pi.ApplicationInfo.DeviceProtectedDataDir " +
                                  pi?.ApplicationInfo?.DeviceProtectedDataDir);
            } catch {}


            {
                // clean start, see if it was an intent/usb attach
                //if (savedInstanceState == null)
                {
                    //DoToastMessage("Init Saved State");
                    proxyIfUsbAttached(this.Intent);

                    Console.WriteLine(this.Intent?.Action);
                    Console.WriteLine(this.Intent?.Categories);
                    Console.WriteLine(this.Intent?.Data);
                    Console.WriteLine(this.Intent?.DataString);
                    Console.WriteLine(this.Intent?.Type);
                }
            }

            GC.Collect();

            try
            {
                Java.Lang.JavaSystem.LoadLibrary("gdal");

                Java.Lang.JavaSystem.LoadLibrary("gdalalljni");

                Java.Lang.JavaSystem.LoadLibrary("gdalwrap");
            }
            catch (System.Exception ex) { Log.Error("GDAL", ex.ToString()); }

            Task.Run(() =>
            {
                var gdaldir = Settings.GetRunningDirectory() + "gdalimages";
                Directory.CreateDirectory(gdaldir);

            // MissionPlanner.Utilities.GDAL.GDALBase = new GDAL.GDAL();

            // GDAL.GDAL.ScanDirectory(gdaldir);

            // GMap.NET.MapProviders.GMapProviders.List.Add(GDAL.GDALProvider.Instance);
            });
            

            //DoToastMessage("Launch App");

            LoadApplication(new App());
        }

        byte[] genTone(int sampleRate, int freqOfTone, int numSamples)
        {
            byte[] generatedSnd = new byte[2 * numSamples];
            double[] sample = new double[numSamples];
            // fill out the array
            for (int i = 0; i < numSamples; ++i)
            {
                sample[i] = System.Math.Sin(2 * System.Math.PI * i / (sampleRate / freqOfTone));
            }

            // convert to 16 bit pcm sound array
            // assumes the sample buffer is normalised.
            int idx = 0;
            foreach (double dVal in sample)
            {
                // scale to maximum amplitude
                short val = (short)((dVal * 32767));
                // in 16 bit wav PCM, first byte is the low order byte
                generatedSnd[idx++] = (byte)(val & 0x00ff);
                generatedSnd[idx++] = (byte)((val & 0xff00) >> 8);

            }

            return generatedSnd;
        }

        void playSound(int freq, int duration)
        {
            var sampleRate = 8000;
            var generatedSnd = genTone(sampleRate, freq, (duration * sampleRate) / 1000);
            AudioTrack audioTrack = new AudioTrack(Stream.Music,
                sampleRate, ChannelConfiguration.Mono, Encoding.Pcm16bit, generatedSnd.Length, AudioTrackMode.Stream);
            audioTrack.Play();
            audioTrack.Write(generatedSnd, 0, generatedSnd.Length);
            Thread.Sleep(duration + 40);
            audioTrack.Stop();
        }

        // 🎮 Android全域でのジョイスティック・モーションイベント最優先ディスパッチ
                // 🎮 ゲームパッド・ジョイスティックのボタンによる誤バック（終了ダイアログ）を防止
                // 🎮 コントローラー（PS4/PS5等のタッチパッド含む）からのクリック・EnterによるPicker誤オープンを完全防止
        public override bool DispatchTouchEvent(global::Android.Views.MotionEvent ev)
        {
            if (ev != null && ev.Device != null && !ev.Device.IsVirtual)
            {
                var src = ev.Device.Sources;
                if ((src & global::Android.Views.InputSourceType.Gamepad) == global::Android.Views.InputSourceType.Gamepad ||
                    (src & global::Android.Views.InputSourceType.Joystick) == global::Android.Views.InputSourceType.Joystick)
                {
                    // コントローラー内蔵タッチパッドからのタッチイベントは画面のUIフォーカスを奪わせない
                    return true;
                }
            }
            return base.DispatchTouchEvent(ev);
        }

        public override bool DispatchKeyEvent(global::Android.Views.KeyEvent e)
        {
            if (e != null)
            {
                int keyCode = (int)e.KeyCode;
                bool isDown = (e.Action == global::Android.Views.KeyEventActions.Down);

                // 🎮 ゲームパッド・ジョイスティック・十字キー・各種ボタンの判定 (PS4/PS5等のタッチパッド内蔵コントローラーも100%全ボタン受信)
                bool isGamePadOrJoystick = false;
                var evSrc = e.Source;
                if (evSrc.HasFlag(global::Android.Views.InputSourceType.Gamepad) ||
                    evSrc.HasFlag(global::Android.Views.InputSourceType.Joystick) ||
                    evSrc.HasFlag(global::Android.Views.InputSourceType.Dpad) ||
                    (e.Device != null && (e.Device.Sources.HasFlag(global::Android.Views.InputSourceType.Gamepad) || e.Device.Sources.HasFlag(global::Android.Views.InputSourceType.Joystick) || e.Device.Sources.HasFlag(global::Android.Views.InputSourceType.Dpad))))
                {
                    isGamePadOrJoystick = true;
                }

                if (global::Android.Views.KeyEvent.IsGamepadButton(e.KeyCode) ||
                    e.KeyCode == global::Android.Views.Keycode.DpadUp ||
                    e.KeyCode == global::Android.Views.Keycode.DpadDown ||
                    e.KeyCode == global::Android.Views.Keycode.DpadLeft ||
                    e.KeyCode == global::Android.Views.Keycode.DpadRight ||
                    e.KeyCode == global::Android.Views.Keycode.DpadCenter ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonA ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonB ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonC ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonX ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonY ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonZ ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonL1 ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonR1 ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonL2 ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonR2 ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonThumbl ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonThumbr ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonStart ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonSelect ||
                    e.KeyCode == global::Android.Views.Keycode.ButtonMode ||
                    (keyCode >= 96 && keyCode <= 110) ||
                    (keyCode >= 19 && keyCode <= 23))
                {
                    isGamePadOrJoystick = true;
                }

                if (isGamePadOrJoystick)
                {
                    if (isDown)
                    {
                        FlightData.LastPressedButtonCode = keyCode;
                    }
                    FlightData.SetButtonState(keyCode, isDown);
                    FlightData.IsJoystickActive = true;

                    // 🛡️ OSのフォーカス移動・UIクリック・ダイアログポップアップを100%完全遮断
                    return true;
                }
            }

            return base.DispatchKeyEvent(e);
        }

        public override bool DispatchGenericMotionEvent(global::Android.Views.MotionEvent ev)
        {
            try
            {
                if (ev != null)
                {
                    // 🕹️ ゲームパッド・ジョイスティック判定 (PS4/PS5 DualShock/DualSense等のタッチパッド内蔵コントローラーも100%確実に受信！)
                    bool isJoystickEvent = false;
                    var evSrc = ev.Source;
                    if (evSrc.HasFlag(global::Android.Views.InputSourceType.Joystick) ||
                        evSrc.HasFlag(global::Android.Views.InputSourceType.Gamepad) ||
                        (ev.Device != null && (ev.Device.Sources.HasFlag(global::Android.Views.InputSourceType.Joystick) || ev.Device.Sources.HasFlag(global::Android.Views.InputSourceType.Gamepad))))
                    {
                        // 画面への直接タッチイベント単独以外はすべてジョイスティック入力として処理
                        if (evSrc != global::Android.Views.InputSourceType.Touchscreen)
                        {
                            isJoystickEvent = true;
                        }
                    }

                    if (isJoystickEvent)
                    {
                        // 接続デバイスからの全軸入力を漏れなく取得
                        float x = ev.GetAxisValue(global::Android.Views.Axis.X);
                        float y = ev.GetAxisValue(global::Android.Views.Axis.Y);
                        float z = ev.GetAxisValue(global::Android.Views.Axis.Z);
                        float rz = ev.GetAxisValue(global::Android.Views.Axis.Rz);
                        float rx = ev.GetAxisValue(global::Android.Views.Axis.Rx);
                        float ry = ev.GetAxisValue(global::Android.Views.Axis.Ry);
                        float throttle = ev.GetAxisValue(global::Android.Views.Axis.Throttle);
                        float rudder = ev.GetAxisValue(global::Android.Views.Axis.Rudder);
                        float gas = ev.GetAxisValue(global::Android.Views.Axis.Gas);
                        float brake = ev.GetAxisValue(global::Android.Views.Axis.Brake);
                        float hatx = ev.GetAxisValue(global::Android.Views.Axis.HatX);
                        float haty = ev.GetAxisValue(global::Android.Views.Axis.HatY);

                        // Roll (Ch1): X または Rx
                        float roll = (x != 0) ? x : rx;
                        // Pitch (Ch2): Y または Ry (反転考慮)
                        float pitch = (y != 0) ? y : ry;
                        // Throttle (Ch3): Throttle, Z, または Gas (-1.0 〜 +1.0)
                        float thr = (throttle != 0) ? throttle : ((z != 0) ? z : ((gas != 0) ? gas : ((haty != 0) ? -haty : 0f)));
                        // Yaw (Ch4): Rudder または Rz
                        float yaw = (rudder != 0) ? rudder : rz;

                        FlightData.LastStickRoll = roll;
                        FlightData.LastStickPitch = pitch;
                        FlightData.LastStickThrottle = thr;
                        FlightData.LastStickYaw = yaw;
                        FlightData.LastRawAxisX = x;
                        FlightData.LastRawAxisY = y;
                        FlightData.LastRawAxisZ = z;
                        FlightData.LastRawAxisRz = rz;
                        FlightData.LastRawAxisRx = rx;
                        FlightData.LastRawAxisRy = ry;
                        FlightData.LastRawThrottle = throttle;
                        FlightData.LastRawRudder = rudder;
                        FlightData.LastRawGas = gas;
                        FlightData.LastRawBrake = brake;
                        FlightData.IsJoystickActive = true;

                        // 🕹️ 十字キー (HatX / HatY) を Dpad ボタンイベント (Keycode 19〜22) として完全連動！
                        // (多くのBluetoothゲームパッドで十字キーがMotionEventとして送られる現象を100%解決)
                        bool dpadLeft = (hatx < -0.5f);
                        bool dpadRight = (hatx > 0.5f);
                        bool dpadUp = (haty < -0.5f);
                        bool dpadDown = (haty > 0.5f);

                        FlightData.SetButtonState((int)global::Android.Views.Keycode.DpadLeft, dpadLeft);
                        FlightData.SetButtonState((int)global::Android.Views.Keycode.DpadRight, dpadRight);
                        FlightData.SetButtonState((int)global::Android.Views.Keycode.DpadUp, dpadUp);
                        FlightData.SetButtonState((int)global::Android.Views.Keycode.DpadDown, dpadDown);

                        if (dpadLeft) FlightData.LastPressedButtonCode = (int)global::Android.Views.Keycode.DpadLeft;
                        else if (dpadRight) FlightData.LastPressedButtonCode = (int)global::Android.Views.Keycode.DpadRight;
                        else if (dpadUp) FlightData.LastPressedButtonCode = (int)global::Android.Views.Keycode.DpadUp;
                        else if (dpadDown) FlightData.LastPressedButtonCode = (int)global::Android.Views.Keycode.DpadDown;

                        // 🛡️ ジョイスティックからのモーション入力時のみUIフォーカス移動を遮断
                        return true;
                    }
                }
            }
            catch (global::System.Exception ex)
            {
                global::Android.Util.Log.Error("MainActivity", "DispatchGenericMotionEvent error: " + ex);
            }

            // 👆 タッチ操作・画面タップ・マウス操作はすべて正常にOSへ通す
            return base.DispatchGenericMotionEvent(ev);
        }

        public override bool OnGenericMotionEvent(global::Android.Views.MotionEvent e)
        {
            return base.OnGenericMotionEvent(e);
        }

        public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            // 🎮 ボタン押下状態をFlightDataへ即座に伝達
            FlightData.LastPressedButtonCode = (int)keyCode;
            FlightData.SetButtonState((int)keyCode, true);
            FlightData.IsJoystickActive = true;

            // 🎮 ゲームパッド・ジョイスティックのボタン操作時はOSのダイアログ・Back処理を完全ガード
            if (global::Android.Views.KeyEvent.IsGamepadButton(keyCode) ||
                keyCode == Keycode.Back ||
                keyCode == Keycode.ButtonA ||
                keyCode == Keycode.ButtonB ||
                keyCode == Keycode.ButtonC ||
                keyCode == Keycode.ButtonX ||
                keyCode == Keycode.ButtonY ||
                keyCode == Keycode.ButtonZ ||
                keyCode == Keycode.ButtonL1 ||
                keyCode == Keycode.ButtonL2 ||
                keyCode == Keycode.ButtonR1 ||
                keyCode == Keycode.ButtonR2 ||
                keyCode == Keycode.ButtonThumbl ||
                keyCode == Keycode.ButtonThumbr ||
                keyCode == Keycode.ButtonStart ||
                keyCode == Keycode.ButtonSelect ||
                keyCode == Keycode.ButtonMode ||
                keyCode == Keycode.DpadCenter ||
                keyCode == Keycode.DpadUp ||
                keyCode == Keycode.DpadDown ||
                keyCode == Keycode.DpadLeft ||
                keyCode == Keycode.DpadRight)
            {
                Log.Debug(TAG, "Game Controller Button Pressed: " + keyCode);
                return true; // OSのBack処理へ渡さずアプリ内で消費
            }

            if (keyCode == Keycode.VolumeUp)
            {
                e.StartTracking();
                return true;
            }

            return base.OnKeyDown(keyCode, e);
        }

        public override bool OnKeyUp([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            // 🎮 ボタン離脱状態をFlightDataへ即座に伝達
            FlightData.SetButtonState((int)keyCode, false);

            // 🎮 ゲームパッド・ジョイスティックのボタン離脱時もOS処理へ流さない
            if (global::Android.Views.KeyEvent.IsGamepadButton(keyCode) ||
                keyCode == Keycode.Back ||
                keyCode == Keycode.ButtonA ||
                keyCode == Keycode.ButtonB ||
                keyCode == Keycode.ButtonC ||
                keyCode == Keycode.ButtonX ||
                keyCode == Keycode.ButtonY ||
                keyCode == Keycode.ButtonZ ||
                keyCode == Keycode.ButtonL1 ||
                keyCode == Keycode.ButtonL2 ||
                keyCode == Keycode.ButtonR1 ||
                keyCode == Keycode.ButtonR2 ||
                keyCode == Keycode.ButtonThumbl ||
                keyCode == Keycode.ButtonThumbr ||
                keyCode == Keycode.ButtonStart ||
                keyCode == Keycode.ButtonSelect ||
                keyCode == Keycode.ButtonMode ||
                keyCode == Keycode.DpadCenter ||
                keyCode == Keycode.DpadUp ||
                keyCode == Keycode.DpadDown ||
                keyCode == Keycode.DpadLeft ||
                keyCode == Keycode.DpadRight)
            {
                Log.Debug(TAG, "Game Controller Button Released: " + keyCode);
                return true;
            }

            if ((e.Flags & KeyEventFlags.CanceledLongPress) == 0)
            {
                if (keyCode == Keycode.VolumeUp)
                {
                    Log.Error(TAG, "Short press KEYCODE_VOLUME_UP");
                    return true;
                }
                else if (keyCode == Keycode.VolumeDown)
                {
                    Log.Error(TAG, "Short press KEYCODE_VOLUME_DOWN");
                    return true;
                }
            }

            return base.OnKeyUp(keyCode, e);
        }

        public override bool OnKeyLongPress([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            Log.Debug(TAG, "OnKeyLongPress " + keyCode);

            if (keyCode == Keycode.VolumeUp)
            {
                Log.Debug(TAG, "Long press KEYCODE_VOLUME_UP");
                return true;
            }
            else if (keyCode == Keycode.VolumeDown)
            {
                Log.Debug(TAG, "Long press KEYCODE_VOLUME_DOWN");
                return true;
            }

            return base.OnKeyLongPress(keyCode, e);
        }

        private void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            Log.Error(TAG, e.Exception.ToString());
            Debugger.Break();
        }

        private void DoToastMessage(string text, ToastLength toastLength = ToastLength.Short)
        {
            try
            {
                // thread to force invoke into ui thread
                Task.Run(() =>
                {
                    if (!this.IsFinishing)
                    {
                        //if (Looper.MainLooper.IsCurrentThread)
                        {
                            // On UI thread.
                            RunOnUiThread(() =>
                            {
                                try
                                {
                                    Toast toast = Toast.MakeText(this, text, toastLength);
                                    toast.Show();
                                }
                                catch
                                {

                                }
                            });
                        }
                    }
                });
            } catch {}
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            Console.WriteLine("OnNewIntent " + intent.Action);
        }

        private void proxyIfUsbAttached(Intent intent) {

            if (intent == null) return;

            if (!UsbManager.ActionUsbDeviceAttached.Equals(intent.Action)) return;

            Log.Verbose(TAG, "usb device attached");

            // WinForms.InitDevice = ()=>
            {
                Log.Info(TAG, "WinForms.InitDevice");
                UsbBroadcastReceiver.OnReceive(this.ApplicationContext, intent);
            };
        }

        protected override void OnStart()
        {
            base.OnStart();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public async Task<PermissionStatus> CheckAndRequestPermissionAsync<T>(T permission)
            where T : Permissions.BasePermission
        {
            Console.WriteLine("Check Perm " + permission.ToString());
            var status = await permission.CheckStatusAsync();
            if (status != PermissionStatus.Granted)
            {
                Console.WriteLine("Request Perm " + permission.ToString());
                status = await permission.RequestAsync();
            }

            Console.WriteLine("Status Perm " + permission.ToString() + " " + status);
            return status;
        }

        private async Task CheckPerm()
        {
            await CheckAndRequestPermissionAsync((new Permissions.LocationWhenInUse()));
            await CheckAndRequestPermissionAsync((new Permissions.StorageWrite()));
        }

        private void AndroidEnvironment_UnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            Log.Error("MP", e.Exception.StackTrace.ToString());
            Debugger.Break();
            e.Handled = true;
            DoToastMessage("ERROR " + e.Exception.Message, ToastLength.Long);
            throw e.Exception;
        }

        protected override void OnResume()
        {
            base.OnResume();

            AcquireLowLatencyWifiLock();

            this.Window.DecorView.SystemUiVisibility =
                (StatusBarVisibility) (SystemUiFlags.LowProfile
                                       | SystemUiFlags.Fullscreen
                                       | SystemUiFlags.HideNavigation
                                       | SystemUiFlags.Immersive
                                       | SystemUiFlags.ImmersiveSticky);

            StartD2DInfo();

            //register the broadcast receivers
            UsbBroadcastReceiver = new UsbDeviceReceiver();
            RegisterReceiver(UsbBroadcastReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));
            RegisterReceiver(UsbBroadcastReceiver, new IntentFilter(UsbManager.ActionUsbDeviceAttached));

            // Register for broadcasts when a device is discovered
            BTBroadcastReceiver = new DeviceDiscoveredReceiver();
            RegisterReceiver(BTBroadcastReceiver, new IntentFilter(BluetoothDevice.ActionFound));
            RegisterReceiver(BTBroadcastReceiver, new IntentFilter(BluetoothAdapter.ActionDiscoveryFinished));
        }

        protected override void OnPause()
        {
            base.OnPause();

            ReleaseWifiLock();

            StopD2DInfo();

            UnregisterReceiver(UsbBroadcastReceiver);

            UnregisterReceiver(BTBroadcastReceiver);
        }

        protected override void OnDestroy()
        {
            ReleaseWifiLock();
            base.OnDestroy();
        }

        public void StopD2DInfo()
        {
            server.Close();
            server = null;
        }

        public void StartD2DInfo()
        {
            {
                try
                {
                    //var d2dinfo = new UnixEndPoint("/tmp/d2dinfo");
                    //var d2dinfo = "songdebugmessage";
                    var d2dinfo = "linkstate";
                    //"d2dsignal";

                    server = new Socket(AddressFamily.Unix, SocketType.Stream, 0);
                    server.Bind(new AbstractUnixEndPoint(d2dinfo));

                    server.Listen(50);

                    Task.Run(() =>
                    {
                        while (server != null)
                        {
                            try
                            {
                                var socket = server.Accept();
                                Thread.Sleep(1);
                                byte[] buffer = new byte[100];
                                var readlen = 0;
                                do
                                {
                                    readlen = socket.Receive(buffer);
                                    if ((readlen > 4) && (readlen >= (4 + buffer[3])))
                                    {
                                        //Log.Info(TAG, "Got " + ASCIIEncoding.ASCII.GetString(buffer, 4, buffer[3]));
                                    }
                                } while (readlen > 0);
                                socket.Close();

                            }
                            catch (Exception ex) { Log.Warn(TAG, ex.ToString()); Thread.Sleep(1000); }
                        }
                    });

                }
                catch (Exception ex) { Log.Warn(TAG, ex.ToString()); }
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error(TAG, e.ExceptionObject.ToString());
            Debugger.Break();
        }

    }

    public class GPS : IGPS
    {
        public Task<(double lat, double lng, double alt)> GetPosition()
        {
            return Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best)).ContinueWith<(double,double,double)>(
                location =>
                {
                    return (location.Result.Latitude, location.Result.Longitude,
                        location.Result.Altitude.HasValue ? location.Result.Altitude.Value : 0.0);
                }
            );
        }
    }

    public class SystemInfo : ISystemInfo
    {
        public string GetSystemTag()
        {
            // android version
            try
            {
                return SysProp.GetProp("ro.build.fingerprint");
            }
            catch
            {
                return "";
            }
        }

        public void StartProcess(string[] cmd)
        {
            Runtime.GetRuntime().Exec(cmd);
        }
    }
}