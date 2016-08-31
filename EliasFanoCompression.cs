using System;
using System.Collections.Generic;
using System.Diagnostics;


// EliasFanoCompression: quasi-succinct compression of sorted integers in C#
//
// Elias-Fano encoding is quasi succinct, which means it is almost as good as the best theoretical possible compression scheme for sorted integers. 
// While it can be used to compress any sorted list of integers, we will use it for compressing posting lists of inverted indexes.
// Based on a research paper by Sebastiano Vigna: http://vigna.di.unimi.it/ftp/papers/QuasiSuccinctIndices.pdf
//
// Copyright (C) 2016 Wolf Garbe
// Version: 1.0
// Author: Wolf Garbe <wolf.garbe@faroo.com>
// Maintainer: Wolf Garbe <wolf.garbe@faroo.com>
// URL: http://blog.faroo.com/2016/08/22/elias-fano_quasi-succinct_compression_of_sorted_integers_in_csharp
// Description: http://blog.faroo.com/2016/08/22/elias-fano_quasi-succinct_compression_of_sorted_integers_in_csharp
//
// License:
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License, 
// version 3.0 (LGPL-3.0) as published by the Free Software Foundation.
// http://www.opensource.org/licenses/LGPL-3.0


static class EliasFanoCompression
{
    public static Random rnd = new Random(500);

    // generates a sorted list of n integers with no duplicates within range
    public static List<uint> generatePostingList(int n, int range)
    {
        if ((n < 1) || (n > range) || (range < 1)) Console.WriteLine("n within 1...range and range>0!");

        List<uint> postingList = new List<uint>(n);

        // hashset fits in RAM && enough gaps (n*1.1<range)
        if ((n <= 10000000) && (n * 1.1 < range))
        {
            // fast for sparse lists, in dense lists many loops because difficult for random to hit remaining gaps, hashset required (RAM), sorting required 
            HashSet<uint> hs = new HashSet<uint>();
            while (hs.Count < n)
            {
                uint docID = (uint)rnd.Next(1, range);
                
                // make sure docid are unique! 
                // strictly positive delta, no zero allowed (we dont allow a zero for the docid because then the delta for the first docid in a posting list could be zero)
                if (hs.Add(docID)) postingList.Add(docID);
            }
            postingList.Sort();
        }
        else
        {
            // slow for sparse lists as it loops through whole range, fast for dense lists, no hashset required, no sorting required
            for (uint i = 1; i <= range; i++)
            {
                // derived from: if ( rnd.Next(range)<n) postingList.Add(i);
                // adjusting probabilities so that exact number n is generated
                if (rnd.Next(range - (int)i) < (n - postingList.Count))
                {
                    postingList.Add(i);
                }
            }
        }

        return postingList;
    }


