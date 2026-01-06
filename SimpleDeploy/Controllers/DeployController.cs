using Microsoft.AspNetCore.Mvc;
using SimpleDeploy.Requests;
using SimpleDeploy.Responses;
using System.Net.Mime;

namespace SimpleDeploy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Consumes(MediaTypeNames.Application.Json)]
    public class DeployController : ControllerBase
    {
        private readonly ILogger<DeployController> _logger;
        private readonly Configuration _config;
        private readonly DeploymentQueueService _deploymentQueueService;
        private readonly IWebserverInterface _fetcher;

        public DeployController(ILogger<DeployController> logger, Configuration config, DeploymentQueueService deploymentQueueService, IWebserverInterface fetcher)
        {
            _logger = logger;
            _config = config;
            _deploymentQueueService = deploymentQueueService;
            _fetcher = fetcher;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            return Ok(new StatusResponse { Status = Status.Running });
        }

        [HttpGet("test")]
        public async Task<IActionResult> GetTestAsync()
        {
            var websites = _fetcher.GetWebsites();
            return Ok(new StatusResponse { Status = Status.Running });
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> DeployAsync(DeploymentRequest request)
        {
            try
            {
                _logger.LogInformation($"Connection from {Request.HttpContext.Connection.RemoteIpAddress} ...");

                // validate authentication
                Request.Headers.TryGetValue("X-Username", out var username);
                Request.Headers.TryGetValue("X-Password", out var password);
                Request.Headers.TryGetValue("X-Token", out var token);
                if (!string.IsNullOrEmpty(_config.Username) && !_config.Username.Equals(username))
                {
                    _logger.LogError($"Invalid username '{username}' provided.");
                    return Unauthorized("Invalid username.");
                }
                if (!string.IsNullOrEmpty(_config.Password) && !_config.Password.Equals(password))
                {
                    _logger.LogError($"Invalid password provided.");
                    return Unauthorized("Invalid password.");
                }
                if (!string.IsNullOrEmpty(_config.AuthToken) && !_config.AuthToken.Equals(token))
                {
                    _logger.LogError($"Invalid authentication token provided.");
                    return Unauthorized("Invalid authentication token.");
                }

                if (_config.Websites.Allow.FirstOrDefault() == "*" || _config.Websites.Allow.Contains(request.Website, StringComparer.InvariantCultureIgnoreCase))
                {
                    // website is allowed to be deployed
                }
                else
                {
                    _logger.LogError($"Website {request.Website} not configured for deployment.");
                    return BadRequest($"Website {request.Website} not configured for deployment.");
                }

                // queue files for deployment
                var queueItem = new DeploymentQueueItem
                {
                    JobId = JobIdFactory.Create(),
                    Website = request.Website,
                    DeploymentScript = request.DeploymentScript,
                    AutoCopy = request.AutoCopy,
                    AutoExtract = request.AutoExtract,
                    DateCreated = DateTime.UtcNow,
                };
                _logger.LogInformation($"Deploying website {request.Website} as job '{queueItem.JobId}'");
                foreach (var file in request.Artifacts)
                {
                    var ms = new MemoryStream(); // do not dispose, we need to pass it along
                    await file.CopyToAsync(ms);
                    queueItem.Artifacts.Add(new ArtifactFile
                    {
                        Filename = file.FileName,
                        Data = ms,
                        DateCreated = DateTime.UtcNow
                    });
                }
                var job = await _deploymentQueueService.EnqueueDeploymentAsync(queueItem);
                if (job == null)
                {
                    return BadRequest($"Failed to queue the job '{queueItem.JobId}'.");
                }
                var log = string.Empty;
                if (request.Interactive)
                {
                    var sb = await _deploymentQueueService.WaitForCompletionAsync(job, TimeSpan.FromSeconds(request.InteractiveTimeout));
                    if (sb != null)
                        log = sb.ToString();
                }

                _logger.LogInformation($"Job '{queueItem.JobId}' created for website {request.Website}");
                return Ok(new DeploymentResponse() { IsSuccess = true, Log = log });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deployment request.");
                return StatusCode(500, new DeploymentResponse() { IsSuccess = false, Message = "Internal server error.", Log = ex.Message });
            }
        }
    }
}