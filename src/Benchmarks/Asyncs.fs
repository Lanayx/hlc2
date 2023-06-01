namespace Benchmarks

open BenchmarkDotNet.Attributes
open System.Threading.Tasks
open Orsak


module TestAsyncs =
    let inline taskTest (x: int) =
        task {
            do! Task.Yield()
            return ()
        }

    let inline asyncTest (x: int) =
        async {
            do! Async.SwitchToThreadPool()
            return ()
        }

    let inline orsakTest (x: int) : Effect<unit, unit, unit> =
        eff {
            do! Task.Yield()
            return ()
        }


[<MemoryDiagnoser>]
type AsyncBenchmarks() =

    static let message = "hello world"

    [<Benchmark>]
    member _.TaskTest() =
        task {
            return! TestAsyncs.taskTest(0)
        }

    [<Benchmark>]
    member _.AsyncTest() =
        async {
            return! TestAsyncs.asyncTest(0)
        } |> Async.StartAsTask

    [<Benchmark>]
    member _.OrsakTest() =
        eff {
            return! TestAsyncs.orsakTest(0)
        } |> Effect.runOrFail ()
