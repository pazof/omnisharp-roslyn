namespace OmniSharp.Options
{
    public class DnxOptions
    {
        public string Alias { get; set; } = "default";
        public string Projects { get; set; }
        public bool EnablePackageRestore { get; set; } = true;
        public int PackageRestoreTimeout { get; set; } = 15;
    }
}