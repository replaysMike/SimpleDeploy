
namespace SimpleDeploy
{
    public interface IWebserverInterface
    {
        ServerWebsite? GetWebsite(string website);
        List<ServerWebsite> GetWebsites();

        /// <summary>
        /// Stop a website
        /// </summary>
        /// <param name="website"></param>
        /// <returns></returns>
        bool Stop(string website);

        /// <summary>
        /// Start a website
        /// </summary>
        /// <param name="website"></param>
        /// <returns></returns>
        bool Start(string website);

        /// <summary>
        /// Restart a website
        /// </summary>
        /// <param name="website"></param>
        /// <returns></returns>
        bool Restart(string website);
    }
}