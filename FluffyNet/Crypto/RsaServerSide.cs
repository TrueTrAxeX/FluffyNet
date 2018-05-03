using System;
using System.Security.Cryptography;

namespace FluffyNet.Crypto
{
    class RsaServerSide
    {
        public string EncryptedText { get; set; }

        public byte[] GeneratedAeskey { get; set; }

        public RsaServerSide(string publicKeyString)
        {
            //get a stream from the string
            var sr = new System.IO.StringReader(publicKeyString);
            //we need a deserializer
            var xs = new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters));
            //get the object back from the stream
            var pubKey = (RSAParameters)xs.Deserialize(sr);

            // На стороне сервера принимаем публичный ключ клиента и шифруем им дальнейшее сообщение (наш AES-256 ключ)
            var csp = new RSACryptoServiceProvider();
            csp.ImportParameters(pubKey);

            var generatedAesKey = MyAes.GenerateKey();

            GeneratedAeskey = generatedAesKey;

            //for encryption, always handle bytes...
            var bytesPlainTextData = generatedAesKey;

            //apply pkcs#1.5 padding and encrypt our data 
            var bytesCypherText = csp.Encrypt(bytesPlainTextData, false);

            //we might want a string representation of our cypher text... base64 will do
            var cypherText = Convert.ToBase64String(bytesCypherText);

            EncryptedText = cypherText;
        }
    }
}
