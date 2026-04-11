using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private int _signBit;
    
    private uint _smallValue; 
    private uint[]? _data;
    
    public bool IsNegative => _signBit == 1;

    private const int KaratsubaThreshold = 32;

    private static readonly IMultiplier _simpleMultiplier = new SimpleMultiplier();
    private static readonly IMultiplier _karatsubaMultiplier = new KaratsubaMultiplier();
    
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits, nameof(digits));

        int length = digits.Length;

        while (length > 0 && digits[length - 1] == 0)
        {
            length--;
        }

        if (length == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
        }
        else if (length == 1)
        {
            _signBit = isNegative ? 1 : 0;
            _smallValue = digits[0];
            _data = null;
        }
        else
        {
            _signBit = isNegative ? 1 : 0; 
            _smallValue = 0;

            try
            {
                _data = new uint[length];
                Array.Copy(digits, _data, length);
            }
            catch (OutOfMemoryException ex)
            {
                throw new InvalidOperationException("Не удалось выделить память под число", ex);
            }
        }
    }
    
    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
        : this(digits?.ToArray() ?? throw new ArgumentNullException(nameof(digits)), isNegative)
    {
    }    

    public BetterBigInteger(string value, int radix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Строка не может быть пустой.", nameof(value));
        }
        if (radix < 2 || radix > 36)
        {
            throw new ArgumentException("Основание системы счисления должно быть от 2 до 36.", nameof(radix));
        }

        value = value.Trim();
        bool isNegative = false;
        int startIndex = 0;

        if (value[0] == '-')
        {
            isNegative = true;
            startIndex = 1;
        }
        else if (value[0] == '+')
        {
            startIndex = 1;
        }

        if (startIndex >= value.Length)
        {
            throw new FormatException("Строка не содержит цифр.");
        }

        BetterBigInteger result = new BetterBigInteger(new uint[] { 0 });
        BetterBigInteger radixBigInt = new BetterBigInteger(new uint[] { (uint)radix });

        for (int i = startIndex; i < value.Length; i++)
        {
            char c = value[i];
            uint digitValue;

            if (c >= '0' && c <= '9')
                digitValue = (uint)(c - '0');
            else if (c >= 'A' && c <= 'Z')
                digitValue = (uint)(c - 'A' + 10);
            else if (c >= 'a' && c <= 'z')
                digitValue = (uint)(c - 'a' + 10);
            else
                throw new FormatException($"Недопустимый символ '{c}'.");

            if (digitValue >= radix)
            {
                throw new FormatException($"Символ недопустим для системы счисления.");
            }

            BetterBigInteger charBigInt = new BetterBigInteger(new uint[] { digitValue });
            result = (result * radixBigInt) + charBigInt;
        }

        ReadOnlySpan<uint> resDigits = result.GetDigits();
        int length = resDigits.Length;

        while (length > 0 && resDigits[length - 1] == 0)
        {
            length--;
        }

        if (length == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
        }
        else if (length == 1)
        {
            _signBit = isNegative ? 1 : 0;
            _smallValue = resDigits[0];
            _data = null;
        }
        else
        {
            _signBit = isNegative ? 1 : 0;
            _smallValue = 0;
            try
            {
                _data = new uint[length];
                resDigits.Slice(0, length).CopyTo(_data);
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException("Не удалось выделить память.");
            }
        }
    }
    
    public ReadOnlySpan<uint> GetDigits()
    {
        return _data != null ? new ReadOnlySpan<uint>(_data) : new uint[] { _smallValue };
    }
    
    public int CompareTo(IBigInteger? other)
    {
        if (other is null) return 1;

        if (!this.IsNegative && other.IsNegative) return 1;
        if (this.IsNegative && !other.IsNegative) return -1;

        ReadOnlySpan<uint> thisDigits = this.GetDigits();
        ReadOnlySpan<uint> otherDigits = other.GetDigits();

        int lengthcomparison = thisDigits.Length.CompareTo(otherDigits.Length);

        if (lengthcomparison != 0)
        {
            return this.IsNegative ? -lengthcomparison : lengthcomparison;
        }

        for (int i = thisDigits.Length -1; i >= 0; i--)
        {
            int digitComparison = thisDigits[i].CompareTo(otherDigits[i]);
            
            if (digitComparison != 0)
            {
                return this.IsNegative ? -digitComparison : digitComparison;
            }
        }

        return 0;
    }

    public bool Equals(IBigInteger? other)
    {
        if (other is null) return false;
        if (this.IsNegative != other.IsNegative) return false;

        ReadOnlySpan<uint> thisDigits = this.GetDigits();
        ReadOnlySpan<uint> otherDigits = other.GetDigits();

        if (thisDigits.Length != otherDigits.Length) return false;

        return thisDigits.SequenceEqual(otherDigits);
    }

    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);
    
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_signBit);

        foreach (uint digit in GetDigits())
        {
            hashCode.Add(digit);
        }

        return hashCode.ToHashCode();
    }
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.IsNegative == b.IsNegative)
        {
            uint[] result = AddMags(a.GetDigits(), b.GetDigits());
            return new BetterBigInteger(result, a.IsNegative);
        }
        else
        {
            int cmp = CompareMags(a.GetDigits(), b.GetDigits());

            if (cmp == 0) return new BetterBigInteger(new uint[] { 0 });

            if (cmp > 0)
            {
                uint[] result = SubtractMags(a.GetDigits(), b.GetDigits());
                return new BetterBigInteger(result, a.IsNegative);
            }
            else
            {
                uint[] result = SubtractMags(b.GetDigits(), a.GetDigits());
                return new BetterBigInteger(result, b.IsNegative);
            }
        }
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a + (-b);
    }

    public static BetterBigInteger operator -(BetterBigInteger a) 
    {
        ArgumentNullException.ThrowIfNull(a);

        ReadOnlySpan<uint> digits = a.GetDigits();
        if (digits.Length == 1 && digits[0] == 0)
        {
            return a;
        }
        return new BetterBigInteger(digits.ToArray(), !a.IsNegative);
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        DivMod(a, b, out BetterBigInteger quotient, out _);
        return quotient;
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        DivMod(a, b, out _, out BetterBigInteger remainder);
        return remainder;
    }
    
    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if ((a.GetDigits().Length == 1 && a.GetDigits()[0] == 0) || (b.GetDigits().Length == 1 && b.GetDigits()[0] == 0)) return new BetterBigInteger(new uint[] { 0 });

        int aLen = a.GetDigits().Length;
        int bLen = b.GetDigits().Length;

        IMultiplier strategy = (aLen >= KaratsubaThreshold && bLen >= KaratsubaThreshold) 
            ? _karatsubaMultiplier 
            : _simpleMultiplier;

        return strategy.Multiply(a, b);
    }   
    
    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return -a - new BetterBigInteger(new uint[] { 1 });
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return PerformBitwise(a, b, '&');
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return PerformBitwise(a, b, '|');
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return PerformBitwise(a, b, '^');
    }

    private static BetterBigInteger PerformBitwise(BetterBigInteger a, BetterBigInteger b, char operation)
    {
        bool aSign = a.IsNegative;
        bool bSign = b.IsNegative;
        
        bool resultSign = operation switch
        {
            '&' => aSign & bSign,
            '|' => aSign | bSign,
            '^' => aSign ^ bSign,
            _ => throw new ArgumentException("Неизвестная операция")
        };

        ReadOnlySpan<uint> aDigits = a.GetDigits();
        ReadOnlySpan<uint> bDigits = b.GetDigits();
        int maxLength = Math.Max(aDigits.Length, bDigits.Length);
        
        uint[] result;
        try
        {
            result = new uint[maxLength + 1]; 
        }
        catch (OutOfMemoryException ex)
        {
            throw new InvalidOperationException("Недостаточно памяти для поразрядной операции.", ex);
        }

        bool carryA = true; 
        bool carryB = true;
        bool carryR = true; 

        for (int i = 0; i <= maxLength; i++)
        {
            uint wordA = i < aDigits.Length ? aDigits[i] : 0;
            uint wordB = i < bDigits.Length ? bDigits[i] : 0;

            if (aSign)
            {
                if (i >= aDigits.Length) wordA = 0xFFFFFFFF;
                else
                {
                    wordA = ~wordA;
                    if (carryA) { if (wordA == uint.MaxValue) wordA = 0; else { wordA++; carryA = false; } }
                }
            }

            if (bSign)
            {
                if (i >= bDigits.Length) wordB = 0xFFFFFFFF;
                else
                {
                    wordB = ~wordB;
                    if (carryB) { if (wordB == uint.MaxValue) wordB = 0; else { wordB++; carryB = false; } }
                }
            }

            uint wordR = operation switch
            {
                '&' => wordA & wordB,
                '|' => wordA | wordB,
                '^' => wordA ^ wordB,
                _ => 0
            };

            if (resultSign)
            {
                wordR = ~wordR;
                if (carryR) { if (wordR == uint.MaxValue) wordR = 0; else { wordR++; carryR = false; } }
            }

            result[i] = wordR;
        }

        return new BetterBigInteger(result, resultSign);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);

        if (shift < 0) return a >> -shift;
        if (shift == 0 || (a.GetDigits().Length == 1 && a.GetDigits()[0] == 0)) return a;

        int wordShift = shift / 32;
        int bitShift = shift % 32;

        ReadOnlySpan<uint> digits = a.GetDigits();
        uint[] result;

        try
        {
            result = new uint[digits.Length + wordShift + 1]; 
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidOperationException("Недостаточно памяти для битового сдвига");
        }

        uint carry = 0;
        int invShift = 32 - bitShift;

        for (int i = 0; i < digits.Length; i++)
        {
            uint current = digits[i];

            if (bitShift == 0)
            {
                result[i + wordShift] = current;
                carry = 0;
            }
            else
            {
                result[i + wordShift] = (current << bitShift) | carry;
                carry = current >> invShift;
            }
        }

        if (carry > 0)
        {
            result[digits.Length + wordShift] = carry;
        }

        return new BetterBigInteger(result, a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        
        if (shift < 0) return a << -shift;
        if (shift == 0) return a;

        int wordShift = shift / 32;
        int bitShift = shift % 32;

        ReadOnlySpan<uint> digits = a.GetDigits();
        
        bool droppedBits = false;
        if (a.IsNegative)
        {
            if (wordShift >= digits.Length) 
            {
                droppedBits = true;
            }
            else
            {
                for (int i = 0; i < wordShift; i++)
                {
                    if (digits[i] != 0) 
                    {
                        droppedBits = true;
                        break;
                    }
                }

                if (!droppedBits && bitShift > 0)
                {
                    uint mask = (1u << bitShift) - 1u;
                    if ((digits[wordShift] & mask) != 0)
                    {
                        droppedBits = true;
                    }
                }
            }
        }

        if (wordShift >= digits.Length) 
        {
            return droppedBits ? new BetterBigInteger(new uint[] { 1 }, true) : new BetterBigInteger(new uint[] { 0 });
        }

        uint[] result;
        try
        {
            result = new uint[digits.Length - wordShift];
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidOperationException("Недостаточно памяти для битового сдвига");
        }

        uint carry = 0;
        int invBitShift = 32 - bitShift;
        
        for (int i = digits.Length - 1; i >= wordShift; i--)
        {
            uint current = digits[i];
            
            if (bitShift == 0)
            {
                result[i - wordShift] = current;
                carry = 0;
            }
            else
            {
                result[i - wordShift] = (current >> bitShift) | carry;
                carry = current << invBitShift;
            }
        }

        if (droppedBits)
        {
            result = AddMags(result, new uint[] { 1u });
        }

        return new BetterBigInteger(result, a.IsNegative);
    }
    
    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;
    
    public override string ToString() => ToString(10);
    
    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
        {
            throw new ArgumentException("Основание системы счисления должно быть в диапазоне от 2 до 36.", nameof(radix));
        }

        ReadOnlySpan<uint> digits = GetDigits();
        if (digits.Length == 1 && digits[0] == 0)
        {
            return "0";
        }

        BetterBigInteger current = new BetterBigInteger(digits.ToArray(), false);
        BetterBigInteger radixBigInt = new BetterBigInteger(new uint[] { (uint)radix });
        BetterBigInteger zero = new BetterBigInteger(new uint[] { 0 });

        ReadOnlySpan<char> chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".AsSpan();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        while (current > zero)
        {
            DivMod(current, radixBigInt, out BetterBigInteger quotient, out BetterBigInteger remainder);
            uint remValue = remainder.GetDigits()[0]; 
            sb.Append(chars[(int)remValue]);
            current = quotient;
        }

        if (this.IsNegative)
        {
            sb.Append('-');
        }

        char[] resultChars = sb.ToString().ToCharArray();
        Array.Reverse(resultChars);
        
        return new string(resultChars);
    }
    
    private static uint[] AddMags(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int maxLength = Math.Max(a.Length, b.Length);
        uint[] result;

        try
        {
            result = new uint[maxLength + 1];
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidOperationException("Недостаточно памяти для выполнения сложения");
        }

        uint carry = 0;

        for (int i = 0; i < maxLength; i++)
        {
            uint uA = i < a.Length ? a[i] : 0;
            uint uB = i < b.Length ? b[i] : 0;

            uint aLow = uA & 0xFFFF;
            uint bLow = uB & 0xFFFF; 

            uint aHigh = uA >> 16;
            uint bHigh = uB >> 16;

            uint sumLow = aLow + bLow + carry;
            uint resLow = sumLow & 0xFFFF;

            uint carryLow = sumLow >> 16;

            uint sumHigh = aHigh + bHigh + carryLow;
            uint resHigh = sumHigh & 0xFFFF;

            carry = sumHigh >> 16;

            result[i] = resLow | (resHigh << 16);
        }

        if (carry > 0)
        {
            result[maxLength] = carry;
        }

        return result;
    }

    private static uint[] SubtractMags(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        uint[] result;

        try
        {
            result = new uint[a.Length];
        }
        catch (OutOfMemoryException) 
        {
            throw new InvalidOperationException("Недостаточно памяти для выполнения вычитания");
        }

        uint acc = 0;

        for (int i = 0; i < a.Length; i++)
        {
            uint uA = a[i];
            uint uB = i < b.Length ? b[i] : 0;

            uint aLow = uA & 0xFFFF;
            uint aHigh = uA >> 16;

            uint bLow = uB & 0xFFFF;
            uint bHigh = uB >> 16;

            uint bLowTotal = bLow + acc;
            uint resLow;

            if (aLow < bLowTotal) {
                uint diff = bLowTotal - aLow; 
                resLow = (~diff + 1) & 0xFFFF;
                acc = 1;
            }
            else
            {
                resLow = aLow - bLowTotal;
                acc = 0;
            }

            uint bHighTotal = bHigh + acc;
            uint resHigh;

            if (aHigh < bHighTotal)
            {
                uint diff = bHighTotal - aHigh;
                resHigh = (~diff + 1) & 0xFFFF;
                acc = 1;
            }
            else
            {
                resHigh = aHigh - bHighTotal;
                acc = 0;
            }

            result[i] = resLow | (resHigh << 16);
        }

        return result;
    }

    private static int CompareMags(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length != b.Length) return a.Length.CompareTo(b.Length);

        for (int i = a.Length - 1; i >= 0; i--)
        {
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        }

        return 0;
    }

    private static int GetBitLength(ReadOnlySpan<uint> digits)
    {
        if (digits.Length == 1 && digits[0] == 0) return 0;
        int length = (digits.Length - 1) * 32;
        uint last = digits[^1]; 
        while (last > 0)
        {
            length++;
            last >>= 1;
        }
        return length;
    }

    private static void DivMod(BetterBigInteger dividend, BetterBigInteger divisor, out BetterBigInteger quotient, out BetterBigInteger remainder)
    {
        ReadOnlySpan<uint> divDigits = divisor.GetDigits();

        if (divDigits.Length == 1 && divDigits[0] == 0)
        {
            throw new DivideByZeroException("Деление на ноль недопустимо.");
        }

        int cmp = CompareMags(dividend.GetDigits(), divisor.GetDigits());

        if (cmp < 0)
        {
            quotient = new BetterBigInteger(new uint[] { 0 });
            remainder = dividend;
            return;
        }
        if (cmp == 0)
        {
            quotient = new BetterBigInteger(new uint[] { 1 }, dividend.IsNegative != divisor.IsNegative);
            remainder = new BetterBigInteger(new uint[] { 0 });
            return;
        }

        BetterBigInteger currentDividend = new BetterBigInteger(dividend.GetDigits().ToArray(), false);
        BetterBigInteger currentDivisor = new BetterBigInteger(divisor.GetDigits().ToArray(), false);
        BetterBigInteger currentQuotient = new BetterBigInteger(new uint[] { 0 });

        int dividendBits = GetBitLength(currentDividend.GetDigits());
        int divisorBits = GetBitLength(currentDivisor.GetDigits());
        int shift = dividendBits - divisorBits;

        currentDivisor <<= shift;

        for (int i = 0; i <= shift; i++)
        {
            currentQuotient <<= 1;

            if (CompareMags(currentDividend.GetDigits(), currentDivisor.GetDigits()) >= 0)
            {
                currentDividend -= currentDivisor;
                currentQuotient += new BetterBigInteger(new uint[] { 1 });
            }

            currentDivisor >>= 1;
        }

        bool qIsNegative = dividend.IsNegative != divisor.IsNegative;
        quotient = new BetterBigInteger(currentQuotient.GetDigits().ToArray(), qIsNegative);
        
        remainder = new BetterBigInteger(currentDividend.GetDigits().ToArray(), dividend.IsNegative);
    }
}