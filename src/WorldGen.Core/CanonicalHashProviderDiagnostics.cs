using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
namespace WorldGen.Core
{
    // Explicit runtime smoke test only. Never called by generation/history,
    // never saved, and never an input to historical or playback decisions.
    public static class CanonicalHashProviderDiagnostics
    {
        const string Empty="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        const string Abc="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        const string Long="248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1";
        static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        static void Require(byte[] actual,string expected){if(Hex(actual)!=expected)throw new CryptographicException("Canonical SHA-256 provider failed a known vector.");}
        public static string Verify()
        {
            byte[] abc=Encoding.ASCII.GetBytes("abc"),offset=Encoding.ASCII.GetBytes("!abc?"),longInput=Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq");string backend;
            using(var sha=CanonicalSha256.Create())
            {
                backend=CanonicalSha256.Backend(sha);Require(sha.ComputeHash(Array.Empty<byte>()),Empty);Require(sha.ComputeHash(offset,1,3),Abc);
                sha.Initialize();sha.TransformBlock(longInput,0,13,null,0);sha.TransformBlock(longInput,13,0,null,0);sha.TransformFinalBlock(longInput,13,longInput.Length-13);Require(sha.Hash,Long);
                sha.Initialize();Require(sha.ComputeHash(abc),Abc);Require(sha.ComputeHash(Array.Empty<byte>()),Empty);
            }
            Parallel.For(0,8,i=>{using(var sha=CanonicalSha256.Create()){sha.TransformBlock(abc,0,1,null,0);sha.TransformFinalBlock(abc,1,2);Require(sha.Hash,Abc);}});
            return "PASS 13 SHA-256 provider vectors; "+backend+"; provider opens="+CanonicalSha256.ProviderOpenAttempts;
        }
    }
}
