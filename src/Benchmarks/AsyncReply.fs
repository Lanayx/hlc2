namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Threading
open System.Threading.Tasks
open System.Threading.Channels
open FSharp.Control.Tasks.V2.ContextInsensitive
open System

type Message1 =
    | MsgType1 of TaskCompletionSource<int>

type Message2 =
    | MsgType2 of AsyncReplyChannel<int>

[<MemoryDiagnoser>]
type AsyncReplyBenchmarks() =

    let mb1 = MailboxProcessor.Start(fun inbox ->
        let rec loop () =
            async {
                match! inbox.Receive() with
                | MsgType1 ch ->
                    do! Async.SwitchToThreadPool()
                    ch.SetResult(1)
                    return! loop()
            }
        loop ()
        )

    let mb2 = MailboxProcessor.Start(fun inbox ->
        let rec loop () =
            async {
                match! inbox.Receive() with
                | MsgType2 ch ->
                    do! Async.SwitchToThreadPool()
                    ch.Reply(1)
                    return! loop()
            }
        loop ()
        )

    let chan = Channel.CreateUnbounded(UnboundedChannelOptions(SingleReader = true, AllowSynchronousContinuations = true))
    do
        Task.Run(fun () ->
            (task {
                while(true) do
                    match! chan.Reader.ReadAsync() with
                    | MsgType1 ch ->
                        do! Task.Yield()
                        ch.SetResult(1)
            }) :> Task
        ) |> ignore

    [<Benchmark>]
    member this.TaskCompletion() =
        task {
            let cts = TaskCompletionSource<int>(TaskContinuationOptions.RunContinuationsAsynchronously)
            mb1.Post(MsgType1 cts)
            return! cts.Task
        }

    [<Benchmark>]
    member this.AsyncReply() =
        task {
            return! mb2.PostAndAsyncReply(fun ch -> MsgType2 ch)
        }

    [<Benchmark>]
    member this.Channel() =
        task {
            let cts = TaskCompletionSource<int>(TaskContinuationOptions.RunContinuationsAsynchronously)
            chan.Writer.TryWrite(MsgType1 cts) |> ignore
            return! cts.Task
        }

//|         Method |      Mean |     Error |    StdDev |    Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
//|--------------- |----------:|----------:|----------:|----------:|-------:|------:|------:|----------:|
//| TaskCompletion |  4.391 us | 0.0853 us | 0.0798 us |  4.404 us | 0.2785 |     - |     - |    1168 B |
//|     AsyncReply | 14.965 us | 0.5447 us | 1.6062 us | 15.453 us | 0.6714 |     - |     - |    2827 B |
//|        Channel |  3.682 us | 0.0704 us | 0.0891 us |  3.682 us | 0.1373 |     - |     - |     576 B |