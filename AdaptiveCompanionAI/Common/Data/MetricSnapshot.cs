using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace AdaptiveCompanionAI.Common.Data
{
    public sealed class MetricSnapshot
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();
        private readonly IReadOnlyList<MetricDescriptor> _descriptors;

        public MetricSnapshot(IReadOnlyList<MetricDescriptor> descriptors)
        {
            _descriptors = descriptors;
            Reset();
        }

        public IReadOnlyList<MetricDescriptor> Descriptors => _descriptors;

        public double Get(string key)
        {
            return _values.TryGetValue(key, out double value) ? value : 0d;
        }

        public void Set(string key, double value)
        {
            _values[key] = value;
        }

        public void Increment(string key, double delta = 1d)
        {
            _values[key] = Get(key) + delta;
        }

        public void Reset()
        {
            _values.Clear();
            foreach (MetricDescriptor descriptor in _descriptors)
            {
                _values[descriptor.Key] = 0d;
            }
        }

        public TagCompound SaveTag()
        {
            TagCompound tag = new TagCompound();
            foreach (KeyValuePair<string, double> pair in _values)
            {
                tag.Add(pair.Key, pair.Value);
            }

            return tag;
        }

        public void LoadTag(TagCompound tag)
        {
            Reset();
            foreach (MetricDescriptor descriptor in _descriptors)
            {
                _values[descriptor.Key] = tag.GetDouble(descriptor.Key);
            }
        }
    }
}
