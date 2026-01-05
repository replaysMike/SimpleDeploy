namespace SimpleDeploy.Cmdlet
{
    public static class SimpleDeployTools
    {
        public static string GetStoreFileLocation(string startPath, string website)
        {
            var storefile = $"{SimpleDeployConstants.StoreFile}{website.ToLower()}{SimpleDeployConstants.StoreExtension}";
            var directory = new DirectoryInfo(startPath);
            while (directory.Parent != null)
            {
                var path = directory.FullName;
                var store = Path.Combine(path, storefile);
                if (File.Exists(store))
                    return store;
                directory = directory.Parent;
            }
            return string.Empty;
        }
    }
}
