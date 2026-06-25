using System.Security.Cryptography;

namespace BlockChain_1.Models
{
    public class Wallet
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public byte[] PublicKey { get; set; }
        private byte[] PrivateKey { get; set; }

        public Wallet(string name, string address, byte[] publicKey, byte[] privateKey)
        {
            Name = name;
            Address = address;
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        public byte[] Sign(byte[] data)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(PrivateKey, out _);
            return ecdsa.SignData(data, HashAlgorithmName.SHA256);
        }
    }
}