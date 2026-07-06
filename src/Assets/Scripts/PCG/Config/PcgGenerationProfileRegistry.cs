using System;
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// StyleKey -> Profile mapping table.
    /// </summary>
    [CreateAssetMenu(fileName = "PcgGenerationProfileRegistry", menuName = "Matrix/PCG/Generation Profile Registry")]
    public sealed class PcgGenerationProfileRegistry : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Style key, e.g. Style_01 / BioLab / Industrial.")]
            public string StyleKey;

            [Tooltip("Profile asset bound to this style key.")]
            public PcgGenerationProfile Profile;

            [Tooltip("Optional map preview sprite used by MissionSelectWindow and LobbyWindow.")]
            public Sprite PreviewSprite;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetProfile(string styleKey, out PcgGenerationProfile profile, out string reason)
        {
            profile = null;
            if (!TryGetEntry(styleKey, out Entry entry, out reason))
                return false;

            profile = entry.Profile;
            return true;
        }

        public bool TryGetEntry(string styleKey, out Entry match, out string reason)
        {
            match = null;
            reason = string.Empty;

            if (entries == null)
            {
                reason = $"Registry '{name}' entries are null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(styleKey))
            {
                reason = "Style key is empty.";
                return false;
            }

            bool hasDuplicate = false;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!string.Equals((entry.StyleKey ?? string.Empty).Trim(), styleKey.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match == null)
                {
                    match = entry;
                }
                else
                {
                    hasDuplicate = true;
                }
            }

            if (match == null)
            {
                reason = $"Style key '{styleKey}' not found in registry '{name}'.";
                return false;
            }

            if (match.Profile == null)
            {
                reason = $"Style key '{styleKey}' exists in registry '{name}', but profile asset is null.";
                return false;
            }

            if (hasDuplicate)
            {
                reason = $"Style key '{styleKey}' has duplicated entries in registry '{name}'. First valid profile will be used: '{match.Profile.name}'.";
            }

            return true;
        }

        private void OnValidate()
        {
            if (entries == null)
            {
                entries = new List<Entry>();
            }

#if UNITY_EDITOR
            ValidateEntriesInEditor();
#endif
        }

#if UNITY_EDITOR
        private void ValidateEntriesInEditor()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                string key = (entry.StyleKey ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    Debug.LogWarning($"[PCG] Registry '{name}' entry #{i} has empty style key.", this);
                    continue;
                }

                if (!keys.Add(key))
                {
                    Debug.LogError($"[PCG] Registry '{name}' has duplicated style key '{key}'.", this);
                }

                if (entry.Profile == null)
                {
                    Debug.LogWarning($"[PCG] Registry '{name}' style '{key}' has null profile.", this);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.Profile.StyleKey) &&
                    !string.Equals(entry.Profile.StyleKey.Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[PCG] Registry '{name}' entry key '{key}' differs from profile key '{entry.Profile.StyleKey}' on profile '{entry.Profile.name}'.", this);
                }
            }
        }
#endif
    }
}
