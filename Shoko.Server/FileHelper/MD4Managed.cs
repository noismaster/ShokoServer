﻿using System;
using System.Security.Cryptography;
using Shoko.Server.Utilities;

namespace Shoko.Server.FileHelper;

public abstract class MD4 : HashAlgorithm
{
    protected MD4()
    {
        // MD4 hash length are 128 bits long
        HashSizeValue = 128;
    }

    public new static MD4 Create()
    {
        // for this to work we must register ourself with CryptoConfig
        return Create("MD4");
    }

    public new static MD4 Create(string hashName)
    {
        var obj = CryptoConfig.CreateFromName(hashName);
        // in case machine.config isn't configured to use any MD4 implementation
        if (obj == null || Utils.IsRunningOnLinuxOrMac())
        {
            obj = new MD4Managed();
        }

        return (MD4)obj;
    }
}

public class MD4Managed : MD4
{
    private readonly uint[] _state;
    private readonly byte[] _buffer;
    private readonly uint[] _count;
    private readonly uint[] _x;
    private readonly byte[] _digest;

    private const int S11 = 3;
    private const int S12 = 7;
    private const int S13 = 11;
    private const int S14 = 19;
    private const int S21 = 3;
    private const int S22 = 5;
    private const int S23 = 9;
    private const int S24 = 13;
    private const int S31 = 3;
    private const int S32 = 9;
    private const int S33 = 11;
    private const int S34 = 15;

    //--- constructor -----------------------------------------------------------

    public MD4Managed()
    {
        // we allocate the context memory
        _state = new uint[4];
        _count = new uint[2];
        _buffer = new byte[64];
        _digest = new byte[16];
        // temporary buffer in MD4Transform that we don't want to keep allocate on each iteration
        _x = new uint[16];
        // the initialize our context
        Initialize();
    }

    public override void Initialize()
    {
        _count[0] = 0;
        _count[1] = 0;
        _state[0] = 0x67452301;
        _state[1] = 0xefcdab89;
        _state[2] = 0x98badcfe;
        _state[3] = 0x10325476;
        // Zeroize sensitive information
        Array.Clear(_buffer, 0, 64);
        Array.Clear(_x, 0, 16);
    }

    protected override void HashCore(byte[] array, int ibStart, int cbSize)
    {
        /* Compute number of bytes mod 64 */
        var index = (int)((_count[0] >> 3) & 0x3F);
        /* Update number of bits */
        _count[0] += (uint)(cbSize << 3);
        if (_count[0] < cbSize << 3)
        {
            _count[1]++;
        }

        _count[1] += (uint)(cbSize >> 29);

        var partLen = 64 - index;
        var i = 0;
        /* Transform as many times as possible. */
        if (cbSize >= partLen)
        {
            //MD4_memcpy((POINTER)&context->buffer[index], (POINTER)input, partLen);
            Buffer.BlockCopy(array, ibStart, _buffer, index, partLen);
            MD4Transform(_state, _buffer, 0);

            for (i = partLen; i + 63 < cbSize; i += 64)
            {
                // MD4Transform (context->state, &input[i]);
                MD4Transform(_state, array, i);
            }

            index = 0;
        }

        /* Buffer remaining input */
        //MD4_memcpy ((POINTER)&context->buffer[index], (POINTER)&input[i], inputLen-i);
        Buffer.BlockCopy(array, ibStart + i, _buffer, index, cbSize - i);
    }

    protected override byte[] HashFinal()
    {
        /* Save number of bits */
        var bits = new byte[8];
        Encode(bits, _count);

        /* Pad out to 56 mod 64. */
        var index = (_count[0] >> 3) & 0x3f;
        var padLen = (int)(index < 56 ? 56 - index : 120 - index);
        HashCore(Padding(padLen), 0, padLen);

        /* Append length (before padding) */
        HashCore(bits, 0, 8);

        /* Store state in digest */
        Encode(_digest, _state);

        // Zeroize sensitive information.
        Initialize();

        return _digest;
    }

    //--- private methods ---------------------------------------------------

    private byte[] Padding(int nLength)
    {
        if (nLength > 0)
        {
            var padding = new byte[nLength];
            padding[0] = 0x80;
            return padding;
        }

        return null;
    }

    /* F, G and H are basic MD4 functions. */

    private uint F(uint x, uint y, uint z)
    {
        return (x & y) | (~x & z);
    }

    private uint G(uint x, uint y, uint z)
    {
        return (x & y) | (x & z) | (y & z);
    }

    private uint H(uint x, uint y, uint z)
    {
        return x ^ y ^ z;
    }

    /* ROTATE_LEFT rotates x left n bits. */

    private uint ROL(uint x, byte n)
    {
        return (x << n) | (x >> (32 - n));
    }

    /* FF, GG and HH are transformations for rounds 1, 2 and 3 */
    /* Rotation is separate from addition to prevent recomputation */

