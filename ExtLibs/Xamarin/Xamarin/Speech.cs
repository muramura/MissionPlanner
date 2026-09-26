using System;
using System.Collections.Concurrent;
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
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ConcurrentQueue<string> _speechQueue = new ConcurrentQueue<string>();
        private readonly object _processLock = new object();
        private bool _isProcessing = false;
        private CancellationTokenSource _currentCts;
        private const int MAX_QUEUE_SIZE = 10;

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
                // キューにまだ余裕があればReady
                return _speechQueue.Count < MAX_QUEUE_SIZE;
            }
        }

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

                // キューが溢れないように古いものを間引く
                while (_speechQueue.Count >= MAX_QUEUE_SIZE)
                {
                    _speechQueue.TryDequeue(out _);
                }

                _speechQueue.Enqueue(text);
                log.Info($"TTS: Enqueued '{text}', Queue size: {_speechQueue.Count}");

                // キュー処理を開始（未開始の場合）
                EnsureQueueProcessing();
            }
            catch (Exception ex)
            {
                log.Error("TTS SpeakAsync error: " + ex.Message);
            }
        }

        private void EnsureQueueProcessing()
        {
            lock (_processLock)
            {
                if (_isProcessing)
                    return;

                _isProcessing = true;
            }

            // メインスレッド（UIスレッド）でキューの処理ループを回す
            global::Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
            {
                await ProcessQueueAsync();
            });
        }

        private async Task ProcessQueueAsync()
        {
            var settings = new SpeechOptions()
            {
                Volume = 1.0f,
                Pitch = 1.0f
            };

            while (true)
            {
                if (!MainV2.speechEnabled())
                {
                    // 音声が無効化されたらキューを破棄
                    ClearQueue();
                    break;
                }

                if (!_speechQueue.TryDequeue(out string text))
                {
                    // キューが空になったら終了
                    break;
                }

                try
                {
                    _currentCts = new CancellationTokenSource();
                    log.Info("TTS: Speaking -> " + text);
                    await TextToSpeech.SpeakAsync(text, settings, _currentCts.Token);
                }
                catch (OperationCanceledException)
                {
                    log.Info("TTS: Speech canceled");
                    break;
                }
                catch (Exception ex)
                {
                    log.Warn("TextToSpeech.SpeakAsync error: " + ex.Message);
                }
                finally
                {
                    _currentCts?.Dispose();
                    _currentCts = null;
                }

                // メッセージ間にわずかな間隔（100ms）を設けて自然に聞き取れるようにする
                try
                {
                    await Task.Delay(100);
                }
                catch { }
            }

            lock (_processLock)
            {
                _isProcessing = false;
                // 処理終了直前に新しいアイテムが入っていた場合は再開
                if (!_speechQueue.IsEmpty)
                {
                    EnsureQueueProcessing();
                }
            }
        }

        private void ClearQueue()
        {
            while (_speechQueue.TryDequeue(out _)) { }
        }

        public void SpeakAsyncCancelAll()
        {
            try
            {
                ClearQueue();
                _currentCts?.Cancel();
            }
            catch (Exception ex)
            {
                log.Warn("TTS SpeakAsyncCancelAll error: " + ex.Message);
            }
        }
    }
}
