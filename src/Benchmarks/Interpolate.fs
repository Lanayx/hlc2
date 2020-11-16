namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System

[<MemoryDiagnoser>]
type InterpolateBenchmarks() =
    let x1 = 123
    let x2 = "Hi"
    let x3 = 123.0

    [<Benchmark>]
    member this.Sprintf() =       
        sprintf "%i %s %f" x1 x2 x3

    [<Benchmark>]
    member this.Interpolate() =   
        $"{x1} {x2} {x3}"
        
    [<Benchmark>]
    member this.StringFormat() =   
        String.Format("{0} {1} {2}", x1, x2, x3)
