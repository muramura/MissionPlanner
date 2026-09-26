using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using MissionPlanner.Utilities;
using Xamarin.Essentials;

namespace MissionPlanner
{
    public class Speech : ISpeech
    {
        private DateTime lastmsg = DateTime.MinValue;
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool speechEnable { get; set; } = true;

        public Speech()
        {
            try
            {
                if (Settings.Instance["speechenable"] != null)
                    speechEnable = Settings.Instance.GetBoolean("speechenable");
            }
            catch { }
        }

        public bool IsReady
        {
            get
            {
                // 直近メッセージから2秒以上経過していればReadyとみなす（固まりを防止）
                return !isBusy || (DateTime.Now - lastmsg).TotalSeconds > 2.0;
            }
        }

        private CancellationTokenSource cts;
        private volatile bool isBusy = false;

        public void SpeakAsync(string text)
        {
            try
            {
                if (!MainV2.speechEnabled())
                {
                    log.Debug("TTS: speech disabled, skipping: " + text);
                    return;
                }

                if (string.IsNullOrWhiteSpace(text))
                    return;

                text = Regex.Replace(text, @"\bPreArm\b", "Pre Arm", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\bdist\b", "distance", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\bNAV\b", "Navigation", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\b([0-9]+)m\b", "$1 meters", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\b([0-9]+)ft\b", "$1 feet", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\b([0-9]+)\bbaud\b", "$1 baudrate", RegexOptions.IgnoreCase);

                lastmsg = DateTime.Now;
                log.Info("TTS: say " + text);

                // Android TTS は UI/MainThread またはアクティビティのコンテキストで確実に駆動する
                global::Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        isBusy = true;
                        try { cts?.Cancel(); } catch { }
                        cts = new CancellationTokenSource();

                        var settings = new SpeechOptions()
                        {
                            Volume = 1.0f,
                            Pitch = 1.0f
                        };

                        await TextToSpeech.SpeakAsync(text, settings, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        log.Warn("TextToSpeech.SpeakAsync error: " + ex.Message);
                    }
                    finally
                    {
                        isBusy = false;
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error("TTS SpeakAsync outer error: " + ex.Message);
            }
        }

        public void SpeakAsyncCancelAll()
        {
            try
            {
                cts?.Cancel();
            }
            catch { }
            isBusy = false;
        }
    }
}
