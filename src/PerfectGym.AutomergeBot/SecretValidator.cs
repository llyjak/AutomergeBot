using System;
using System.Security.Cryptography;
using System.Text;

namespace PerfectGym.AutomergeBot
{
    /// <summary>
    /// Validates received payload from GitHub and its signature against "secret".
    /// </summary>
    public class SecretValidator
    {
        private readonly string _secret;

        public SecretValidator(string secret)
        {
            _secret = secret;
        }
        public bool VerifySecret(string requestSignature, string requestPayload)
        {
            if (requestSignature.StartsWith("sha1=", StringComparison.OrdinalIgnoreCase))
            {
                var signature = requestSignature.Substring("sha1=".Length);
                var secretBytes = Encoding.UTF8.GetBytes(_secret);
                var payloadBytes = Encoding.UTF8.GetBytes(requestPayload);

                using (var hmSha1 = new HMACSHA1(secretBytes))
                {
                    var hash = hmSha1.ComputeHash(payloadBytes);
                    var hashString = ToHexString(hash);
                    if (hashString.Equals(signature))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string ToHexString(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.AppendFormat("{0:x2}", b);
            }
            return builder.ToString();
        }
    }
}