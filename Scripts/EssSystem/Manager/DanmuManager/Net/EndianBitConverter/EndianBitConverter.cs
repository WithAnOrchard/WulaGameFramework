// Copyright (C) Microsoft Corporation. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace BiliBiliDanmu.Net.Internal
{
    public abstract class EndianBitConverter
    {
        public static EndianBitConverter LittleEndian { get; } = new LittleEndianBitConverter();


        public static EndianBitConverter BigEndian { get; } = new BigEndianBitConverter();


        public abstract bool IsLittleEndian { get; }


        public byte[] GetBytes(bool value)
        {
            return new[] { value ? (byte)1 : (byte)0 };
        }


        public byte[] GetBytes(char value)
        {
            return GetBytes((short)value);
        }


        public byte[] GetBytes(double value)
        {
            var val = System.BitConverter.DoubleToInt64Bits(value);
            return GetBytes(val);
        }


        public abstract byte[] GetBytes(short value);


        public abstract byte[] GetBytes(int value);


        public abstract byte[] GetBytes(long value);

        public byte[] GetBytes(float value)
        {
            var val = new SingleConverter(value).GetIntValue();
            return GetBytes(val);
        }


        public byte[] GetBytes(ushort value)
        {
            return GetBytes((short)value);
        }


        public byte[] GetBytes(uint value)
        {
            return GetBytes((int)value);
        }

        public byte[] GetBytes(ulong value)
        {
            return GetBytes((long)value);
        }

        public bool ToBoolean(byte[] value, int startIndex)
        {
            CheckArguments(value, startIndex, 1);

            return value[startIndex] != 0;
        }

        public char ToChar(byte[] value, int startIndex)
        {
            return (char)ToInt16(value, startIndex);
        }


        public double ToDouble(byte[] value, int startIndex)
        {
            var val = ToInt64(value, startIndex);
            return System.BitConverter.Int64BitsToDouble(val);
        }


        public abstract short ToInt16(byte[] value, int startIndex);


        public abstract int ToInt32(byte[] value, int startIndex);

        public abstract long ToInt64(byte[] value, int startIndex);


        public float ToSingle(byte[] value, int startIndex)
        {
            var val = ToInt32(value, startIndex);
            return new SingleConverter(val).GetFloatValue();
        }


        public ushort ToUInt16(byte[] value, int startIndex)
        {
            return (ushort)ToInt16(value, startIndex);
        }


        public uint ToUInt32(byte[] value, int startIndex)
        {
            return (uint)ToInt32(value, startIndex);
        }


        public ulong ToUInt64(byte[] value, int startIndex)
        {
            return (ulong)ToInt64(value, startIndex);
        }

        // Testing showed that this method wasn't automatically being inlined, and doing so offers a significant performance improvement.
#if !NET40
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void CheckArguments(byte[] value, int startIndex, int byteLength)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            // confirms startIndex is not negative or too far along the byte array
            if ((uint)startIndex > value.Length - byteLength) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}