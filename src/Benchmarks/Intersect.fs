namespace Benchmarks

open BenchmarkDotNet.Attributes
open System.Collections.Generic
open System.Linq
open System.Collections.Specialized
open System.Collections

[<MemoryDiagnoser>]
type IntersectBenchmarks() =
    let x = Array.init 5000 (fun i -> i * 3)  //[|1;13;34;54;65;87;21|]
    let y = Array.init 5000 (fun i -> i * 9) //[|2;13;24;57;65;89;21;19|]
    let b1 = BitArray(45000)
    let b2 = BitArray(45000)
    do x |> Array.iter (fun i -> b1.[i] <- true)
    do x |> Array.iter (fun i -> b2.[i] <- true)

    member val arr1 = x
    member val arr2 = y
    member val roar1 = Collections.Special.RoaringBitmap.Create(x)
    member val roar2 = Collections.Special.RoaringBitmap.Create(y)
    member val bit1 = b1
    member val bit2 = b2


    [<Benchmark>]
    member this.LinqIntersect() =
         this.arr1.Intersect(this.arr2).Count()

    [<Benchmark>]
    member this.WhileLoop() =
        let first = this.arr1
        let second = this.arr2
        let mutable count = 0
        let mutable i = 0
        let mutable j = 0
        while i < first.Length do
            j <- 0
            while j < second.Length do
                if first.[i] = second.[j]
                then
                    count <- count + 1
                    j <- second.Length
                else
                    j <- j + 1
            i <- i + 1
        count

    [<Benchmark>]
    member this.Roaring() =
        int (this.roar1 &&& this.roar2).Cardinality

    [<Benchmark>]
    member this.BitArray() =
        let result = this.bit1.Clone() :?> BitArray
        result.And(this.bit2) |> ignore
        BitmapIndex.BitHelper.GetCardinality(result)


// 8-element array
//        Method |      Mean |      Error |     StdDev |    Median | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
//-------------- |----------:|-----------:|-----------:|----------:|------------:|------------:|------------:|--------------------:|
// LinqIntersect | 533.63 ns | 10.3221 ns | 11.8870 ns | 529.67 ns |      0.1593 |           - |           - |               672 B |
//     WhileLoop |  77.13 ns |  0.9522 ns |  0.8907 ns |  77.17 ns |           - |           - |           - |                   - |
//       Roaring | 146.41 ns |  2.9877 ns |  6.4950 ns | 143.57 ns |      0.0837 |           - |           - |               352 B |
//      BitArray | 105.79 ns |  1.1796 ns |  0.9850 ns | 105.72 ns |      0.0285 |           - |           - |               120 B |


// 5000-element array
//        Method |          Mean |       Error |      StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
//-------------- |--------------:|------------:|------------:|------------:|------------:|------------:|--------------------:|
// LinqIntersect |    330.484 us |   5.0030 us |   4.1778 us |     62.0117 |     30.7617 |     30.7617 |            262664 B |
//     WhileLoop | 21,617.591 us | 428.7734 us | 614.9340 us |           - |           - |           - |                   - |
//       Roaring |     12.044 us |   0.2385 us |   0.4421 us |      2.8381 |           - |           - |             11920 B |
//      BitArray |      5.916 us |   0.0819 us |   0.0726 us |      2.7008 |           - |           - |             11352 B |