namespace SimpleDeploy.Cmdlet.Services
{
    public class ArtifactService
    {
        public string StoreFile { get; set; } = string.Empty;
        public List<string> ArtifactStore { get; set; } = new();

        public void CreateOrLoadStore(string store)
        {
            StoreFile = store;
            if (File.Exists(store))
            {
                var artifacts = System.IO.File.ReadAllText(store);
                var artifactList = artifacts.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                ArtifactStore = artifactList;
            }
            else
            {
                ArtifactStore = new();
            }
        }

        public string CreateOrLoadStore(string path, string deploymentName)
        {
            var store = SimpleDeployTools.GetStoreFileLocation(path, deploymentName);
            if (string.IsNullOrEmpty(store))
            {
                // no existing store, create one here
                store = Path.Combine(path, $"{SimpleDeployConstants.StoreFile}{deploymentName.ToLower()}{SimpleDeployConstants.StoreExtension}");
            }
            StoreFile = store;

            CreateOrLoadStore(store);
            return store;
        }

        public bool Remove()
        {
            if (File.Exists(StoreFile))
                System.IO.File.Delete(StoreFile);
            return true;
        }

        public bool AddArtifact(string artifactFile)
        {
            if (string.IsNullOrEmpty(StoreFile)) throw new Exception("Store not loaded!");

            if (ArtifactStore.Contains(artifactFile))
            {
                // already exists
                return false;
            }
            ArtifactStore.Add(artifactFile);
            //System.IO.File.AppendAllText(StoreFile, $"{artifactFile}{Environment.NewLine}");
            return true;
        }

        public bool RemoveArtifact(string artifactFile)
        {
            if (ArtifactStore.Contains(artifactFile))
            {
                return ArtifactStore.Remove(artifactFile);
            }
            return false;
        }

        public bool WriteArtifactDatabase(string storeFile, List<string>? store = null)
        {
            try
            {
                System.IO.File.WriteAllText(storeFile, string.Join(Environment.NewLine, store ?? ArtifactStore));
                return true;
            }
            catch
            {
            }
            return false;
        }
    }
}
