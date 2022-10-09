using Amazon.DynamoDBv2.DataModel;

namespace RandoSettingsManagerService.Model
{
    [DynamoDBTable("QuickShareSettingsStore")]
    public class SettingsRecord
    {
        [DynamoDBHashKey]
        public string? ID { get; set; }

        public long ExpiryTimestamp { get; set; }
        
        public byte[]? Data { get; set; }
    }
}
    