
namespace XsensDOT_Offline_CSV_Processer.Utilities
{
    /// <summary>
    /// Class to provide True chances for the CSV parser
    /// </summary>
    class BooleanSecondChance
    {
        private int _numberOfChancesLeft = 0;
        /// <summary>
        /// Constructor. The number of chances is equivalent to the number of time(s) the method "Feeling Lucky" will fire 'true' after which it will always fire 'false'. To be used in a short-circuit fashion to skip gaps in a CSV file.
        /// </summary>
        public BooleanSecondChance(int numberOfTrueChances)
        {
            this._numberOfChancesLeft = numberOfTrueChances;
        }

        /// <summary>
        /// Default Constructor. The method "Feeling Lucky" will fire 'true' a single time after which it will always fire 'false'. To be used in a short-circuit fashion to skip gaps in a CSV file.
        /// </summary>
        public BooleanSecondChance()
        {
            this._numberOfChancesLeft = 1;
        }

        /// <summary>
        /// 'Feeling Lucky'. Will return 'true' for x times set in the constructor and then indefinitely return 'false'. To be used in a short-circuit fashion to skip gaps in a CSV file.
        /// </summary>
        public bool FeelingLucky()
        {
            if (_numberOfChancesLeft <= 0) return false;
            _numberOfChancesLeft --;
            return true;
        }
    }
}
