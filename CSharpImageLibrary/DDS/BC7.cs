﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CSharpImageLibrary.DDS.DDS_BlockHelpers;
using UsefulThings;
using System.Numerics;

namespace CSharpImageLibrary.DDS
{
    /// <summary>
    /// Adapted almost wholesale from DirectXTex from Microsoft. https://github.com/Microsoft/DirectXTex
    /// </summary>
    internal static class BC7
    {
        #region Structs
        struct Mode
        {
            public readonly int Partitions;
            public readonly int PartitionBits;
            public readonly int IndexPrecision;
            public readonly LDRColour RawRGBPrecision;
            public readonly LDRColour RGBPrecisionWithP;
            public readonly int APrecision;
            public readonly int PBits;
            public readonly int RotationBits;
            public readonly int IndexModeBits;
            public readonly int modeVal;

            public Mode(int modeVal, int partitions, int partitionBits, int IndexPrecision, LDRColour rawrgbPrecision, LDRColour RGBPrecisionWithP, int APrecision, int PBits, int RotationBits, int IndexModeBits)
            {
                this.modeVal = modeVal;
                this.Partitions = partitions;
                this.PartitionBits = partitionBits;
                this.IndexPrecision = IndexPrecision;
                this.RawRGBPrecision = rawrgbPrecision;
                this.RGBPrecisionWithP = RGBPrecisionWithP;
                this.APrecision = APrecision;
                this.PBits = PBits;
                this.RotationBits = RotationBits;
                this.IndexModeBits = IndexModeBits;
            }
        }




        public struct LDRColour
        {
            public int R;
            public int G;
            public int B;
            public int A;

            public LDRColour(byte R, byte G, byte B, byte A)
            {
                this.R = R;
                this.B = B;
                this.G = G;
                this.A = A;
            }

            public LDRColour(float r, float g, float b, float a)
            {
                R = (byte)((r > 1f ? 1f : r) * 255);
                G = (byte)((g > 1f ? 1f : g) * 255);
                B = (byte)((b > 1f ? 1f : g) * 255);
                A = (byte)((a > 1f ? 1f : a) * 255);
            }

            public override string ToString()
            {
                return $"R: {R}, G: {G}, B: {B}, A: {A}";
            }
        }

        public struct HDRColour
        {
            public float R, G, B, A;

            public HDRColour(LDRColour colour)
            {
                R = colour.R * 1f / 255f;
                G = colour.G * 1f / 255f;
                B = colour.B * 1f / 255f;
                A = colour.A * 1f / 255f;
            }

            public override string ToString()
            {
                return $"R: {R}, G: {G}, B: {B}, A: {A}";
            }
        }
        #endregion Structs

        #region Constants
        const int BC67_WEIGHT_MAX = 64;
        const int BC67_WEIGHT_ROUND = 32;
        const int BC67_WEIGHT_SHIFT = 6;
        const int NUM_PIXELS_PER_BLOCK = 16;
        const int BC7_MAX_REGIONS = 3;
        const int BC7_MAX_SHAPES = 64;
        const int BC7_MAX_INDICIES = 16;
        const int BC7_NUM_CHANNELS = 4;
        #endregion Constants

