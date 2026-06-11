using System.Security.Cryptography;
using System.Text;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Checksum do save (doc 12 §4.7): SHA256(json + SALT) em hex minúsculo.
    /// Barra tampering casual de moedas — não é segurança criptográfica de verdade
    /// (o SALT está no binário), e sim um custo extra para edição manual do arquivo.
    /// Formato do payload: linha 1 = checksum, resto = json ("&lt;checksum&gt;\n&lt;json&gt;").
    /// </summary>
    public static class SaveChecksum
    {
        // Trocar o SALT invalida todos os saves existentes — NUNCA mudar após o launch.
        private const string Salt = "MAR-save-v1-9f2c6b71";

        public static string Compute(string json)
        {
            // SHA256.Create + loop hex: APIs disponíveis no netstandard2.1 do Unity 2022
            // (HashData/ToHexString são .NET 5+, não existem lá).
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json + Salt));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string Pack(string json)
        {
            return Compute(json) + "\n" + json;
        }

        public static bool TryUnpack(string payload, out string json)
        {
            json = null;
            if (string.IsNullOrEmpty(payload)) return false;

            // Só o PRIMEIRO '\n' separa checksum de json; newlines seguintes pertencem ao json.
            int split = payload.IndexOf('\n');
            if (split < 0) return false;

            string checksum = payload.Substring(0, split);
            string body = payload.Substring(split + 1);
            if (Compute(body) != checksum) return false;

            json = body;
            return true;
        }
    }
}
