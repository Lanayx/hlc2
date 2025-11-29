namespace Benchmarks

open BenchmarkDotNet.Attributes
open System.Threading.Tasks
open Orsak


module NestedTasks =
    let readyResult = Task.FromResult(1)

    let test1normal () =
        task {
            return! readyResult
        }
    let test3normal () =
        task {
            let! x = test1normal()
            let! y = test1normal()
            return x + y
        }

    let inline test1inline () =
        task {
            return! readyResult
        }
    let inline test3inline () =
        task {
            let! x = test1inline()
            let! y = test1inline()
            return x + y
        }


[<MemoryDiagnoser>]
type NestedTasksBenchmarks() =

    [<Benchmark>]
    member _.Test3normal() =
        task {
            return! NestedTasks.test3normal()
        }

    [<Benchmark>]
    member _.Test3inline() =
        task {
            return! NestedTasks.test3inline()
        }

    [<Benchmark>]
    member _.TestBaseline() =
        task {
            let! x = NestedTasks.test1normal()
            let! y = NestedTasks.test1normal()
            return x + y
        }

    [<Benchmark>]
    member _.TestBaselineInline() =
        task {
            let! x = NestedTasks.test1inline()
            let! y = NestedTasks.test1inline()
            return x + y
        }


// BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.26200.7171)
// AMD Ryzen 5 5600H with Radeon Graphics, 1 CPU, 12 logical and 6 physical cores
// .NET SDK=10.0.100
//   [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2 DEBUG
//   DefaultJob : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
//
//
// |             Method |     Mean |    Error |   StdDev | Allocated |
// |------------------- |---------:|---------:|---------:|----------:|
// |        Test3normal | 47.76 ns | 0.277 ns | 0.216 ns |         - |
// |        Test3inline | 45.36 ns | 0.329 ns | 0.291 ns |         - |
// |       TestBaseline | 31.31 ns | 0.297 ns | 0.277 ns |         - |
// | TestBaselineInline | 32.76 ns | 0.296 ns | 0.277 ns |         - |
