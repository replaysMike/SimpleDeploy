namespace SimpleDeploy
{
    /// <summary>
    /// For now we are using a very rudinmentary authentication service.
    /// This needs to be converted to use json web tokens (JWT) and support public key authentication.
    /// </summary>
    public class AuthenticatorService
    {
        private readonly Configuration _config;

        public AuthenticatorService(Configuration config)
        {
            _config = config;
        }

        public bool IsAuthenticated(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (string.IsNullOrWhiteSpace(password)) return false;

            if (!string.IsNullOrWhiteSpace(_config.Username) && _config.Username.Equals(username)
                && !string.IsNullOrWhiteSpace(_config.Password) && _config.Password.Equals(password))
                return true;

            return false;
        }

        public bool IsAuthenticated(string token)
        {
            if (!string.IsNullOrWhiteSpace(_config.AuthToken) && _config.AuthToken.Equals(token))
                return true;
            return false;
        }
    }
}
