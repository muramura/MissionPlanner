namespace MissionPlanner.Utilities
{
    public interface ISpeech
    {
        bool speechEnable { get; set; }
        bool IsReady { get; }
        void SpeakAsync(string text);
        void SpeakAsyncCancelAll();
    }

    public static class SpeechExtensions
    {
        public static void SpeakAsync(this ISpeech speech, string text, int severity)
        {
            if (speech == null) return;
            try
            {
                var method = speech.GetType().GetMethod("SpeakAsync", new[] { typeof(string), typeof(int) });
                if (method != null)
                {
                    method.Invoke(speech, new object[] { text, severity });
                    return;
                }
            }
            catch { }
            speech.SpeakAsync(text);
        }
    }
}