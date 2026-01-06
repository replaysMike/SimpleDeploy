using NetTools;
using System.Net;

namespace SimpleDeploy.Middleware
{
    public class IpRestrictionMiddleware
    {
        private readonly ILogger<IpRestrictionMiddleware> _logger;
        private readonly RequestDelegate _next;
        private readonly Configuration _config;

        public IpRestrictionMiddleware(RequestDelegate next, Configuration config, ILogger<IpRestrictionMiddleware> logger)
        {
            _next = next;
            _config = config;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIpAddress = context.Connection.RemoteIpAddress;
            // allow serving of the root webpage
            if (context.Request.Path != "/" && context.Request.Path != "/index.html")
            {
                if (!IsAllowed(remoteIpAddress))
                {
                    _logger.LogWarning($"Forbidden request from IP: {remoteIpAddress} (ipv4: {remoteIpAddress?.MapToIPv4()})");
                    // If the IP is not allowed, return a 403 Forbidden response
                    context.Response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
                    return;
                }
            }

            await _next(context);
        }

        private bool IsAllowed(IPAddress? ipAddress)
        {
            if (ipAddress == null)
                return false;
            if (!_config.IpWhitelist.Any())
                return true;
            var allowedIps = _config.IpWhitelist;
            var ipRanges = new List<IPAddressRange>();
            foreach (var range in allowedIps)
            {
                var ipRange = IPAddressRange.Parse(range);
                ipRanges.Add(ipRange);
            }

            // determine if this ip is defined in the allowed list
            var ipv4Address = ipAddress.MapToIPv4();
            var isAllowed = false;
            // enforce this setting via the appsettings
            //if (IPAddress.IsLoopback(ipAddress)) 
            //    isAllowed = true;
            if (!isAllowed)
                isAllowed = ipRanges.Any(x => x.Contains(ipv4Address));

            return isAllowed;
        }
    }
}
