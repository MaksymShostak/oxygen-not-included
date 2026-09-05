#nullable enable

namespace DeliveryTemperatureLimit
{
    internal sealed class OptionsEditSession
    {
        private readonly DeliveryTemperatureOptionsStore store;
        private DeliveryTemperatureLimitOptions saved;
        internal DeliveryTemperatureOptionsStore.Snapshot Snapshot { get; private set; }
        internal string DisplayUnit { get; }
        internal ConstructionTemperatureDraft Range { get; }
        internal bool CheckWarnings { get; set; }
        internal bool LimitConstruction { get; set; }
        internal bool NeedsDisambiguation { get; private set; }

        internal OptionsEditSession(DeliveryTemperatureOptionsStore store)
        {
            this.store = store;
            DisplayUnit = OptionsTemperatureUnits.Current;
            Snapshot = store.Read();
            saved = store.Values(Snapshot, DisplayUnit);
            CheckWarnings = saved.CheckTemperatureForStatusItems;
            LimitConstruction = saved.UnderConstructionLimit;
            Range = new ConstructionTemperatureDraft(saved.MinConstructionTemperature,
                saved.MaxConstructionTemperature,
                value => OptionsTemperatureUnits.ToKelvin(value, DisplayUnit),
                value => OptionsTemperatureUnits.Format(value, DisplayUnit));
            // Disambiguation is only needed for custom non-default legacy limits when active.
            NeedsDisambiguation = Snapshot.IsLegacy && !Snapshot.IsDefaultLegacy && LimitConstruction;
        }

        internal bool IsDirty
        {
            get
            {
                if (Snapshot.IsLegacy || Range.IsDirty ||
                    CheckWarnings != saved.CheckTemperatureForStatusItems ||
                    LimitConstruction != saved.UnderConstructionLimit) return true;
                // An invalid existing file is not an unsaved user edit.
                return Range.Validate(out int low, out int high) == ConstructionRangeError.None &&
                    (low != saved.MinConstructionTemperature || high != saved.MaxConstructionTemperature);
            }
        }

        internal bool RestartPending =>
            !saved.HasSameValues(DeliveryTemperatureLimitOptions.Instance);

        internal void ConfirmLegacyUnit(string unit)
        {
            var interpreted = store.Values(Snapshot, unit);
            Range.Reset(interpreted.MinConstructionTemperature,
                interpreted.MaxConstructionTemperature);
            NeedsDisambiguation = false;
        }

        internal void DismissDisambiguation()
        {
            NeedsDisambiguation = false;
        }

        internal void RestoreDefaults()
        {
            var defaults = new DeliveryTemperatureLimitOptions();
            CheckWarnings = defaults.CheckTemperatureForStatusItems;
            LimitConstruction = defaults.UnderConstructionLimit;
            Range.Reset(defaults.MinConstructionTemperature,
                defaults.MaxConstructionTemperature);
            NeedsDisambiguation = false;
        }

        internal ConstructionRangeError Save(out bool written)
        {
            written = false;
            ConstructionRangeError error = Range.Validate(out int low, out int high);
            if (error != ConstructionRangeError.None) return error;
            var candidate = new DeliveryTemperatureLimitOptions
            {
                CheckTemperatureForStatusItems = CheckWarnings,
                UnderConstructionLimit = LimitConstruction,
                MinConstructionTemperature = low,
                MaxConstructionTemperature = high
            };
            if (Snapshot.IsLegacy || !candidate.HasSameValues(saved))
            {
                Snapshot = store.Save(Snapshot, candidate);
                written = true;
            }
            saved = candidate;
            Range.Reset(low, high);
            NeedsDisambiguation = false;
            return ConstructionRangeError.None;
        }

        internal DeliveryTemperatureLimitOptions SavedCopy() => saved.Copy();
    }
}