    public static void EliasFanoCompress(List<uint> postingList, byte[] compressedBuffer, ref int compressedBufferPointer2)
    {
        // Elias Fano Coding
        // compress sorted integers: Given n and u we have a monotone sequence 0 ≤ x0, x1, x2, ... , xn-1 ≤ u
        // at most 2 + log(u / n) bits per element
        // Quasi-succinct: less than half a bit away from succinct bound!
        // https://en.wikipedia.org/wiki/Unary_coding
        // http://vigna.di.unimi.it/ftp/papers/QuasiSuccinctIndices.pdf
        // http://shonan.nii.ac.jp/seminar/029/wp-content/uploads/sites/12/2013/07/Sebastiano_Shonan.pdf
        // http://www.di.unipi.it/~ottavian/files/elias_fano_sigir14.pdf
        // http://hpc.isti.cnr.it/hpcworkshop2014/PartitionedEliasFanoIndexes.pdf

        Stopwatch sw = Stopwatch.StartNew();

        uint lastDocID = 0;

        ulong buffer1 = 0;
        int bufferLength1 = 0;
        ulong buffer2 = 0;
        int bufferLength2 = 0;

        uint largestblockID = (uint)postingList[postingList.Count - 1];
        double averageDelta = (double)largestblockID / (double)postingList.Count;
        double averageDeltaLog = Math.Log(averageDelta, 2);
        int lowBitsLength = (int)Math.Floor(averageDeltaLog); if (lowBitsLength < 0) lowBitsLength = 0;
        ulong lowBitsMask = (((ulong)1 << lowBitsLength) - 1);

        int compressedBufferPointer1 = 0;

        // +6 : for docid number, lowerBitsLength and ceiling
        compressedBufferPointer2 = lowBitsLength * postingList.Count / 8 + 6; 

        // store postingList.Count for decompression: LSB first
        compressedBuffer[compressedBufferPointer1++] = (byte)(postingList.Count & 255);
        compressedBuffer[compressedBufferPointer1++] = (byte)((postingList.Count >> 8) & 255);
        compressedBuffer[compressedBufferPointer1++] = (byte)((postingList.Count >> 16) & 255);
        compressedBuffer[compressedBufferPointer1++] = (byte)((postingList.Count >> 24) & 255);

        // store lowerBitsLength for decompression
        compressedBuffer[compressedBufferPointer1++] = (byte)lowBitsLength;

        foreach (uint docID in postingList)
        {
            // docID strictly monotone/increasing numbers, docIdDelta strictly positive, no zero allowed
            uint docIdDelta = (docID - lastDocID - 1); 

            // low bits
            // Store the lower l= log(u / n) bits explicitly
            // binary packing/bit packing

            buffer1 <<= lowBitsLength;
            buffer1 |= (docIdDelta & lowBitsMask);
            bufferLength1 += lowBitsLength;

            // flush buffer to compressedBuffer
            while (bufferLength1 > 7)
            {
                bufferLength1 -= 8;
                compressedBuffer[compressedBufferPointer1++] = (byte)(buffer1 >> bufferLength1);          
            }

            // high bits
            // Store high bits as a sequence of unary coded gaps
            // 0=1, 1=01, 2=001, 3=0001, ...
            // https://en.wikipedia.org/wiki/Unary_coding

            // length of unary code 
            uint unaryCodeLength = (uint)(docIdDelta >> lowBitsLength) + 1; 
            buffer2 <<= (int)unaryCodeLength;
            
            // set most right bit 
            buffer2 |= 1;
            bufferLength2 += (int)unaryCodeLength;

            // flush buffer to compressedBuffer
            while (bufferLength2 > 7)
            {
                bufferLength2 -= 8; 
                compressedBuffer[compressedBufferPointer2++] = (byte)(buffer2 >> bufferLength2);  
            }

            lastDocID = docID;
        }

        // final flush buffer
        if (bufferLength1 > 0)
        {
            compressedBuffer[compressedBufferPointer1++] = (byte)(buffer1 << (8 - bufferLength1));
        }

        if (bufferLength2 > 0)
        {
            compressedBuffer[compressedBufferPointer2++] = (byte)(buffer2 << (8 - bufferLength2));
        }

        Console.WriteLine("\rCompression:   " + sw.ElapsedMilliseconds.ToString("N0") + " ms  " + postingList.Count.ToString("N0") +" DocID  delta: " + averageDelta.ToString("N2") + "  low bits: " + lowBitsLength.ToString() + "   bits/DocID: " + ((double)compressedBufferPointer2 * (double)8 / (double)postingList.Count).ToString("N2")+" (" + (2+averageDeltaLog).ToString("N2")+")  uncompressed: " + ((ulong)postingList.Count*4).ToString("N0") + "  compressed: " + compressedBufferPointer2.ToString("N0") +"  ratio: "+ ((double)postingList.Count * 4/ compressedBufferPointer2).ToString("N2")) ;
    }

    public static uint[,] decodingTableHighBits = new uint[256, 8];
    public static byte[] decodingTableDocIdNumber = new byte[256];
    public static byte[] decodingTableHighBitsCarryover = new byte[256];

    public static void eliasFanoCreateDecodingTable()
    {
        for (int i = 0; i < 256; i++)
        {
            byte zeroCount = 0;
            for (int j = 7; j >= 0; j--)
            {
                // count 1 within i
                if ((i & (1 << j)) > 0)
                {
                    // unary code of high bits of nth docid within this byte
                    decodingTableHighBits[i, decodingTableDocIdNumber[i] ] = zeroCount; 

                    // docIdNumber = number of docid = number of 1 within one byte
                    decodingTableDocIdNumber[i]++;
                    zeroCount = 0;
                }
                else
                {
                    // count 0 since last 1 within i
                    zeroCount++;
                }
            }
            // number of trailing zeros (zeros carryover), if whole byte=0 then unaryCodeLength+=8
            decodingTableHighBitsCarryover[i] = zeroCount;
        }
    }

