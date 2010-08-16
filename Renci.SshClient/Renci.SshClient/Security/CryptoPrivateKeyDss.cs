﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Renci.SshClient.Security
{
    public class CryptoPrivateKeyDss : CryptoPrivateKey
    {
        private byte[] _p;
        private byte[] _q;
        private byte[] _g;
        private byte[] _x;
        private byte[] _publicKey;

        public override string Name
        {
            get { return "ssh-dss"; }
        }

        public override void Load(IEnumerable<byte> data)
        {
            using (var ms = new MemoryStream(data.ToArray()))
            using (var binr = new BinaryReader(ms)) //wrap Memory Stream with BinaryReader for easy reading
            {
                byte bt = 0;
                ushort twobytes = 0;
                int elems = 0;

                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)	//data read as little endian order (actual data order for Sequence is 30 81)
                    binr.ReadByte();	//advance 1 byte
                else if (twobytes == 0x8230)
                    binr.ReadInt16();	//advance 2 bytes
                else
                    throw new InvalidOperationException("Not valid DSS Key.");

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)	//version number
                    throw new NotSupportedException("Not supported DSS Key version.");
                bt = binr.ReadByte();
                if (bt != 0x00)
                    throw new InvalidOperationException("Not valid DSS Key.");

                //------  all private key components are Integer sequences ----
                elems = CryptoPrivateKeyDss.GetIntegerSize(binr);
                this._p = binr.ReadBytes(elems);

                elems = CryptoPrivateKeyDss.GetIntegerSize(binr);
                this._q = binr.ReadBytes(elems);

                elems = CryptoPrivateKeyDss.GetIntegerSize(binr);
                this._g = binr.ReadBytes(elems);

                elems = CryptoPrivateKeyDss.GetIntegerSize(binr);
                this._publicKey = binr.ReadBytes(elems);

                elems = CryptoPrivateKeyDss.GetIntegerSize(binr);
                this._x = binr.ReadBytes(elems);
            }
        }

        public override CryptoPublicKey GetPublicKey()
        {
            return new CryptoPublicKeyDss();
        }

        public override IEnumerable<byte> GetSignature(IEnumerable<byte> key)
        {
            var data = key.ToArray();
            using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
            using (var cs = new System.Security.Cryptography.CryptoStream(System.IO.Stream.Null, sha1, System.Security.Cryptography.CryptoStreamMode.Write))
            {
                DSAParameters DSAKeyInfo = new DSAParameters();

                DSAKeyInfo.X = this._x.TrimLeadinZero().ToArray();
                DSAKeyInfo.P = this._p.TrimLeadinZero().ToArray();
                DSAKeyInfo.Q = this._q.TrimLeadinZero().ToArray();
                DSAKeyInfo.G = this._g.TrimLeadinZero().ToArray();

                cs.Write(data, 0, data.Length);

                cs.Close();

                var DSA = new System.Security.Cryptography.DSACryptoServiceProvider();
                DSA.ImportParameters(DSAKeyInfo);
                var DSAFormatter = new RSAPKCS1SignatureFormatter(DSA);
                DSAFormatter.SetHashAlgorithm("SHA1");

                var signature = DSAFormatter.CreateSignature(sha1);

                return new SignatureKeyData
                {
                    AlgorithmName = this.Name,
                    Signature = signature,
                }.GetBytes();
            }
        }

        public override bool VerifySignature(IEnumerable<byte> hash, IEnumerable<byte> signature)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<byte> GetBytes()
        {
            throw new NotImplementedException();
        }

        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            byte lowbyte = 0x00;
            byte highbyte = 0x00;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02)		//expect integer
                return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();	// data size in next byte
            else
                if (bt == 0x82)
                {
                    highbyte = binr.ReadByte();	// data size in next 2 bytes
                    lowbyte = binr.ReadByte();
                    byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                    count = BitConverter.ToInt32(modint, 0);
                }
                else
                {
                    count = bt;		// we already have the data size
                }

            return count;
        }

    }
}