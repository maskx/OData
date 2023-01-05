namespace maskx.OData.Infrastructure
{
    public class BaseProperty
    {
        public Entity Entity { get; internal set; }
        public BaseProperty(string originalName) { this.OriginalName = originalName; }
        public BaseProperty(string name, string originalName)
        {
            this.Name = name;
            this.OriginalName = originalName;
        }
        public string Name { get; internal set; }
        public string OriginalName { get; private set; }
    }
}
