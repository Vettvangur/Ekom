using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ekom.Utilities
{
    public class LoopTimer
    {
        private const int SampleSize = 100;  // Number of iterations to sample
        private const double Threshold = 0.005; // Time threshold in seconds (5ms)

        private readonly int _totalIterations;
        private readonly List<double> _sampleTimes = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly ILogger _logger;
        private readonly string _nodeAlias;

        public LoopTimer(int totalIterations, ILogger logger, string nodeAlias)
        {
            _totalIterations = totalIterations;
            _logger = logger;
            _nodeAlias = nodeAlias;
        }

        public void StartIteration()
        {
            _stopwatch.Restart();
        }

        public void EndIteration()
        {
            _stopwatch.Stop();

            if (_totalIterations < 1000)
            {
                return;
            }

            if (_sampleTimes.Count > SampleSize)
            {
                return;
            }

            _sampleTimes.Add(_stopwatch.Elapsed.TotalSeconds);

            if (_sampleTimes.Count != SampleSize)
            {
                return;
            }

            var averageTime = CalculateAverage(_sampleTimes);

            // Estimate total time based on average
            var estimatedTotalTime = averageTime * _totalIterations;

            if (averageTime > Threshold)
            {
                _logger.LogWarning(
                    $"WARNING: Estimated total time for {_totalIterations} iterations of {_nodeAlias}: {estimatedTotalTime} seconds. Average loop time for first {SampleSize} iterations is {averageTime * 1000} milliseconds, which exceeds the threshold of {Threshold * 1000} milliseconds.");
            }
           
            
        }

        private double CalculateAverage(List<double> times)
        {
            var sum = times.Sum();
            return sum / times.Count;
        }
    }
}
