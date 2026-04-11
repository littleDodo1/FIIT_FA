using System;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
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
            return new BetterBigInteger(new uint[] { 0 });
        }

        uint[] a16 = SplitTo16(aDigits);
        uint[] b16 = SplitTo16(bDigits);

        uint[] res16;

        try
        {
            res16 = new uint[a16.Length + b16.Length];
        }
        catch (OutOfMemoryException ex)
        {
            throw new InvalidOperationException("Недостаточно памяти для выполнения умножения.", ex);
        }

        for (int i = 0; i < b16.Length; i++)
        {
            uint bVal = b16[i];

            if (bVal == 0) continue;

            uint carry = 0;

            for (int j = 0; j < a16.Length; j++)
            {
                uint aVal = a16[j]; 
                uint prod = aVal * bVal;
                
                uint curCellVal = res16[i + j];
                uint prodLow = prod & 0xFFFF;
                uint prodHigh = prod >> 16;

                uint sumLow32 = prodLow + curCellVal + carry;

                res16[i + j] = sumLow32 & 0xFFFF; 
                
                carry = prodHigh + (sumLow32 >> 16);
            }

            if (carry > 0)
            {
                int k = i + a16.Length; 
                while (carry > 0 && k < res16.Length)
                {
                    uint sum = res16[k] + carry;
                    res16[k] = sum & 0xFFFF; 
                    carry = sum >> 16; 
                    k++;
                }
            }
        }

        uint[] res32 = MergeTo32(res16);
        
        bool isNegative = a.IsNegative != b.IsNegative;
        return new BetterBigInteger(res32, isNegative);
    }

    private static uint[] SplitTo16(ReadOnlySpan<uint> digits)
    {
        uint[] result;
        
        try
        {
            result = new uint[digits.Length * 2];
        }
        catch (OutOfMemoryException ex)
        {
            throw new InvalidOperationException("Недостаточно памяти.", ex);
        }


        for (int i = 0; i < digits.Length; i++)
        {
            result[i * 2] = digits[i] & 0xFFFF;
            result[i * 2 + 1] = digits[i] >> 16;
        }

        return result;
    }

    private static uint[] MergeTo32(uint[] digits)
    {
        int len = (digits.Length + 1) / 2;
        uint[] result;

        try
        {
            result = new uint[len];
        }
        catch (OutOfMemoryException ex)
        {
            throw new InvalidOperationException("Недостаточно памяти.", ex);
        }

        for (int i = 0; i < len; i++)
        {
            uint low = digits[i * 2];
            uint high = (i * 2 + 1 < digits.Length) ? digits[i * 2 + 1] : 0;

            result[i] = low | (high << 16);
        }

        return result;
    }
}