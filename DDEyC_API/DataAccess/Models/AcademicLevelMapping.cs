namespace DDEyC_API.Models
{
    public class AcademicLevelMapping
    {
        private readonly Dictionary<string, int> _nameToId = new();
        private readonly Dictionary<int, string> _idToName = new();

        public AcademicLevelMapping(List<AcademicLevelPattern> patterns)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                int id = i + 1; // IDs start at 1
                _nameToId[patterns[i].Level] = id;
                _idToName[id] = patterns[i].Level;
            }
        }

        public int GetId(string name) => _nameToId.TryGetValue(name, out var id) ? id : 0;
        public string GetName(int id) => _idToName.TryGetValue(id, out var name) ? name : string.Empty;
        public int GetMinLevel(IEnumerable<int> levels) => levels.Any() ? levels.Min() : 0;
    }
}