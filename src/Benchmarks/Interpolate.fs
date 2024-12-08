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

    [<Benchmark>]
    member this.InterpolateTyped() =
        $"%d{x1} %s{x2} %f{x3}"

// BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22631.4460)
// AMD Ryzen 5 5600H with Radeon Graphics, 1 CPU, 12 logical and 6 physical cores
// .NET SDK=9.0.100
//   [Host]     : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2 DEBUG
//   DefaultJob : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
//
//
// |           Method |     Mean |   Error |  StdDev |   Gen0 | Allocated |
// |----------------- |---------:|--------:|--------:|-------:|----------:|
// |          Sprintf | 370.7 ns | 4.10 ns | 3.84 ns | 0.0582 |     488 B |
// |      Interpolate | 257.9 ns | 5.19 ns | 5.77 ns | 0.0467 |     392 B |
// |     StringFormat | 145.7 ns | 0.67 ns | 0.56 ns | 0.0114 |      96 B |
// | InterpolateTyped | 218.8 ns | 1.73 ns | 1.53 ns | 0.0420 |     352 B |
