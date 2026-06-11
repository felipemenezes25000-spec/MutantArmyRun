using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Persistence.Tests
{
    public class ChecksumTests
    {
        private const string JsonExemplo = "{\"schemaVersion\":1,\"coins\":500}";

        // ---------- Compute ----------

        [Fact]
        public void Compute_RetornaSha256HexDe64Chars()
        {
            string c = SaveChecksum.Compute(JsonExemplo);
            Assert.Equal(64, c.Length);
            Assert.Matches(new Regex("^[0-9a-f]{64}$"), c);
        }

        [Fact]
        public void Compute_EDeterministico()
        {
            Assert.Equal(SaveChecksum.Compute(JsonExemplo), SaveChecksum.Compute(JsonExemplo));
        }

        [Fact]
        public void Compute_MudaQuandoJsonMuda()
        {
            string outro = JsonExemplo.Replace("500", "501");
            Assert.NotEqual(SaveChecksum.Compute(JsonExemplo), SaveChecksum.Compute(outro));
        }

        [Fact]
        public void Compute_UsaSalt_NaoEShaPuroDoJson()
        {
            // SHA256 sem salt seria trivialmente recomputável por quem edita o save na mão
            byte[] puro = SHA256.HashData(Encoding.UTF8.GetBytes(JsonExemplo));
            string puroHex = Convert.ToHexString(puro).ToLowerInvariant();
            Assert.NotEqual(puroHex, SaveChecksum.Compute(JsonExemplo));
        }

        // ---------- Pack ----------

        [Fact]
        public void Pack_FormatoChecksumNewlineJson()
        {
            string payload = SaveChecksum.Pack(JsonExemplo);
            Assert.Equal(SaveChecksum.Compute(JsonExemplo) + "\n" + JsonExemplo, payload);
        }

        // ---------- TryUnpack ----------

        [Fact]
        public void TryUnpack_RoundTrip_RecuperaJsonOriginal()
        {
            string payload = SaveChecksum.Pack(JsonExemplo);
            bool ok = SaveChecksum.TryUnpack(payload, out string json);
            Assert.True(ok);
            Assert.Equal(JsonExemplo, json);
        }

        [Fact]
        public void TryUnpack_JsonComNewlinesInternos_RoundTripIntegro()
        {
            // só o PRIMEIRO '\n' separa checksum de json — o resto pertence ao json
            string jsonMultilinha = "{\n  \"coins\": 500\n}";
            bool ok = SaveChecksum.TryUnpack(SaveChecksum.Pack(jsonMultilinha), out string json);
            Assert.True(ok);
            Assert.Equal(jsonMultilinha, json);
        }

        [Fact]
        public void TryUnpack_JsonAdulterado1Char_RetornaFalse()
        {
            string payload = SaveChecksum.Pack(JsonExemplo);
            // adultera 1 char na porção do json (tampering casual de moedas)
            string adulterado = payload.Substring(0, payload.Length - 2) + "9}";
            Assert.NotEqual(payload, adulterado);
            Assert.False(SaveChecksum.TryUnpack(adulterado, out _));
        }

        [Fact]
        public void TryUnpack_ChecksumAdulterado_RetornaFalse()
        {
            string payload = SaveChecksum.Pack(JsonExemplo);
            char primeiro = payload[0] == 'a' ? 'b' : 'a';
            string adulterado = primeiro + payload.Substring(1);
            Assert.False(SaveChecksum.TryUnpack(adulterado, out _));
        }

        [Fact]
        public void TryUnpack_PayloadSemNewline_RetornaFalseSemLancar()
        {
            Assert.False(SaveChecksum.TryUnpack("payload-sem-quebra-de-linha", out string json));
            Assert.Null(json);
        }

        [Fact]
        public void TryUnpack_PayloadNuloOuVazio_RetornaFalseSemLancar()
        {
            Assert.False(SaveChecksum.TryUnpack(null, out string j1));
            Assert.Null(j1);
            Assert.False(SaveChecksum.TryUnpack("", out string j2));
            Assert.Null(j2);
        }
    }
}
