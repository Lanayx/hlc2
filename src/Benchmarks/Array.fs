namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Linq

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
    member this.ReverseLinq() =
        let arr = this.arr
        arr
            .Reverse()
            .Select(fun x -> x*x)
            .Where(fun x -> x % 2 = 0)
            .Take(5)
            .ToArray()

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


//|       Method |       Mean |    Error |   StdDev |     Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
//|------------- |-----------:|---------:|---------:|-----------:|-------:|------:|------:|----------:|
//| ReverseArray | 4,947.8 ns | 66.74 ns | 55.73 ns | 4,947.4 ns | 2.4490 |     - |     - |   10272 B |
//|   ReverseSeq | 1,946.1 ns | 38.29 ns | 52.42 ns | 1,941.5 ns | 1.1463 |     - |     - |    4800 B |
//|  ReverseLinq | 1,225.3 ns | 24.56 ns | 69.66 ns | 1,195.2 ns | 1.0643 |     - |     - |    4456 B |
//|  SeqCompExpr |   667.0 ns |  5.38 ns |  4.77 ns |   667.3 ns | 0.1926 |     - |     - |     808 B |
//|    SeqLambda | 1,527.9 ns | 18.92 ns | 16.77 ns | 1,528.7 ns | 0.6199 |     - |     - |    2600 B |

