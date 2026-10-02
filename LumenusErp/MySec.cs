using System.Security.Cryptography;
using System.Text;

namespace LumenusErp
{
    public class MySec
    {
        private static string _token = "";

        public static void Configure(string? token) => _token = token ?? "";

        public static bool IsValidToken(string token)
        {
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(token))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(_token));
        }
    }
}
