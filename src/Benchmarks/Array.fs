namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http

[<MemoryDiagnoser>]
type ArrayBenchmarks() =
    member val arr = [|1..1000|]


    [<Benchmark>]
    member this.ReverseArray() =
        let arr = this.arr
        arr
        |> Array.rev
        |> Array.map (fun x -> x*x)
        |> Array.filter (fun x -> x % 2 = 0)
        |> Array.take 5

    [<Benchmark>]
    member this.ReverseSeq() =
        let arr = this.arr
        arr
        |> Seq.rev
        |> Seq.map (fun x -> x*x)
        |> Seq.filter (fun x -> x % 2 = 0)
        |> Seq.take 5
        |> Seq.toArray

    [<Benchmark>]
    member this.SeqCompExpr() =
        let arr = this.arr
        seq {
            let mutable i = 1
            while i <= arr.Length do
                yield arr.[arr.Length - i]
                i <- i + 1
        }
        |> Seq.map (fun x -> x * x)
        |> Seq.filter (fun x -> x % 2 = 0)
        |> Seq.take 5
        |> Seq.toArray

    [<Benchmark>]
    member this.SeqLambda() =
        let arr = this.arr
        Seq.init arr.Length (fun i -> arr.[arr.Length - i - 1])
        |> Seq.map (fun x -> x * x)
        |> Seq.filter (fun x -> x % 2 = 0)
        |> Seq.take 5
        |> Seq.toArray


//       Method |       Mean |     Error |    StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
//------------- |-----------:|----------:|----------:|------------:|------------:|------------:|--------------------:|
// ReverseArray | 5,507.5 ns | 134.72 ns | 132.31 ns |      2.4490 |           - |           - |             10296 B |
//   ReverseSeq | 1,939.0 ns |  35.71 ns |  31.66 ns |      1.1559 |           - |           - |              4856 B |
//  SeqCompExpr |   809.8 ns |  16.05 ns |  25.91 ns |      0.2050 |           - |           - |               864 B |
//    SeqLambda | 1,868.3 ns |  39.09 ns |  67.43 ns |      0.6294 |           - |           - |              2656 B |
