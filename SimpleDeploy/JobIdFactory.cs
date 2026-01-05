using System.Security.Cryptography;

namespace SimpleDeploy
{
    public static class JobIdFactory
    {
        private const int Length = 6;
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        /// <summary>
        /// Generate a new job id
        /// </summary>
        /// <returns></returns>
        public static string Create()
        {
            var result = new char[Length];
            using (var rng = RandomNumberGenerator.Create())
            {
                var randomBytes = new byte[Length];
                rng.GetBytes(randomBytes);

                for (var i = 0; i < Length; i++)
                {
                    result[i] = Chars[randomBytes[i] % Chars.Length];
                }
            }
            return new string(result);
        }
    }
}
