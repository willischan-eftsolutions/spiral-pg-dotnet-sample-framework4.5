using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Web.Script.Serialization;

namespace dotnet_framework_spiral_checkout_sample
{
    // This example is working on .net framework 4.5
    class Program
    {
        const String clientId = "000000000000001";
        const String clientPrivateKey = @"C:\cert\01pri.xml";
        const String serverPublicKey = @"C:\cert\01pub.xml";

        static String RSA_Sha256_Signature(String data, String fileName)
        {
            // Using XML format, there are online tool available to convert pem into xml format
            // e.g.: https://the-x.cn/en-us/certificate/PemToXml.aspx
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(File.ReadAllText(fileName));

            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] binData = encoder.GetBytes(data);
            byte[] binSignature = rsa.SignData(binData, CryptoConfig.CreateFromName("SHA256"));
            return Convert.ToBase64String(binSignature);
        }

        static bool RSA_Sha256_Verify(String data, String signature, String fileName)
        {
            // Using XML format, there are online tool available to convert pem into xml format
            // e.g.: https://the-x.cn/en-us/certificate/PemToXml.aspx
            RSACryptoServiceProvider rsa =  new RSACryptoServiceProvider();
            rsa.FromXmlString(File.ReadAllText(fileName));

            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] binData = encoder.GetBytes(data);
            byte[] binSignature = Convert.FromBase64String(signature);
            return rsa.VerifyData(binData, CryptoConfig.CreateFromName("SHA256"), binSignature);
        }

        static async System.Threading.Tasks.Task<string> webAPIRequestAsync()
        {
            // Set time and merchant ref
            DateTime dt = DateTime.Now;
            DateTime utcTime = dt.ToUniversalTime();

            String timeString = string.Format("{0}Z", utcTime.ToString("s"));
            Console.WriteLine("ISO Time: " + timeString);

            String merchantRef = utcTime.Ticks.ToString();
            Console.WriteLine("Merchant Ref: " + merchantRef);

            // construct the body
            var bodydata = new
            {
                clientId = clientId,
                cmd = "SALESESSION",
                type = "VM",
                amt = 0.1,
                merchantRef = merchantRef,
                channel = "WEB",
                successUrl = "https://www.google.com",
                failureUrl = "https://www.google.com",
                webhookUrl = "https://www.google.com",
                goodsName = "Testing Goods"
            };
            //string body = JsonSerializer.Serialize(bodydata);
            string body = new JavaScriptSerializer().Serialize(bodydata);
            Console.WriteLine("Body: " + body);

            // calculate header
            String signature = RSA_Sha256_Signature(clientId + merchantRef + timeString, clientPrivateKey);
            Console.WriteLine("Signature: " + signature);

            // construct HTTP client 
            HttpClient httpClient = new HttpClient();
            var requestMessage = new HttpRequestMessage(HttpMethod.Put, "https://cjpazdufok.execute-api.ap-east-1.amazonaws.com/v1/merchants/" + clientId + "/transactions/" + merchantRef);
            requestMessage.Headers.Clear();
            requestMessage.Headers.Add("Spiral-Request-Datetime", timeString);
            requestMessage.Headers.Add("Spiral-Client-Signature", signature);
            requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // send and receive
            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

            Console.WriteLine("Status code: " + response.StatusCode);

            // verify signature
            signature = response.Headers.GetValues("Spiral-Server-Signature").FirstOrDefault();
            String signData = clientId + merchantRef + response.Headers.GetValues("Spiral-Request-Datetime").FirstOrDefault();
            if (RSA_Sha256_Verify(signData, signature, serverPublicKey))
                Console.WriteLine("Server signature verified!");
            else
                Console.WriteLine("Server signature verification failed");

            string result = await response.Content.ReadAsStringAsync();

            return result;
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // Generate Signature
            Console.WriteLine("Client ID: " + clientId);

            String response = webAPIRequestAsync().GetAwaiter().GetResult();

            Console.WriteLine("Response: " + response);

            Console.WriteLine("Bye World!");
        }
    }
}