        #region Tables
        // 3,64,16
        static byte[][][] PartitionTable = new byte[3][][]
        {   // 1 Region case has no subsets (all 0)
            new byte[64][]
            {
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            },

            new byte[64][]
            {   // BC6H/BC7 Partition Set for 2 Subsets
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 }, // Shape 0
                new byte[16] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 }, // Shape 1
                new byte[16] { 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1 }, // Shape 2
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 1 }, // Shape 3
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1 }, // Shape 4
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1 }, // Shape 5
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1 }, // Shape 6
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1 }, // Shape 7
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1 }, // Shape 8
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, // Shape 9
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1 }, // Shape 10
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1 }, // Shape 11
                new byte[16] { 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, // Shape 12
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 }, // Shape 13
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, // Shape 14
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 }, // Shape 15
                new byte[16] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1 }, // Shape 16
                new byte[16] { 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, // Shape 17
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0 }, // Shape 18
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0 }, // Shape 19
                new byte[16] { 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, // Shape 20
                new byte[16] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0 }, // Shape 21
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0 }, // Shape 22
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1 }, // Shape 23
                new byte[16] { 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0 }, // Shape 24
                new byte[16] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0 }, // Shape 25
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0 }, // Shape 26
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0 }, // Shape 27
                new byte[16] { 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0 }, // Shape 28
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, // Shape 29
                new byte[16] { 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0 }, // Shape 30
                new byte[16] { 0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0 }, // Shape 31

                                                                // BC7 Partition Set for 2 Subsets (second-half)
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 }, // Shape 32
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1 }, // Shape 33
                new byte[16] { 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0 }, // Shape 34
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0 }, // Shape 35
                new byte[16] { 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0 }, // Shape 36
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0 }, // Shape 37
                new byte[16] { 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1 }, // Shape 38
                new byte[16] { 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1 }, // Shape 39
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0 }, // Shape 40
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0 }, // Shape 41
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0 }, // Shape 42
                new byte[16] { 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 0 }, // Shape 43
                new byte[16] { 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0 }, // Shape 44
                new byte[16] { 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1 }, // Shape 45
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1 }, // Shape 46
                new byte[16] { 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0 }, // Shape 47
                new byte[16] { 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, // Shape 48
                new byte[16] { 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0 }, // Shape 49
                new byte[16] { 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0 }, // Shape 50
                new byte[16] { 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0 }, // Shape 51
                new byte[16] { 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1 }, // Shape 52
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1 }, // Shape 53
                new byte[16] { 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0 }, // Shape 54
                new byte[16] { 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0 }, // Shape 55
                new byte[16] { 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1 }, // Shape 56
                new byte[16] { 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1 }, // Shape 57
                new byte[16] { 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1 }, // Shape 58
                new byte[16] { 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1 }, // Shape 59
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 }, // Shape 60
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, // Shape 61
                new byte[16] { 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0 }, // Shape 62
                new byte[16] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1 }  // Shape 63
            },

            new byte[64][]
            {   // BC7 Partition Set for 3 Subsets
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 1, 2, 2, 2, 2 }, // Shape 0
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 2, 1 }, // Shape 1
                new byte[16] { 0, 0, 0, 0, 2, 0, 0, 1, 2, 2, 1, 1, 2, 2, 1, 1 }, // Shape 2
                new byte[16] { 0, 2, 2, 2, 0, 0, 2, 2, 0, 0, 1, 1, 0, 1, 1, 1 }, // Shape 3
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2 }, // Shape 4
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 2, 2 }, // Shape 5
                new byte[16] { 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1 }, // Shape 6
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1 }, // Shape 7
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2 }, // Shape 8
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2 }, // Shape 9
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2 }, // Shape 10
                new byte[16] { 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2 }, // Shape 11
                new byte[16] { 0, 1, 1, 2, 0, 1, 1, 2, 0, 1, 1, 2, 0, 1, 1, 2 }, // Shape 12
                new byte[16] { 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 2 }, // Shape 13
                new byte[16] { 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 2, 1, 2, 2, 2 }, // Shape 14
                new byte[16] { 0, 0, 1, 1, 2, 0, 0, 1, 2, 2, 0, 0, 2, 2, 2, 0 }, // Shape 15
                new byte[16] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 2 }, // Shape 16
                new byte[16] { 0, 1, 1, 1, 0, 0, 1, 1, 2, 0, 0, 1, 2, 2, 0, 0 }, // Shape 17
                new byte[16] { 0, 0, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1, 2, 2 }, // Shape 18
                new byte[16] { 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 1 }, // Shape 19
                new byte[16] { 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2 }, // Shape 20
                new byte[16] { 0, 0, 0, 1, 0, 0, 0, 1, 2, 2, 2, 1, 2, 2, 2, 1 }, // Shape 21
                new byte[16] { 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 2, 2, 0, 1, 2, 2 }, // Shape 22
                new byte[16] { 0, 0, 0, 0, 1, 1, 0, 0, 2, 2, 1, 0, 2, 2, 1, 0 }, // Shape 23
                new byte[16] { 0, 1, 2, 2, 0, 1, 2, 2, 0, 0, 1, 1, 0, 0, 0, 0 }, // Shape 24
                new byte[16] { 0, 0, 1, 2, 0, 0, 1, 2, 1, 1, 2, 2, 2, 2, 2, 2 }, // Shape 25
                new byte[16] { 0, 1, 1, 0, 1, 2, 2, 1, 1, 2, 2, 1, 0, 1, 1, 0 }, // Shape 26
                new byte[16] { 0, 0, 0, 0, 0, 1, 1, 0, 1, 2, 2, 1, 1, 2, 2, 1 }, // Shape 27
                new byte[16] { 0, 0, 2, 2, 1, 1, 0, 2, 1, 1, 0, 2, 0, 0, 2, 2 }, // Shape 28
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 2, 0, 0, 2, 2, 2, 2, 2 }, // Shape 29
                new byte[16] { 0, 0, 1, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 0, 1, 1 }, // Shape 30
                new byte[16] { 0, 0, 0, 0, 2, 0, 0, 0, 2, 2, 1, 1, 2, 2, 2, 1 }, // Shape 31
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 2, 2, 2 }, // Shape 32
                new byte[16] { 0, 2, 2, 2, 0, 0, 2, 2, 0, 0, 1, 2, 0, 0, 1, 1 }, // Shape 33
                new byte[16] { 0, 0, 1, 1, 0, 0, 1, 2, 0, 0, 2, 2, 0, 2, 2, 2 }, // Shape 34
                new byte[16] { 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0 }, // Shape 35
                new byte[16] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0 }, // Shape 36
                new byte[16] { 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0 }, // Shape 37
                new byte[16] { 0, 1, 2, 0, 2, 0, 1, 2, 1, 2, 0, 1, 0, 1, 2, 0 }, // Shape 38
                new byte[16] { 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1 }, // Shape 39
                new byte[16] { 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0, 1, 1 }, // Shape 40
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2 }, // Shape 41
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 2, 1, 2, 1, 2, 1 }, // Shape 42
                new byte[16] { 0, 0, 2, 2, 1, 1, 2, 2, 0, 0, 2, 2, 1, 1, 2, 2 }, // Shape 43
                new byte[16] { 0, 0, 2, 2, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 1, 1 }, // Shape 44
                new byte[16] { 0, 2, 2, 0, 1, 2, 2, 1, 0, 2, 2, 0, 1, 2, 2, 1 }, // Shape 45
                new byte[16] { 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 0, 1, 0, 1 }, // Shape 46
                new byte[16] { 0, 0, 0, 0, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1 }, // Shape 47
                new byte[16] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2 }, // Shape 48
                new byte[16] { 0, 2, 2, 2, 0, 1, 1, 1, 0, 2, 2, 2, 0, 1, 1, 1 }, // Shape 49
                new byte[16] { 0, 0, 0, 2, 1, 1, 1, 2, 0, 0, 0, 2, 1, 1, 1, 2 }, // Shape 50
                new byte[16] { 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1, 2 }, // Shape 51
                new byte[16] { 0, 2, 2, 2, 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2 }, // Shape 52
                new byte[16] { 0, 0, 0, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2 }, // Shape 53
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 2, 2 }, // Shape 54
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 1, 2 }, // Shape 55
                new byte[16] { 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 2, 2, 2, 2, 2, 2 }, // Shape 56
                new byte[16] { 0, 0, 2, 2, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 2, 2 }, // Shape 57
                new byte[16] { 0, 0, 2, 2, 1, 1, 2, 2, 1, 1, 2, 2, 0, 0, 2, 2 }, // Shape 58
                new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2 }, // Shape 59
                new byte[16] { 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1 }, // Shape 60
                new byte[16] { 0, 2, 2, 2, 1, 2, 2, 2, 0, 2, 2, 2, 1, 2, 2, 2 }, // Shape 61
                new byte[16] { 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, // Shape 62
                new byte[16] { 0, 1, 1, 1, 2, 0, 1, 1, 2, 2, 0, 1, 2, 2, 2, 0 }  // Shape 63
            }
        };  
        
        
        // 3,64,3
        static byte[][][] FixUpTable = new byte[3][][]  
        {
            new byte[64][]
            {   // No fix-ups for 1st subset for BC6H or BC7
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 },
                new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }, new byte[3] { 0, 0, 0 }
            },

            new byte[64][]
            {   // BC6H/BC7 Partition Set Fixups for 2 Subsets
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0, 2, 0 },
                new byte[3] { 0, 2, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0, 2, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 2, 0 },
                new byte[3] { 0, 8, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 2, 0 },
                
                 // BC7 Partition Set Fixups for 2 Subsets (second-half)
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0, 6, 0 }, new byte[3] { 0, 8, 0 },
                new byte[3] { 0, 2, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0, 2, 0 }, new byte[3] { 0, 8, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 2, 0 },
                new byte[3] { 0, 2, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0, 6, 0 },
                new byte[3] { 0, 6, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 6, 0 }, new byte[3] { 0, 8, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 2, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 }, new byte[3] { 0,15, 0 },
                new byte[3] { 0,15, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0, 2, 0 }, new byte[3] { 0,15, 0 }
            },

            new byte[64][]
            {   // BC7 Partition Set Fixups for 3 Subsets
                new byte[3] { 0, 3,15 }, new byte[3] { 0, 3, 8 }, new byte[3] { 0,15, 8 }, new byte[3] { 0,15, 3 },
                new byte[3] { 0, 8,15 }, new byte[3] { 0, 3,15 }, new byte[3] { 0,15, 3 }, new byte[3] { 0,15, 8 },
                new byte[3] { 0, 8,15 }, new byte[3] { 0, 8,15 }, new byte[3] { 0, 6,15 }, new byte[3] { 0, 6,15 },
                new byte[3] { 0, 6,15 }, new byte[3] { 0, 5,15 }, new byte[3] { 0, 3,15 }, new byte[3] { 0, 3, 8 },
                new byte[3] { 0, 3,15 }, new byte[3] { 0, 3, 8 }, new byte[3] { 0, 8,15 }, new byte[3] { 0,15, 3 },
                new byte[3] { 0, 3,15 }, new byte[3] { 0, 3, 8 }, new byte[3] { 0, 6,15 }, new byte[3] { 0,10, 8 },
                new byte[3] { 0, 5, 3 }, new byte[3] { 0, 8,15 }, new byte[3] { 0, 8, 6 }, new byte[3] { 0, 6,10 },
                new byte[3] { 0, 8,15 }, new byte[3] { 0, 5,15 }, new byte[3] { 0,15,10 }, new byte[3] { 0,15, 8 },
                new byte[3] { 0, 8,15 }, new byte[3] { 0,15, 3 }, new byte[3] { 0, 3,15 }, new byte[3] { 0, 5,10 },
                new byte[3] { 0, 6,10 }, new byte[3] { 0,10, 8 }, new byte[3] { 0, 8, 9 }, new byte[3] { 0,15,10 },
                new byte[3] { 0,15, 6 }, new byte[3] { 0, 3,15 }, new byte[3] { 0,15, 8 }, new byte[3] { 0, 5,15 },
                new byte[3] { 0,15, 3 }, new byte[3] { 0,15, 6 }, new byte[3] { 0,15, 6 }, new byte[3] { 0,15, 8 },
                new byte[3] { 0, 3,15 }, new byte[3] { 0,15, 3 }, new byte[3] { 0, 5,15 }, new byte[3] { 0, 5,15 },
                new byte[3] { 0, 5,15 }, new byte[3] { 0, 8,15 }, new byte[3] { 0, 5,15 }, new byte[3] { 0,10,15 },
                new byte[3] { 0, 5,15 }, new byte[3] { 0,10,15 }, new byte[3] { 0, 8,15 }, new byte[3] { 0,13,15 },
                new byte[3] { 0,15, 3 }, new byte[3] { 0,12,15 }, new byte[3] { 0, 3,15 }, new byte[3] { 0, 3, 8 }
            }
        };

        static int[] AWeights2 = new int[] { 0, 21, 43, 64 };
        static int[] AWeights3 = new int[] { 0, 9, 18, 27, 37, 46, 55, 64 };
        static int[] AWeights4 = new int[] { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };
        #endregion Tables

        // Mode: Partitions, partitionBits, indexPrecision, rgbPrecision, rgbPrecisionWithP, APrecision, PBits, Rotation, IndexMode
        static Mode[] Modes = new Mode[] {
            /* Mode 0: */ new Mode(0, 2, 4, 3, new LDRColour(4, 4, 4, 0), new LDRColour(5, 5, 5, 0), 0, 6, 0, 0),
            /* Mode 1: */ new Mode(1, 1, 6, 3, new LDRColour(6, 6, 6, 0), new LDRColour(7, 7, 7, 0), 0, 2, 0, 0),
            /* Mode 2: */ new Mode(2, 2, 6, 2, new LDRColour(5, 5, 5, 0), new LDRColour(5, 5, 5, 0), 0, 0, 0, 0),
            /* Mode 3: */ new Mode(3, 1, 6, 2, new LDRColour(7, 7, 7, 0), new LDRColour(8, 8, 8, 0), 0, 4, 0, 0),
            /* Mode 4: */ new Mode(4, 0, 0, 2, new LDRColour(5, 5, 5, 6), new LDRColour(5, 5, 5, 6), 3, 0, 2, 1),
            /* Mode 5: */ new Mode(5, 0, 0, 2, new LDRColour(7, 7, 7, 8), new LDRColour(7, 7, 7, 8), 2, 0, 2, 0),
            /* Mode 6: */ new Mode(6, 0, 0, 4, new LDRColour(7, 7, 7, 7), new LDRColour(8, 8, 8, 8), 0, 2, 0, 0),
            /* Mode 7: */ new Mode(7, 1, 6, 2, new LDRColour(5, 5, 5, 5), new LDRColour(6, 6, 6, 6), 0, 4, 0, 0),
        };

        #region Decompression
        public static LDRColour[] DecompressBC7(byte[] source, int sourceStart)
        {
            int start = 0;
            while (start < 128 && GetBit(source, sourceStart, ref start) == 0) { }
            int modeVal = start - 1;
            Mode mode = Modes[modeVal];

            var outColours = new LDRColour[NUM_PIXELS_PER_BLOCK];

            if (modeVal < 8)
            {
                int partitions = mode.Partitions;
                int numEndPoints = (partitions + 1) << 1;
                int indexPrecision = mode.IndexPrecision;
                int APrecision = mode.APrecision;
                int i;
                int[] P = new int[mode.PBits];
                int shape = GetBits(source, sourceStart, ref start, mode.PartitionBits);
                int rotation = GetBits(source, sourceStart, ref start, mode.RotationBits);
                int indexMode = GetBits(source, sourceStart, ref start, mode.IndexModeBits);

                LDRColour[] c = new LDRColour[6];
                LDRColour RGBPrecision = mode.RawRGBPrecision;
                LDRColour RGBPrecisionWithP = mode.RGBPrecisionWithP;

                // Red
                for(i = 0; i < numEndPoints; i++)
                {
                    if (start + RGBPrecision.R > 128)
                        Debugger.Break();  // Error

                    c[i].R = GetBits(source, sourceStart, ref start, RGBPrecision.R);
                }

                // Green
                for (i = 0; i < numEndPoints; i++)
                {
                    if (start + RGBPrecision.G > 128)
                        Debugger.Break();  // Error

                    c[i].G = GetBits(source, sourceStart, ref start, RGBPrecision.G);
                }

                // Blue
                for (i = 0; i < numEndPoints; i++)
                {
                    if (start + RGBPrecision.B > 128)
                        Debugger.Break();  // Error

                    c[i].B = GetBits(source, sourceStart, ref start, RGBPrecision.B);
                }

                // Alpha
                for (i = 0; i < numEndPoints; i++)
                {
                    if (start + RGBPrecision.A > 128)
                        Debugger.Break();  // Error

                    c[i].A = RGBPrecision.A == 0 ? 255 : GetBits(source, sourceStart, ref start, RGBPrecision.A);
                }

                // P Bits
                for (i = 0; i < mode.PBits; i++)
                {
                    if (start > 127)
                    {
                        Debugger.Break();
                        // Error
                    }

                    P[i] = GetBit(source, sourceStart, ref start);
                }


                // Adjust for P bits
                bool rDiff = RGBPrecision.R != RGBPrecisionWithP.R;
                bool gDiff = RGBPrecision.G != RGBPrecisionWithP.B;
                bool bDiff = RGBPrecision.G != RGBPrecisionWithP.G;
                bool aDiff = RGBPrecision.A != RGBPrecisionWithP.A;
                if (mode.PBits != 0)
                {
                    for (i = 0; i < numEndPoints; i++)
                    {
                        int pi = i * mode.PBits / numEndPoints;
                        if (rDiff)
                            c[i].R = (c[i].R << 1) | P[pi];

                        if (gDiff)
                            c[i].G = (c[i].G << 1) | P[pi];

                        if (bDiff)
                            c[i].B = (c[i].B << 1) | P[pi];

                        if (aDiff)
                            c[i].A = (c[i].A << 1) | P[pi];
                    }
                }

                for (i = 0; i < numEndPoints; i++)
                    c[i] = Unquantise(c[i], RGBPrecisionWithP);

                int[] w1 = new int[NUM_PIXELS_PER_BLOCK];
                int[] w2 = new int[NUM_PIXELS_PER_BLOCK];

                // Read colour indicies
                for (i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                {
                    int numBits = IsFixUpOffset(partitions, shape, i) ? indexPrecision - 1 : indexPrecision;
                    if (start + numBits > 128)
                    {
                        Debugger.Break();
                        // Error
                    }
                    w1[i] = GetBits(source, sourceStart, ref start, numBits);
                }

                // Read Alpha
                if (APrecision != 0)
                {
                    for (i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                    {
                        int numBits = i != 0 ? APrecision : APrecision - 1;
                        if (start + numBits > 128)
                        {
                        Debugger.Break();
                            // Error
                        }
                        w2[i] = GetBits(source, sourceStart, ref start, numBits);
                    }
                }


                for (i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                {
                    int region = PartitionTable[partitions][shape][i];
                    LDRColour outPixel;
                    if (APrecision == 0)
                        outPixel = Interpolate(c[region << 1], c[(region << 1) + 1], w1[i], w1[i], indexPrecision, indexPrecision);
                    else
                    {
                        if (indexMode == 0)
                            outPixel = Interpolate(c[region << 1], c[(region << 1) + 1], w1[i], w2[i], indexPrecision, APrecision);
                        else
                            outPixel = Interpolate(c[region << 1], c[(region << 1) + 1], w2[i], w1[i], APrecision, indexPrecision);
                    }

                    switch (rotation)
                    {
                        case 1:
                            int temp = outPixel.R;
                            outPixel.R = outPixel.A;
                            outPixel.A = temp;
                            break;
                        case 2:
                            temp = outPixel.G;
                            outPixel.G = outPixel.A;
                            outPixel.A = temp;
                            break;
                        case 3:
                            temp = outPixel.B;
                            outPixel.B = outPixel.A;
                            outPixel.A = temp;
                            break;
                    }

                    outColours[i] = outPixel;
                }
                return outColours;
            }
            else
            {
                return outColours;
            }
        }


        #region Decompression Helpers
        private static LDRColour Interpolate(LDRColour lDRColour1, LDRColour lDRColour2, int wc, int wa, int wcPrec, int waPrec)
        {
            LDRColour temp = InterpolateRGB(lDRColour1, lDRColour2, wc, wcPrec);
            temp.A = InterpolateA(lDRColour1, lDRColour2, wa, waPrec);
            return temp;
        }

        private static int InterpolateA(LDRColour lDRColour1, LDRColour lDRColour2, int wa, int waPrec)
        {
            int[] weights = null;
            switch (waPrec)
            {
                case 2:
                    weights = AWeights2;
                    break;
                case 3:
                    weights = AWeights3;
                    break;
                case 4:
                    weights = AWeights4;
                    break;
                default:
                    return 0;
            }
            return (lDRColour1.A * (BC67_WEIGHT_MAX - weights[wa]) + lDRColour2.A * weights[wa] + BC67_WEIGHT_ROUND) >> BC67_WEIGHT_SHIFT;
        }

        private static LDRColour InterpolateRGB(LDRColour lDRColour1, LDRColour lDRColour2, int wc, int wcPrec)
        {
            LDRColour temp = new LDRColour();
            int[] weights = null;
            switch (wcPrec)
            {
                case 2:
                    weights = AWeights2;
                    break;
                case 3:
                    weights = AWeights3;
                    break;
                case 4:
                    weights = AWeights4;
                    break;
                default:
                    return temp;
            }
            temp.R = (lDRColour1.R * (BC67_WEIGHT_MAX - weights[wc]) + lDRColour2.R * weights[wc] + BC67_WEIGHT_ROUND) >> BC67_WEIGHT_SHIFT;
            temp.G = (lDRColour1.G * (BC67_WEIGHT_MAX - weights[wc]) + lDRColour2.G * weights[wc] + BC67_WEIGHT_ROUND) >> BC67_WEIGHT_SHIFT;
            temp.B = (lDRColour1.B * (BC67_WEIGHT_MAX - weights[wc]) + lDRColour2.B * weights[wc] + BC67_WEIGHT_ROUND) >> BC67_WEIGHT_SHIFT;
            return temp;
        }

        static bool IsFixUpOffset(int partitions, int shape, int offset)
        {
            for (int i = 0; i <= partitions; i++)
            {
                if (offset == FixUpTable[partitions][shape][i])
                    return true;
            }

            return false;
        }

        static LDRColour Unquantise(LDRColour colour, LDRColour rGBPrecisionWithP)
        {
            LDRColour temp = new LDRColour()
            {
                R = Unquantise(colour.R, rGBPrecisionWithP.R),
                G = Unquantise(colour.G, rGBPrecisionWithP.G),
                B = Unquantise(colour.B, rGBPrecisionWithP.B),
                A = rGBPrecisionWithP.A > 0 ? Unquantise(colour.A, rGBPrecisionWithP.A) : 255
            };
            return temp;
        }

        private static int Unquantise(int r1, int r2)
        {
            int temp = r1 << (8 - r2);
            return temp | (temp >> r2);
        }

        static int GetBit(byte[] source, int sourceStart, ref int start)
        {
            int uIndex = start >> 3;
            int ret = (source[sourceStart + uIndex] >> (start - (uIndex << 3))) & 0x01;
            start++;
            return ret;
        }

        static int GetBits(byte[] source, int sourceStart, ref int start, int length)
        {
            if (length == 0)
                return 0;

            int uIndex = start >> 3;
            int uBase = start - (uIndex << 3);
            int ret = 0;

            if (uBase + length > 8)
            {
                int firstIndexBits = 8 - uBase;
                int nextIndexBits = length - firstIndexBits;
                ret = (source[sourceStart + uIndex] >> uBase) | ((source[sourceStart + uIndex + 1] & ((1 << nextIndexBits) - 1)) << firstIndexBits);
            }
            else
                ret = (source[sourceStart + uIndex] >> uBase) & ((1 << length) - 1);

            
            start += length;
            return ret;
        }
        #endregion Decompression Helpers
        #endregion Decompression


        #region Compression
        struct LDRColourEndPointPair
        {
            public LDRColour A;
            public LDRColour B;

            public LDRColourEndPointPair(LDRColour a, LDRColour b)
            {
                A = a;
                B = b;
            }
        }

        internal static void CompressBC7Block(byte[] source, int sourceStart, int sourceLineLength, byte[] destination, int destStart)  // Flags?
        {
            LDRColourEndPointPair[][] AllEndPoints = new LDRColourEndPointPair[BC7_MAX_SHAPES][];
            for(int i = 0; i < BC7_MAX_SHAPES; i++)
                AllEndPoints[i] = new LDRColourEndPointPair[BC7_MAX_REGIONS];

            LDRColour[] block = new LDRColour[NUM_PIXELS_PER_BLOCK];

            // Fill block for now
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    block[i * 4 + j] = new LDRColour(
                        source[sourceStart + (i * sourceLineLength) + j * 4 + 2],  // Red
                        source[sourceStart + (i * sourceLineLength) + j * 4 + 1],  // Green
                        source[sourceStart + (i * sourceLineLength) + j * 4],      // Blue
                        source[sourceStart + (i * sourceLineLength) + j * 4 + 3]); // Alpha
                }
            }


            int modeVal = 0;
            float MSEBest = float.MaxValue;

            for (modeVal = 0; modeVal < 8 && MSEBest > 0; modeVal++)
            {
                Mode mode = Modes[modeVal];
                int shapes = 1 << mode.PartitionBits;
                int numRots = 1 << mode.RotationBits;
                int numIdxMode = 1 << mode.IndexModeBits;
                int items = Math.Max(1, shapes >> 2);

                float[] roughMSEs = new float[BC7_MAX_SHAPES];
                int[] auShape = new int[BC7_MAX_SHAPES];

                for (int r = 0; r < numRots && MSEBest > 0; r++)
                {
                    switch (r)
                    {
                        case 1:
                            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            {
                                // Assume ComponentSize is bytes.
                                var temp = block[i].R;
                                block[i].R = block[i].A;
                                block[i].A = temp;
                            }
                            break;
                        case 2:
                            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            {
                                // Assume ComponentSize is bytes.
                                var temp = block[i].G;
                                block[i].G = block[i].A;
                                block[i].A = temp;
                            }
                            break;
                        case 3:
                            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            {
                                // Assume ComponentSize is bytes.
                                var temp = block[i].B;
                                block[i].B = block[i].A;
                                block[i].A = temp;
                            }
                            break;
                    }

                    for (int im = 0; im < numIdxMode && MSEBest > 0; im++)
                    {
                        for (int s = 0; s < shapes; s++)
                        {
                            roughMSEs[s] = RoughMSE(modeVal, AllEndPoints, block, s, im);
                            auShape[s] = s;
                        }

                        for(int i = 0; i < items; i++)
                        {
                            for (int j = i + 1; j < shapes; j++)
                            {
                                if (roughMSEs[i] > roughMSEs[j])
                                {
                                    var temp = roughMSEs[i];
                                    roughMSEs[i] = roughMSEs[j];
                                    roughMSEs[j] = temp;

                                    var temp2 = auShape[j];
                                    auShape[i] = auShape[j];
                                    auShape[j] = temp2;
                                }
                            }
                        }

                        for (int i = 0; i < items && MSEBest > 0; i++)
                        {
                            float mse = Refine(auShape[i], r, im, AllEndPoints, mode, block, destination, destStart);
                            if (mse < MSEBest)
                                MSEBest = mse;
                        }
                    }

                    switch (r)
                    {
                        case 1: 
                            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            {
                                var temp = block[i].A;
                                block[i].A = block[i].R;
                                block[i].R = temp;
                            }
                            break;
                        case 2:
                            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            {
                                var temp = block[i].A;
                                block[i].A = block[i].G;
                                block[i].G = temp;
                            }
                            break;
                        case 3:
                            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            {
                                var temp = block[i].A;
                                block[i].A = block[i].B;
                                block[i].B = temp;
                            }
                            break;
                    }
                }
            }
        }

        #region Compression Helpers
        static float Refine(int shape, int rotation, int indexMode, LDRColourEndPointPair[][] AllEndPoints, Mode mode, LDRColour[] block, byte[] destination, int destStart)
        {
            LDRColourEndPointPair[] EndPoints = AllEndPoints[shape];
            
            LDRColourEndPointPair[] OrgEndPoints = new LDRColourEndPointPair[BC7_MAX_REGIONS];
            LDRColourEndPointPair[] OptEndPoints = new LDRColourEndPointPair[BC7_MAX_REGIONS];

            int[] orgIdx = new int[NUM_PIXELS_PER_BLOCK];
            int[] orgIdx2 = new int[NUM_PIXELS_PER_BLOCK];
            int[] optIdx = new int[NUM_PIXELS_PER_BLOCK];
            int[] optIdx2 = new int[NUM_PIXELS_PER_BLOCK];

            float[] orgErr = new float[BC7_MAX_REGIONS];
            float[] optErr = new float[BC7_MAX_REGIONS];

            for (int p = 0; p <= mode.Partitions; p++)
            {
                OrgEndPoints[p].A = Quantize(EndPoints[p].A, mode.RGBPrecisionWithP);
                OrgEndPoints[p].B = Quantize(EndPoints[p].B, mode.RGBPrecisionWithP);
            }

            AssignIndicies(shape, indexMode, mode, OrgEndPoints, orgIdx, orgIdx2, orgErr, block);
            OptimiseEndPoints(mode, indexMode, block, orgErr, OrgEndPoints, OptEndPoints);
            AssignIndicies(shape, indexMode, mode, OrgEndPoints, orgIdx, orgIdx2, orgErr, block);

            float orgTotErr = 0, optTotErr = 0;
            for (int p = 0; p <= mode.Partitions; p++)
            {
                orgTotErr += orgErr[p];
                optTotErr += optErr[p];
            }

            if (optTotErr < orgTotErr)
            {
                EmitBlock(mode, destination, destStart, rotation, indexMode, shape, OptEndPoints, optIdx, optIdx2);
                return optTotErr;
            }
            else
            {
                EmitBlock(mode, destination, destStart, rotation, indexMode, shape, OrgEndPoints, orgIdx, orgIdx2);
                return orgTotErr;
            }
        }

        static void EmitBlock(Mode mode, byte[] destination, int destStart, int rotation, int indexMode, int shape, LDRColourEndPointPair[] endPoints, int[] indicies, int[] alphaIndicies)
        {
            int i = 0;
            int startBit = 0;

            SetBits(ref startBit, mode.modeVal, 0, destination, destStart);
            SetBits(ref startBit, 1, 1, destination, destStart);
            SetBits(ref startBit, mode.RotationBits, rotation, destination, destStart);
            SetBits(ref startBit, mode.IndexModeBits, indexMode, destination, destStart);
            SetBits(ref startBit, mode.PartitionBits, shape, destination, destStart);

            int[] prec = new int[BC7_NUM_CHANNELS] { mode.RawRGBPrecision.R, mode.RawRGBPrecision.G, mode.RawRGBPrecision.B, mode.RawRGBPrecision.A };
            int[] precP = new int[BC7_NUM_CHANNELS] { mode.RGBPrecisionWithP.R, mode.RGBPrecisionWithP.G, mode.RGBPrecisionWithP.B, mode.RGBPrecisionWithP.A };

            if (mode.PBits != 0)
            {
                int numEP = (1 + mode.Partitions) << 1;
                int[] aPVote = new int[BC7_MAX_REGIONS << 1] { 0, 0, 0, 0, 0, 0 };
                int[] aCount = new int[BC7_MAX_REGIONS << 1] { 0, 0, 0, 0, 0, 0 };

                
                for (int ch = 0; ch < BC7_NUM_CHANNELS; ch++)
                {
                    int ep = 0;
                    for (i = 0; i <= mode.Partitions; i++)
                    {
                        int[] A = new int[BC7_NUM_CHANNELS] { endPoints[i].A.R, endPoints[i].A.G, endPoints[i].A.B, endPoints[i].A.A };
                        int[] B = new int[BC7_NUM_CHANNELS] { endPoints[i].B.R, endPoints[i].B.G, endPoints[i].B.B, endPoints[i].B.A };

                        if (prec[ch] == precP[ch])
                        {
                            SetBits(ref startBit, prec[ch], A[ch], destination, destStart);
                            SetBits(ref startBit, prec[ch], B[ch], destination, destStart);
                        }
                        else
                        {
                            SetBits(ref startBit, prec[ch], A[ch] >> 1, destination, destStart);
                            SetBits(ref startBit, prec[ch], B[ch] >> 1, destination, destStart);

                            int idx = ep++ * mode.PBits / numEP;
                            aPVote[idx] += A[ch] & 0x01;
                            aCount[idx]++;

                            idx = ep++ * mode.PBits / numEP;
                            aPVote[idx] += B[ch] & 0x01;
                            aCount[idx]++;
                        }
                    }
                }

                for (i = 0; i < mode.PBits; i++)
                    SetBits(ref startBit, 1, aPVote[i] > (aCount[i] >> 1) ? 1 : 0, destination, destStart);
            }
            else
            {
                for (int ch = 0; ch < BC7_NUM_CHANNELS; ch++)
                {
                    for (i = 0; i <= mode.Partitions; i++)
                    {
                        int[] A = new int[BC7_NUM_CHANNELS] { endPoints[i].A.R, endPoints[i].A.G, endPoints[i].A.B, endPoints[i].A.A };
                        int[] B = new int[BC7_NUM_CHANNELS] { endPoints[i].B.R, endPoints[i].B.G, endPoints[i].B.B, endPoints[i].B.A };

                        SetBits(ref startBit, prec[ch], A[ch], destination, destStart);
                        SetBits(ref startBit, prec[ch], B[ch], destination, destStart);
                    }
                }
            }

            int[] aI1 = indexMode != 0 ? alphaIndicies : indicies;
            int[] aI2 = indexMode != 0 ? indicies : alphaIndicies;

            for (i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
            {
                if (IsFixUpOffset(mode.Partitions, shape, i))
                    SetBits(ref startBit, mode.IndexPrecision - 1, aI1[i], destination, destStart);
                else
                    SetBits(ref startBit, mode.IndexPrecision, aI1[i], destination, destStart);
            }

            if (mode.APrecision != 0)
                for (i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                    SetBits(ref startBit, i != 0 ? mode.APrecision : mode.APrecision - 1, aI2[i], destination, destStart);
        }

        static void SetBits(ref int start, int length, int value, byte[] destination, int destStart)
        {
            if (length == 0)
                return;

            int index = start >> 3;
            int uBase = start - (index << 3);

            if (uBase + length > 8)
            {
                int firstIndexBits = 8 - uBase;
                int nextIndexBits = length - firstIndexBits;
                destination[destStart + index] &= (byte)~(((1 << firstIndexBits) - 1) << uBase);
                destination[destStart + index] |= (byte)(value << uBase);
                destination[destStart + index + 1] &= (byte)~((1 << nextIndexBits) - 1);
                destination[destStart + index + 1] |= (byte)(value >> firstIndexBits);
            }
            else
            {
                destination[destStart + index] &= (byte)~(((1 << length) - 1) << uBase);
                destination[destStart + index] |= (byte)(value << uBase);
            }

            start += length;
        }

        static void OptimiseEndPoints(Mode mode, int indexMode, LDRColour[] block, float[] afOrgErr, LDRColourEndPointPair[] aOrgEndPoints, LDRColourEndPointPair[] aOptEndPoints)
        {
            LDRColour[] pixels = new LDRColour[NUM_PIXELS_PER_BLOCK];

            for (int p = 0; p <= mode.Partitions; p++)
            {
                // Collect pixels in region
                int np = 0;
                for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                    pixels[np++] = block[i];

                OptimiseOne(afOrgErr[p], mode, ref aOrgEndPoints[p], ref aOptEndPoints[p], indexMode, block, np);
            }

        }

        static void OptimiseOne(float fOrgErr, Mode mode, ref LDRColourEndPointPair org, ref LDRColourEndPointPair opt, int indexMode, LDRColour[] block, int np)
        {
            float fOptErr = fOrgErr;
            opt = org; 
            LDRColourEndPointPair new_a = new LDRColourEndPointPair(), new_b = new LDRColourEndPointPair(), newEndPoints = new LDRColourEndPointPair();
            bool do_b;

            // Optimise each channel separately
            for (int ch = 0; ch < BC7_NUM_CHANNELS; ch++)
            {
                switch (ch)
                {
                    case 0:
                        if (mode.RGBPrecisionWithP.R == 0)
                            continue;
                        break;
                    case 1:
                        if (mode.RGBPrecisionWithP.G == 0)
                            continue;
                        break;
                    case 2:
                        if (mode.RGBPrecisionWithP.B == 0)
                            continue;
                        break;
                    case 3:
                        if (mode.RGBPrecisionWithP.A == 0)
                            continue;
                        break;
                }
                

                // figure out which endpoint when perturbed gives the most improvement and start there
                // if we just alternate, we can easily end up in a local minima
                float fErr0 = PerturbOne(mode, ch, ref new_a, ref opt, fOptErr, false, indexMode, block, np);
                float fErr1 = PerturbOne(mode, ch, ref new_b, ref opt, fOptErr, true, indexMode, block, np);

                int copt_a = 0, copt_b = 0, cnew_a = 0, cnew_b = 0;
                switch (ch)
                {
                    case 0:
                        copt_a = opt.A.R;
                        copt_b = opt.B.R;
                        cnew_a = new_a.A.R;
                        cnew_b = new_a.B.R;
                        break;
                    case 1:
                        copt_a = opt.A.G;
                        copt_b = opt.B.G;
                        cnew_a = new_a.A.G;
                        cnew_b = new_a.B.G;
                        break;
                    case 2:
                        copt_a = opt.A.B;
                        copt_b = opt.B.B;
                        cnew_a = new_a.A.B;
                        cnew_b = new_a.B.B;
                        break;
                    case 3:
                        copt_a = opt.A.A;
                        copt_b = opt.B.A;
                        cnew_a = new_a.A.A;
                        cnew_b = new_a.B.A;
                        break;
                }

                if (fErr0 < fErr1)
                {
                    if (fErr0 >= fOptErr)
                        continue;

                    copt_a = cnew_a;
                    fOptErr = fErr0;
                    do_b = true;
                }
                else
                {
                    if (fErr1 >= fOptErr)
                        continue;

                    copt_b = cnew_b;
                    fOptErr = fErr1;
                    do_b = false;
                }

                // Now alternate end points and keep trying till there is not improvement
                for (;;)
                {
                    float err = PerturbOne(mode, ch, ref newEndPoints, ref opt, fOptErr, do_b, indexMode, block, np);
                    if (err >= fOptErr)
                        break;

                    if (!do_b)
                        copt_a = cnew_a;
                    else
                        copt_b = cnew_b;

                    fOptErr = err;
                    do_b = !do_b;
                }

                // Set variables
                switch (ch)
                {
                    case 0:
                        opt.A.R = copt_a;
                        opt.B.R = copt_b;
                        new_a.A.R = cnew_a;
                        new_a.B.R = cnew_b;
                        break;
                    case 1:
                        opt.A.G = copt_a;
                        opt.B.G = copt_b;
                        new_a.A.G = cnew_a;
                        new_a.B.G = cnew_b;
                        break;
                    case 2:
                        opt.A.B = copt_a;
                        opt.B.B = copt_b;
                        new_a.A.B = cnew_a;
                        new_a.B.B = cnew_b;
                        break;
                    case 3:
                        opt.A.A = copt_a;
                        opt.B.A = copt_b;
                        new_a.A.A = cnew_a;
                        new_a.B.A = cnew_b;
                        break;
                }
            }

            // Small exhaustive search around global minima to be sure
            Exhaustive(mode, 0, opt, ref opt.A.R, ref opt.B.R, mode.RGBPrecisionWithP.R, fOrgErr, indexMode, np, block);
            Exhaustive(mode, 1, opt, ref opt.A.G, ref opt.B.G, mode.RGBPrecisionWithP.G, fOrgErr, indexMode, np, block);
            Exhaustive(mode, 2, opt, ref opt.A.B, ref opt.B.B, mode.RGBPrecisionWithP.B, fOrgErr, indexMode, np, block);
            Exhaustive(mode, 3, opt, ref opt.A.A, ref opt.B.A, mode.RGBPrecisionWithP.A, fOrgErr, indexMode, np, block);
        }

        static unsafe void Exhaustive(Mode mode, int ch, LDRColourEndPointPair endPoint, ref int A, ref int B, int prec, float fOrgErr, int indexMode, int np, LDRColour[] block)
        {
            LDRColourEndPointPair temp;
            if (fOrgErr == 0)
                return;

            int delta = 5;


            // Figure out range of A and B
            temp = endPoint;
            int alow = Math.Max(0, A + delta);
            int ahigh = Math.Min((1 << prec) - 1, A + delta);
            int blow = Math.Max(0, B - delta);
            int bhigh = Math.Min((1 << prec) - 1, B + delta);
            int amin = 0, bmin = 0;

            int* tempAChannel = (int*)0, tempBChannel = (int*)0;
            switch (ch)
            {
                case 0:
                    tempAChannel = &temp.A.R;
                    tempBChannel = &temp.B.R;
                    break;
                case 1:
                    tempAChannel = &temp.A.G;
                    tempBChannel = &temp.B.G;
                    break;
                case 2:
                    tempAChannel = &temp.A.B;
                    tempBChannel = &temp.B.B;
                    break;
                case 3:
                    tempAChannel = &temp.A.A;
                    tempBChannel = &temp.B.A;
                    break;
            }

            float bestErr = fOrgErr;
            if (A <= B)
            {
                // Keep A <= B
                for (int a = alow; a <= ahigh; a++)
                {
                    for (int b = Math.Max(a, blow); b < bhigh; b++)
                    {
                        *tempAChannel = a;
                        *tempBChannel = b;

                        float err = MapColours(indexMode, mode, temp, np, block, bestErr);
                        if (err < bestErr)
                        {
                            amin = a;
                            bmin = b;
                            bestErr = err;
                        }
                    }
                }
            }
            else
            {
                for (int b = blow; b < bhigh; b++)
                {
                    for (int a = Math.Max(b, alow); a <= ahigh; a++)
                    {
                        *tempAChannel = a;
                        *tempBChannel = b;

                        float err = MapColours(indexMode, mode, temp, np, block, bestErr);
                        if (err < bestErr)
                        {
                            amin = a;
                            bmin = b;
                            bestErr = err;
                        }
                    }
                }
            }

            if (bestErr < fOrgErr)
            {
                A = amin;
                B = bmin;
                fOrgErr = bestErr;
            }
        }

        static float PerturbOne(Mode mode, int ch, ref LDRColourEndPointPair newEndPoints, ref LDRColourEndPointPair oldEndPoints, float oldErr, bool do_b, int indexMode, LDRColour[] block, int np)
        {
            LDRColourEndPointPair temp = newEndPoints = oldEndPoints;
            float minErr = oldErr;

            int prec = 0;
            int new_c = 0;
            int tmp_c = 0;
            switch (ch)
            {
                case 0:
                    prec = mode.RGBPrecisionWithP.R;
                    new_c = do_b ? newEndPoints.B.R : newEndPoints.A.R;
                    tmp_c = do_b ? temp.B.R : temp.A.R;
                    break;
                case 1:
                    prec = mode.RGBPrecisionWithP.G;
                    new_c = do_b ? newEndPoints.B.G : newEndPoints.A.G;
                    tmp_c = do_b ? temp.B.G : temp.A.G;
                    break;
                case 2:
                    prec = mode.RGBPrecisionWithP.B;
                    new_c = do_b ? newEndPoints.B.B : newEndPoints.A.B;
                    tmp_c = do_b ? temp.B.B : temp.A.B;
                    break;
                case 3:
                    prec = mode.RGBPrecisionWithP.A;
                    new_c = do_b ? newEndPoints.B.A : newEndPoints.A.A;
                    tmp_c = do_b ? temp.B.A : temp.A.A;
                    break;
            }

            // Log search for best error for this end point
            for (int step = 1 << (prec - 1); step != 0; step >>= 1)
            {
                bool improved = false;
                int bestStep = 0;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int tmp = new_c + sign * step;
                    if (tmp < 0 || tmp >= (1 << prec))
                        continue;
                    else
                        tmp_c = tmp;

                    float totalError = MapColours(indexMode, mode, temp, np, block, minErr);
                    {
                        improved = true;
                        minErr = totalError;
                        bestStep = sign * step;
                    }
                }

                // If improvement, move endpoint and continue from there
                if (improved)
                    new_c += bestStep;
            }

            // Update variables
            switch (ch)
            {
                case 0:
                    if (do_b)
                        newEndPoints.B.R = new_c;
                    else
                        newEndPoints.A.R = new_c;
                    break;
                case 1:
                    if (do_b)
                        newEndPoints.B.G = new_c;
                    else
                        newEndPoints.A.G = new_c;
                    break;
                case 2:
                    if (do_b)
                        newEndPoints.B.B = new_c;
                    else
                        newEndPoints.A.B = new_c;
                    break;
                case 3:
                    if (do_b)
                        newEndPoints.B.A = new_c;
                    else
                        newEndPoints.A.A = new_c;
                    break;
            }

            return minErr;
        }

        static float MapColours(int indexMode, Mode mode, LDRColourEndPointPair endPoints, int np, LDRColour[] block, float minErr)
        {
            int indexPrec = indexMode != 0 ? mode.APrecision : mode.IndexPrecision;
            int aIndexPrec = indexMode != 0 ? mode.IndexPrecision : mode.APrecision;

            LDRColour[] palette = new LDRColour[BC7_MAX_INDICIES];
            float totalErr = 0;

            GeneratePaletteQuantized(endPoints, mode, palette);
            for (int i = 0; i < np; i++)
            {
                totalErr += ComputeError(block[i], palette, indexPrec, aIndexPrec);
                if (totalErr > minErr)
                {
                    totalErr = float.MaxValue;
                    break;
                }
            }

            return totalErr;
        }

        static void AssignIndicies(int shape, int indexMode, Mode mode, LDRColourEndPointPair[] endPoints, int[] indicies, int[] alphaIndicies, float[] afTotalErr, LDRColour[] block)
        {
            int numIndicies = 1 << mode.IndexPrecision;
            int alphaNumIndicies = 1 << mode.APrecision;

            int highestIndexBit = numIndicies >> 1;
            int alphaHighestIndexBit = alphaNumIndicies >> 1;

            LDRColour[][] palette = new LDRColour[BC7_MAX_REGIONS][];
            for (int i = 0; i < BC7_MAX_REGIONS; i++)
                palette[i] = new LDRColour[BC7_MAX_INDICIES];

            // Get list of possibles
            for (int p = 0; p <= mode.Partitions; p++)
            {
                GeneratePaletteQuantized(endPoints[p], mode, palette[p]);
                afTotalErr[p] = 0;
            }

            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
            {
                int region = PartitionTable[mode.Partitions][shape][i];
                afTotalErr[region] += ComputeError(block[i], palette[region], numIndicies, alphaNumIndicies);
            }

            // Swap endpoints as needed to ensure that indicies at index_positions have a 0 high-order bit
            if (mode.APrecision == 0)
            {
                for (int p = 0; p <= mode.Partitions; p++)
                {
                    if ((indicies[FixUpTable[mode.Partitions][shape][p]] & highestIndexBit) != 0)
                    {
                        var temp = endPoints[p].B;
                        endPoints[p].B = endPoints[p].A;
                        endPoints[p].A = endPoints[p].B;

                        for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            if (PartitionTable[mode.Partitions][shape][i] == p)
                                indicies[i] = numIndicies - 1 - indicies[i];
                    }
                }
            }
            else
            {
                for (int p = 0; p <= mode.Partitions; p++)
                {
                    if ((indicies[FixUpTable[mode.Partitions][shape][p]] & highestIndexBit) != 0)
                    {
                        var temp = endPoints[p].A.R;
                        endPoints[p].A.R = endPoints[p].B.R;
                        endPoints[p].B.R = temp;

                        temp = endPoints[p].A.G;
                        endPoints[p].A.G = endPoints[p].B.G;
                        endPoints[p].B.G = temp;

                        temp = endPoints[p].A.B;
                        endPoints[p].A.B = endPoints[p].B.B;
                        endPoints[p].B.B = temp;

                        for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            if (PartitionTable[mode.Partitions][shape][i] == p)
                                indicies[i] = numIndicies - 1 - indicies[i];
                    }

                    if ((alphaIndicies[0] & alphaHighestIndexBit) != 0)
                    {
                        var temp = endPoints[p].A.A;
                        endPoints[p].A.A = endPoints[p].B.A;
                        endPoints[p].B.A = temp;

                        for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                            alphaIndicies[i] = alphaNumIndicies - 1 - alphaIndicies[i];
                    }
                }
            }
        }

        static void GeneratePaletteQuantized(LDRColourEndPointPair endPoints, Mode mode, LDRColour[] palette)
        {
            int numIndicies = 1 << mode.IndexPrecision;
            int alphaNumIndicies = 1 << mode.APrecision;

            LDRColour a = Unquantise(endPoints.A, mode.RGBPrecisionWithP);
            LDRColour b = Unquantise(endPoints.B, mode.RGBPrecisionWithP);

            if (mode.APrecision == 0)
            {
                for (int i = 0; i < numIndicies; i++)
                    palette[i] = Interpolate(a, b, i, i, mode.IndexPrecision, mode.IndexPrecision);
            }
            else
            {
                for (int i = 0; i < numIndicies; i++)
                    palette[i].A = InterpolateA(a, b, i, mode.IndexPrecision);

                for (int i = 0; i < numIndicies; i++)
                    palette[i] = InterpolateRGB(a, b, i, mode.APrecision);
            }
        }

        static LDRColour Quantize(LDRColour colour, LDRColour precision)
        {
            LDRColour q = new LDRColour
            {
                R = Quantize(colour.R, precision.R),
                G = Quantize(colour.G, precision.G),
                B = Quantize(colour.B, precision.B)
            };

            if (precision.A != 0)
                q.A = Quantize(colour.A, precision.A);
            else
                q.A = 255;

            return q;
        }

        static int Quantize(int c, int precision)
        {
            int round = Math.Min(255, c + (1 << (7 - precision)));
            return round >> (8 - precision);
        }

        static float RoughMSE(int modeVal, LDRColourEndPointPair[][] allEndPoints, LDRColour[] block, int shape, int indexMode)
        {
            LDRColourEndPointPair[] EndPoints = allEndPoints[shape];

            Mode mode = Modes[modeVal];

            int partitions = mode.Partitions;
            int indexPrecision = mode.IndexPrecision;
            int alphaPrecision = mode.APrecision;
            int numIndicies = 1 << indexPrecision;
            int alphaNumIndicies = 1 << alphaPrecision;

            int[] pixelIndicies = new int[NUM_PIXELS_PER_BLOCK];
            LDRColour[][] palette = new LDRColour[BC7_MAX_REGIONS][];
            for (int i = 0; i < BC7_MAX_REGIONS; i++)
                palette[i] = new LDRColour[BC7_MAX_INDICIES];


            // Convert block to "HDR", I'm just putting it in the 0-1 range.
            RGBColour[] Colour = new RGBColour[block.Length];
            for (int i = 0; i < block.Length; i++)
                Colour[i] = new RGBColour(block[i].R / 255f, block[i].G / 255f, block[i].B / 255f, block[i].A / 255f);

            for (int p = 0; p <= partitions; p++)
            {
                int np = 0;
                for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                    if (PartitionTable[partitions][shape][i] == p)
                        pixelIndicies[np++] = i;

                if (np == 1)
                {
                    EndPoints[p].A = block[pixelIndicies[0]];
                    EndPoints[p].B = block[pixelIndicies[0]];
                    continue;
                }
                else if (np == 2)
                {
                    EndPoints[p].A = block[pixelIndicies[0]];
                    EndPoints[p].B = block[pixelIndicies[1]];
                    continue;
                }

                if (alphaPrecision == 0)
                {
                    RGBColour[] MinMax= OptimiseRGB(Colour, 4);
                    EndPoints[p].A = new LDRColour(MinMax[0].r, MinMax[0].g, MinMax[0].b, MinMax[0].a);
                    EndPoints[p].B = new LDRColour(MinMax[1].r, MinMax[1].g, MinMax[1].b, MinMax[1].a);
                }
                else
                {
                    int maxA = 0, minA = 255;
                    for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
                    {
                        minA = Math.Min(minA, block[pixelIndicies[i]].A);
                        maxA = Math.Min(maxA, block[pixelIndicies[i]].A);
                    }

                    RGBColour[] MinMax = OptimiseRGB(Colour, 4);
                    EndPoints[p].A = new LDRColour(MinMax[0].r, MinMax[0].g, MinMax[0].b, MinMax[0].a);
                    EndPoints[p].B = new LDRColour(MinMax[1].r, MinMax[1].g, MinMax[1].b, MinMax[1].a);
                    EndPoints[p].A.A = minA;
                    EndPoints[p].B.A = maxA;
                }
            }

            if (alphaPrecision == 0)
            {
                for(int p = 0; p <= partitions; p++)
                    for (int i = 0; i < numIndicies; i++)
                        palette[p][i] = Interpolate(EndPoints[p].A, EndPoints[p].B, i, i, indexPrecision, indexPrecision);
            }
            else
            {
                for (int p = 0; p <= partitions; p++)
                {
                    for (int i = 0; i < numIndicies; i++)
                        palette[p][i] = InterpolateRGB(EndPoints[p].A, EndPoints[p].B, i, indexPrecision);

                    for(int i = 0; i < alphaPrecision; i++)
                        palette[p][i].A = InterpolateA(EndPoints[p].A, EndPoints[p].B, i, alphaPrecision);
                }
            }

            float totalError = 0;
            for (int i = 0; i < NUM_PIXELS_PER_BLOCK; i++)
            {
                int region = PartitionTable[partitions][shape][i];
                totalError += ComputeError(block[i], palette[region], numIndicies, alphaNumIndicies);
            }

            return totalError;
        }

        private static float ComputeError(LDRColour pixelColour, LDRColour[] palette, int numIndicies, int numAlphaIndicies)
        {
            float totalError = 0, bestError = float.MaxValue;

            // Vector dots
            var vPixel = new Vector4(pixelColour.R, pixelColour.G, pixelColour.B, pixelColour.A);

            for (int i = 0; i < numIndicies && bestError > 0; i++)
            {
                var tPixel = new Vector4(palette[i].R, palette[i].G, palette[i].B, palette[i].A);
                tPixel = vPixel - tPixel;

                float err = Vector4.Dot(tPixel, tPixel);
                if (err > bestError)
                    break;

                if (err < bestError)
                    bestError = err;
            }

            totalError += bestError;

            if (numAlphaIndicies != 1)
            {
                bestError = float.MaxValue;
                for (int i=0;i<numAlphaIndicies && bestError > 0; i++)
                {
                    float ea = pixelColour.A - palette[i].A;
                    float err = ea * ea;
                    if (err > bestError)
                        break;

                    if (err < bestError)
                        bestError = err;
                }
            }

            totalError += bestError;

            return totalError;
        }
        #endregion Compression Helpers
        #endregion Compression
    }
}
