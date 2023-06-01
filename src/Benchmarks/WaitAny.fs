namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Threading
open System.Threading.Tasks
open System

[<MemoryDiagnoser>]
type WaitAnyBenchmarks() =

    [<Benchmark>]
    member this.WaitAny() =
        use cts = new CancellationTokenSource()
        let token = cts.Token
        let t1 =
            task {
                let! x = (task { return DateTime.Now.IsDaylightSavingTime()  })
                return token.IsCancellationRequested && x
            }
        let t2 =
            task {
                do! Task.Delay(1)
                return token.IsCancellationRequested
            }
        Task.WaitAny <| [| t1; t2 |] |> ignore
        cts.Cancel()

    [<Benchmark>]
    member this.Choice() =

        let t1 =
            async {
                let! token = Async.CancellationToken
                let! x = (async { return DateTime.Now.IsDaylightSavingTime() })
                return Some (token.IsCancellationRequested && x)
            }
        let t2 =
            async {
                let! token = Async.CancellationToken
                do! Async.Sleep(1)
                return Some token.IsCancellationRequested
            }
        [| t1; t2 |] |> Async.Choice |> Async.RunSynchronously