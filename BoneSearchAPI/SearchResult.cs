using System.Text.Json.Serialization;

namespace BoneSearchAPI
{
    public class SearchResult
    {
        [JsonInclude]
        public string title { get; set; }
        [JsonInclude]
        public bool https { get; set; }
        [JsonInclude]
        public string domain { get; set; }
        [JsonInclude]
        public string path { get; set; }
        [JsonInclude]
        public string metadesc { get; set; }
        [JsonInclude]
        public string category { get; set; }
    }
}
