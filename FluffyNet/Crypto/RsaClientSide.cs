using System;
using System.Security.Cryptography;

namespace FluffyNet.Crypto
{
    class RsaClientSide
    {
        public RSAParameters PrivateKey { get; set; }
        public RSAParameters PublicKey { get; set; }

        public string PublicKeyString
        {
            get
            {
                string pubKeyString;
                {
                    //we need some buffer
                    var sw = new System.IO.StringWriter();
                    //we need a serializer
                    var xs = new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters));
                    //serialize the key into the stream
                    xs.Serialize(sw, PublicKey);
                    //get the string from the stream
                    pubKeyString = sw.ToString();
                }

                return pubKeyString;
            }
        }

        private readonly RSACryptoServiceProvider _csp = new RSACryptoServiceProvider(2048);

        /// <summary>
        /// Создает приватные и публичные ключи
        /// </summary>
        public void CreatePrivateAndPublicKeys()
        {
            PrivateKey = _csp.ExportParameters(true);
            PublicKey = _csp.ExportParameters(false);
        }
        
        public RsaClientSide()
        {
            CreatePrivateAndPublicKeys();

            Console.WriteLine("KEY PUB: " + PublicKeyString);
        }

        public byte[] Step2(string encryptedText)
        {
            //first, get our bytes back from the base64 string ...
            var bytesCypherText = Convert.FromBase64String(encryptedText);

            //we want to decrypt, therefore we need a csp and load our private key
            //_csp = new RSACryptoServiceProvider();
            //csp.ImportParameters(privKey);

            //decrypt and strip pkcs#1.5 padding
            var bytesPlainTextData = _csp.Decrypt(bytesCypherText, false);

            return bytesPlainTextData;
        }
    }
}
