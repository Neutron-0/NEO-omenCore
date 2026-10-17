namespace OmenCore.Hardware
{
    /// <summary>
    /// Notices when two temperature sensors that should be independent keep returning the same value. The Transcend 14 export
    /// (#157) showed the BIOS CPU and GPU readings frozen at exactly the same figure again and again (56 and 56, 60 and 60),
    /// which fits one thermal zone answering both indices. This only observes; it changes no reading.
    /// </summary>
    public sealed class SensorAliasTracker
    {
        public const int ReportAfterReads = 30;

        private int _identicalReads;
        private bool _reported;

        /// <summary>Feed every paired reading. Returns true once, when the pair has been identical for <see cref="ReportAfterReads"/> reads in a row.</summary>
        public bool Observe(double first, double second)
        {
            if (first <= 0 || second <= 0 || System.Math.Abs(first - second) >= 0.1)
            {
                _identicalReads = 0;
                return false;
            }

            _identicalReads++;
            if (_identicalReads < ReportAfterReads || _reported) return false;

            _reported = true;
            return true;
        }
    }
}
