namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http

type MatchBenchmarks() =
    member val arr = [|1;2;3|]

    [<Benchmark>]
    member this.Match() =       
        for i in this.arr do
            match i with
            | 2 -> 2
            | 1 -> 0
            | 0 -> 1
            |> ignore

    [<Benchmark>]
    member this.BitMatch() =   
        for i in this.arr do
            (i >>> 1) + ((i &&& 1) ^^^ 1) |> ignore
            
