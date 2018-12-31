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
    member this.Simple() =
        let arr = this.arr
        seq {
            let mutable i = 1
            while i > 0 do
                yield arr.[arr.Length - i]
                i <- i + 1                
        }
        |> Seq.map (fun x -> x * x)
        |> Seq.filter (fun x -> x % 2 = 0)
        |> Seq.take 5
        |> Seq.toArray

//       Method |       Mean |     Error |    StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
//------------- |-----------:|----------:|----------:|------------:|------------:|------------:|--------------------:|
// ReverseArray | 8,369.5 ns | 166.03 ns | 272.79 ns |      3.2654 |           - |           - |             10296 B |
//   ReverseSeq | 2,349.2 ns | 136.48 ns | 402.41 ns |      1.5411 |           - |           - |              4856 B |
//       Simple |   772.7 ns |  15.40 ns |  37.48 ns |      0.2737 |           - |           - |               864 B |