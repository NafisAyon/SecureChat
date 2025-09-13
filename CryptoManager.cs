using System.Security.Cryptography;
using System.Text;

namespace Chatting
{
    public class CryptoManager
    {
        public RSAParameters RsaPrivateKey;
        public RSAParameters RsaPublicKey;
        public RSAParameters PeerRsaPublicKey;

        private ECDiffieHellmanCng dh;
        private byte[] sharedAesKey;

        public CryptoManager()
        {
            GenerateRsaKeys();
            GenerateDhKeys();
        }

        public void GenerateRsaKeys()
        {
            using (var rsa = RSA.Create(2048))
            {
                RsaPrivateKey = rsa.ExportParameters(true);
                RsaPublicKey = rsa.ExportParameters(false);
            }
        }

        public void GenerateDhKeys()
        {
            dh = new ECDiffieHellmanCng
            {
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            };
        }

        public byte[] GetDhPublicKey() => dh.PublicKey.ToByteArray();

        public void GenerateSharedKey(byte[] peerPubKey)
        {
            using var peerKey = ECDiffieHellmanCngPublicKey.FromByteArray(peerPubKey, CngKeyBlobFormat.EccPublicBlob);
            sharedAesKey = dh.DeriveKeyMaterial(peerKey);
        }

        public byte[] SignData(byte[] data)
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(RsaPrivateKey);
            return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        public bool VerifySignature(byte[] signature, byte[] data)
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(PeerRsaPublicKey);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        public string EncryptMessage(string plaintext)
        {
            if (sharedAesKey == null) return plaintext;

            using var aes = new AesGcm(sharedAesKey);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[16];

            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            byte[] result = new byte[nonce.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

            return Convert.ToBase64String(result);
        }

        public string DecryptMessage(string base64Cipher)
        {
            if (sharedAesKey == null) return base64Cipher;

            try
            {
                byte[] input = Convert.FromBase64String(base64Cipher);
                byte[] nonce = new byte[12];
                byte[] tag = new byte[16];
                byte[] ciphertext = new byte[input.Length - 12 - 16];

                Buffer.BlockCopy(input, 0, nonce, 0, 12);
                Buffer.BlockCopy(input, 12, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(input, 12 + ciphertext.Length, tag, 0, 16);

                byte[] plaintext = new byte[ciphertext.Length];
                using var aes = new AesGcm(sharedAesKey);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                return Encoding.UTF8.GetString(plaintext);
            }
            catch
            {
                return "--- DECRYPTION FAILED ---";
            }
        }

        public string ExportPublicRsa()
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(RsaPublicKey);
            return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        }

        public void ImportPeerRsa(string base64Key)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64Key), out _);
            PeerRsaPublicKey = rsa.ExportParameters(false);
        }
    }
}