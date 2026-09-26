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

        public struct SpeechItem
        {
            public string Text;
            public int Severity;
            public DateTime EnqueueTime;
        }

        // 最新メッセージを保持するスレッドセーフキュー
        private readonly ConcurrentQueue<SpeechItem> _speechQueue = new ConcurrentQueue<SpeechItem>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationTokenSource _currentPlayCts;
        private volatile bool _isSpeaking = false;

        public bool speechEnable { get; set; } = true;

        public Speech()
        {
            try
            {
                if (Settings.Instance["speechenable"] != null)
                    speechEnable = Settings.Instance.GetBoolean("speechenable");
            }
            catch { }

            // バックグラウンドで定期的にキューを確認するループを開始
            Task.Run(QueueWorkerLoopAsync);
        }

        public bool IsReady => !_isSpeaking && _speechQueue.IsEmpty;

        public void SpeakAsync(string text)
        {
            // デフォルト優先度は INFO (6)
            SpeakAsync(text, 6);
        }

        public void SpeakAsync(string text, int severity)
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

                // 【重要】新しいメッセージをキューに入れようとしたら、キューをクリアしてから、キューに格納する
                ClearQueue();

                var item = new SpeechItem
                {
                    Text = text,
                    Severity = severity,
                    EnqueueTime = DateTime.Now
                };

                _speechQueue.Enqueue(item);
                log.Info($"TTS: Queued latest message (sev={severity}): '{text}'");
            }
            catch (Exception ex)
            {
                log.Error("TTS SpeakAsync error: " + ex.Message);
            }
        }

        private void ClearQueue()
        {
            while (_speechQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// 音声読み上げは定期的にキューを確認する（周期: 200ms）
        /// キューに文言が登録されていたら、キューから取得して、メッセージの優先度以上だったら音声読み上げを行い、優先度低いなら捨てる。
        /// </summary>
        private async Task QueueWorkerLoopAsync()
        {
            log.Info("TTS: QueueWorkerLoop started");
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // 200msごとに定期確認
                    await Task.Delay(200, _cts.Token);

                    // 音声が無効ならキューをクリアしてスキップ
                    if (!MainV2.speechEnabled())
                    {
                        ClearQueue();
                        continue;
                    }

                    // 現在音声読み上げ中の場合は完了するまで待つ
                    if (_isSpeaking)
                    {
                        continue;
                    }

                    // キューに文言が登録されていたら、キューから取得
                    if (_speechQueue.TryDequeue(out SpeechItem item))
                    {
                        // メッセージの優先度判定
                        // MAV_SEVERITY: 0(EMERGENCY) 〜 7(DEBUG)
                        // 数値が小さいほど高優先度。Settings.GetInt32("severity", 4) 以下なら優先度以上
                        int minSeverity = 4;
                        try
                        {
                            minSeverity = Settings.Instance.GetInt32("severity", 4);
                        }
                        catch { }

                        if (item.Severity <= minSeverity)
                        {
                            // 優先度以上だったら音声読み上げを行う
                            log.Info($"TTS: Severity eligible ({item.Severity} <= {minSeverity}), speaking: '{item.Text}'");
                            await PlaySpeechAsync(item.Text);
                        }
                        else
                        {
                            // 優先度低いなら捨てる
                            log.Info($"TTS: Dropped low severity message ({item.Severity} > {minSeverity}): '{item.Text}'");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.Warn("TTS QueueWorkerLoop exception: " + ex.Message);
                }
            }
            log.Info("TTS: QueueWorkerLoop stopped");
        }

        /// <summary>
        /// 実際の読み上げを行い、完了するまで待機（await）する
        /// </summary>
        private async Task PlaySpeechAsync(string text)
        {
            _isSpeaking = true;
            try
            {
                _currentPlayCts?.Dispose();
                _currentPlayCts = new CancellationTokenSource();

                var tcs = new TaskCompletionSource<bool>();

                // Android TTS は UI/MainThread で安全に駆動
                global::Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var settings = new SpeechOptions()
                        {
                            Volume = 1.0f,
                            Pitch = 1.0f
                        };

                        await TextToSpeech.SpeakAsync(text, settings, _currentPlayCts.Token);
                        tcs.TrySetResult(true);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        log.Warn("TextToSpeech.SpeakAsync error: " + ex.Message);
                        tcs.TrySetResult(false);
                    }
                });

                await tcs.Task;
            }
            catch (Exception ex)
            {
                log.Warn("TTS PlaySpeechAsync error: " + ex.Message);
            }
            finally
            {
                _isSpeaking = false;
            }
        }

        public void SpeakAsyncCancelAll()
        {
            try
            {
                ClearQueue();
                _currentPlayCts?.Cancel();
            }
            catch (Exception ex)
            {
                log.Warn("TTS SpeakAsyncCancelAll error: " + ex.Message);
            }
            finally
            {
                _isSpeaking = false;
            }
        }
    }
}
