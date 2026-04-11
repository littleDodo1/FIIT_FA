using System;
using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        ReadOnlySpan<uint> aDigits = a.GetDigits();
        ReadOnlySpan<uint> bDigits = b.GetDigits();

        if ((aDigits.Length == 1 && aDigits[0] == 0) ||
            (bDigits.Length == 1 && bDigits[0] == 0))
        {
            return new BetterBigInteger([0]);
        }

        uint[] a16 = SplitTo16(aDigits);
        uint[] b16 = SplitTo16(bDigits);

        int neededLength = a16.Length + b16.Length - 1;

        int n = 1;
        while (n < neededLength) {
            n <<= 1;
        }

        Complex[] fa = new Complex[n];
        Complex[] fb = new Complex[n]; 

        for (int i = 0; i < a16.Length; i++) fa[i] = new Complex(a16[i], 0);
        for (int i = 0; i < b16.Length; i++) fb[i] = new Complex(b16[i], 0);

        ExecuteFft(fa, false);
        ExecuteFft(fb, false);

        for (int i = 0; i < n; i++)
        {
            fa[i] *= fb[i];
        }

        ExecuteFft(fa, true);

        uint[] res16 = new uint[n];
        long carry = 0;

        for (int i = 0; i < neededLength; i++)
        {
            long val = (long)Math.Round(fa[i].Real) + carry;

            res16[i] = (uint)(val & 0xFFFF);
            carry = val >> 16;
        }

        int tailIndex = neededLength;
        while (carry > 0 && tailIndex < n)
        { 
            res16[tailIndex] = (uint)(carry & 0xFFFF);
            carry >>= 16;
            tailIndex++;
        }

        uint[] result32 = MergeTo32(res16, tailIndex, carry);

        bool isNegative = a.IsNegative != b.IsNegative; 
        
        return new BetterBigInteger(result32, isNegative);
    }

    private static void ExecuteFft(Complex[] a, bool invert)
    {
        int n = a.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            
            if (i < j)
            {
                (a[i], a[j]) = (a[j], a[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = 2 * Math.PI / len * (invert ? -1 : 1);
            Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
            
            for (int i = 0; i < n; i += len)
            {
                Complex w = Complex.One; 
                int half = len / 2;
                
                for (int j = 0; j < half; j++)
                {
                    Complex u = a[i + j];
                    Complex v = a[i + j + half] * w;
                    
                    a[i + j] = u + v;
                    a[i + j + half] = u - v;
                    w *= wlen;
                }
            }
        }

        if (invert)
        {
            for (int i = 0; i < n; i++)
            {
                a[i] /= n;
            }
        }
    }

    private static uint[] SplitTo16(ReadOnlySpan<uint> digits)
    {
        uint[] result = new uint[digits.Length * 2];

        for (int i = 0; i < digits.Length; i++)
        {
            result[i * 2] = digits[i] & 0xFFFF;
            result[i * 2 + 1] = digits[i] >> 16;
        }

        return result;
    }

    private static uint[] MergeTo32(uint[] digits16, int usedLength, long leftOverCarry)
    {
        int length32 = (usedLength + 1) / 2;
        
        if (leftOverCarry > 0) length32++; 

        uint[] result = new uint[length32];
        
        for (int i = 0; i < (usedLength + 1) / 2; i++)
        {
            uint low = digits16[i * 2];
            uint high = (i * 2 + 1 < usedLength) ? digits16[i * 2 + 1] : 0;
            result[i] = low | (high << 16);
        }

        if (leftOverCarry > 0)
        {
            result[length32 - 1] = (uint)leftOverCarry;
        }

        return result;
    }
}