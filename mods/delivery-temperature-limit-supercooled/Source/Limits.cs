using KSerialization;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    public class TemperatureLimit : KMonoBehaviour
    {
        [Serialize]
        [SerializeField] // needed so that making copies of an instance copies the private field too
        private int lowLimit = 0; // 0 Kelvin

        [Serialize]
        [SerializeField]
        private int highLimit = 0; // if 0, then not active

        public const int MinValue = 0;

        public const int MaxValue = 5000; // diamond melts at ~4200K

        public bool IsDisabled() => ( highLimit <= 0 );
        public int LowLimit => lowLimit;
        public int HighLimit => highLimit;

        // GetComponent() calls may add up being somewhat expensive when called often,
        // so instead cache the mapping using the GameObject's Instance ID (to avoid C++ native transition overhead).
        private static Dictionary< int, TemperatureLimit > fastMap
            = new Dictionary< int, TemperatureLimit >();

        public static TemperatureLimit Get( GameObject gameObject )
        {
            if (gameObject == null)
                return null;
            if( fastMap.TryGetValue( gameObject.GetInstanceID(), out TemperatureLimit limit ))
                return limit;
            return null;
        }

        private static readonly EventSystem.IntraObjectHandler<TemperatureLimit> OnCopySettingsDelegate
            = new EventSystem.IntraObjectHandler<TemperatureLimit>(delegate(TemperatureLimit component, object data)
        {
            component.OnCopySettings(data);
        });

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate);
        }

        private void OnCopySettings(object data)
        {
            CopySettings( TemperatureLimit.Get((GameObject)data));
        }

        public void CopySettings( TemperatureLimit source )
        {
            if (source != null)
            {
                lowLimit = source.lowLimit;
                highLimit = source.highLimit;
                SetDirty();
            }
        }

        public void SetLowLimit(int value)
        {
            lowLimit = Math.Max( Math.Min( value, MaxValue ), MinValue );
            SetDirty();
        }

        public void SetHighLimit(int value)
        {
            highLimit = Math.Max( Math.Min( value, MaxValue ), MinValue );
            SetDirty();
        }

        public void Disable()
        {
            highLimit = 0;
            SetDirty();
        }

        public bool AllowedByTemperature( float temperature )
        {
            if( highLimit == 0 ) // limit disabled
                return true;
            int t = (int) temperature;
            return lowLimit <= t && t < highLimit;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            fastMap[ gameObject.GetInstanceID() ] = this;
            allLimits.Add( this );
            SetDirty();
        }

        protected override void OnCleanUp()
        {
            allLimits.Remove( this );
            SetDirty();
            fastMap.Remove( gameObject.GetInstanceID() );
            base.OnCleanUp();
        }

        // Some game code groups resources by their tag (material) and then processes
        // such groups as a whole. That wouldn't work with temperature limits, as resources
        // with different temperatures may need different handling. But grouping by each
        // existing temperature would be expensive, and some of this code is called very
        // often, so instead only group them by existing limits: For example if there
        // is storage #1 with limit 10C-30C and storage #2 with limit 20C-30C, then it's
        // enough to group by min-10C, 10C-20C, 20C-30C and 30C-max, as e.g. 15C is different
        // from 25C (accepted vs not accepted by #2), but 21C vs 29C doesn't matter. For that
        // reason this code collects all such groups and gives them integer indexes,
        // which is then easier and faster to handle.
        // Additional complication is that some of the relevant game code is run in threads,
        // so this needs to be thread-safe. This is handled by the hot code being lockless
        // if possible, only checking one volatile atomic flag and then accessing pre-built data.
        // If something changes, the flag is set, and then the data is rebuilt with a lock
        // held. Race conditions in the lockless code don't matter, the code is called repeatedly
        // (temperatures change over time), so if it works with data that is out of date,
        // it'll be updated again later.
        private static object lockObject = new object();
        private static List< TemperatureLimit > allLimits = new List< TemperatureLimit >();
        private volatile static bool limitsDirty = true;
        // The temperature index data that are updated and used publicly. They are in one object
        // so that they are atomically updated together. Code reading them should get a reference
        // to the object and then use that for all operations.
        public class TemperatureIndexData
        {
            // A list of temperature values where groups end (in the example above, this would
            // be { 10, 20, 30, max }.
            private List< int > indexTemperatures;
            public List< int > IndexTemperatures => indexTemperatures;
            // The inverse of indexTemperatures. There are only 5000 (MinValue <= t < MaxValue) integer
            // temperatures, and TemperatureIndex() seems to be called for hot code, so replace
            // lookup in a loop with O(1) indexing.
            private System.Int16[] temperaturesToIndex;

            public TemperatureIndexData( List< int > indexTemperatures, System.Int16[] temperaturesToIndex )
            {
                this.indexTemperatures = indexTemperatures;
                this.temperaturesToIndex = temperaturesToIndex;
            }

            private int SafeGetIndex( int temperature )
            {
                if( temperature < MinValue )
                    return 0;
                if( temperature >= MaxValue )
                    return indexTemperatures.Count - 1;
                return temperaturesToIndex[ temperature ];
            }

            public int TemperatureIndex( float temperature )
            {
                return SafeGetIndex( (int) temperature );
            }

            public int TemperatureIndexCount()
            {
                return indexTemperatures.Count;
            }


            // The range end is exclusive.
            public (int, int) TemperatureIndexes( TemperatureLimit limit )
            {
                if( limitsDirty )
                    UpdateIndexes();
                int lowIndex = SafeGetIndex( limit.lowLimit );
                int highIndex = indexTemperatures.Count; // one beyond last
                if( limit.highLimit < MaxValue )
                    highIndex = SafeGetIndex( limit.highLimit );
                return ( lowIndex, highIndex );
            }
        };

        private static TemperatureIndexData temperatureIndexData;

        public static TemperatureIndexData getTemperatureIndexData()
        {
            if( limitsDirty )
                UpdateIndexes();
            return temperatureIndexData;
        }

        private static void SetDirty()
        {
            lock( lockObject )
            {
                limitsDirty = true;
            }
        }

        private static void UpdateIndexes()
        {
            lock( lockObject )
            {
                if( !limitsDirty )
                    return;
                limitsDirty = false;
                List< int > tmp = new List< int >();
                tmp.Add( TemperatureLimit.MaxValue );
                foreach( TemperatureLimit limit in allLimits )
                {
                    if( !limit.IsDisabled())
                    {
                        tmp.Add( Math.Max( MinValue, Math.Min( limit.LowLimit, MaxValue ) ) );
                        tmp.Add( Math.Max( MinValue, Math.Min( limit.HighLimit, MaxValue ) ) );
                    }
                }
                tmp.Sort();
                tmp = tmp.Distinct().ToList();
                if( temperatureIndexData == null || !tmp.SequenceEqual( temperatureIndexData.IndexTemperatures ))
                {
                    System.Int16[] newTemperaturesToIndex = new System.Int16[ MaxValue - MinValue ];
                    int pos = 0;
                    for( System.Int16 i = 0; i < tmp.Count; ++i )
                    {
                        int value = tmp[ i ];
                        while( pos < value )
                            newTemperaturesToIndex[ pos++ ] = i;
                    }
                    // Change the reference, which is atomic in C#.
                    temperatureIndexData = new TemperatureIndexData( tmp, newTemperaturesToIndex );
                }
            }
        }

        protected override void OnLoadLevel()
        {
            // It appears that re-loading a save does not properly clean up objects, clean up.
            base.OnLoadLevel();
            fastMap.Clear();
            allLimits.Clear();
            SetDirty();
        }
    }
}