    private void FF(ref uint a, uint b, uint c, uint d, uint x, byte s)
    {
        a += F(b, c, d) + x;
        a = ROL(a, s);
    }

    private void GG(ref uint a, uint b, uint c, uint d, uint x, byte s)
    {
        a += G(b, c, d) + x + 0x5a827999;
        a = ROL(a, s);
    }

    private void HH(ref uint a, uint b, uint c, uint d, uint x, byte s)
    {
        a += H(b, c, d) + x + 0x6ed9eba1;
        a = ROL(a, s);
    }

    private void Encode(byte[] output, uint[] input)
    {
        for (int i = 0, j = 0; j < output.Length; i++, j += 4)
        {
            output[j] = (byte)input[i];
            output[j + 1] = (byte)(input[i] >> 8);
            output[j + 2] = (byte)(input[i] >> 16);
            output[j + 3] = (byte)(input[i] >> 24);
        }
    }

    private void Decode(uint[] output, byte[] input, int index)
    {
        for (int i = 0, j = index; i < output.Length; i++, j += 4)
        {
            output[i] = (uint)(input[j] | (input[j + 1] << 8) | (input[j + 2] << 16) | (input[j + 3] << 24));
        }
    }

    private void MD4Transform(uint[] state, byte[] block, int index)
    {
        var a = state[0];
        var b = state[1];
        var c = state[2];
        var d = state[3];

        Decode(_x, block, index);

        /* Round 1 */
        FF(ref a, b, c, d, _x[0], S11); /* 1 */
        FF(ref d, a, b, c, _x[1], S12); /* 2 */
        FF(ref c, d, a, b, _x[2], S13); /* 3 */
        FF(ref b, c, d, a, _x[3], S14); /* 4 */
        FF(ref a, b, c, d, _x[4], S11); /* 5 */
        FF(ref d, a, b, c, _x[5], S12); /* 6 */
        FF(ref c, d, a, b, _x[6], S13); /* 7 */
        FF(ref b, c, d, a, _x[7], S14); /* 8 */
        FF(ref a, b, c, d, _x[8], S11); /* 9 */
        FF(ref d, a, b, c, _x[9], S12); /* 10 */
        FF(ref c, d, a, b, _x[10], S13); /* 11 */
        FF(ref b, c, d, a, _x[11], S14); /* 12 */
        FF(ref a, b, c, d, _x[12], S11); /* 13 */
        FF(ref d, a, b, c, _x[13], S12); /* 14 */
        FF(ref c, d, a, b, _x[14], S13); /* 15 */
        FF(ref b, c, d, a, _x[15], S14); /* 16 */

        /* Round 2 */
        GG(ref a, b, c, d, _x[0], S21); /* 17 */
        GG(ref d, a, b, c, _x[4], S22); /* 18 */
        GG(ref c, d, a, b, _x[8], S23); /* 19 */
        GG(ref b, c, d, a, _x[12], S24); /* 20 */
        GG(ref a, b, c, d, _x[1], S21); /* 21 */
        GG(ref d, a, b, c, _x[5], S22); /* 22 */
        GG(ref c, d, a, b, _x[9], S23); /* 23 */
        GG(ref b, c, d, a, _x[13], S24); /* 24 */
        GG(ref a, b, c, d, _x[2], S21); /* 25 */
        GG(ref d, a, b, c, _x[6], S22); /* 26 */
        GG(ref c, d, a, b, _x[10], S23); /* 27 */
        GG(ref b, c, d, a, _x[14], S24); /* 28 */
        GG(ref a, b, c, d, _x[3], S21); /* 29 */
        GG(ref d, a, b, c, _x[7], S22); /* 30 */
        GG(ref c, d, a, b, _x[11], S23); /* 31 */
        GG(ref b, c, d, a, _x[15], S24); /* 32 */

        HH(ref a, b, c, d, _x[0], S31); /* 33 */
        HH(ref d, a, b, c, _x[8], S32); /* 34 */
        HH(ref c, d, a, b, _x[4], S33); /* 35 */
        HH(ref b, c, d, a, _x[12], S34); /* 36 */
        HH(ref a, b, c, d, _x[2], S31); /* 37 */
        HH(ref d, a, b, c, _x[10], S32); /* 38 */
        HH(ref c, d, a, b, _x[6], S33); /* 39 */
        HH(ref b, c, d, a, _x[14], S34); /* 40 */
        HH(ref a, b, c, d, _x[1], S31); /* 41 */
        HH(ref d, a, b, c, _x[9], S32); /* 42 */
        HH(ref c, d, a, b, _x[5], S33); /* 43 */
        HH(ref b, c, d, a, _x[13], S34); /* 44 */
        HH(ref a, b, c, d, _x[3], S31); /* 45 */
        HH(ref d, a, b, c, _x[11], S32); /* 46 */
        HH(ref c, d, a, b, _x[7], S33); /* 47 */
        HH(ref b, c, d, a, _x[15], S34); /* 48 */

        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
    }
}
