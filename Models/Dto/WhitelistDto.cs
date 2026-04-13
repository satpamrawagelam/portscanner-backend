namespace portscanner_backend.Models.Dto
{
    public class WhitelistUpdateDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public List<int> WhitelistedPorts { get; set; } = new List<int>();
    }
}
