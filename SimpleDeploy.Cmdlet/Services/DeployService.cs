using SimpleDeploy.Responses;
using System.Text.Json;

namespace SimpleDeploy.Cmdlet.Services
{
    public class DeployService
    {
        public void Deploy(ArtifactService artifactService, string website, string? deploymentScript, string host, string username, string password, string token, int port, int timeout, int requestTimeout, bool autoCopy, bool autoExtract, bool ignoreCert, Action<string> onVerbose, Action<string> onWarning)
        {
            // submit a POST request to the deployment server
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(website), "Website");
            form.Add(new StringContent(deploymentScript ?? string.Empty), "DeploymentScript");
            form.Add(new StringContent(autoCopy.ToString()), "AutoCopy");
            form.Add(new StringContent(autoExtract.ToString()), "AutoExtract");

            foreach (var artifact in artifactService.ArtifactStore)
            {
                if (string.IsNullOrWhiteSpace(artifact))
                    continue;
                var filePath = Path.GetFullPath(artifact);
                onVerbose($"Adding artifact file: {filePath}");
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    if (File.Exists(filePath))
                    {
                        var fileStream = File.OpenRead(filePath);
                        var streamContent = new StreamContent(fileStream);

                        form.Add(streamContent, "Artifacts", Path.GetFileName(filePath));
                    }
                    else
                    {
                        onWarning($"Artifact '{filePath}' does not exist!");
                        return;
                    }
                }
            }

            try
            {
                // we can't run this in an async task due to the use of WriteWarning methods restricting the method usage
                HttpResponseMessage? response = null;
                var handler = new SocketsHttpHandler
                {
                    // connect timeout (5 sec)
                    ConnectTimeout = TimeSpan.FromSeconds(timeout),
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            if (ignoreCert)
                                return true; // ignore SSL errors
                            return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                        }
                    }
                };
                var client = new HttpClient(handler)
                {
                    // message timeout (5 min)
                    Timeout = TimeSpan.FromSeconds(requestTimeout)
                };
                if (!string.IsNullOrEmpty(username))
                    client.DefaultRequestHeaders.Add("X-Username", username);
                if (!string.IsNullOrEmpty(password))
                    client.DefaultRequestHeaders.Add("X-Password", password);
                if (!string.IsNullOrEmpty(token))
                    client.DefaultRequestHeaders.Add("X-Token", token);

                var uri = host.Contains("http") ? new Uri(host) : new Uri($"https://{host}:{port}/deploy");
                onVerbose($"Submitting to {uri}");
                try
                {
                    response = client.PostAsync(uri, form).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    onVerbose($"Exception: [{ex.GetType()}] {ex.GetBaseException().Message}");
                    if ((ex is TaskCanceledException || ex is HttpRequestException)
                        && ex.GetBaseException().Message.Contains("timeout", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // try http
                        if (uri.Scheme == Uri.UriSchemeHttps)
                        {
                            try
                            {
                                uri = new Uri($"http://{host}:{port}/deploy");
                                onVerbose($"Unable to connect, trying http...");
                                client = new HttpClient()
                                {
                                    Timeout = TimeSpan.FromSeconds(timeout)
                                };
                                client.DefaultRequestHeaders.Add("X-Username", username);
                                client.DefaultRequestHeaders.Add("X-Password", password);
                                client.DefaultRequestHeaders.Add("X-Token", token);
                                response = client.PostAsync(uri, form).GetAwaiter().GetResult();
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine($"Exception: [{ex2.GetType()}] {ex2.GetBaseException().Message}");
                                if ((ex2 is TaskCanceledException || ex2 is HttpRequestException) 
                                    && ex2.GetBaseException().Message.Contains("timeout", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    onWarning($"Timeout exceeded, could not connect.");
                                    return;
                                }
                                else
                                {
                                    if (ex2.GetBaseException().Message.Contains("certificate was rejected"))
                                        onWarning($"Failed to submit deployment due to failed SSL certificate check. Try using the --ignorecert option.");
                                    else
                                        onWarning($"Failed to connect to {uri}: {ex2.GetType()}:{ex2.GetBaseException().Message}");
                                    return;

                                }
                            }
                        }
                        else
                        {
                            // could not connect
                            onWarning($"Timeout exceeded, could not connect.");
                            return;
                        }
                    }
                    else
                    {
                        if (ex.GetBaseException().Message.Contains("certificate was rejected"))
                            onWarning($"Failed to submit deployment due to failed SSL certificate check. Try using the --ignorecert option.");
                        else
                            onWarning($"Https Post exception! {ex.GetType()}: {ex.GetBaseException().Message}");
                        return;
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(responseBody))
                    {
                        onWarning($"No response received from server. Status Code: {response.StatusCode}, Content-Length: {responseBody.Length}");
                        return;
                    }
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    };
                    var body = JsonSerializer.Deserialize<DeploymentResponse>(responseBody, options);
                    if (body?.IsSuccess == true)
                    {
                        Console.WriteLine($"Deployment successful for {website}.");
                        // clear the local artifact store on successful deployment
                        artifactService.Remove();
                    }
                    else
                    {
                        onWarning($"Failed to deploy for {website}. Http Code: {response.StatusCode} Response: {responseBody}");
                    }
                }
                else
                {
                    switch(response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.Unauthorized:
                            onWarning($"Unauthorized! Invalid username, password, or token.");
                            break;
                        case System.Net.HttpStatusCode.BadRequest:
                            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            onWarning($"Bad Request! {responseBody}");
                            break;
                        case System.Net.HttpStatusCode.Forbidden:
                            onWarning($"Forbidden. Your IP address is not allowed to connect to this server.");
                            break;
                        case System.Net.HttpStatusCode.NotFound:
                            onWarning($"Deployment endpoint not found at {uri}. Ensure the deployment server is running and the URL is correct.");
                            return;
                        case System.Net.HttpStatusCode.RequestTimeout:
                            onWarning($"Request timed out when connecting to {uri}. The server may be busy or unreachable.");
                            return;
                        default:
                            onWarning($"Failed to deploy for {website}. Http Code: {response.StatusCode}");
                            break;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
