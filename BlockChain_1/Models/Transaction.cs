using System;
using System.Text;

namespace BlockChain_1.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public decimal Amount { get; set; }
        public DateTime TimeStamp { get; set; }
        public decimal Fee { get; set; }
        public byte[] SenderPublicKey { get; set; }
        public byte[] Signature { get; set; }

        public Transaction(string from, string to, decimal amount, byte[] senderPublicKey)
        {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.UtcNow;
            SenderPublicKey = senderPublicKey;
        }

        public string ToHashString()
        {
            var sig = Signature != null ? Convert.ToHexString(Signature) : string.Empty;
            return $"{Id}|{From}->{To}|{Amount}|{TimeStamp:O}|{Fee}|{sig}";
        }

        public byte[] GetDataToSign()
        {
            string raw = $"{Id}{From}{To}{Amount}{TimeStamp:O}";
            return Encoding.UTF8.GetBytes(raw);
        }
        public int GetSizeInBytes()
        {
            string payload = $"{Id}|{From}->{To}|{Amount}|{TimeStamp:O}|{Fee}";
            return Encoding.UTF8.GetByteCount(payload);
        }
    }
}