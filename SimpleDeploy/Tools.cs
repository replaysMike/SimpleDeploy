namespace SimpleDeploy
{
    public static class Tools
    {
        public static string DisplayFileSize(long length)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (length == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(length);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(length) * num).ToString() + suf[place];
        }

        public static long CheckAvailableSpace(string folderPath)
        {
            try
            {
                // Get the drive information from the folder path
                DriveInfo driveInfo = new DriveInfo(folderPath);

                if (driveInfo.IsReady)
                {
                    long availableFreeSpaceBytes = driveInfo.AvailableFreeSpace;
                    return availableFreeSpaceBytes;
                }
            }
            catch (Exception) { }
            return 0;
        }
    }
}
