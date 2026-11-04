using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    [FilePath("Library/CGCombatFramework/UnitScan.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class UnitScanCache : ScriptableSingleton<UnitScanCache>
    {
        [SerializeField] private string script = string.Empty;
        [SerializeField] private List<string> guids = new();
        [SerializeField] private List<string> hashes = new();
        [SerializeField] private List<bool> units = new();

        private Dictionary<string, int> index;

        public string Script => script;
        public bool Dirty { get; private set; }

        public bool TryGet(string guid, string hash, out bool isUnit)
        {
            isUnit = false;
            if(!Index().TryGetValue(guid, out int at) || hashes[at] != hash) return false;
            isUnit = units[at];
            return true;
        }

        public void Set(string guid, string hash, bool isUnit)
        {
            if(string.IsNullOrEmpty(guid)) return;
            if(Index().TryGetValue(guid, out int at))
            {
                if(hashes[at] == hash && units[at] == isUnit) return;
                hashes[at] = hash;
                units[at] = isUnit;
            }
            else
            {
                index[guid] = guids.Count;
                guids.Add(guid);
                hashes.Add(hash);
                units.Add(isUnit);
            }
            Dirty = true;
        }

        public void Keep(HashSet<string> seen)
        {
            int before = guids.Count;
            for(int i = guids.Count - 1; i >= 0; i--)
            {
                if(seen.Contains(guids[i])) continue;
                guids.RemoveAt(i);
                hashes.RemoveAt(i);
                units.RemoveAt(i);
            }
            if(guids.Count == before) return;
            index = null;
            Dirty = true;
        }

        public void Reset(string scriptGuid)
        {
            script = scriptGuid ?? string.Empty;
            guids.Clear();
            hashes.Clear();
            units.Clear();
            index = null;
            Dirty = true;
        }

        public void Persist()
        {
            if(!Dirty) return;
            Dirty = false;
            Save(false);
        }

        private Dictionary<string, int> Index()
        {
            if(index != null) return index;
            index = new Dictionary<string, int>(guids.Count);
            for(int i = 0; i < guids.Count; i++)
            {
                index[guids[i]] = i;
            }
            return index;
        }
    }
}
