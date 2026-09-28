using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Aayam.Utilities
{
    public class PerformanceProfiler : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private UnityEngine.UI.Text _displayText;

        [Header("Settings")]
        [SerializeField] private bool  _showOverlay     = true;
        [SerializeField] private float _sampleInterval  = 1f;

        private float _deltaTime;
        private int   _frameCount;
        private float _timeAccumulator;
        private float _currentFps;
        private float _minFps = float.MaxValue;
        private float _maxFps;
        private float _sessionStartTime;

        private readonly System.Collections.Generic.List<float> _frameTimes = new(1024);

        private void Start()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            _showOverlay = false;
#endif
            _sessionStartTime = Time.time;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _deltaTime += (dt - _deltaTime) * 0.1f;
            _frameTimes.Add(dt);

            _frameCount++;
            _timeAccumulator += dt;

            if (_timeAccumulator >= _sampleInterval)
            {
                _currentFps = _frameCount / _timeAccumulator;

                if (Time.time > _sessionStartTime + 3f)
                {
                    if (_currentFps < _minFps) _minFps = _currentFps;
                    if (_currentFps > _maxFps) _maxFps = _currentFps;
                }

                _frameCount      = 0;
                _timeAccumulator = 0f;

                if (_showOverlay) UpdateOverlay();
            }
        }

        private void UpdateOverlay()
        {
            if (_displayText == null) return;

            long allocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long reservedMB  = Profiler.GetTotalReservedMemoryLong()  / (1024 * 1024);
            long monoMB      = Profiler.GetMonoUsedSizeLong()         / (1024 * 1024);

            string fpsColor = _currentFps >= 55f ? "#4CAF50" :
                              _currentFps >= 40f ? "#FF9800" : "#F44336";

            var sb = new StringBuilder(512);
            sb.AppendLine("<b>PERFORMANCE</b>");
            sb.AppendLine($"FPS: <color={fpsColor}><b>{_currentFps:F0}</b></color>  Min: {_minFps:F0}  Max: {_maxFps:F0}");
            sb.AppendLine($"Frame: {_deltaTime * 1000f:F1}ms  1%Low: {Get1PercentLow():F1}ms");
            sb.AppendLine();
            sb.AppendLine("<b>MEMORY</b>");
            sb.AppendLine($"Alloc: {allocatedMB}MB  Rsrv: {reservedMB}MB  Mono: {monoMB}MB");
            sb.AppendLine();
            sb.AppendLine($"Res: {Screen.width}x{Screen.height}  DPI: {Screen.dpi:F0}");
            sb.AppendLine($"Session: {(Time.time - _sessionStartTime):F0}s");

            _displayText.text = sb.ToString();
        }

        private float Get1PercentLow()
        {
            if (_frameTimes.Count < 10) return _deltaTime * 1000f;

            var sorted = new System.Collections.Generic.List<float>(_frameTimes);
            sorted.Sort((a, b) => b.CompareTo(a));

            int count1Pct = Mathf.Max(1, sorted.Count / 100);
            float sum = 0f;
            for (int i = 0; i < count1Pct; i++)
                sum += sorted[i];

            return (sum / count1Pct) * 1000f;
        }

        public void SaveReport()
        {
            long allocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

            var sb = new StringBuilder();
            sb.AppendLine("=== AAYAM PERFORMANCE REPORT ===");
            sb.AppendLine($"Date:       {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device:     {SystemInfo.deviceModel}");
            sb.AppendLine($"OS:         {SystemInfo.operatingSystem}");
            sb.AppendLine($"GPU:        {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"RAM:        {SystemInfo.systemMemorySize} MB");
            sb.AppendLine($"Resolution: {Screen.width}x{Screen.height} @ {Screen.dpi:F0}dpi");
            sb.AppendLine();
            sb.AppendLine($"Session Duration: {(Time.time - _sessionStartTime):F0}s");
            sb.AppendLine($"Average FPS:      {_currentFps:F1}");
            sb.AppendLine($"Min FPS:          {_minFps:F1}");
            sb.AppendLine($"Max FPS:          {_maxFps:F1}");
            sb.AppendLine($"1% Low (ms):      {Get1PercentLow():F2}");
            sb.AppendLine($"Allocated Memory: {allocatedMB} MB");
            sb.AppendLine();
            sb.AppendLine("=== BENCHMARK RESULTS ===");
            sb.AppendLine($"FPS >= 55:     {(_minFps >= 55f ? "PASS" : "FAIL")}");
            sb.AppendLine($"Memory < 400MB:{(allocatedMB < 400 ? "PASS" : "FAIL")}");
            sb.AppendLine($"1% Low < 25ms: {(Get1PercentLow() < 25f ? "PASS" : "FAIL")}");

            string path = System.IO.Path.Combine(Application.persistentDataPath, "PerfReport.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Profiler] Report saved: {path}");
        }

        public void ToggleOverlay() => _showOverlay = !_showOverlay;
        public float CurrentFps     => _currentFps;
        public float MinFps         => _minFps;
        public float MaxFps         => _maxFps;
    }
}
