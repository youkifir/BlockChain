using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public string ToRowString()
        {
            if(Signature != null)
            {
                return $"{Id} | {From} -> {To} | Amount: {Amount} | Time: {TimeStamp.ToString("O")} {Convert.ToHexString(Signature)}";
            }
            return $"{Id} | {From} -> {To} | Amount: {Amount} | Time: {TimeStamp.ToString("O")} | Fee: {Fee}";
        }
        public byte[] GetDataToSign()
        {
            string raw = $"{Id}{From}{To}{Amount}{TimeStamp:O}";
            return Encoding.UTF8.GetBytes(raw);
        }

        public Transaction(string from, string to, decimal amount, byte[] senderPublicKey)
        {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.UtcNow;
            SenderPublicKey = senderPublicKey;
        }
    }
}