    public static void EliasFanoDecompress(byte[] compressedBuffer, int compressedBufferPointer, uint[] postingList, ref int resultPointer)
    {
        Stopwatch sw = Stopwatch.StartNew();

        // array is faster than list, but wastes space with fixed size
        // this is only important for decompression, not for compressed intersection (because we have only a fraction of results)

        int lowBitsPointer = 0;
        ulong lastDocID = 0;
        byte highBitsCarryover = 0;

        // read postingList.Count for decompression: LSB first
        int postingListCount = compressedBuffer[lowBitsPointer++];
        postingListCount |= (int)compressedBuffer[lowBitsPointer++] << 8;
        postingListCount |= (int)compressedBuffer[lowBitsPointer++] << 16;
        postingListCount |= (int)compressedBuffer[lowBitsPointer++] << 24;

        // read fanoParamInt for decompression
        byte lowBitsLength = compressedBuffer[lowBitsPointer++];

        // decompress low bits
        byte lowBitsCount = 0;
        byte lowBits = 0;

        // decompress high bits
        ulong docID = 0;
        for (int highBitsPointer =lowBitsLength * postingListCount / 8 + 6 ; highBitsPointer < compressedBufferPointer; highBitsPointer++)
        {
            byte cb = compressedBuffer[highBitsPointer];

            //number of docids contained within one byte    
            byte docIdNumber = decodingTableDocIdNumber[cb]; 
            docID += highBitsCarryover;

            // number of trailing zeros (zeros carryover), if whole byte=0 then unaryCodeLength+=8
            highBitsCarryover = decodingTableHighBitsCarryover[cb];

            for (byte i = 0; i < docIdNumber; i++)
            {
                // decompress low bits
                docID <<= lowBitsCount;
                docID |= lowBits & ((1u << lowBitsCount) - 1u); //mask remainder from previous lowBits, then add/or to docid 

                while (lowBitsCount < lowBitsLength)
                {
                    docID <<= 8;
                    
                    lowBits = compressedBuffer[lowBitsPointer++];
                    docID |= lowBits;
                    lowBitsCount += 8;
                }
                lowBitsCount -= lowBitsLength;
                // move right bits which belong to next docid
                docID >>= lowBitsCount; 

                // decompress high bits
                // 1 byte contains high bits in unary code of 0..8 docid's 
                docID += (decodingTableHighBits[cb, i] << lowBitsLength) + lastDocID + 1u;
                postingList[resultPointer++] = (uint)docID; lastDocID = docID; docID = 0;
            }
        }
        Console.WriteLine("\rDecompression: " + sw.ElapsedMilliseconds.ToString("N0") + " ms  " + postingListCount.ToString("N0") + " DocID");
    }

    static void Main(string[] args)
    {      
        // init
        Console.Write("Create decoding table...");
        eliasFanoCreateDecodingTable();

        Console.SetWindowSize(Math.Min(180, Console.LargestWindowWidth), Console.WindowHeight);
        
        int indexedPages = 1000000000;

        // may be increased to 1,000,000,000 (>2 GB) if: >=16 GB RAM, 64 bit Windows, .NET version >= 4.5,  <gcAllowVeryLargeObjects> in config file, Project / Properties / Buld / Prefer 32-bit disabled!
        // http://stackoverflow.com/questions/25225249/maxsize-of-array-in-net-framework-4-5
        int maximumPostingListLength = 1000000000; 

        for (int postingListLength = 10; postingListLength <= maximumPostingListLength; postingListLength *= 10)
        { 
            // posting list creation
            Console.Write("\rCreate posting list...");
            List<uint> postingList1 = generatePostingList(postingListLength, indexedPages);

            // compression
            Console.Write("\rCompress posting list...");
            byte[] compressedBuffer1 = new byte[postingListLength * 5];
            int compressedBufferPointer1 = 0;
            EliasFanoCompress(postingList1, compressedBuffer1, ref compressedBufferPointer1);

            // decompression
            Console.Write("Decompress posting list...");
            uint[] postingList10 = new uint[postingListLength];
            int resultPointer1 = 0;
            EliasFanoDecompress(compressedBuffer1, compressedBufferPointer1, postingList10, ref resultPointer1);

            // verification
            Console.Write("Verify posting list...");
            
            bool error = false;
            for (int i = 0; i < resultPointer1; i++) if (postingList1[i] != postingList10[i]) { error=true;break; }
            if (resultPointer1 != postingList1.Count) error = true;
            if (error) Console.WriteLine("\rVerification failed!  ");      
        }
       

        Console.WriteLine("\rPress any key to exit");
        Console.ReadKey();
    }
}

