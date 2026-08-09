namespace oojjrs.oplat
{
    public readonly struct PlatformUser
    {
        public string DisplayName { get; }
        public string Id { get; }

        public PlatformUser(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }
}
