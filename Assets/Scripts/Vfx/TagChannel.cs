using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Vfx
{
    public enum TagChannel
    {
        X,
        Y,
        Z,
        W
    }

    public static class TagChannelExtensions
    {
        public static uint ReadTag(this TagChannel channel, Vector4 data)
        {
            return channel switch
            {
                TagChannel.X => (uint)FloatToInt(data.x),
                TagChannel.Y => (uint)FloatToInt(data.y),
                TagChannel.Z => (uint)FloatToInt(data.z),
                TagChannel.W => (uint)FloatToInt(data.w),
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
            };
        }

        public static Vector4 WriteTagVector(this TagChannel channel, uint value)
        {
            var f = IntToFloat((int)value);
            return new Vector4(
                channel == TagChannel.X ? f : 0f,
                channel == TagChannel.Y ? f : 0f,
                channel == TagChannel.Z ? f : 0f,
                channel == TagChannel.W ? f : 0f
            );
        }

        private static int FloatToInt(float value)
        {
            var c = new IntFloatUnion { floatValue = value };
            return c.intValue;
        }

        private static float IntToFloat(int value)
        {
            var c = new IntFloatUnion { intValue = value };
            return c.floatValue;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct IntFloatUnion
        {
            [FieldOffset(0)] public int intValue;
            [FieldOffset(0)] public float floatValue;
        }
    }
}
