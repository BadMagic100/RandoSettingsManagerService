namespace RandoSettingsManagerService.Model
{
    public class RetrieveSettingsInput
    {
        public string? SettingsKey { get; set; }
    }

    public class RetrieveSettingsOutput
    {
        public bool Found { get; set; } = false;

        /// <summary>
        /// Base64 encoded settings data
        /// </summary>
        public string? Settings { get; set; }
    }
}
